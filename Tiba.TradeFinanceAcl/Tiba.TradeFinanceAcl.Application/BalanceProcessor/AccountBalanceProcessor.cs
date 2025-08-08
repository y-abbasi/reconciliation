using Newtonsoft.Json;

namespace Tiba.TradeFinanceAcl.Application.BalanceProcessor;

using System;
using System.Collections.Generic;
using System.Linq;

public static class AccountBalanceProcessor
{
    /// <summary>
    /// Processes journal entries and balances, handling two-nature accounts by pooling turnovers
    /// and allocating against opening balances.
    /// </summary>
    public static List<AccountTurnoverResult> Process(
        IEnumerable<AccountBalanceInput> balances,
        IEnumerable<JournalEntry> entries)
    {
        var balanceList = balances.ToList();

        // Compute net turnover per account from journal
        var netMap = entries
            .GroupBy(e => e.AccountCode)
            .ToDictionary(g => g.Key, g => ComputeNet(g));

        var results = new List<AccountTurnoverResult>();
        var processed = new HashSet<string>();

        foreach (var bal in balanceList)
        {
            if (!string.IsNullOrEmpty(bal.OppositeAccountCode)
                && balanceList.Any(b => b.AccountCode == bal.OppositeAccountCode)
                && !processed.Contains(bal.AccountCode)
                && !processed.Contains(bal.OppositeAccountCode))
            {
                // Two-nature pair
                var opp = balanceList.First(b => b.AccountCode == bal.OppositeAccountCode);
                var (net1, nat1) = netMap.GetValueOrDefault(bal.AccountCode, (0m, BalanceNature.Debit));
                var (net2, nat2) = netMap.GetValueOrDefault(opp.AccountCode, (0m, BalanceNature.Debit));
                var (pool, nature) =
                    nat1 == nat2 ? (net1 + net2, nat1) : (Math.Abs(net1 - net2), net1 > net2 ? nat1 : nat2);
                {
                    // Allocate to first by opening
                    decimal alloc1 = Math.Min(pool, bal.OpeningBalance);
                    decimal alloc2 = pool - alloc1;

                    results.Add(CreateResult(bal, alloc1, nature));
                    results.Add(CreateResult(opp, alloc2, nature));
                    processed.Add(bal.AccountCode);
                    processed.Add(opp.AccountCode);
                    continue;
                }
            }

            if (processed.Contains(bal.AccountCode)) continue;

            // Single-nature or unmatched
            var (net, nat) = netMap.GetValueOrDefault(bal.AccountCode, (0m, BalanceNature.Debit));
            // Cap to opening if opening>0
            decimal alloc = bal.OpeningBalance > 0 ? Math.Min(net, bal.OpeningBalance) : net;
            results.Add(CreateResult(bal, alloc, nat));
        }

        return results;
    }

    private static (decimal net, BalanceNature nature) ComputeNet(IEnumerable<JournalEntry> entries)
    {
        var deb = entries.Sum(e => e.TotalDebit);
        var cr = entries.Sum(e => e.TotalCredit);
        return cr >= deb ? (cr - deb, BalanceNature.Credit) : (deb - cr, BalanceNature.Debit);
    }

    private static AccountTurnoverResult CreateResult(
        AccountBalanceInput bal,
        decimal net,
        BalanceNature nat)
    {
        var openingSigned = bal.NormalBalance == BalanceNature.Debit ? bal.OpeningBalance : -bal.OpeningBalance;
        var turnoverSigned = nat == BalanceNature.Debit ? net : -net;
        var closingSigned = bal.NormalBalance == BalanceNature.Debit ? bal.ClosingBalance : -bal.ClosingBalance;

        var diff = openingSigned + turnoverSigned - closingSigned;
        var diffNat = diff >= 0 ? BalanceNature.Debit : BalanceNature.Credit;

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
            DifferenceNature = diffNat
        };
    }
}