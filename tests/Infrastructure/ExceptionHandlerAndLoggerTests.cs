using System;
using System.Threading.Tasks;
using Moq;
using YLproxy.Infrastructure;
using Xunit;

namespace YLproxy.Tests.Infrastructure;

public sealed class ExceptionHandlerAndLoggerTests
{
    [Fact]
    public void Handle_ShouldLogAndNotify()
    {
        var mock = new Mock<ILogger>();
        var notified = false;
        ExceptionHandler.OnUserNotification = (_, _) => notified = true;

        ExceptionHandler.Handle(new InvalidOperationException("boom"), mock.Object, "ctx", showUser: true, data: new { Id = 1 });

        mock.Verify(m => m.Log(LogLevel.Error, It.Is<string>(s => s.Contains("ctx")), It.IsAny<Exception>(), It.IsAny<object?>()), Times.Once);
        Assert.True(notified);
        ExceptionHandler.OnUserNotification = null;
    }

    [Fact]
    public void TryCatch_ShouldReturnDefaultOnException()
    {
        var mock = new Mock<ILogger>();
        var result = ExceptionHandler.TryCatch(() => throw new InvalidOperationException("x"), mock.Object, "sync", 99);

        Assert.Equal(99, result);
    }

    [Fact]
    public async Task TryCatchAsync_ShouldReturnDefaultOnException()
    {
        var mock = new Mock<ILogger>();
        var result = await ExceptionHandler.TryCatchAsync<int>(async () =>
        {
            await Task.Yield();
            throw new InvalidOperationException("x");
        }, mock.Object, "async", 88);

        Assert.Equal(88, result);
    }

    [Fact]
    public void Logger_StaticMethods_ShouldForwardToDefaultLogger()
    {
        var mock = new Mock<ILogger>();
        Logger.Default = mock.Object;

        Logger.Info("i", new { A = 1 });
        Logger.Warn("w");
        Logger.Debug("d");
        Logger.Error("e");
        Logger.Fatal("f");
        Logger.Error("ex", new InvalidOperationException("boom"));
        Logger.Fatal("fx", new InvalidOperationException("boom"));

        mock.Verify(m => m.Log(LogLevel.Info, "i", It.IsAny<object?>()), Times.Once);
        mock.Verify(m => m.Log(LogLevel.Warn, "w", It.IsAny<object?>()), Times.Once);
        mock.Verify(m => m.Log(LogLevel.Debug, "d", It.IsAny<object?>()), Times.Once);
        mock.Verify(m => m.Log(LogLevel.Error, "e", It.IsAny<object?>()), Times.Once);
        mock.Verify(m => m.Log(LogLevel.Fatal, "f", It.IsAny<object?>()), Times.Once);
        mock.Verify(m => m.Log(LogLevel.Error, "ex", It.IsAny<Exception>(), It.IsAny<object?>()), Times.Once);
        mock.Verify(m => m.Log(LogLevel.Fatal, "fx", It.IsAny<Exception>(), It.IsAny<object?>()), Times.Once);
    }

    [Fact]
    public void Logger_ContextAndJson_ShouldWork()
    {
        var context = Logger.CreateContext(("K1", 1), ("K2", "v"));

        Assert.Equal(2, context.Count);
        Assert.Contains("\"K1\":1", Logger.ToJson(context));
        Assert.Equal("null", Logger.ToJson(null));
    }
}
