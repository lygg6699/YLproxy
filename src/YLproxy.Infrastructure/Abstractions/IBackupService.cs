namespace YLproxy.Infrastructure.Abstractions;

public interface IBackupService
{
    string CreateBackup(string reason = "manual");

    IReadOnlyList<string> ListBackups(int take = 20);

    void RestoreBackup(string backupFilePath);
}
