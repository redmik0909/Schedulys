using Microsoft.Data.Sqlite;

namespace Schedulys.Data.Db;

public sealed class SqliteConnectionFactory
{
    private readonly string _cs;
    public SqliteConnectionFactory(string databasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
        _cs = $"Data Source={databasePath};Foreign Keys=False";
    }

    public SqliteConnection Create() => new SqliteConnection(_cs);
}