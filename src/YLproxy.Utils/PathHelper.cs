namespace YLproxy.Utils;

/// <summary>
/// Provides cross-platform path manipulation utilities.
/// Abstracts <see cref="Path.Combine"/>, <see cref="Path.GetFullPath"/> and
/// directory separator handling to improve cross-platform compatibility.
/// </summary>
public static class PathHelper
{
    /// <summary>
    /// Combines multiple path segments into a single path.
    /// Delegates to <see cref="Path.Combine(string[])"/>.
    /// </summary>
    public static string Combine(params string[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        return Path.Combine(parts);
    }

    /// <summary>
    /// Returns the fully qualified path for the given path.
    /// Delegates to <see cref="Path.GetFullPath(string)"/>.
    /// </summary>
    public static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.GetFullPath(path);
    }

    /// <summary>
    /// Ensures the path ends with the platform's preferred directory separator character.
    /// </summary>
    public static string EnsureDirectorySeparator(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
               + Path.DirectorySeparatorChar;
    }

    /// <summary>
    /// Gets the directory name from a file path, or the path itself if it has no directory component.
    /// </summary>
    public static string GetDirectoryName(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.GetDirectoryName(path) ?? path;
    }

    /// <summary>
    /// Gets the file name from a path (with extension).
    /// </summary>
    public static string GetFileName(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.GetFileName(path);
    }

    /// <summary>
    /// Gets the file name from a path (without extension).
    /// </summary>
    public static string GetFileNameWithoutExtension(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.GetFileNameWithoutExtension(path);
    }

    /// <summary>
    /// Determines whether the given path is fully qualified (absolute).
    /// </summary>
    public static bool IsPathRooted(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.IsPathRooted(path);
    }

    /// <summary>
    /// Changes the extension of a path string.
    /// </summary>
    public static string ChangeExtension(string path, string? extension)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.ChangeExtension(path, extension);
    }

    /// <summary>
    /// Creates a temporary file name in the given directory (does not create the file).
    /// </summary>
    public static string GetTempFileName(string? directory = null)
    {
        var dir = directory ?? Path.GetTempPath();
        return Combine(dir, Guid.NewGuid().ToString("N") + ".tmp");
    }

    /// <summary>
    /// Creates all directories and subdirectories in the specified path if they don't exist.
    /// </summary>
    public static void EnsureDirectoryExists(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var dir = GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    /// <summary>
    /// Safely combines path segments and normalizes the result.
    /// Equivalent to calling <see cref="Combine"/> then <see cref="Normalize"/>.
    /// </summary>
    public static string Resolve(params string[] parts)
    {
        return Normalize(Combine(parts));
    }
}

