using BankingApi.EventReceiver.Models;

namespace BankingApi.EventReceiver.Services;

public interface ITransactionProcessor
{
    Task ProcessTransactionAsync(BalanceChangeEvent transactionMessage, CancellationToken cancellationToken = default);
}
