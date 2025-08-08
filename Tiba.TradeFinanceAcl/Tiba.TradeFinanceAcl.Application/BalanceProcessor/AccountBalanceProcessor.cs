namespace Tiba.TradeFinanceAcl.Application.BalanceProcessor;

using System;
using System.Collections.Generic;
using System.Linq;

   
    public static class AccountBalanceProcessor
    {
        public static List<AccountTurnoverResult> Process(
            IEnumerable<AccountBalanceInput> balances,
            IEnumerable<JournalEntry> entries)
        {
            var balanceList = balances.ToList();
            var entriesByAccount = entries
                .GroupBy(e => e.AccountCode)
                .ToDictionary(g => g.Key, g => g.ToList());

            var results = new List<AccountTurnoverResult>();

            foreach (var balance in balanceList)
            {
                var related = new List<JournalEntry>();

                // Add entries posted directly to this account
                if (entriesByAccount.TryGetValue(balance.AccountCode, out var ownEntries))
                    related.AddRange(ownEntries);

                // Include opposite entries
                if (!string.IsNullOrEmpty(balance.OppositeAccountCode) &&
                    entriesByAccount.TryGetValue(balance.OppositeAccountCode, out var oppEntries))
                {
                    // If the opposite account has a defined balance, include direct entries
                    bool oppHasBalance = balanceList.Any(b => b.AccountCode == balance.OppositeAccountCode);
                    if (oppHasBalance)
                    {
                        related.AddRange(oppEntries);
                    }
                    else
                    {
                        // Otherwise invert debit/credit
                        related.AddRange(oppEntries.Select(e => new JournalEntry
                        {
                            AccountCode = balance.AccountCode,
                            TotalDebit = e.TotalCredit,
                            TotalCredit = e.TotalDebit,
                            Date = e.Date
                        }));
                    }
                }

                // Calculate net turnover
                var (net, nature) = related.Any()
                    ? NetTurnover(related)
                    : (0m, BalanceNature.Debit);

                // Cap turnover
                net = CapTurnover(net, nature, balance);

                // Build result
                results.Add(CalcResult(balance, net, nature));
            }

            return results;
        }

        private static (decimal net, BalanceNature nature) NetTurnover(IEnumerable<JournalEntry> ent)
        {
            decimal totalDebits = ent.Sum(x => x.TotalDebit);
            decimal totalCredits = ent.Sum(x => x.TotalCredit);
            if (totalCredits >= totalDebits)
                return (totalCredits - totalDebits, BalanceNature.Credit);
            return (totalDebits - totalCredits, BalanceNature.Debit);
        }

        private static decimal CapTurnover(decimal net, BalanceNature nature, AccountBalanceInput bal)
        {
            if (nature == BalanceNature.Credit)
            {
                return bal.NormalBalance == BalanceNature.Debit
                    ? Math.Min(net, bal.OpeningBalance)
                    : Math.Min(net, bal.ClosingBalance);
            }
            return bal.NormalBalance == BalanceNature.Credit
                ? Math.Min(net, bal.OpeningBalance)
                : Math.Min(net, bal.ClosingBalance);
        }

        private static AccountTurnoverResult CalcResult(
            AccountBalanceInput bal,
            decimal net,
            BalanceNature nat)
        {
            decimal opSigned = bal.NormalBalance == BalanceNature.Debit ? bal.OpeningBalance : -bal.OpeningBalance;
            decimal turnSigned = nat == BalanceNature.Debit ? net : -net;
            decimal clSigned = bal.NormalBalance == BalanceNature.Debit ? bal.ClosingBalance : -bal.ClosingBalance;

            decimal diff = opSigned + turnSigned - clSigned;
            var diffNature = diff >= 0 ? BalanceNature.Debit : BalanceNature.Credit;

            return new AccountTurnoverResult
            {
                AccountCode = bal.AccountCode,
                OpeningBalance = bal.OpeningBalance,
                OpeningBalanceNature = bal.NormalBalance,
                TotalTurnover = net,
                TotalTurnoverNature = nat,
                ClosingBalance = bal.ClosingBalance,
                ClosingBalanceNature = bal.NormalBalance,
                Difference = Math.Abs(diff),
                DifferenceNature = diffNature
            };
        }
    }
