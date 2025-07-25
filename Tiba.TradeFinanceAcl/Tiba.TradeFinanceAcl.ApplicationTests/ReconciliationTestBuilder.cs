using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NSubstitute;
using Tiba.TradeFinanceAcl.Application;
using Xunit;

namespace Tiba.TradeFinanceAcl.Tests
{
    // Builder برای مقداردهی Arrange
    internal class ReconciliationTestBuilder
    {
        private readonly Dictionary<(string? parent, int level), List<AccountAggregate>> _legacyAggregates = new();

        private readonly Dictionary<(string? parent, int level), Dictionary<string, AccountAggregate>>
            _currentAggregates = new();

        private readonly List<TransactionRecord> _legacyTxns = new();
        private readonly List<TransactionRecord> _currentTxns = new();


        public ReconciliationTestBuilder WithLegacyAggregates(string? parent, int level,
            params (string key, decimal balance, decimal turnover)[] acc)
        {
            _legacyAggregates.Add((parent, level),
                acc.Select(a => new AccountAggregate { GroupKey = a.key, Balance = a.balance, Turnover = a.turnover })
                    .ToList());
            return this;
        }

        public ReconciliationTestBuilder WithCurrentAggregates(string? parent, int level,
            params (string key, decimal balance, decimal turnover)[] accounts)
        {
            _currentAggregates.Add((parent, level),
                accounts.Select(a => new AccountAggregate
                    { GroupKey = a.key, Balance = a.balance, Turnover = a.turnover }).ToDictionary(a => a.GroupKey));
            return this;
        }

        public ReconciliationTestBuilder WithLegacyTransaction(Guid id, string accountKey)
        {
            _legacyTxns.Add(new TransactionRecord { TransactionId = id, AccountCode = accountKey });
            return this;
        }

        public ReconciliationTestBuilder WithCurrentTransaction(Guid id, string accountKey)
        {
            _currentTxns.Add(new TransactionRecord { TransactionId = id, AccountCode = accountKey });
            return this;
        }

        public void SetupRepositories(ILegacyAccountRepository legacyRepo, IAccountRepository accountRepo)
        {
            // تنظیم برای هر سطح و هر فیلتر
            foreach (var keyValuePair in _legacyAggregates)
            {
                legacyRepo
                    .GetAccountAggregatesAsync(Arg.Is<AccountFilter>(a => a.ParentKey == keyValuePair.Key.parent))
                    .Returns(keyValuePair.Value.ToArray());
            }

            foreach (var keyValuePair in _currentAggregates)
            {
                accountRepo
                    .GetAccountAggregatesAsync(Arg.Is<AccountFilter>(a => a.ParentKey == keyValuePair.Key.parent))
                    .Returns(keyValuePair.Value);
            }

            if (_currentAggregates.Count == 0)
                accountRepo
                    .GetAccountAggregatesAsync(Arg.Any<AccountFilter>())
                    .Returns(new Dictionary<string, AccountAggregate>());
            legacyRepo.GetTransactionsAsync(Arg.Any<string>()).Returns([]);
            foreach (var keyValuePair in _legacyTxns.GroupBy(a => a.AccountCode))
                legacyRepo.GetTransactionsAsync(keyValuePair.Key).Returns(keyValuePair.ToList());
            accountRepo.GetTransactionsAsync(Arg.Any<string>()).Returns([]);
            foreach (var keyValuePair in _legacyTxns.GroupBy(a => a.AccountCode))
                accountRepo.GetTransactionsAsync(keyValuePair.Key).Returns(_currentTxns);
        }

        public AccountReconciliationService BuildService(ILegacyAccountRepository legacyRepo,
            IAccountRepository accountRepo)
            => new AccountReconciliationService(legacyRepo, accountRepo, new AccountUnifier());
    }
}