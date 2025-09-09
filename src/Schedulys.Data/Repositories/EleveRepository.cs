using Dapper;
using Schedulys.Core.Interfaces;
using Schedulys.Core.Models;
using Schedulys.Data.Db;

namespace Schedulys.Data.Repositories;

public sealed class EleveRepository : IEleveRepository
{
    private readonly SqliteConnectionFactory _factory;
    public EleveRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<int> CreateAsync(Eleve e)
    {
        const string sql = @"INSERT INTO Eleves (Nom, ClasseId, TiersTemps, Annee)
                             VALUES (@Nom, @ClasseId, @TiersTemps, @Annee);
                             SELECT last_insert_rowid();";
        using var cn = _factory.Create();
        var id = await cn.ExecuteScalarAsync<long>(sql, e);
        return (int)id;
    }

    public async Task<Eleve?> GetAsync(int id)
    {
        const string sql = "SELECT Id, Nom, ClasseId, TiersTemps, Annee FROM Eleves WHERE Id=@id;";
        using var cn = _factory.Create();
        return await cn.QuerySingleOrDefaultAsync<Eleve>(sql, new { id });
    }

    public async Task<IReadOnlyList<Eleve>> ListByClasseAsync(int classeId, bool? tiersTemps = null)
    {
        using var cn = _factory.Create();
        var rows = await cn.QueryAsync<Eleve>(
            @"SELECT Id, Nom, ClasseId, TiersTemps, Annee
              FROM Eleves
              WHERE ClasseId=@classeId AND (@tt IS NULL OR TiersTemps=@tt)
              ORDER BY Nom ASC",
            new { classeId, tt = tiersTemps });
        return rows.ToList();
    }

    public async Task<bool> UpdateAsync(Eleve e)
    {
        const string sql = @"UPDATE Eleves
                             SET Nom=@Nom, ClasseId=@ClasseId, TiersTemps=@TiersTemps, Annee=@Annee
                             WHERE Id=@Id;";
        using var cn = _factory.Create();
        var n = await cn.ExecuteAsync(sql, e);
        return n > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        const string sql = "DELETE FROM Eleves WHERE Id=@id;";
        using var cn = _factory.Create();
        var n = await cn.ExecuteAsync(sql, new { id });
        return n > 0;
    }

    public async Task<int> CountForClasseAndTTAsync(int classeId, bool tiersTemps)
    {
        const string sql = "SELECT COUNT(*) FROM Eleves WHERE ClasseId=@classeId AND TiersTemps=@tt;";
        using var cn = _factory.Create();
        var n = await cn.ExecuteScalarAsync<long>(sql, new { classeId, tt = tiersTemps });
        return (int)n;
    }
}