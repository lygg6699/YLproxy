using YLproxy.Utils;

namespace YLproxy.Tests;

[Trait("Category", "Unit")]
public class PathHelperTests
{
    [Fact]
    public void Combine_WithMultipleSegments_ReturnsCombinedPath()
    {
        var result = PathHelper.Combine("a", "b", "c");
        Assert.Equal(Path.Combine("a", "b", "c"), result);
    }

    [Fact]
    public void Combine_WithSingleSegment_ReturnsSamePath()
    {
        var result = PathHelper.Combine("single");
        Assert.Equal("single", result);
    }

    [Fact]
    public void Combine_WithNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PathHelper.Combine(null!));
    }

    [Fact]
    public void Normalize_WithRelativePath_ReturnsFullPath()
    {
        var result = PathHelper.Normalize(".");
        Assert.True(Path.IsPathRooted(result));
    }

    [Fact]
    public void Normalize_WithNull_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentException>(() => PathHelper.Normalize(null!));
    }

    [Fact]
    public void Normalize_WithEmptyString_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentException>(() => PathHelper.Normalize(""));
    }

    [Fact]
    public void EnsureDirectorySeparator_WithTrailingSlash_ReturnsNormalized()
    {
        var result = PathHelper.EnsureDirectorySeparator("a/b/");
        Assert.EndsWith(Path.DirectorySeparatorChar.ToString(), result);
        Assert.DoesNotContain("//", result.Replace('\\', '/'));
    }

    [Fact]
    public void EnsureDirectorySeparator_WithoutTrailingSlash_AddsIt()
    {
        var result = PathHelper.EnsureDirectorySeparator("a/b");
        Assert.EndsWith(Path.DirectorySeparatorChar.ToString(), result);
    }

    [Fact]
    public void GetDirectoryName_FromFilePath_ReturnsDirectory()
    {
        var result = PathHelper.GetDirectoryName("a/b/file.txt");
        var expected = Path.GetDirectoryName("a/b/file.txt")!;
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetFileName_FromFilePath_ReturnsFileName()
    {
        var result = PathHelper.GetFileName("a/b/file.txt");
        Assert.Equal("file.txt", result);
    }

    [Fact]
    public void GetFileNameWithoutExtension_FromFilePath_ReturnsNameOnly()
    {
        var result = PathHelper.GetFileNameWithoutExtension("a/b/file.txt");
        Assert.Equal("file", result);
    }

    [Fact]
    public void IsPathRooted_WithAbsolutePath_ReturnsTrue()
    {
        var result = PathHelper.IsPathRooted(Path.GetFullPath("."));
        Assert.True(result);
    }

    [Fact]
    public void IsPathRooted_WithRelativePath_ReturnsFalse()
    {
        var result = PathHelper.IsPathRooted("relative/path");
        Assert.False(result);
    }

    [Fact]
    public void ChangeExtension_ReplacesExtension()
    {
        var result = PathHelper.ChangeExtension("file.txt", ".json");
        Assert.Equal("file.json", result);
    }

    [Fact]
    public void GetTempFileName_ReturnsPathWithTmpExtension()
    {
        var result = PathHelper.GetTempFileName();
        Assert.EndsWith(".tmp", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetTempFileName_WithCustomDirectory_ReturnsPathInDirectory()
    {
        var dir = Path.GetTempPath();
        var result = PathHelper.GetTempFileName(dir);
        Assert.StartsWith(dir, result, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(".tmp", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_CombinesAndNormalizes()
    {
        var result = PathHelper.Resolve("a", "b", "c");
        Assert.True(Path.IsPathRooted(result));
    }

    [Fact]
    public void EnsureDirectoryExists_CreatesDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var testFile = Path.Combine(tempDir, "test.txt");
        try
        {
            PathHelper.EnsureDirectoryExists(testFile);
            Assert.True(Directory.Exists(tempDir));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir);
        }
    }

    /// <summary>
    /// Verifies cross-platform separator behavior: PathHelper.Combine
    /// should use the platform's native directory separator.
    /// </summary>
    [Fact]
    public void Combine_UsesPlatformDirectorySeparator()
    {
        var result = PathHelper.Combine("a", "b", "c");
        var sep = Path.DirectorySeparatorChar;
        var expected = $"a{sep}b{sep}c";
        Assert.Equal(expected, result);
    }

    /// <summary>
    /// Verifies that EnsureDirectorySeparator trims duplicates.
    /// </summary>
    [Fact]
    public void EnsureDirectorySeparator_TrimsDuplicates()
    {
        var sep = Path.DirectorySeparatorChar;
        var input = $"a{sep}{sep}{sep}b{sep}{sep}";
        var result = PathHelper.EnsureDirectorySeparator(input);
        Assert.Equal($"a{sep}b{sep}", result);
    }
}

