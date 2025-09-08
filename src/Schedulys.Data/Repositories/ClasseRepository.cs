using System.Linq;
using Dapper;
using Schedulys.Core.Interfaces;
using Schedulys.Core.Models;
using Schedulys.Data.Db;

namespace Schedulys.Data.Repositories;

public sealed class ClasseRepository : IClasseRepository
{
    private readonly SqliteConnectionFactory _factory;
    public ClasseRepository(SqliteConnectionFactory f) => _factory = f;

    public async Task<int> CreateAsync(Classe c)
    {
        const string sql = @"INSERT INTO Classes (Nom, Effectif, Annee)
                             VALUES (@Nom, @Effectif, @Annee);
                             SELECT last_insert_rowid();";
        using var cn = _factory.Create();
        return (int)(long)await cn.ExecuteScalarAsync<long>(sql, c);
    }

    public async Task<Classe?> GetAsync(int id)
    {
        using var cn = _factory.Create();
        return await cn.QuerySingleOrDefaultAsync<Classe>(
            "SELECT Id, Nom, Effectif, Annee FROM Classes WHERE Id=@id;", new { id });
    }

    public async Task<IReadOnlyList<Classe>> ListAsync(string? search = null, string? annee = null)
    {
        using var cn = _factory.Create();
        if (string.IsNullOrWhiteSpace(search))
        {
            var rows = await cn.QueryAsync<Classe>(
                "SELECT Id, Nom, Effectif, Annee FROM Classes WHERE (@annee IS NULL OR Annee=@annee) ORDER BY Nom ASC",
                new { annee });
            return rows.ToList();
        }
        var r = await cn.QueryAsync<Classe>(
            "SELECT Id, Nom, Effectif, Annee FROM Classes WHERE Nom LIKE @q AND (@annee IS NULL OR Annee=@annee) ORDER BY Nom ASC",
            new { q = $"%{search}%", annee });
        return r.ToList();
    }

    public async Task<bool> UpdateAsync(Classe c)
    {
        using var cn = _factory.Create();
        var n = await cn.ExecuteAsync(
            "UPDATE Classes SET Nom=@Nom, Effectif=@Effectif, Annee=@Annee WHERE Id=@Id;", c);
        return n > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var cn = _factory.Create();
        var n = await cn.ExecuteAsync("DELETE FROM Classes WHERE Id=@id;", new { id });
        return n > 0;
    }
}