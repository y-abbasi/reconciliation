using FluentAssertions;
using Tiba.TradeFinanceAcl.Application.BalanceProcessor;

namespace Tiba.TradeFinanceAcl.Tests.BalanceProcessor;

public class AccountBalanceProcessorTests
    {
        [Fact]
        public void SingleNatureAccount_ShouldCalculateTurnoverCorrectly()
        {
            // Arrange
            var balances = new[]
            {
                new AccountBalanceInput
                {
                    AccountCode = "S1",
                    NormalBalance = BalanceNature.Debit,
                    OpeningBalance = 100m,
                    ClosingBalance = 50m
                }
            };
            var entries = new[]
            {
                new JournalEntry { AccountCode = "S1", TotalDebit = 30m, TotalCredit = 10m, Date = DateTime.Today },
                new JournalEntry { AccountCode = "S1", TotalDebit = 0m, TotalCredit = 20m, Date = DateTime.Today }
            };

            // Act
            var result = AccountBalanceProcessor.Process(balances, entries).Single();

            // Assert
            result.TotalTurnover.Should().Be(0m);
            result.TotalTurnoverNature.Should().Be(BalanceNature.Credit);
            result.Difference.Should().Be(50m);
            result.DifferenceNature.Should().Be(BalanceNature.Debit);
        }

        [Fact]
        public void TwoNatureAccount_ShouldCapCreditTurnoverCorrectly()
        {
            // Arrange
            var balances = new[]
            {
                new AccountBalanceInput
                {
                    AccountCode = "A",
                    NormalBalance = BalanceNature.Debit,
                    OpeningBalance = 1000m,
                    ClosingBalance = 0m,
                    OppositeAccountCode = "B"
                },
                new AccountBalanceInput
                {
                    AccountCode = "B",
                    NormalBalance = BalanceNature.Credit,
                    OpeningBalance = 0m,
                    ClosingBalance = 100m,
                    OppositeAccountCode = "A"
                }
            };
            var entries = new[]
            {
                new JournalEntry { AccountCode = "A", TotalDebit = 0m, TotalCredit = 500m, Date = DateTime.Today },
                new JournalEntry { AccountCode = "B", TotalDebit = 0m, TotalCredit = 600m, Date = DateTime.Today }
            };

            // Act
            var results = AccountBalanceProcessor.Process(balances, entries);
            var aResult = results.Single(r => r.AccountCode == "A");
            var bResult = results.Single(r => r.AccountCode == "B");

            // Assert A
            aResult.TotalTurnover.Should().Be(1000m);
            aResult.TotalTurnoverNature.Should().Be(BalanceNature.Credit);

            // Assert B
            bResult.TotalTurnover.Should().Be(100m);
            bResult.TotalTurnoverNature.Should().Be(BalanceNature.Credit);
        }

        [Fact]
        public void NoJournalEntries_ShouldReturnZeroTurnoverAndCorrectDifference()
        {
            // Arrange
            var balances = new[]
            {
                new AccountBalanceInput
                {
                    AccountCode = "X",
                    NormalBalance = BalanceNature.Credit,
                    OpeningBalance = 200m,
                    ClosingBalance = 150m
                }
            };
            var entries = Array.Empty<JournalEntry>();

            // Act
            var result = AccountBalanceProcessor.Process(balances, entries).Single();

            // Assert
            result.TotalTurnover.Should().Be(0m);
            result.TotalTurnoverNature.Should().Be(BalanceNature.Debit);
            result.Difference.Should().Be(50m);
            result.DifferenceNature.Should().Be(BalanceNature.Credit);
        }

        [Fact]
        public void MixedEntriesAcrossAccounts_ShouldAggregateAndCapCorrectly()
        {
            // Arrange
            var balances = new List<AccountBalanceInput>
            {
                new AccountBalanceInput
                {
                    AccountCode = "C",
                    NormalBalance = BalanceNature.Debit,
                    OpeningBalance = 300m,
                    ClosingBalance = 50m,
                    OppositeAccountCode = "D"
                }
            };
            var entries = new List<JournalEntry>
            {
                new JournalEntry { AccountCode = "C", TotalDebit = 100m, TotalCredit = 0m, Date = DateTime.Today },
                new JournalEntry { AccountCode = "D", TotalDebit = 0m, TotalCredit = 400m, Date = DateTime.Today }
            };

            // Act
            var result = AccountBalanceProcessor.Process(balances, entries).Single();

            // Assert
            result.TotalTurnover.Should().Be(50m);
            result.TotalTurnoverNature.Should().Be(BalanceNature.Debit);
        }
    }