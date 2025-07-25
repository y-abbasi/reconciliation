namespace Tiba.TradeFinanceAcl.Application;

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