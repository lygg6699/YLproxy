using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Data.Sqlite;
using YLproxy.Infrastructure;
using YLproxy.Models;
using YLproxy.Utils;

namespace YLproxy.Core.Data;

public class SqliteProxyRepository : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ISecurityService _securityService;
    private readonly ILogger _logger;
    private bool _disposed;

    public SqliteProxyRepository(
        string? dbPath = null,
        ISecurityService? securityService = null,
        ILogger? logger = null)
    {
        _logger = logger ?? LoggerFactory.CreateLogger();

        if (securityService is null && !OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("DPAPI credential storage requires Windows.");

        _securityService = securityService ?? new DpapiSecurityService();

        var resolvedPath = string.IsNullOrWhiteSpace(dbPath)
            ? PathResolver.ResolvePath("data", "ylproxy.db")
            : (System.IO.Path.IsPathFullyQualified(dbPath)
                ? System.IO.Path.GetFullPath(dbPath)
                : PathResolver.ResolvePath(dbPath));

        var dir = System.IO.Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrWhiteSpace(dir))
            System.IO.Directory.CreateDirectory(dir);

        _connection = new SqliteConnection($"Data Source={resolvedPath}");
        _connection.Open();

        using var pragmaCmd = _connection.CreateCommand();
        pragmaCmd.CommandText = "PRAGMA journal_mode=WAL";
        pragmaCmd.ExecuteNonQuery();

        InitializeDatabase();
    }

    /// <summary>
    /// Begins a SQLite transaction on the shared connection.
    /// </summary>
    public SqliteTransaction BeginTransaction()
    {
        ThrowIfDisposed();
        return _connection.BeginTransaction();
    }

    /// <summary>
    /// Adds a proxy item using an existing transaction.
    /// </summary>
    public virtual int AddWithTransaction(SqliteTransaction transaction, ProxyItem proxy)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(proxy);

        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                INSERT INTO proxies (Name, RemoteHost, RemotePort, Username, Password, LocalHost, LocalPort, Status, CreateTime)
                VALUES (@Name, @RemoteHost, @RemotePort, @Username, @Password, @LocalHost, @LocalPort, @Status, @CreateTime);
                SELECT last_insert_rowid();
                """;

            cmd.Parameters.AddWithValue("@Name", proxy.Name);
            cmd.Parameters.AddWithValue("@RemoteHost", proxy.RemoteHost);
            cmd.Parameters.AddWithValue("@RemotePort", proxy.RemotePort);
            cmd.Parameters.AddWithValue("@Username", _securityService.Encrypt(proxy.Username));
            cmd.Parameters.AddWithValue("@Password", _securityService.Encrypt(proxy.Password));
            cmd.Parameters.AddWithValue("@LocalHost", proxy.LocalHost);
            cmd.Parameters.AddWithValue("@LocalPort", proxy.LocalPort);
            cmd.Parameters.AddWithValue("@Status", (int)proxy.Status);
            cmd.Parameters.AddWithValue("@CreateTime", proxy.CreateTime.ToString("o"));

            var newId = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
            return newId;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to add proxy to SQLite (transactional).", ex);
            throw;
        }
    }

    private void InitializeDatabase()
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS proxies (
                Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                Name        TEXT    NOT NULL,
                RemoteHost  TEXT    NOT NULL,
                RemotePort  INTEGER NOT NULL,
                Username    TEXT    NOT NULL DEFAULT '',
                Password    TEXT    NOT NULL DEFAULT '',
                LocalHost   TEXT    NOT NULL,
                LocalPort   INTEGER NOT NULL,
                Status      INTEGER NOT NULL DEFAULT 0,
                CreateTime  TEXT    NOT NULL
            );
            """;

        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to initialize SQLite database.", ex);
            throw;
        }
    }

    public List<ProxyItem> GetAll()
    {
        ThrowIfDisposed();
        var result = new List<ProxyItem>();

        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, RemoteHost, RemotePort, Username, Password, LocalHost, LocalPort, Status, CreateTime FROM proxies ORDER BY Id";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(ReadProxyItem(reader));
            }
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to load proxies from SQLite.", ex);
            throw;
        }

        return result;
    }

    public ProxyItem? GetById(int id)
    {
        ThrowIfDisposed();
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, RemoteHost, RemotePort, Username, Password, LocalHost, LocalPort, Status, CreateTime FROM proxies WHERE Id = @Id";
            cmd.Parameters.AddWithValue("@Id", id);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return ReadProxyItem(reader);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to load proxy {id} from SQLite.", ex);
            throw;
        }

        return null;
    }

    public virtual int Add(ProxyItem proxy)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(proxy);

        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO proxies (Name, RemoteHost, RemotePort, Username, Password, LocalHost, LocalPort, Status, CreateTime)
                VALUES (@Name, @RemoteHost, @RemotePort, @Username, @Password, @LocalHost, @LocalPort, @Status, @CreateTime);
                SELECT last_insert_rowid();
                """;

            cmd.Parameters.AddWithValue("@Name", proxy.Name);
            cmd.Parameters.AddWithValue("@RemoteHost", proxy.RemoteHost);
            cmd.Parameters.AddWithValue("@RemotePort", proxy.RemotePort);
            cmd.Parameters.AddWithValue("@Username", _securityService.Encrypt(proxy.Username));
            cmd.Parameters.AddWithValue("@Password", _securityService.Encrypt(proxy.Password));
            cmd.Parameters.AddWithValue("@LocalHost", proxy.LocalHost);
            cmd.Parameters.AddWithValue("@LocalPort", proxy.LocalPort);
            cmd.Parameters.AddWithValue("@Status", (int)proxy.Status);
            cmd.Parameters.AddWithValue("@CreateTime", proxy.CreateTime.ToString("o"));

            var newId = Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
            return newId;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to add proxy to SQLite.", ex);
            throw;
        }
    }

    public void Update(ProxyItem proxy)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(proxy);

        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = """
                UPDATE proxies
                SET Name = @Name,
                    RemoteHost = @RemoteHost,
                    RemotePort = @RemotePort,
                    Username = @Username,
                    Password = @Password,
                    LocalHost = @LocalHost,
                    LocalPort = @LocalPort,
                    Status = @Status,
                    CreateTime = @CreateTime
                WHERE Id = @Id
                """;

            cmd.Parameters.AddWithValue("@Id", proxy.Id);
            cmd.Parameters.AddWithValue("@Name", proxy.Name);
            cmd.Parameters.AddWithValue("@RemoteHost", proxy.RemoteHost);
            cmd.Parameters.AddWithValue("@RemotePort", proxy.RemotePort);
            cmd.Parameters.AddWithValue("@Username", _securityService.Encrypt(proxy.Username));
            cmd.Parameters.AddWithValue("@Password", _securityService.Encrypt(proxy.Password));
            cmd.Parameters.AddWithValue("@LocalHost", proxy.LocalHost);
            cmd.Parameters.AddWithValue("@LocalPort", proxy.LocalPort);
            cmd.Parameters.AddWithValue("@Status", (int)proxy.Status);
            cmd.Parameters.AddWithValue("@CreateTime", proxy.CreateTime.ToString("o"));

            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to update proxy {proxy.Id} in SQLite.", ex);
            throw;
        }
    }

    public void Delete(int id)
    {
        ThrowIfDisposed();
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM proxies WHERE Id = @Id";
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to delete proxy {id} from SQLite.", ex);
            throw;
        }
    }

    public int Count()
    {
        ThrowIfDisposed();
        try
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM proxies";
            return Convert.ToInt32(cmd.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
        catch (Exception ex)
        {
            _logger.Error("Failed to count proxies in SQLite.", ex);
            throw;
        }
    }

    private ProxyItem ReadProxyItem(SqliteDataReader reader)
    {
        return new ProxyItem
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            RemoteHost = reader.GetString(2),
            RemotePort = reader.GetInt32(3),
            Username = _securityService.Decrypt(reader.GetString(4)),
            Password = _securityService.Decrypt(reader.GetString(5)),
            LocalHost = reader.GetString(6),
            LocalPort = reader.GetInt32(7),
            Status = (ProxyStatus)reader.GetInt32(8),
            CreateTime = DateTime.Parse(reader.GetString(9), null, System.Globalization.DateTimeStyles.RoundtripKind),
        };
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(SqliteProxyRepository));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            _connection.Close();
            _connection.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
