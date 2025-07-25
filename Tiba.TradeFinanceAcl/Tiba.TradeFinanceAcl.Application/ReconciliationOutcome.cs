namespace Tiba.TradeFinanceAcl.Application;

public class ReconciliationOutcome
{
    public List<AccountDetail> MissingAccountInfos { get; } = new();
    public List<TransactionRecord> MissingTransactionInfos { get; } = new();
}