using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Schedulys.Core.Interfaces;
using Schedulys.Core.Models;
using Schedulys.Data.Db;

namespace Schedulys.Data.Repositories;

public sealed class ZoneSurveillanceRepository : IZoneSurveillanceRepository
{
    private readonly SqliteConnectionFactory _factory;
    public ZoneSurveillanceRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<int> CreateAsync(ZoneSurveillance z)
    {
        using var cn = _factory.Create();
        return (int)await cn.ExecuteScalarAsync<long>(
            @"INSERT INTO ZonesSurveillance(Nom, Ordre) VALUES (@Nom, @Ordre);
              SELECT last_insert_rowid();", z);
    }

    public async Task<IReadOnlyList<ZoneSurveillance>> ListAsync()
    {
        using var cn = _factory.Create();
        return (await cn.QueryAsync<ZoneSurveillance>(
            "SELECT * FROM ZonesSurveillance ORDER BY Ordre, Nom")).AsList();
    }

    public async Task<bool> UpdateAsync(ZoneSurveillance z)
    {
        using var cn = _factory.Create();
        var rows = await cn.ExecuteAsync(
            "UPDATE ZonesSurveillance SET Nom=@Nom, Ordre=@Ordre WHERE Id=@Id", z);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var cn = _factory.Create();
        var rows = await cn.ExecuteAsync(
            "DELETE FROM ZonesSurveillance WHERE Id=@id", new { id });
        return rows > 0;
    }
}
