namespace Tiba.TradeFinanceAcl.Application.BalanceProcessor;

public class JournalEntry
{
    public string AccountCode { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public DateTime Date { get; set; }
}