using FluentAssertions;
using Newtonsoft.Json;
using NSubstitute;
using Tiba.TradeFinanceAcl.Application;
using Xunit.Abstractions;

namespace Tiba.TradeFinanceAcl.Tests;

public class AccountReconciliationServiceExtendedTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly ILegacyAccountRepository _legacyRepo = Substitute.For<ILegacyAccountRepository>();
    private readonly IAccountRepository _accountRepo = Substitute.For<IAccountRepository>();
    private readonly ReconciliationTestBuilder _builder = new();

    public AccountReconciliationServiceExtendedTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task ExecuteAsync_MultipleAggregates_MixedResults()
    {
        // Arrange: A missing account, B present but missing transaction
        var keyA = "AAAABAAAABAAAABAAAAB";
        var keyB = "00100" + "AAAAASSSSS" + "DDDDEEEEFF" + "FFFFFFFF";
        var keyB1 = "00100" + "AAAAAAAAAA" + "DDDDEEEEFF" + "FFFFFFFF";
        var txId = Guid.NewGuid();

        _builder
            .WithLegacyAggregates(null, 1,
                (keyA, 100, 0), (keyB, 50, 0))
            .WithCurrentAggregates(null, 1, (keyB1, 50, 0))
            .WithLegacyTransaction(txId, keyB);
        _builder.SetupRepositories(_legacyRepo, _accountRepo);
        var service = _builder.BuildService(_legacyRepo, _accountRepo);

        // Act: simulate final level
        var outcome = await service.ExecuteAsync(level: 3);

        // Assert
        outcome.MissingAccountInfos.Should().BeEquivalentTo(new AccountDetail[]
        {
            new() { FullCode = keyA, IsDualNature = false, NormalBalance = "Credit" },
        });
        outcome.MissingTransactionInfos.Should().ContainSingle(t => t.TransactionId == txId);
    }

    [Fact]
    public async Task ExecuteAsync_DualNatureAccount_CreatesTwoDetails()
    {
        // Arrange: simulate dual-nature CBI (10 chars with halves different)
        var key = "BRNCH" + "12345" + "67890" + "CBKDBCBKCR"; // cbiDb=12345, cbiCr=67890
        _builder.WithLegacyAggregates(null, 1, (key, 20, 1000));
        _builder.SetupRepositories(_legacyRepo, _accountRepo);
        var service = _builder.BuildService(_legacyRepo, _accountRepo);

        // Act at final level
        var outcome = await service.ExecuteAsync(level: 3);

        // Assert
        outcome.MissingAccountInfos.Should().BeEquivalentTo(new AccountDetail[]
        {
            new()
            {
                Balance = 0M,
                FullCode = "BRNCH1234512345CBKDBCBKCR",
                IsDualNature = true,
                NormalBalance = "Debit"
            },
            new()
            {
                Balance = 0M,
                FullCode = "BRNCH6789067890CBKDBCBKCR",
                IsDualNature = true,
                NormalBalance = "Credit"
            },
        });
    }

    [Fact]
    public async Task ExecuteAsync_AccountAndTransactionMissing_BothReported()
    {
        // Arrange
        var key = "ZZZZYYYYXX";
        var txId = Guid.NewGuid();
        _builder
            .WithLegacyAggregates(null, 1, (key, 30, 5))
            // no current aggregate -> missing account
            .WithLegacyTransaction(txId, key); // missing txn too
        _builder.SetupRepositories(_legacyRepo, _accountRepo);
        var service = _builder.BuildService(_legacyRepo, _accountRepo);

        // Act
        var outcome = await service.ExecuteAsync(level: 3);

        // Assert
        outcome.MissingAccountInfos.Should().BeEquivalentTo(new AccountDetail[]
        {
            new()
            {
                Balance = 0M,
                FullCode = "ZZZZYZZZZY",
                IsDualNature = true,
                NormalBalance = "Debit"
            },
            new()
            {
                Balance = 0M,
                FullCode = "YYYXXYYYXX",
                IsDualNature = true,
                NormalBalance = "Credit"
            },
        });
        outcome.MissingTransactionInfos.Should().ContainSingle(t => t.TransactionId == txId);
    }

    [Fact]
    public async Task ExecuteAsync_DrillDownStopsAtMaxLevels()
    {
        // Arrange: set mismatch at each level to force recursion
        string[] codes =
            ["AAAAABBBBB", "AAAAABBBBBCBK12345BB", "12345AAAAABBBBBCBK12345BB", "12345AAAAABBBBBCBK12345BB----------",];
        for (int level = 0; level < 4; level++)
        {
            _legacyRepo.GetAccountAggregatesAsync(AccountFilter.DefaultFilters[level]
                    .WithParent(level == 0 ? null : codes[level - 1]))
                .Returns(new[] { new AccountAggregate { GroupKey = codes[level], Balance = 1, Turnover = 1 } });
            _accountRepo.GetAccountAggregatesAsync(AccountFilter.DefaultFilters[level]
                    .WithParent(level == 0 ? null : codes[level - 1]))
                .Returns(new Dictionary<string, AccountAggregate>());
        }

        // transactions empty
        _legacyRepo.GetTransactionsAsync(Arg.Any<string>()).Returns([]);
        _accountRepo.GetTransactionsAsync(Arg.Any<string>()).Returns([]);
        var service = new AccountReconciliationService(_legacyRepo, _accountRepo, new AccountUnifier());

        // Act
        var outcome = await service.ExecuteAsync();

        // Assert: recursion should stop after level 3
        outcome.MissingAccountInfos.Should().HaveCount(2); // level3 dual-nature check or single
    }

    [Fact]
    public async Task ExecuteAsync_DrillDownStopsAtMaxLevels1()
    {
        // Arrange: set mismatch at each level to force recursion
        string[] codes =
            ["AAAAABBBBB", "AAAAABBBBBCBK12345BB", "12345AAAAABBBBBCBK12345BB", "12345AAAAABBBBBCBK12345BB----------",];
        for (int level = 0; level < 4; level++)
        {
            _legacyRepo.GetAccountAggregatesAsync(AccountFilter.DefaultFilters[level]
                    .WithParent(level == 0 ? null : codes[level - 1]))
                .Returns(new[] { new AccountAggregate { GroupKey = codes[level], Balance = 1, Turnover = 1 } });
            _accountRepo.GetAccountAggregatesAsync(AccountFilter.DefaultFilters[level]
                    .WithParent(level == 0 ? null : codes[level - 1]))
                .Returns(new Dictionary<string, AccountAggregate>()
                    { { "12345AAAAAAAAAACBK12345BB----------", new AccountAggregate(){ GroupKey = codes[level], Balance = 11, Turnover = 1 } } });
        }

        var tx = new TransactionRecord
        {
            TransactionId = Guid.NewGuid(), AccountCode = "12345AAAAABBBBBCBK12345BB----------", Amount = 200,
            Currency = "USD"
        };

        // transactions empty
        _legacyRepo.GetTransactionsAsync(Arg.Any<string>()).Returns(new List<TransactionRecord>()
        {
            tx
        });
        _accountRepo.GetTransactionsAsync(Arg.Any<string>()).Returns([]);
        var service = new AccountReconciliationService(_legacyRepo, _accountRepo, new AccountUnifier());

        // Act
        var outcome = await service.ExecuteAsync();

        // Assert: recursion should stop after level 3
        outcome.MissingAccountInfos.Should().BeEmpty();
        outcome.MissingTransactionInfos.Should().BeEquivalentTo([tx]);
    }
}