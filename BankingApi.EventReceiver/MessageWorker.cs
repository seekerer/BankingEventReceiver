using BankingApi.EventReceiver.Exceptions;
using BankingApi.EventReceiver.Extensions;
using BankingApi.EventReceiver.Models;
using BankingApi.EventReceiver.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BankingApi.EventReceiver;

public class MessageWorker : BackgroundService
{
    private readonly IServiceBusReceiver _serviceBusReceiver;
    private readonly ITransactionProcessor _transactionProcessor;
    private readonly IRetryHelper _retryService;
    private readonly ILogger<MessageWorker> _logger;
    private readonly TimeSpan _emptyQueueDelay = TimeSpan.FromSeconds(10);

    public MessageWorker(
        IServiceBusReceiver serviceBusReceiver,
        ITransactionProcessor transactionProcessor,
        IRetryHelper retryService,
        ILogger<MessageWorker> logger)
    {
        _serviceBusReceiver = serviceBusReceiver ?? throw new ArgumentNullException(nameof(serviceBusReceiver));
        _transactionProcessor = transactionProcessor ?? throw new ArgumentNullException(nameof(transactionProcessor));
        _retryService = retryService ?? throw new ArgumentNullException(nameof(retryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Message worker started and listening for banking transactions...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessMessageAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Message worker stopping due to cancellation request");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in message worker main loop. Will continue processing.");
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
        }

        _logger.LogInformation("Message worker stopped");
    }

    private async Task ProcessMessageAsync(CancellationToken cancellationToken)
    {
        EventMessage? eventMessage = null;
        
        try
        {
            eventMessage = await _serviceBusReceiver.Peek();
            
            if (eventMessage == null)
            {
                _logger.LogDebug("Empty message was received, ignoring...");
                await Task.Delay(_emptyQueueDelay, cancellationToken);
                return;
            }

            _logger.LogInformation("Received message {MessageId} (Processing count: {ProcessingCount})", eventMessage.Id, eventMessage.ProcessingCount);

            if (eventMessage.ProcessingCount >= 3)
            {
                _logger.LogWarning("Message {MessageId} Will be moved to dead letter.", eventMessage.Id);
                await _serviceBusReceiver.MoveToDeadLetter(eventMessage);
                return;
            }

            await ProcessMessageWithRetryAsync(eventMessage, cancellationToken);
            
            await _serviceBusReceiver.Complete(eventMessage);
            _logger.LogInformation("Successfully completed processing of message {MessageId}", eventMessage.Id);
        }
        catch (Exception ex) when (eventMessage != null)
        {
            await HandleMessageProcessingErrorAsync(eventMessage, ex, cancellationToken);
        }
    }

    private async Task ProcessMessageWithRetryAsync(EventMessage eventMessage, CancellationToken cancellationToken)
    {
        try
        {
            await _retryService.RunWithRetryAsync(async () =>
            {
                var transactionMessage = DeserializeBalanceChangeEvent(eventMessage);
                await _transactionProcessor.ProcessTransactionAsync(transactionMessage, cancellationToken);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message {MessageId}", eventMessage.Id);
            throw;
        }
    }

    private async Task HandleMessageProcessingErrorAsync(EventMessage eventMessage, Exception exception, CancellationToken cancellationToken)
    {
        try
        {
            if (ShouldMoveToDeadLetter(exception))
            {
                _logger.LogWarning("Moving message {MessageId} to dead letter due to non-transient error: {ErrorMessage}", eventMessage.Id, exception.Message);
                await _serviceBusReceiver.MoveToDeadLetter(eventMessage);
            }
            else
            {
                _logger.LogWarning("Abandoning message {MessageId} due to transient error: {ErrorMessage}", eventMessage.Id, exception.Message);
                await _serviceBusReceiver.Abandon(eventMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle error for message {MessageId}. Original error: {OriginalError}",eventMessage.Id, exception.Message);
        }
    }

    private bool ShouldMoveToDeadLetter(Exception exception)
    {
        return exception switch
        {
            JsonException => true,
            ArgumentException => true,
            InvalidAccountIdException => true,
            InvalidBalanceChangeEventException => true,
            InvalidTransactionTypeException => true,
            TransactionAlreadyProcessedException => true,
            ProcessingException ex => !ex.IsTransient,
            _ => false
        };
    }

    private BalanceChangeEvent DeserializeBalanceChangeEvent(EventMessage eventMessage)
    {
        if (string.IsNullOrEmpty(eventMessage.MessageBody))
        {
            _logger.LogError("Message {MessageId} has empty body", eventMessage.Id);
            throw new InvalidBalanceChangeEventException();
        }

        try
        {
            var transactionMessage = JsonSerializer.Deserialize<BalanceChangeEvent>(
                eventMessage.MessageBody, 
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (transactionMessage == null)
            {
                _logger.LogError("Message {MessageId} could not be deserialized into BalanceChangeEvent", eventMessage.Id);
                AbortDueToParsingError();
            }

            if (transactionMessage?.Id == Guid.Empty)
            {
                _logger.LogError("Message {MessageId} has invalid event ID", eventMessage.Id);
                AbortDueToParsingError();
            }

            return transactionMessage;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "JSON parsing error for message {MessageId}", eventMessage.Id);
            AbortDueToParsingError();
            return null;
        }
    }

    private static void AbortDueToParsingError()
    {
        throw new InvalidBalanceChangeEventException();
    }
}
