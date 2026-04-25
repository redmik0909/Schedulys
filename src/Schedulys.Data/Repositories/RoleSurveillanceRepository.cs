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
            @"INSERT INTO RolesSurveillance(SessionId, TypeRole, SurveillantId, Local, DureeMinutes)
              VALUES (@SessionId, @TypeRole, @SurveillantId, @Local, @DureeMinutes);
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

    public async Task<bool> UpdateAsync(RoleSurveillance r)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        var rows = await cn.ExecuteAsync(
            @"UPDATE RolesSurveillance SET SessionId=@SessionId, TypeRole=@TypeRole,
              SurveillantId=@SurveillantId, Local=@Local, DureeMinutes=@DureeMinutes
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
