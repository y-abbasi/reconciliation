namespace Tiba.TradeFinanceAcl.Application;

public class TransactionRecord
{
    public string AccountCode { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; }
    public DateTime EffectiveDate { get; set; }
    public TransactionType Type { get; set; }
    public string VoucherNumber { get; set; }
    public int Sequence { get; set; }
    public Guid TransactionId { get; set; }
}