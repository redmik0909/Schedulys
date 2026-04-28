using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Schedulys.Core.Interfaces;
using Schedulys.Core.Models;
using Schedulys.Data.Db;

namespace Schedulys.Data.Repositories;

public sealed class EpreuveRepository : IEpreuveRepository
{
    private readonly SqliteConnectionFactory _factory;
    public EpreuveRepository(SqliteConnectionFactory f) => _factory = f;

    public async Task<int> CreateAsync(Epreuve e)
    {
        const string sql = @"INSERT INTO Epreuves (Nom, ClasseId, DureeMinutes, TiersTemps, Ministerielle, Niveau, Annee)
                             VALUES (@Nom, @ClasseId, @DureeMinutes, @TiersTemps, @Ministerielle, @Niveau, @Annee);
                             SELECT last_insert_rowid();";
        using var cn = _factory.Create();
        return (int)(long)await cn.ExecuteScalarAsync<long>(sql, e);
    }

    public async Task<Epreuve?> GetAsync(int id)
    {
        using var cn = _factory.Create();
        return await cn.QuerySingleOrDefaultAsync<Epreuve>(
            "SELECT * FROM Epreuves WHERE Id=@id;", new { id });
    }

    public async Task<IReadOnlyList<Epreuve>> ListAsync(int? classeId = null, string? search = null, string? annee = null)
    {
        using var cn = _factory.Create();
        var sql = "SELECT * FROM Epreuves WHERE 1=1";
        var dyn = new DynamicParameters();
        if (!string.IsNullOrWhiteSpace(search)) { sql += " AND Nom LIKE @q"; dyn.Add("q", $"%{search}%"); }
        if (!string.IsNullOrWhiteSpace(annee))  { sql += " AND Annee=@annee"; dyn.Add("annee", annee); }
        sql += " ORDER BY CASE WHEN Niveau=0 THEN 99 ELSE Niveau END ASC, Nom ASC";
        return (await cn.QueryAsync<Epreuve>(sql, dyn)).ToList();
    }

    public async Task<bool> UpdateAsync(Epreuve e)
    {
        using var cn = _factory.Create();
        var n = await cn.ExecuteAsync(
            "UPDATE Epreuves SET Nom=@Nom, DureeMinutes=@DureeMinutes, TiersTemps=@TiersTemps, Ministerielle=@Ministerielle, Niveau=@Niveau, Annee=@Annee WHERE Id=@Id;", e);
        return n > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        using var tx = cn.BeginTransaction();
        await cn.ExecuteAsync("DELETE FROM EpreuveGroupes WHERE EpreuveId=@id", new { id }, tx);
        await cn.ExecuteAsync("DELETE FROM Creneaux    WHERE EpreuveId=@id", new { id }, tx);
        await cn.ExecuteAsync("DELETE FROM GroupesExamen WHERE EpreuveId=@id", new { id }, tx);
        var n = await cn.ExecuteAsync("DELETE FROM Epreuves WHERE Id=@id", new { id }, tx);
        tx.Commit();
        return n > 0;
    }

    public async Task<IReadOnlyList<int>> GetGroupeIdsAsync(int epreuveId)
    {
        using var cn = _factory.Create();
        return (await cn.QueryAsync<int>(
            "SELECT ClasseId FROM EpreuveGroupes WHERE EpreuveId=@epreuveId",
            new { epreuveId })).AsList();
    }

    public async Task SetGroupesAsync(int epreuveId, IEnumerable<int> classeIds)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        using var tx = cn.BeginTransaction();
        await cn.ExecuteAsync("DELETE FROM EpreuveGroupes WHERE EpreuveId=@epreuveId",
            new { epreuveId }, tx);
        foreach (var classeId in classeIds)
            await cn.ExecuteAsync(
                "INSERT INTO EpreuveGroupes(EpreuveId, ClasseId) VALUES (@epreuveId, @classeId)",
                new { epreuveId, classeId }, tx);
        tx.Commit();
    }

    public async Task<IReadOnlyList<Epreuve>> GetByClasseAsync(int classeId, string? annee = null)
    {
        using var cn = _factory.Create();
        var sql = annee is null
            ? @"SELECT e.* FROM Epreuves e
                JOIN EpreuveGroupes eg ON eg.EpreuveId = e.Id
                WHERE eg.ClasseId = @classeId ORDER BY e.Nom"
            : @"SELECT e.* FROM Epreuves e
                JOIN EpreuveGroupes eg ON eg.EpreuveId = e.Id
                WHERE eg.ClasseId = @classeId AND e.Annee = @annee ORDER BY e.Nom";
        return (await cn.QueryAsync<Epreuve>(sql, new { classeId, annee })).AsList();
    }
}