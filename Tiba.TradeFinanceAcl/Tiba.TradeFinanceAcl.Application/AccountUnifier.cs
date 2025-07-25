namespace Tiba.TradeFinanceAcl.Application;

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