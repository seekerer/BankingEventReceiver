using BankingApi.EventReceiver.Exceptions;
using BankingApi.EventReceiver.Extensions;
using BankingApi.EventReceiver.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BankingApi.EventReceiver.Services;

public class TransactionProcessor : ITransactionProcessor
{
    private readonly BankingApiDbContext _context;
    private readonly ILogger<TransactionProcessor> _logger;

    public TransactionProcessor(BankingApiDbContext context, ILogger<TransactionProcessor> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ProcessTransactionAsync(BalanceChangeEvent balanceChangeEvent, CancellationToken cancellationToken = default)
    {
        if (balanceChangeEvent == null)
            throw new ArgumentNullException(nameof(balanceChangeEvent));

        _logger.LogInformation("Processing event {BalanceChangeEventId} of type {TransactionType} for account {AccountId} with amount {Amount}",
            balanceChangeEvent.Id, balanceChangeEvent.MessageType, balanceChangeEvent.BankAccountId, balanceChangeEvent.Amount);

        // we should aim at least for atomicity otherwise we can't guarantee consistency
        using (var transaction = await _context.Database.BeginTransactionAsync(cancellationToken))
        {
            try
            {
                var bankAccount = await _context.BankAccounts.FirstOrDefaultAsync(ba => ba.Id == balanceChangeEvent.BankAccountId, cancellationToken);

                if (bankAccount == null)
                {
                    _logger.LogWarning("Account {AccountId} does not Exist! TransactionId: {TransactionId}", balanceChangeEvent.BankAccountId, balanceChangeEvent.Id);
                    throw new InvalidAccountIdException(balanceChangeEvent.BankAccountId.ToString() ?? "");
                }

                var currentAccountBalance = bankAccount.Balance;
                var newBalance = CalculateNewBalance(currentAccountBalance, balanceChangeEvent);

                bankAccount.Balance = newBalance;

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("Account {AccountId} balance updated from {OldBalance} to {NewBalance}",
                    balanceChangeEvent.Id, balanceChangeEvent.BankAccountId, currentAccountBalance, newBalance);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogWarning(ex, "Concurrency conflict while processing transaction {TransactionId} for account {AccountId}. Will retry.",
                    balanceChangeEvent.Id, balanceChangeEvent.BankAccountId);
                throw;

            }
            catch (Exception ex) when (!(ex.IsTransient()))
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Unexpected error processing transaction {TransactionId} for account {AccountId}", balanceChangeEvent.Id, balanceChangeEvent.BankAccountId);

                throw;
            }
        }
    }

    private decimal CalculateNewBalance(decimal currentBalance, BalanceChangeEvent transactionMessage)
    {
        return transactionMessage.TransactionType switch
        {
            TransactionType.Credit => currentBalance + transactionMessage.Amount,
            TransactionType.Debit => currentBalance - transactionMessage.Amount,
            _ => throw new InvalidTransactionTypeException($"Unsupported transaction type: {transactionMessage.TransactionType}")
        };
    }
}
