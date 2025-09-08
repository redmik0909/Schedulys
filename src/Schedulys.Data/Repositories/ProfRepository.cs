using Dapper;
using Schedulys.Core.Interfaces;
using Schedulys.Core.Models;
using Schedulys.Data.Db;

namespace Schedulys.Data.Repositories;

public sealed class ProfRepository : IProfRepository
{
    private readonly SqliteConnectionFactory _factory;
    public ProfRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<int> CreateAsync(Prof p)
    {
        const string sql = @"INSERT INTO Profs (Nom, Role, Annee)
                             VALUES (@Nom, @Role, @Annee);
                             SELECT last_insert_rowid();";
        using var cn = _factory.Create();
        var id = await cn.ExecuteScalarAsync<long>(sql, p);
        return (int)id;
    }

    public async Task<Prof?> GetAsync(int id)
    {
        const string sql = "SELECT Id, Nom, Role, Annee FROM Profs WHERE Id=@id;";
        using var cn = _factory.Create();
        return await cn.QuerySingleOrDefaultAsync<Prof>(sql, new { id });
    }

    public async Task<IReadOnlyList<Prof>> ListAsync(string? search = null, string? annee = null)
    {
        using var cn = _factory.Create();
        if (string.IsNullOrWhiteSpace(search))
        {
            var rows = await cn.QueryAsync<Prof>(
                "SELECT Id, Nom, Role, Annee FROM Profs WHERE (@annee IS NULL OR Annee=@annee) ORDER BY Nom ASC",
                new { annee });
            return rows.ToList();
        }
        var r = await cn.QueryAsync<Prof>(
            "SELECT Id, Nom, Role, Annee FROM Profs WHERE Nom LIKE @q AND (@annee IS NULL OR Annee=@annee) ORDER BY Nom ASC",
            new { q = $"%{search}%", annee });
        return r.ToList();
    }

    public async Task<bool> UpdateAsync(Prof p)
    {
        const string sql = "UPDATE Profs SET Nom=@Nom, Role=@Role, Annee=@Annee WHERE Id=@Id;";
        using var cn = _factory.Create();
        return (await cn.ExecuteAsync(sql, p)) > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        const string sql = "DELETE FROM Profs WHERE Id=@id;";
        using var cn = _factory.Create();
        return (await cn.ExecuteAsync(sql, new { id })) > 0;
    }
}