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
        const string sql = @"
            INSERT INTO Classes (Nom, Code, Description, ProfId, Effectif, Niveau, Annee)
            VALUES (@Nom, @Code, @Description, @ProfId, @Effectif, @Niveau, @Annee);
            SELECT last_insert_rowid();";
        using var cn = _factory.Create();
        return (int)(long)await cn.ExecuteScalarAsync<long>(sql, c);
    }

    public async Task<Classe?> GetAsync(int id)
    {
        using var cn = _factory.Create();
        return await cn.QuerySingleOrDefaultAsync<Classe>(@"
            SELECT c.Id, c.Nom, c.Niveau, c.Code, c.Description, c.ProfId, c.Effectif, c.Annee,
                   COALESCE(p.Nom, '') as NomProf
            FROM Classes c
            LEFT JOIN Profs p ON p.Id = c.ProfId
            WHERE c.Id=@id;", new { id });
    }

    public async Task<IReadOnlyList<Classe>> ListAsync(string? search = null, string? annee = null)
    {
        using var cn = _factory.Create();
        const string baseSelect = @"
            SELECT c.Id, c.Nom, c.Niveau, c.Code, c.Description, c.ProfId, c.Effectif, c.Annee,
                   COALESCE(p.Nom, '') as NomProf
            FROM Classes c
            LEFT JOIN Profs p ON p.Id = c.ProfId";

        IEnumerable<Classe> rows;
        if (string.IsNullOrWhiteSpace(search))
        {
            rows = await cn.QueryAsync<Classe>(
                baseSelect + " WHERE (@annee IS NULL OR c.Annee=@annee) ORDER BY c.Code ASC, c.Nom ASC",
                new { annee });
        }
        else
        {
            rows = await cn.QueryAsync<Classe>(
                baseSelect + " WHERE (c.Code LIKE @q OR c.Description LIKE @q OR c.Nom LIKE @q) AND (@annee IS NULL OR c.Annee=@annee) ORDER BY c.Code ASC",
                new { q = $"%{search}%", annee });
        }
        return rows.ToList();
    }

    public async Task<bool> UpdateAsync(Classe c)
    {
        using var cn = _factory.Create();
        var n = await cn.ExecuteAsync(@"
            UPDATE Classes
            SET Nom=@Nom, Code=@Code, Description=@Description,
                ProfId=@ProfId, Effectif=@Effectif, Niveau=@Niveau, Annee=@Annee
            WHERE Id=@Id;", c);
        return n > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        using var tx = cn.BeginTransaction();
        await cn.ExecuteAsync("DELETE FROM EpreuveGroupes WHERE ClasseId=@id", new { id }, tx);
        await cn.ExecuteAsync("DELETE FROM GroupesExamen WHERE ClasseId=@id", new { id }, tx);
        await cn.ExecuteAsync("DELETE FROM Creneaux WHERE EpreuveId IN (SELECT Id FROM Epreuves WHERE ClasseId=@id)", new { id }, tx);
        await cn.ExecuteAsync("DELETE FROM GroupesExamen WHERE EpreuveId IN (SELECT Id FROM Epreuves WHERE ClasseId=@id)", new { id }, tx);
        await cn.ExecuteAsync("DELETE FROM EpreuveGroupes WHERE EpreuveId IN (SELECT Id FROM Epreuves WHERE ClasseId=@id)", new { id }, tx);
        await cn.ExecuteAsync("DELETE FROM Epreuves WHERE ClasseId=@id", new { id }, tx);
        await cn.ExecuteAsync("DELETE FROM Eleves WHERE ClasseId=@id", new { id }, tx);
        var n = await cn.ExecuteAsync("DELETE FROM Classes WHERE Id=@id", new { id }, tx);
        tx.Commit();
        return n > 0;
    }
}
