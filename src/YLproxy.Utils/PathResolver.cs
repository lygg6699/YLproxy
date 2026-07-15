using System.Reflection;

namespace YLproxy.Utils;

public static class PathResolver
{
    public static string GetRepositoryRoot()
    {
        // New: Based on known assembly location for precise location (highest priority)
        var assemblyPath = typeof(PathResolver).Assembly.Location;
        if (!string.IsNullOrEmpty(assemblyPath))
        {
            var directory = Path.GetDirectoryName(assemblyPath);
            if (!string.IsNullOrEmpty(directory))
            {
                // Traverse upward to find repository root markers
                foreach (var ancestor in EnumerateAncestors(directory))
                {
                    if (IsRepositoryRoot(ancestor))
                    {
                        return ancestor;
                    }
                }
            }
        }

        // Preserve all existing search strategies as fallback
        foreach (var start in GetSearchRoots())
        {
            foreach (var directory in EnumerateAncestors(start))
            {
                if (IsRepositoryRoot(directory))
                {
                    return directory;
                }
            }
        }

        return Path.GetFullPath(AppContext.BaseDirectory);
    }

    public static string ResolvePath(params string[] relativeSegments)
    {
        ArgumentNullException.ThrowIfNull(relativeSegments);

        var root = GetRepositoryRoot();
        return Path.GetFullPath(Path.Combine(root, Path.Combine(relativeSegments)));
    }

    private static IEnumerable<string> GetSearchRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in new[]
                 {
                     AppContext.BaseDirectory,
                     Environment.CurrentDirectory,
                     Assembly.GetEntryAssembly()?.Location,
                     Assembly.GetExecutingAssembly().Location
                 })
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(candidate);
            if (Path.HasExtension(fullPath))
            {
                fullPath = Path.GetDirectoryName(fullPath) ?? fullPath;
            }

            if (!string.IsNullOrWhiteSpace(fullPath) && seen.Add(fullPath))
            {
                yield return fullPath;
            }
        }
    }

    private static IEnumerable<string> EnumerateAncestors(string startPath)
    {
        var current = Path.GetFullPath(startPath);

        while (true)
        {
            yield return current;

            var parent = Directory.GetParent(current);
            if (parent is null)
            {
                break;
            }

            current = parent.FullName;
        }
    }

    private static bool IsRepositoryRoot(string directory)
    {
        return File.Exists(Path.Combine(directory, "YLproxy.sln"));
    }
}
