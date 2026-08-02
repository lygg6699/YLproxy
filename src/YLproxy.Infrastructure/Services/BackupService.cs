using System.IO.Compression;
using YLproxy.Infrastructure.Abstractions;
using YLproxy.Utils;

namespace YLproxy.Infrastructure.Services;

public sealed class BackupService : IBackupService
{
    private readonly string _repositoryRoot;
    private readonly string _backupDirectory;
    private readonly string _dataDirectory;

    public BackupService(string? repositoryRoot = null)
    {
        _repositoryRoot = repositoryRoot ?? PathResolver.GetRepositoryRoot();
        _backupDirectory = Path.Combine(_repositoryRoot, "backup");
        _dataDirectory = Path.Combine(_repositoryRoot, "data");
        Directory.CreateDirectory(_backupDirectory);
    }

    public string CreateBackup(string reason = "manual")
    {
        var safeReason = string.IsNullOrWhiteSpace(reason) ? "manual" : reason.Trim().Replace(' ', '-');
        var fileName = $"YLproxy-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{safeReason}.zip";
        var output = Path.Combine(_backupDirectory, fileName);

        using var zip = ZipFile.Open(output, ZipArchiveMode.Create);
        AddIfExists(zip, Path.Combine(_repositoryRoot, "AppSettings.json"), "AppSettings.json");

        if (Directory.Exists(_dataDirectory))
        {
            foreach (var path in Directory.GetFiles(_dataDirectory, "*.json", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(path);
                AddIfExists(zip, path, Path.Combine("data", name));
            }
        }

        return output;
    }

    public IReadOnlyList<string> ListBackups(int take = 20)
    {
        if (!Directory.Exists(_backupDirectory))
            return [];

        return Directory
            .GetFiles(_backupDirectory, "YLproxy-backup-*.zip", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetCreationTimeUtc)
            .Take(Math.Max(1, take))
            .ToList();
    }

    public void RestoreBackup(string backupFilePath)
    {
        if (string.IsNullOrWhiteSpace(backupFilePath) || !File.Exists(backupFilePath))
            throw new FileNotFoundException("Backup file not found.", backupFilePath);

        using var zip = ZipFile.OpenRead(backupFilePath);
        foreach (var entry in zip.Entries)
        {
            if (string.IsNullOrWhiteSpace(entry.FullName) || entry.FullName.EndsWith('/'))
                continue;

            var normalized = entry.FullName.Replace('\\', '/');
            if (!normalized.Equals("AppSettings.json", StringComparison.OrdinalIgnoreCase)
                && !normalized.StartsWith("data/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var target = Path.GetFullPath(Path.Combine(_repositoryRoot, normalized));
            if (!target.StartsWith(_repositoryRoot, StringComparison.OrdinalIgnoreCase))
                continue;

            var dir = Path.GetDirectoryName(target);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);

            entry.ExtractToFile(target, overwrite: true);
        }
    }

    private static void AddIfExists(ZipArchive zip, string sourcePath, string entryPath)
    {
        if (File.Exists(sourcePath))
            zip.CreateEntryFromFile(sourcePath, entryPath);
    }
}
