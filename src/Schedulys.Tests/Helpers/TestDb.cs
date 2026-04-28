using Schedulys.Data;
using Schedulys.Data.Db;

namespace Schedulys.Tests.Helpers;

/// <summary>
/// Base SQLite temporaire avec schéma complet — se nettoie automatiquement après chaque test.
/// Usage: await using var tdb = await TestDb.CreateAsync();
/// </summary>
internal sealed class TestDb : IAsyncDisposable
{
    public DataContext Db    { get; }
    public string      Path  { get; }

    private TestDb(DataContext db, string path) { Db = db; Path = path; }

    public static async Task<TestDb> CreateAsync()
    {
        var path    = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"schedulys_test_{Guid.NewGuid():N}.db");
        var factory = new SqliteConnectionFactory(path);
        await SchemaInitializer.InitAsync(factory);
        return new TestDb(new DataContext(path), path);
    }

    public async ValueTask DisposeAsync()
    {
        await Task.Yield();
        try { System.IO.File.Delete(Path); } catch { /* ignore */ }
    }
}
