using BankingApi.EventReceiver.Exceptions;
using BankingApi.EventReceiver.Models;
using BankingApi.EventReceiver.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.Json;

namespace BankingApi.EventReceiver.Tests;

[TestClass]
public class MessageWorkerTests
{
    private Mock<IServiceBusReceiver> serviceBusReceiverMock = null!;
    private Mock<ITransactionProcessor> transactionProcessorMock = null!;
    private Mock<IRetryHelper> retryServiceMock = null!;
    private Mock<ILogger<MessageWorker>> loggerMock = null!;
    private MessageWorker messageWorker = null!;
    private CancellationTokenSource cancellationTokenSource = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        serviceBusReceiverMock = new Mock<IServiceBusReceiver>();
        transactionProcessorMock = new Mock<ITransactionProcessor>();
        retryServiceMock = new Mock<IRetryHelper>();
        loggerMock = new Mock<ILogger<MessageWorker>>();
        cancellationTokenSource = new CancellationTokenSource();

        messageWorker = new MessageWorker(
            serviceBusReceiverMock.Object,
            transactionProcessorMock.Object,
            retryServiceMock.Object,
            loggerMock.Object);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        cancellationTokenSource?.Dispose();
    }

    [TestMethod]
    public void Constructor_WithNullServiceBusReceiver_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            new MessageWorker(
                null!,
                transactionProcessorMock.Object,
                retryServiceMock.Object,
                loggerMock.Object));
    }

    [TestMethod]
    public void Constructor_WithNullTransactionProcessor_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            new MessageWorker(
                serviceBusReceiverMock.Object,
                null!,
                retryServiceMock.Object,
                loggerMock.Object));
    }

    [TestMethod]
    public void Constructor_WithNullRetryService_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            new MessageWorker(
                serviceBusReceiverMock.Object,
                transactionProcessorMock.Object,
                null!,
                loggerMock.Object));
    }

    [TestMethod]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            new MessageWorker(
                serviceBusReceiverMock.Object,
                transactionProcessorMock.Object,
                retryServiceMock.Object,
                null!));
    }

    [TestMethod]
    public async Task ExecuteAsync_WhenNoMessages_WaitsAndContinues()
    {
        serviceBusReceiverMock.Setup(x => x.Peek())
            .ReturnsAsync((EventMessage?)null);

        cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(100));

        await messageWorker.StartAsync(cancellationTokenSource.Token);
        await Task.Delay(50);
        await messageWorker.StopAsync(cancellationTokenSource.Token);

        serviceBusReceiverMock.Verify(x => x.Peek(), Times.AtLeastOnce);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithValidMessage_ProcessesSuccessfully()
    {
        var transactionMessage = new BalanceChangeEvent
        {
            Id = Guid.NewGuid(),
            MessageType = TransactionType.Credit.ToString().ToString(),
            BankAccountId = Guid.NewGuid(),
            Amount = 100.50m
        };

        var eventMessage = new EventMessage
        {
            Id = Guid.NewGuid(),
            MessageBody = JsonSerializer.Serialize(transactionMessage),
            ProcessingCount = 1
        };

        serviceBusReceiverMock.SetupSequence(x => x.Peek())
            .ReturnsAsync(eventMessage)
            .ReturnsAsync((EventMessage?)null);

        retryServiceMock.Setup(x => x.RunWithRetryAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> func, CancellationToken ct) => func());

        cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(500));

        await messageWorker.StartAsync(cancellationTokenSource.Token);
        await Task.Delay(100);
        await messageWorker.StopAsync(cancellationTokenSource.Token);

        retryServiceMock.Verify(x => x.RunWithRetryAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()), Times.Once);
        transactionProcessorMock.Verify(x => x.ProcessTransactionAsync(It.IsAny<BalanceChangeEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        serviceBusReceiverMock.Verify(x => x.Complete(eventMessage), Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithHighProcessingCount_MovesToDeadLetter()
    {
        var eventMessage = new EventMessage
        {
            Id = Guid.NewGuid(),
            MessageBody = "{}",
            ProcessingCount = 3
        };

        serviceBusReceiverMock.SetupSequence(x => x.Peek())
            .ReturnsAsync(eventMessage)
            .ReturnsAsync((EventMessage?)null);

        cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(500));

        await messageWorker.StartAsync(cancellationTokenSource.Token);
        await Task.Delay(100);
        await messageWorker.StopAsync(cancellationTokenSource.Token);

        serviceBusReceiverMock.Verify(x => x.MoveToDeadLetter(eventMessage), Times.Once);
        transactionProcessorMock.Verify(x => x.ProcessTransactionAsync(It.IsAny<BalanceChangeEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithEmptyMessageBody_MovesToDeadLetter()
    {
        var eventMessage = new EventMessage
        {
            Id = Guid.NewGuid(),
            MessageBody = "",
            ProcessingCount = 1
        };

        serviceBusReceiverMock.SetupSequence(x => x.Peek())
            .ReturnsAsync(eventMessage)
            .ReturnsAsync((EventMessage?)null);

        retryServiceMock.Setup(x => x.RunWithRetryAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> func, CancellationToken ct) => func());

        cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(500));

        await messageWorker.StartAsync(cancellationTokenSource.Token);
        await Task.Delay(100);
        await messageWorker.StopAsync(cancellationTokenSource.Token);

        serviceBusReceiverMock.Verify(x => x.MoveToDeadLetter(eventMessage), Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithInvalidJson_MovesToDeadLetter()
    {
        var eventMessage = new EventMessage
        {
            Id = Guid.NewGuid(),
            MessageBody = "invalid json {",
            ProcessingCount = 1
        };

        serviceBusReceiverMock.SetupSequence(x => x.Peek())
            .ReturnsAsync(eventMessage)
            .ReturnsAsync((EventMessage?)null);

        retryServiceMock.Setup(x => x.RunWithRetryAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> func, CancellationToken ct) => func());

        cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(500));

        await messageWorker.StartAsync(cancellationTokenSource.Token);
        await Task.Delay(100);
        await messageWorker.StopAsync(cancellationTokenSource.Token);

        serviceBusReceiverMock.Verify(x => x.MoveToDeadLetter(eventMessage), Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithTransientException_AbandonsMessage()
    {
        var transactionMessage = new BalanceChangeEvent
        {
            Id = Guid.NewGuid(),
            MessageType = TransactionType.Credit.ToString(),
            BankAccountId = Guid.NewGuid(),
            Amount = 100.50m
        };

        var eventMessage = new EventMessage
        {
            Id = Guid.NewGuid(),
            MessageBody = JsonSerializer.Serialize(transactionMessage),
            ProcessingCount = 1
        };

        serviceBusReceiverMock.SetupSequence(x => x.Peek())
            .ReturnsAsync(eventMessage)
            .ReturnsAsync((EventMessage?)null);

        retryServiceMock.Setup(x => x.RunWithRetryAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Transient error"));

        cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(500));

        await messageWorker.StartAsync(cancellationTokenSource.Token);
        await Task.Delay(100);
        await messageWorker.StopAsync(cancellationTokenSource.Token);

        serviceBusReceiverMock.Verify(x => x.Abandon(eventMessage), Times.Once);
    }

    [TestMethod]
    public async Task ExecuteAsync_WithAccountNotFoundException_MovesToDeadLetter()
    {
        var transactionMessage = new BalanceChangeEvent
        {
            Id = Guid.NewGuid(),
            MessageType = TransactionType.Credit.ToString(),
            BankAccountId = Guid.NewGuid(),
            Amount = 100.50m
        };

        var eventMessage = new EventMessage
        {
            Id = Guid.NewGuid(),
            MessageBody = JsonSerializer.Serialize(transactionMessage),
            ProcessingCount = 1
        };

        serviceBusReceiverMock.SetupSequence(x => x.Peek())
            .ReturnsAsync(eventMessage)
            .ReturnsAsync((EventMessage?)null);

        retryServiceMock.Setup(x => x.RunWithRetryAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidAccountIdException(transactionMessage.BankAccountId.ToString()));

        cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(50000));

        await messageWorker.StartAsync(cancellationTokenSource.Token);
        await Task.Delay(100);
        await messageWorker.StopAsync(cancellationTokenSource.Token);

        serviceBusReceiverMock.Verify(x => x.MoveToDeadLetter(eventMessage), Times.Once);
    }

    [TestMethod]
    public async Task ParseTransactionMessage_WithEmptyGuid_ThrowsTransactionProcessingException()
    {
        var transactionMessage = new BalanceChangeEvent
        {
            Id = Guid.Empty,
            MessageType = TransactionType.Credit.ToString(),
            BankAccountId = Guid.NewGuid(),
            Amount = 100.50m
        };

        var eventMessage = new EventMessage
        {
            Id = Guid.NewGuid(),
            MessageBody = JsonSerializer.Serialize(transactionMessage),
            ProcessingCount = 1
        };

        serviceBusReceiverMock.SetupSequence(x => x.Peek())
            .ReturnsAsync(eventMessage)
            .ReturnsAsync((EventMessage?)null);

        retryServiceMock.Setup(x => x.RunWithRetryAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .Returns((Func<Task> func, CancellationToken ct) => func());

        cancellationTokenSource.CancelAfter(TimeSpan.FromMilliseconds(500));

        await messageWorker.StartAsync(cancellationTokenSource.Token);
        await Task.Delay(100);
        await messageWorker.StopAsync(cancellationTokenSource.Token);

        serviceBusReceiverMock.Verify(x => x.MoveToDeadLetter(eventMessage), Times.Once);
    }
}
