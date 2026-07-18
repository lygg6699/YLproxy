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
    public void PreFlightResult_Defaults_HaveEmptyCollections()
    {
        var result = new PreFlightResult();
        Assert.False(result.Passed); // default is false
        Assert.NotNull(result.Errors);
        Assert.NotNull(result.Warnings);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void PreFlightResult_Passed_DefaultsToFalse()
    {
        var result = new PreFlightResult();
        // Passed is explicitly set by Run(), defaults to false
        Assert.False(result.Passed);
    }

    [Fact]
    public void PreFlightResult_CanSetPassedProgrammatically()
    {
        var result = new PreFlightResult { Passed = true };
        Assert.True(result.Passed);
        Assert.NotNull(result.Errors);
        Assert.NotNull(result.Warnings);
    }

    [Fact]
    public void AutoStartService_IsAutoStartEnabled_DoesNotThrow()
    {
        // Should not crash on any platform
        var enabled = AutoStartService.IsAutoStartEnabled();
        Assert.True(enabled || !enabled); // always passes—just verifying it doesn't throw
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

