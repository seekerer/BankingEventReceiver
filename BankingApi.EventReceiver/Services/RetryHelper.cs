using BankingApi.EventReceiver.Extensions;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace BankingApi.EventReceiver.Services;

public class RetryHelper : IRetryHelper
{
    private ILogger<RetryHelper> _logger;
    private static readonly int[] retryDelays = { 5, 25, 125 };
   
    public RetryHelper(ILogger<RetryHelper> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<T> RunWithRetry<T>(Func<Task<T>> func, CancellationToken cancellationToken = default)
    {
        return await defaultPolicy.ExecuteAsync(async (ct) => await func(), cancellationToken);
    }

    public async Task RunWithRetryAsync(Func<Task> func, CancellationToken cancellationToken = default)
    {
        await defaultPolicy.ExecuteAsync(async (ct) => await func(), cancellationToken);
    }

    private AsyncRetryPolicy defaultPolicy => Policy
            .Handle<Exception>(ex => ex.IsTransient())
            .WaitAndRetryAsync(
                retryCount: retryDelays.Length,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(retryDelays[retryAttempt - 1]),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning(outcome,
                        "Transient error occurred. Waiting {DelaySeconds} seconds before retry attempt {RetryCount}. Error: {ErrorMessage}",
                        timespan.TotalSeconds, retryCount, outcome.Message);
                });
}
