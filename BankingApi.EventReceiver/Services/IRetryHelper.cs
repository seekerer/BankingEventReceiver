using BankingApi.EventReceiver.Models;

namespace BankingApi.EventReceiver.Services;

public interface IRetryHelper
{
    Task<T> RunWithRetry<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);

    Task RunWithRetryAsync(Func<Task> operation, CancellationToken cancellationToken = default);

}
