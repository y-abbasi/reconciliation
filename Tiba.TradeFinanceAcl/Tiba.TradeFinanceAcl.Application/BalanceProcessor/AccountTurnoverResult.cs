namespace Tiba.TradeFinanceAcl.Application.BalanceProcessor;

public class AccountTurnoverResult
{
    public string AccountCode { get; set; }
    public decimal OpeningBalance { get; set; }
    public BalanceNature OpeningBalanceNature { get; set; }
    public decimal TotalTurnover { get; set; }
    public BalanceNature TotalTurnoverNature { get; set; }
    public decimal ClosingBalance { get; set; }
    public BalanceNature ClosingBalanceNature { get; set; }
    public decimal Difference { get; set; }
    public BalanceNature DifferenceNature { get; set; }
}