using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Tiba.TradeFinanceAcl.Application
{
    /// <summary>
    /// نمایندهٔ یک حساب با تراز و گردش
    /// </summary>
    public class AccountAggregate
    {
        public string GroupKey { get; set; }
        public decimal Balance { get; set; }
        public decimal Turnover { get; set; }
        public int Level { get; set; }

        public bool IsDualNatureSgb() =>
            GroupKey.CbiDb() != GroupKey.CbiCr();
    }

    public static class SgbCodingExtensions
    {
        private static int CbiStartIndex(this string code) => code.Length <= 20 ? 0 : 5;
        private static int CbkStartIndex(this string code) => code.Length <= 20 ? 5 : 10;
        public static string CbiDb(this string code) => code[code.CbiStartIndex()..(code.CbiStartIndex() + 5)];
        public static string CbiCr(this string code) => code[code.CbkStartIndex()..(code.CbkStartIndex() + 5)];

        public static string ToAccountCodeDebit(this string code) => code[..CbiStartIndex(code)] + code.CbiDb() +
                                                                     code.CbiDb() + code[(CbiStartIndex(code) + 10)..];

        public static string ToAccountCodeCredit(this string code) => code[..CbiStartIndex(code)] + code.CbiCr() +
                                                                      code.CbiCr() + code[(CbiStartIndex(code) + 10)..];
    }

    /// <summary>
    /// رابط تجمیع حساب‌ها بر مبنای حساب‌های دوما‌هیتی SGB
    /// </summary>
    public interface IAccountUnifier
    {
        /// <param name="sgbDualKeys">کدهای 10‌رقمی دوماهیتی در SGB</param>
        /// <param name="systemAccounts">تمام حساب‌های 10‌رقمی در سیستم ما (شامل single-nature و کدهای مشتق‌شده)</param>
        /// <returns>
        /// لیست AccountAggregate که شامل:
        /// - هر حساب single-nature در SGB (روشن بودن both halves)
        /// - هر حساب dual-nature در SGB تجمیع‌شده از دو کد جداگانه در سیستم
        /// </returns>
        IEnumerable<AccountAggregate> Unify(
            IEnumerable<string> sgbDualKeys,
            IEnumerable<AccountAggregate> systemAccounts);
    }

    public class AccountUnifier : IAccountUnifier
    {
        public IEnumerable<AccountAggregate> Unify(
            IEnumerable<string> sgbDualKeys,
            IEnumerable<AccountAggregate> systemAccounts)
        {
            var result = new List<AccountAggregate>();
            var sysDict = systemAccounts.ToDictionary(a => a.GroupKey);


            // 2. برای هر Dual-Nature در SGB، تجمیع دو کد سیستم
            foreach (var dualKey in sgbDualKeys)
            {
                // حساب‌های سیستم: first5*2 و second5*2
                var debitCode = dualKey.ToAccountCodeDebit();
                var creditCode =dualKey.ToAccountCodeCredit();

                sysDict.Remove(debitCode, out var acc1);
                sysDict.Remove(creditCode, out var acc2);
                if (acc1 == null && acc2 == null)
                    continue;

                var unified = new AccountAggregate
                {
                    GroupKey = dualKey,
                    Level = 0,
                    Balance = (acc1?.Balance ?? 0) + (acc2?.Balance ?? 0),
                    Turnover = (acc1?.Turnover ?? 0) + (acc2?.Turnover ?? 0)
                };
                result.Add(unified);
            }

            return result.Union(sysDict.Values);
        }
    }

    public record AccountFilter(Range GroupKeyRange, Range? DetailKeyRange, string? ParentKey)
    {
        public static readonly AccountFilter[] DefaultFilters = new[]
        {
            new AccountFilter(new Range(5, 15), null, null),
            new AccountFilter(new Range(5, 25), new Range(5, 15), null),
            new AccountFilter(new Range(0, 25), new Range(5, 25), null),
            new AccountFilter(new Range(0, 30), new Range(0, 25), null)
        };

        public AccountFilter WithParent(string? parent) => this with { ParentKey = parent };
    }

    public class ReconciliationOutcome
    {
        public List<AccountDetail> MissingAccountInfos { get; } = new();
        public List<TransactionRecord> MissingTransactionInfos { get; } = new();
    }

    public class AccountReconciliationService(
        ILegacyAccountRepository legacyRepo,
        IAccountRepository accountRepo,
        IAccountUnifier accountUnifier)
    {
        private const int MaxLevels = 4;

        public async Task<ReconciliationOutcome> ExecuteAsync(string parentKey = null, int level = 0)
        {
            var outcome = new ReconciliationOutcome();
            if (level >= MaxLevels) return outcome;

            var filter = AccountFilter.DefaultFilters[level].WithParent(parentKey);
            var legacyAggregates = await legacyRepo.GetAccountAggregatesAsync(filter);
            var currentAggregates =
                accountUnifier.Unify(legacyAggregates.Where(a => a.IsDualNatureSgb())
                        .Select(a => a.GroupKey),
                    (await accountRepo
                        .GetAccountAggregatesAsync(filter)).Values).ToDictionary(a => a.GroupKey);
            if (level == MaxLevels - 1)
            {
                foreach (var agg in legacyAggregates)
                {
                    if (!currentAggregates.ContainsKey(agg.GroupKey))
                        AddMissingFinalAccount(agg, outcome);

                    var legacyTxns = await legacyRepo.GetTransactionsAsync(agg.GroupKey);
                    // accountUnifier.Unify()
                    var currentTxns = await accountRepo.GetTransactionsAsync(agg.GroupKey);
                    var existingIds = new HashSet<Guid>(currentTxns.Select(t => t.TransactionId));
                    foreach (var txn in legacyTxns)
                        if (!existingIds.Contains(txn.TransactionId))
                            outcome.MissingTransactionInfos.Add(txn);
                }

                return outcome;
            }

            foreach (var agg in legacyAggregates)
            {
                if (currentAggregates.TryGetValue(agg.GroupKey, out var curr) &&
                    !HasDifference(agg, curr)) continue;
                var nested = await ExecuteAsync(agg.GroupKey, level + 1);
                outcome.MissingAccountInfos.AddRange(nested.MissingAccountInfos);
                outcome.MissingTransactionInfos.AddRange(nested.MissingTransactionInfos);
            }

            return outcome;
        }

        private static void AddMissingFinalAccount(AccountAggregate agg, ReconciliationOutcome outcome)
        {
            if (agg.IsDualNatureSgb())
            {
                outcome.MissingAccountInfos.Add(BuildDetail(agg, "Debit"));
                outcome.MissingAccountInfos.Add(BuildDetail(agg, "Credit"));
            }
            else
                outcome.MissingAccountInfos.Add(BuildDetail(agg));
        }

        private static bool HasDifference(AccountAggregate a, AccountAggregate b)
            => a.Balance != b.Balance || a.Turnover != b.Turnover;

        private static string ExtractCbi(string code)
        {
            if (code.Length <= 5) return string.Empty;
            var length = Math.Min(10, code.Length - 5);
            return code.Substring(5, length);
        }

        private static AccountDetail BuildDetail(AccountAggregate agg, string normalBalance)
        {
            return new AccountDetail
            {
                FullCode = normalBalance == "Debit"
                    ? agg.GroupKey.ToAccountCodeDebit()
                    : agg.GroupKey.ToAccountCodeCredit(),
                IsDualNature = true,
                NormalBalance = normalBalance,
            };
        }

        private static AccountDetail BuildDetail(AccountAggregate agg)
        {
            return new AccountDetail
            {
                FullCode = agg.GroupKey.ToAccountCodeDebit(),
                IsDualNature = false,
                NormalBalance = agg.Balance <= 0 ? "Debit" : "Credit",
            };
        }
    }

    public class AccountDetail
    {
        public string FullCode { get; set; }
        public bool IsDualNature { get; set; }
        public string NormalBalance { get; set; }
        public decimal Balance { get; set; }
    }

    public class TransactionRecord
    {
        public string AccountCode { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; }
        public DateTime EffectiveDate { get; set; }
        public TransactionType Type { get; set; }
        public string VoucherNumber { get; set; }
        public int Sequence { get; set; }
        public Guid TransactionId { get; set; }
    }

    public enum TransactionType
    {
        Debit,
        Credit
    }

    public interface IAccountRepository
    {
        Task<Dictionary<string, AccountAggregate>> GetAccountAggregatesAsync(AccountFilter filter);
        Task<List<TransactionRecord>> GetTransactionsAsync(string groupKey);
    }

    public interface ILegacyAccountRepository
    {
        Task<AccountAggregate[]> GetAccountAggregatesAsync(AccountFilter filter);
        Task<List<TransactionRecord>> GetTransactionsAsync(string groupKey);
    }
}