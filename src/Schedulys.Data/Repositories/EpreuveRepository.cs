using System.Linq;
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
        const string sql = @"INSERT INTO Epreuves (Nom, ClasseId, DureeMinutes, TiersTemps, Ministerielle, Annee)
                             VALUES (@Nom, @ClasseId, @DureeMinutes, @TiersTemps, @Ministerielle, @Annee);
                             SELECT last_insert_rowid();";
        using var cn = _factory.Create();
        return (int)(long)await cn.ExecuteScalarAsync<long>(sql, e);
    }

    public async Task<Epreuve?> GetAsync(int id)
    {
        using var cn = _factory.Create();
        return await cn.QuerySingleOrDefaultAsync<Epreuve>(
            "SELECT Id, Nom, ClasseId, DureeMinutes, TiersTemps, Ministerielle, Annee FROM Epreuves WHERE Id=@id;", new { id });
    }

    public async Task<IReadOnlyList<Epreuve>> ListAsync(int? classeId = null, string? search = null, string? annee = null)
    {
        using var cn = _factory.Create();
        var sql = "SELECT Id, Nom, ClasseId, DureeMinutes, TiersTemps, Ministerielle, Annee FROM Epreuves WHERE 1=1";
        var dyn = new DynamicParameters();
        if (classeId is not null) { sql += " AND ClasseId=@classeId"; dyn.Add("classeId", classeId); }
        if (!string.IsNullOrWhiteSpace(search)) { sql += " AND Nom LIKE @q"; dyn.Add("q", $"%{search}%"); }
        if (!string.IsNullOrWhiteSpace(annee)) { sql += " AND Annee=@annee"; dyn.Add("annee", annee); }
        sql += " ORDER BY Nom ASC";

        var rows = await cn.QueryAsync<Epreuve>(sql, dyn);
        return rows.ToList();
    }

    public async Task<bool> UpdateAsync(Epreuve e)
    {
        using var cn = _factory.Create();
        var n = await cn.ExecuteAsync(
            "UPDATE Epreuves SET Nom=@Nom, ClasseId=@ClasseId, DureeMinutes=@DureeMinutes, TiersTemps=@TiersTemps, Ministerielle=@Ministerielle, Annee=@Annee WHERE Id=@Id;", e);
        return n > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var cn = _factory.Create();
        var n = await cn.ExecuteAsync("DELETE FROM Epreuves WHERE Id=@id;", new { id });
        return n > 0;
    }
}