using System;
using Xunit;
using YLproxy.Infrastructure;

namespace YLproxy.Tests;

public class AppSettingsServiceTests
{
    [Fact]
    public void Constructor_NullConfigPath_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AppSettingsService(null!));
    }

    [Fact]
    public void Constructor_EmptyConfigPath_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new AppSettingsService(""));
    }

    [Fact]
    public void Constructor_InvalidConfigPath_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new AppSettingsService("InvalidPath/AppSettings.json"));
    }
}
