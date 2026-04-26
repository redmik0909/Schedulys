using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Schedulys.Core.Interfaces;
using Schedulys.Core.Models;
using Schedulys.Data.Db;

namespace Schedulys.Data.Repositories;

public sealed class QuotaMinutesRepository : IQuotaMinutesRepository
{
    private readonly SqliteConnectionFactory _factory;
    public QuotaMinutesRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<int> CreateAsync(QuotaMinutes q)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        return (int)await cn.ExecuteScalarAsync<long>(
            @"INSERT INTO QuotasMinutes(ProfId, JourCycle, MinutesMax, AnneeScolaire)
              VALUES (@ProfId, @JourCycle, @MinutesMax, @AnneeScolaire);
              SELECT last_insert_rowid();", q);
    }

    public async Task<QuotaMinutes?> GetByProfAsync(int profId, int jourCycle = 0, string? annee = null)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        var sql = annee is null
            ? "SELECT * FROM QuotasMinutes WHERE ProfId=@profId AND JourCycle=@jourCycle LIMIT 1"
            : "SELECT * FROM QuotasMinutes WHERE ProfId=@profId AND JourCycle=@jourCycle AND AnneeScolaire=@annee LIMIT 1";
        return await cn.QueryFirstOrDefaultAsync<QuotaMinutes>(sql, new { profId, jourCycle, annee });
    }

    public async Task<IReadOnlyList<QuotaMinutes>> GetAllByProfAsync(int profId, string? annee = null)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        var sql = annee is null
            ? "SELECT * FROM QuotasMinutes WHERE ProfId=@profId ORDER BY JourCycle"
            : "SELECT * FROM QuotasMinutes WHERE ProfId=@profId AND AnneeScolaire=@annee ORDER BY JourCycle";
        return (await cn.QueryAsync<QuotaMinutes>(sql, new { profId, annee })).AsList();
    }

    public async Task<IReadOnlyList<QuotaMinutes>> ListAsync(string? annee = null)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        var sql = annee is null
            ? "SELECT * FROM QuotasMinutes ORDER BY ProfId, JourCycle"
            : "SELECT * FROM QuotasMinutes WHERE AnneeScolaire=@annee ORDER BY ProfId, JourCycle";
        return (await cn.QueryAsync<QuotaMinutes>(sql, new { annee })).AsList();
    }

    public async Task<bool> UpsertAsync(QuotaMinutes q)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        var rows = await cn.ExecuteAsync(
            @"INSERT INTO QuotasMinutes(ProfId, JourCycle, MinutesMax, AnneeScolaire)
              VALUES (@ProfId, @JourCycle, @MinutesMax, @AnneeScolaire)
              ON CONFLICT(ProfId, JourCycle, AnneeScolaire) DO UPDATE SET MinutesMax=excluded.MinutesMax", q);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        var rows = await cn.ExecuteAsync(
            "DELETE FROM QuotasMinutes WHERE Id=@id", new { id });
        return rows > 0;
    }
}
