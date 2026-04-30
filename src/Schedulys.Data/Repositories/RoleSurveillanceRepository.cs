using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Schedulys.Core.Interfaces;
using Schedulys.Core.Models;
using Schedulys.Data.Db;

namespace Schedulys.Data.Repositories;

public sealed class RoleSurveillanceRepository : IRoleSurveillanceRepository
{
    private readonly SqliteConnectionFactory _factory;
    public RoleSurveillanceRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<int> CreateAsync(RoleSurveillance r)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        return (int)await cn.ExecuteScalarAsync<long>(
            @"INSERT INTO RolesSurveillance(SessionId, Date, TypeRole, SurveillantId, HeureDebut, HeureFin, DureeMinutes)
              VALUES (@SessionId, @Date, @TypeRole, @SurveillantId, @HeureDebut, @HeureFin, @DureeMinutes);
              SELECT last_insert_rowid();", r);
    }

    public async Task<RoleSurveillance?> GetAsync(int id)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        return await cn.QueryFirstOrDefaultAsync<RoleSurveillance>(
            "SELECT * FROM RolesSurveillance WHERE Id=@id", new { id });
    }

    public async Task<IReadOnlyList<RoleSurveillance>> ListBySessionAsync(int sessionId)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        return (await cn.QueryAsync<RoleSurveillance>(
            "SELECT * FROM RolesSurveillance WHERE SessionId=@sessionId ORDER BY TypeRole",
            new { sessionId })).AsList();
    }

    public async Task<IReadOnlyList<RoleSurveillance>> ListAllAsync()
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        return (await cn.QueryAsync<RoleSurveillance>(
            "SELECT * FROM RolesSurveillance ORDER BY SessionId, TypeRole")).AsList();
    }

    public async Task<IReadOnlyList<RoleSurveillance>> ListByPeriodeAsync(DateOnly debut, DateOnly fin)
    {
        using var cn = _factory.Create();
        return (await cn.QueryAsync<RoleSurveillance>(
            @"SELECT * FROM RolesSurveillance
              WHERE Date BETWEEN @d0 AND @d1
              ORDER BY Date, HeureDebut",
            new { d0 = debut.ToString("yyyy-MM-dd"), d1 = fin.ToString("yyyy-MM-dd") })).AsList();
    }

    public async Task<bool> UpdateAsync(RoleSurveillance r)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        var rows = await cn.ExecuteAsync(
            @"UPDATE RolesSurveillance SET SessionId=@SessionId, Date=@Date, TypeRole=@TypeRole,
              SurveillantId=@SurveillantId, HeureDebut=@HeureDebut, HeureFin=@HeureFin, DureeMinutes=@DureeMinutes
              WHERE Id=@Id", r);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        var rows = await cn.ExecuteAsync(
            "DELETE FROM RolesSurveillance WHERE Id=@id", new { id });
        return rows > 0;
    }
}
