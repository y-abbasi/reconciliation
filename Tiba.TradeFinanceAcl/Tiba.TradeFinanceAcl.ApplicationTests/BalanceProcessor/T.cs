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
        public void TwoNatureAccount_ShouldCapCreditTurnoverCorrectly2()
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
                new JournalEntry { AccountCode = "A", TotalDebit = 0m, TotalCredit = 1000m, Date = DateTime.Today },
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
            bResult.TotalTurnover.Should().Be(600m);
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
                new()
                {
                    AccountCode = "C", NormalBalance = BalanceNature.Debit, OpeningBalance = 300m, ClosingBalance = 50m, OppositeAccountCode = "D"
                },
                new()
                {
                    AccountCode = "D", NormalBalance = BalanceNature.Credit, OpeningBalance = 0m, ClosingBalance = 0m, OppositeAccountCode = "C"
                }
            };
            var entries = new List<JournalEntry>
            {
                new() { AccountCode = "C", TotalDebit = 100m, TotalCredit = 0m, Date = DateTime.Today },
                new() { AccountCode = "D", TotalDebit = 0m, TotalCredit = 400m, Date = DateTime.Today }
            };

            // Act
            var results = AccountBalanceProcessor.Process(balances, entries);
            var aResult = results.Single(r => r.AccountCode == "C");
            var bResult = results.Single(r => r.AccountCode == "D");

            // Assert
            aResult.Should().BeEquivalentTo(new AccountTurnoverResult
            {
                AccountCode = "C",
                OpeningBalance = 300m,
                OpeningBalanceNature = BalanceNature.Debit,
                TotalTurnover = 300m,
                TotalTurnoverNature = BalanceNature.Credit,
                ClosingBalance = 50m,
                ClosingBalanceNature = BalanceNature.Debit,
                Difference = 50,
                DifferenceNature = BalanceNature.Credit
            });
            bResult.Should().BeEquivalentTo(new 
            {
                AccountCode = "D",
                OpeningBalance = 0m,
                OpeningBalanceNature = BalanceNature.Credit,
                TotalTurnover = 0m,
                TotalTurnoverNature = BalanceNature.Credit,
                ClosingBalance = 0m,
                ClosingBalanceNature = BalanceNature.Credit,
                Difference = 0,
            });

        }
    }