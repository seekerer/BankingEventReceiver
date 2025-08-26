using BankingApi.EventReceiver.Exceptions;
using BankingApi.EventReceiver.Models;
using BankingApi.EventReceiver.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;

namespace BankingApi.EventReceiver.Tests;

[TestClass]
public class TransactionProcessorTests
{
    private BankingApiDbContext context = null!;
    private Mock<ILogger<TransactionProcessor>> loggerMock = null!;
    private TransactionProcessor transactionProcessor = null!;
    private DbContextOptions<BankingApiDbContext> options = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        options = new DbContextOptionsBuilder<BankingApiDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        context = new BankingApiDbContext(options);
        loggerMock = new Mock<ILogger<TransactionProcessor>>();
        transactionProcessor = new TransactionProcessor(context, loggerMock.Object);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        context.Dispose();
    }

    [TestMethod]
    public void Constructor_WithNullContext_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            new TransactionProcessor(null!, loggerMock.Object));
    }

    [TestMethod]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            new TransactionProcessor(context, null!));
    }

    [TestMethod]
    public async Task ProcessTransactionAsync_WithNullTransactionMessage_ThrowsArgumentNullException()
    {
        await Assert.ThrowsExceptionAsync<ArgumentNullException>(() =>
            transactionProcessor.ProcessTransactionAsync(null!));
    }

    [TestMethod]
    public async Task ProcessTransactionAsync_WithUnsupportedMessageType_ThrowsUnsupportedMessageTypeException()
    {
        var existingAccountId = Guid.NewGuid();
        context.Add(new BankAccount
        {
            Id = existingAccountId,
            Balance = 10m
        });
        await context.SaveChangesAsync();
        var transactionMessage = new BalanceChangeEvent
        {
            Id = Guid.NewGuid(),
            MessageType = "Whatever",
            BankAccountId = existingAccountId,
            Amount = 100.50m
        };

        var exception = await Assert.ThrowsExceptionAsync<InvalidTransactionTypeException>(() =>
            transactionProcessor.ProcessTransactionAsync(transactionMessage));

        Assert.IsTrue(exception.Message.Contains("Unsupported transaction type"));
    }

    [TestMethod]
    public async Task ProcessTransactionAsync_WithNonExistentAccount_ThrowsAccountNotFoundException()
    {
        var transactionMessage = new BalanceChangeEvent
        {
            Id = Guid.NewGuid(),
            MessageType = TransactionType.Credit.ToString(),
            BankAccountId = Guid.NewGuid(),
            Amount = 8322.60m
        };

        var exception = await Assert.ThrowsExceptionAsync<InvalidAccountIdException>(() =>
            transactionProcessor.ProcessTransactionAsync(transactionMessage));

        Assert.IsTrue(exception.Message.Contains(transactionMessage.BankAccountId.ToString()));
    }

    [TestMethod]
    public async Task ProcessTransactionAsync_WithCreditTransaction_IncreasesBalance()
    {
        var accountId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();

        var bankAccount = new BankAccount
        {
            Id = accountId,
            Balance = 92816m
        };

        context.BankAccounts.Add(bankAccount);
        await context.SaveChangesAsync();

        var transactionMessage = new BalanceChangeEvent
        {
            Id = transactionId,
            MessageType = TransactionType.Credit.ToString(),
            BankAccountId = accountId,
            Amount = 1000m
        };

        await transactionProcessor.ProcessTransactionAsync(transactionMessage);

        var updatedAccount = await context.BankAccounts.FirstAsync(a => a.Id == accountId);
        Assert.AreEqual(93816m, updatedAccount.Balance);

    }

    [TestMethod]
    public async Task ProcessTransactionAsync_WithDebitTransaction_DecreasesBalance()
    {
        var accountId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();

        var bankAccount = new BankAccount
        {
            Id = accountId,
            Balance = 1000m
        };

        context.BankAccounts.Add(bankAccount);
        await context.SaveChangesAsync();

        var transactionMessage = new BalanceChangeEvent
        {
            Id = transactionId,
            MessageType = TransactionType.Debit.ToString(),
            BankAccountId = accountId,
            Amount = 500m
        };

        await transactionProcessor.ProcessTransactionAsync(transactionMessage);

        var updatedAccount = await context.BankAccounts.FirstAsync(a => a.Id == accountId);
        Assert.AreEqual(500m, updatedAccount.Balance);
    }

    [TestMethod]
    public async Task ProcessTransactionAsync_WithDebitTransaction_AllowsNegativeBalance()
    {
        var accountId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();

        var bankAccount = new BankAccount
        {
            Id = accountId,
            Balance = 100m
        };

        context.BankAccounts.Add(bankAccount);
        await context.SaveChangesAsync();

        var transactionMessage = new BalanceChangeEvent
        {
            Id = transactionId,
            MessageType = TransactionType.Debit.ToString(),
            BankAccountId = accountId,
            Amount = 150m
        };

        await transactionProcessor.ProcessTransactionAsync(transactionMessage);

        var updatedAccount = await context.BankAccounts.FirstAsync(a => a.Id == accountId);
        Assert.AreEqual(-50m, updatedAccount.Balance);
    }

    [TestMethod]
    public async Task ProcessTransactionAsync_WithGeneralException_ThrowsObjectDisposedException()
    {
        var accountId = Guid.NewGuid();
        var transactionId = Guid.NewGuid();

        var bankAccount = new BankAccount
        {
            Id = accountId,
            Balance = 1000m
        };

        context.BankAccounts.Add(bankAccount);
        await context.SaveChangesAsync();

        context.Dispose();

        var transactionMessage = new BalanceChangeEvent
        {
            Id = transactionId,
            MessageType = TransactionType.Credit.ToString(),
            BankAccountId = accountId,
            Amount = 100.50m
        };

        var exception = await Assert.ThrowsExceptionAsync<ObjectDisposedException>(() =>
            transactionProcessor.ProcessTransactionAsync(transactionMessage));

        Assert.IsNotNull(exception);
        Assert.IsTrue(exception.Message.Contains("disposed") || exception.Message.Contains("BankingApiDbContext"));
    }

    [TestMethod]
    public async Task ProcessTransactionAsync_WithMultipleTransactions_ProcessesSequentially()
    {
        var accountId = Guid.NewGuid();

        var bankAccount = new BankAccount
        {
            Id = accountId,
            Balance = 1000m
        };

        context.BankAccounts.Add(bankAccount);
        await context.SaveChangesAsync();

        var creditTransaction = new BalanceChangeEvent
        {
            Id = Guid.NewGuid(),
            MessageType = TransactionType.Credit.ToString(),
            BankAccountId = accountId,
            Amount = 200m
        };

        var debitTransaction = new BalanceChangeEvent
        {
            Id = Guid.NewGuid(),
            MessageType = TransactionType.Debit.ToString(),
            BankAccountId = accountId,
            Amount = 150m
        };

        await transactionProcessor.ProcessTransactionAsync(creditTransaction);
        await transactionProcessor.ProcessTransactionAsync(debitTransaction);

        var updatedAccount = await context.BankAccounts.FirstAsync(a => a.Id == accountId);
        Assert.AreEqual(1050m, updatedAccount.Balance);
    }
}
