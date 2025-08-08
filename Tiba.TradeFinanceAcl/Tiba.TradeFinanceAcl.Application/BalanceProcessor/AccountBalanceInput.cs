namespace Tiba.TradeFinanceAcl.Application.BalanceProcessor;

public class AccountBalanceInput
{
    public string AccountCode { get; set; }
    public BalanceNature NormalBalance { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public string Currency { get; set; }
    public string OppositeAccountCode { get; set; }
}