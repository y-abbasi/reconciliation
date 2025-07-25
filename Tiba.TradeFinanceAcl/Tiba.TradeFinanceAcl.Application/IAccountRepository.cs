namespace Tiba.TradeFinanceAcl.Application;

public interface IAccountRepository
{
    Task<Dictionary<string, AccountAggregate>> GetAccountAggregatesAsync(AccountFilter filter);
    Task<List<TransactionRecord>> GetTransactionsAsync(string groupKey);
}