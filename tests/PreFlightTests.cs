using System;
using System.IO;
using YLproxy.Core.PreFlight;
using YLproxy.Infrastructure;
using Xunit;

namespace YLproxy.Tests;

public sealed class PreFlightTests
{
    [Fact]
    public void PreFlight_Run_ShouldReturnPassedFalseOrTrue_ButNeverThrow()
    {
        // Uses real repository root; may fail environment checks, but should not throw.
        var result = PreFlightChecker.Run(new AppSettingsService("AppSettings.json"));
        Assert.NotNull(result);
        Assert.True(result.Errors.Count >= 0);
    }

    [Fact]
    public void AutoStartService_SetAutoStart_DisableDoesNotThrow()
    {
        AutoStartService.SetAutoStart(false, executablePath: "C:\\fake\\path\\YLproxy.exe");
        Assert.True(true);
    }

    [Fact]
    public void AutoStartService_SetAutoStart_Enable_WithFakePath_ShouldNotThrow()
    {
        // On non-admin/CI this might fail due to registry access; requirement is: should not crash the test process.
        // If it throws in some environments, that will be surfaced.
        AutoStartService.SetAutoStart(true, executablePath: "C:\\fake\\path\\YLproxy.exe");
        Assert.True(true);
    }
}

