using System;
using System.Globalization;
using System.Windows.Media;
using YLproxy.GUI;
using YLproxy.GUI.Converters;
using YLproxy.Models;
using Xunit;

namespace YLproxy.Tests.GUI;

public sealed class ConverterAndCommandTests
{
    [Fact]
    public void RelayCommand_ExecuteAndCanExecute_WorkAsExpected()
    {
        var executed = false;
        var cmd = new RelayCommand(() => executed = true, () => true);

        Assert.True(cmd.CanExecute(null));
        cmd.Execute(null);
        Assert.True(executed);
    }

    [Fact]
    public void RelayCommandT_InvalidParameter_ShouldThrow()
    {
        var cmd = new RelayCommand<int>(_ => { }, _ => true);
        Assert.Throws<InvalidOperationException>(() => cmd.Execute("invalid"));
    }

    [Fact]
    public void RelayCommandT_NullReferenceType_ShouldExecute()
    {
        string? captured = "x";
        var cmd = new RelayCommand<string?>(value => captured = value);

        cmd.Execute(null);

        Assert.Null(captured);
    }

    [Theory]
    [InlineData(ProxyStatus.Running, 0x2E, 0x7D, 0x32)]
    [InlineData(ProxyStatus.Failed, 0xB0, 0x00, 0x20)]
    [InlineData(ProxyStatus.Stopped, 0x88, 0x88, 0x88)]
    public void StatusToColorConverter_ShouldMapStatusToColor(ProxyStatus status, byte r, byte g, byte b)
    {
        var converter = new StatusToColorConverter();
        var result = converter.Convert(status, typeof(Brush), string.Empty, CultureInfo.InvariantCulture);

        var brush = Assert.IsType<SolidColorBrush>(result);
        Assert.Equal(Color.FromRgb(r, g, b), brush.Color);
    }

    [Fact]
    public void ApiStatusColorConverter_ShouldMapStrings()
    {
        var converter = new ApiStatusColorConverter();

        var running = Assert.IsType<SolidColorBrush>(converter.Convert("running", typeof(Brush), string.Empty, CultureInfo.InvariantCulture));
        var stopped = Assert.IsType<SolidColorBrush>(converter.Convert("STOPPED", typeof(Brush), string.Empty, CultureInfo.InvariantCulture));

        Assert.Equal(Color.FromRgb(0x2E, 0x7D, 0x32), running.Color);
        Assert.Equal(Color.FromRgb(0x88, 0x88, 0x88), stopped.Color);
    }

    [Fact]
    public void InverseBoolConverter_ShouldInvertBothWays()
    {
        var converter = new InverseBoolConverter();

        Assert.Equal(false, converter.Convert(true, typeof(bool), string.Empty, CultureInfo.InvariantCulture));
        Assert.Equal(true, converter.ConvertBack(false, typeof(bool), string.Empty, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void LogLevelToColorConverter_ShouldHandleEntryAndString()
    {
        var converter = new LogLevelToColorConverter();

        var entryBrush = Assert.IsType<SolidColorBrush>(converter.Convert(new LogEntry { Level = LogLevel.Error }, typeof(Brush), string.Empty, CultureInfo.InvariantCulture));
        var textBrush = Assert.IsType<SolidColorBrush>(converter.Convert("[WARN] something", typeof(Brush), string.Empty, CultureInfo.InvariantCulture));

        Assert.Equal(Color.FromRgb(0xB0, 0x00, 0x20), entryBrush.Color);
        Assert.Equal(Color.FromRgb(0xF9, 0xA8, 0x25), textBrush.Color);
    }

    [Fact]
    public void Converters_ConvertBack_ForUnsupported_ShouldThrow()
    {
        Assert.Throws<NotSupportedException>(() => new StatusToColorConverter().ConvertBack(new object(), typeof(object), string.Empty, CultureInfo.InvariantCulture));
        Assert.Throws<NotSupportedException>(() => new ApiStatusColorConverter().ConvertBack(new object(), typeof(object), string.Empty, CultureInfo.InvariantCulture));
        Assert.Throws<NotSupportedException>(() => new LogLevelToColorConverter().ConvertBack(new object(), typeof(object), string.Empty, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void LogEntry_FromRawString_ShouldDetectLevel()
    {
        var fatal = LogEntry.FromRawString("[FATAL] boom");
        var warn = LogEntry.FromRawString("[WARN] caution");

        Assert.Equal(LogLevel.Fatal, fatal.Level);
        Assert.Equal(LogLevel.Warn, warn.Level);
    }
}
