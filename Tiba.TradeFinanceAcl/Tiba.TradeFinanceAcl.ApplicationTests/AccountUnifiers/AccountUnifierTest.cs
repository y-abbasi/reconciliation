using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Tiba.TradeFinanceAcl.Application;
using Xunit;

namespace Tiba.TradeFinanceAcl.Tests
{
    public class AccountUnifierTests
    {
        private readonly IAccountUnifier _sut = new AccountUnifier();

        [Fact]
        public void SgbCodingExtensions_CbiMethods_WorkFor10And20CharCodes()
        {
            // Arrange & Act
            var code10 = "ABCDE" + "12345"; // 10 chars
            var code20 = "-----" + code10 + code10; // 20 chars

            // Assert
            code10.CbiDb().Should().Be("ABCDE");
            code10.CbiCr().Should().Be("12345");
            code20.CbiDb().Should().Be("ABCDE");
            code20.CbiCr().Should().Be("12345");
        }

        [Fact]
        public void Unify_NoDualKeys_ReturnsAllSystemAccounts()
        {
            // Arrange
            var sgbDualKeys = new List<string>();
            var sys = new[]
            {
                new AccountAggregate { GroupKey = "AAAAAAAAAA", Balance = 1, Turnover = 1 },
                new AccountAggregate { GroupKey = "BBBBBBBBBB", Balance = 2, Turnover = 2 }
            };

            // Act
            var result = _sut.Unify(sgbDualKeys, sys).ToList();

            // Assert
            result.Should().BeEquivalentTo(sys, opts => opts.WithStrictOrdering());
        }

        [Fact]
        public void Unify_DualKey_UnifiesAndExcludesSystemCodes()
        {
            // Arrange
            var dualKey = "CCCCC" + "DDDDD";
            var sgbDualKeys = new[] { dualKey };
            var debitCode = string.Concat(dualKey.CbiDb(), dualKey.CbiDb());
            var creditCode = string.Concat(dualKey.CbiCr(), dualKey.CbiCr());
            var sys = new List<AccountAggregate>
            {
                new AccountAggregate { GroupKey = debitCode, Balance = 5, Turnover = 1 },
                new AccountAggregate { GroupKey = creditCode, Balance = 3, Turnover = 2 },
                new AccountAggregate { GroupKey = "EEEEEEEEEE", Balance = 9, Turnover = 9 }
            };

            // Act
            var result = _sut.Unify(sgbDualKeys, sys).ToList();

            // Assert: unified plus leftover
            result.Should().HaveCount(2);
            var unified = result.First(a => a.GroupKey == dualKey);
            unified.Balance.Should().Be(8);
            unified.Turnover.Should().Be(3);
            result.Should().ContainSingle(a => a.GroupKey == "EEEEEEEEEE");
        }

        [Fact]
        public void Unify_PartialMissing_CreatesUnifiedWithAvailableOnly()
        {
            // Arrange
            var dualKey = "EEEEE" + "FFFFF";
            var sgbDualKeys = new[] { dualKey };
            var debitCode = dualKey.CbiDb() + dualKey.CbiDb();
            // only debit exists
            var sys = new[] { new AccountAggregate { GroupKey = debitCode, Balance = 7, Turnover = 0 } };

            // Act
            var result = _sut.Unify(sgbDualKeys, sys).ToList();

            // Assert
            result.Should().ContainSingle(a => a.GroupKey == dualKey && a.Balance == 7 && a.Turnover == 0);
        }

    }
}
