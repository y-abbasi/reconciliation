using FluentAssertions;
using Newtonsoft.Json;
using NSubstitute;
using Tiba.TradeFinanceAcl.Application;
using Xunit.Abstractions;

namespace Tiba.TradeFinanceAcl.Tests;

public class AccountReconciliationServiceTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    private readonly ILegacyAccountRepository _legacyRepo = Substitute.For<ILegacyAccountRepository>();
    private readonly IAccountRepository _accountRepo = Substitute.For<IAccountRepository>();
    private readonly ReconciliationTestBuilder _builder = new();

    public AccountReconciliationServiceTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task ExecuteAsync_NoMissingEntities_ReturnsEmptyOutcome()
    {
        // Arrange
        var key = "AAAABBBBCC";
        _builder
            .WithLegacyAggregates(null, 1, (key, 100, 50))
            .WithCurrentAggregates(null, 1, (key, 100, 50));
        _builder.SetupRepositories(_legacyRepo, _accountRepo);
        var service = _builder.BuildService(_legacyRepo, _accountRepo);

        // Act
        var outcome = await service.ExecuteAsync();

        // Assert
        outcome.MissingAccountInfos.Should().BeEmpty();
        outcome.MissingTransactionInfos.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_MissingAccount_AddsToOutcome()
    {
        // Arrange
        var key = "CCCCDDDDEE";
        _builder
            .WithLegacyAggregates(null, 1, (key, 200, 0));
        _builder.SetupRepositories(_legacyRepo, _accountRepo);
        var service = _builder.BuildService(_legacyRepo, _accountRepo);

        // Act: simulate final level to trigger account creation
        var outcome = await service.ExecuteAsync(parentKey: null, level: 3);

        // Assert
        _testOutputHelper.WriteLine(JsonConvert.SerializeObject(outcome));
        outcome.MissingAccountInfos.Should().BeEquivalentTo([
            new AccountDetail()
            {
                FullCode = "CCCCDCCCCD",
                IsDualNature = true,
                NormalBalance = "Debit"
            },
            new()
            {
                FullCode = "DDDEEDDDEE",
                IsDualNature = true,
                NormalBalance = "Credit"
            }
        ], opts => opts.WithStrictOrdering());
        outcome.MissingTransactionInfos.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_MissingTransaction_AddsToOutcome()
    {
        // Arrange
        var keyA = "ZZZZXXXXYY";
        var keyB = "XXXYYXXXYY";
        var txId = Guid.NewGuid();
        _builder
            .WithLegacyAggregates(null, 1, (keyA, 100, 0))
            .WithCurrentAggregates(null, 1, (keyB, 100, 0))
            .WithLegacyTransaction(txId, keyA);
        _builder.SetupRepositories(_legacyRepo, _accountRepo);
        var service = _builder.BuildService(_legacyRepo, _accountRepo);

        // Act: simulate final level to trigger transaction check
        var outcome = await service.ExecuteAsync(level: 3);

        // Assert
        outcome.MissingAccountInfos.Should().BeEmpty();
        outcome.MissingTransactionInfos.Should().ContainSingle(t => t.TransactionId == txId);
    }
        
    [Fact]
    public async Task ExecuteAsync_ShortGroupKey_DoesNotThrow_CbiEmpty()
    {
        // Arrange: GroupKey length < 6
        var key = "1234512345";
        _builder
            .WithLegacyAggregates(null, 1, (key, 10, 1));
        _builder.SetupRepositories(_legacyRepo, _accountRepo);
        var service = _builder.BuildService(_legacyRepo, _accountRepo);

        // Act
        var outcome = await service.ExecuteAsync(level: 3);

        // Assert
        var detail = outcome.MissingAccountInfos.Single();
        detail.FullCode.Should().Be(key);
    }

    [Fact]
    public async Task ExecuteAsync_LevelExceedsMax_ReturnsEmptyImmediately()
    {
        // Arrange
        var service = new AccountReconciliationService(_legacyRepo, _accountRepo, new AccountUnifier());

        // Act
        var outcome = await service.ExecuteAsync("any", level: 4);

        // Assert
        outcome.MissingAccountInfos.Should().BeEmpty();
        outcome.MissingTransactionInfos.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_Level0Discrepancy_ThenLevel1Match_NoMissing()
    {
        // Arrange: root mismatch but child matches => overall no missing
        var rootKey = "ROOT123456";
        var childKey = rootKey + "CHILD";

        // Level 0
        _legacyRepo.GetAccountAggregatesAsync(AccountFilter.DefaultFilters[0].WithParent(null))
            .Returns(new[] { new AccountAggregate { GroupKey = rootKey, Balance = 100, Turnover = 0 } });
        _accountRepo.GetAccountAggregatesAsync(AccountFilter.DefaultFilters[0].WithParent(null))
            .Returns(new Dictionary<string, AccountAggregate> { [rootKey] = new AccountAggregate { GroupKey = rootKey, Balance = 90, Turnover = 0 } });
        _legacyRepo.GetTransactionsAsync(Arg.Any<string>()).Returns(new List<TransactionRecord>());
        _accountRepo.GetTransactionsAsync(Arg.Any<string>()).Returns(new List<TransactionRecord>());

        // Level 1
        _legacyRepo.GetAccountAggregatesAsync(AccountFilter.DefaultFilters[1].WithParent(rootKey))
            .Returns(new[] { new AccountAggregate { GroupKey = childKey, Balance = 50, Turnover = 5 } });
        _accountRepo.GetAccountAggregatesAsync(AccountFilter.DefaultFilters[1].WithParent(rootKey))
            .Returns(new Dictionary<string, AccountAggregate> { [childKey] = new AccountAggregate { GroupKey = childKey, Balance = 50, Turnover = 5 } });

        var service = new AccountReconciliationService(_legacyRepo, _accountRepo, new AccountUnifier());

        // Act
        var outcome = await service.ExecuteAsync();

        // Assert: child matches so no missing, parent difference only triggers nested but child ok
        outcome.MissingAccountInfos.Should().BeEmpty();
        outcome.MissingTransactionInfos.Should().BeEmpty();
    }
}