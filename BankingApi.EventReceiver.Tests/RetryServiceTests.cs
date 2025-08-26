using BankingApi.EventReceiver.Models;
using BankingApi.EventReceiver.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace BankingApi.EventReceiver.Tests;

[TestClass]
public class RetryServiceTests
{
    private Mock<ILogger<RetryHelper>> loggerMock = null!;
    private RetryHelper retryService = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        loggerMock = new Mock<ILogger<RetryHelper>>();
        retryService = new RetryHelper(loggerMock.Object);
    }

    [TestMethod]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            new RetryHelper(null!));
    }

    [TestMethod]
    public async Task ExecuteWithRetryAsync_WithSuccessfulOperation_ExecutesOnce()
    {
        var executionCount = 0;
        var operation = new Func<Task>(() =>
        {
            executionCount++;
            return Task.CompletedTask;
        });

        await retryService.RunWithRetryAsync(operation);

        Assert.AreEqual(1, executionCount);
    }

    [TestMethod]
    public async Task ExecuteWithRetryAsync_Generic_WithSuccessfulOperation_ReturnsResult()
    {
        var expectedResult = "Success";
        var executionCount = 0;
        var operation = new Func<Task<string>>(() =>
        {
            executionCount++;
            return Task.FromResult(expectedResult);
        });

        var result = await retryService.RunWithRetry(operation);

        Assert.AreEqual(expectedResult, result);
        Assert.AreEqual(1, executionCount);
    }

    [TestMethod]
    public async Task ExecuteWithRetryAsync_WithTransientException_RetriesAndSucceeds()
    {
        var executionCount = 0;
        var operation = new Func<Task>(() =>
        {
            executionCount++;
            if (executionCount < 3)
            {
                throw new DbUpdateConcurrencyException();
            }
            return Task.CompletedTask;
        });

        await retryService.RunWithRetryAsync(operation);

        Assert.AreEqual(3, executionCount);
    }

    [TestMethod]
    public async Task ExecuteWithRetryAsync_Generic_WithTransientException_RetriesAndReturnsResult()
    {
        var expectedResult = 42;
        var executionCount = 0;
        var operation = new Func<Task<int>>(() =>
        {
            executionCount++;
            if (executionCount < 2)
            {
                throw new DbUpdateConcurrencyException("Concurrency error");
            }
            return Task.FromResult(expectedResult);
        });

        var result = await retryService.RunWithRetry(operation);

        Assert.AreEqual(expectedResult, result);
        Assert.AreEqual(2, executionCount);
    }

    [TestMethod]
    public async Task ExecuteWithRetryAsync_WithNonTransientException_DoesNotRetry()
    {
        var executionCount = 0;
        var operation = new Func<Task>(() =>
        {
            executionCount++;
            throw new ArgumentException("Bad Type");
        });

        await Assert.ThrowsExceptionAsync<ArgumentException>(() =>
            retryService.RunWithRetryAsync(operation));

        Assert.AreEqual(1, executionCount);
    }
}
