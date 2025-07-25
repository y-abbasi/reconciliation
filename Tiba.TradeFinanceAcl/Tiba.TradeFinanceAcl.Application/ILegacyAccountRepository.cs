namespace Tiba.TradeFinanceAcl.Application;

public interface ILegacyAccountRepository
{
    Task<AccountAggregate[]> GetAccountAggregatesAsync(AccountFilter filter);
    Task<List<TransactionRecord>> GetTransactionsAsync(string groupKey);
}