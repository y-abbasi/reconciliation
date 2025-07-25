using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Tiba.TradeFinanceAcl.Application
{
    /// <summary>
    /// نمایندهٔ یک حساب با تراز و گردش
    /// </summary>
    public class AccountAggregate
    {
        public string GroupKey { get; set; }
        public decimal Balance { get; set; }
        public decimal Turnover { get; set; }
        public int Level { get; set; }

        public bool IsDualNatureSgb() =>
            GroupKey.CbiDb() != GroupKey.CbiCr();
    }
}