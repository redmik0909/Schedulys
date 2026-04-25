using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Schedulys.Core.Interfaces;
using Schedulys.Core.Models;
using Schedulys.Data.Db;

namespace Schedulys.Data.Repositories;

public sealed class SessionRepository : ISessionRepository
{
    private readonly SqliteConnectionFactory _factory;
    public SessionRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<int> CreateAsync(Session s)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        return (int)await cn.ExecuteScalarAsync<long>(
            @"INSERT INTO Sessions(Date, Periode, HeureDebut, AnneeScolaire)
              VALUES (@Date, @Periode, @HeureDebut, @AnneeScolaire);
              SELECT last_insert_rowid();", s);
    }

    public async Task<Session?> GetAsync(int id)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        return await cn.QueryFirstOrDefaultAsync<Session>(
            "SELECT * FROM Sessions WHERE Id=@id", new { id });
    }

    public async Task<IReadOnlyList<Session>> ListAsync(string? annee = null)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        var sql = annee is null
            ? "SELECT * FROM Sessions ORDER BY Date, Periode"
            : "SELECT * FROM Sessions WHERE AnneeScolaire=@annee ORDER BY Date, Periode";
        return (await cn.QueryAsync<Session>(sql, new { annee })).AsList();
    }

    public async Task<IReadOnlyList<Session>> ListByDateAsync(DateOnly date)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        return (await cn.QueryAsync<Session>(
            "SELECT * FROM Sessions WHERE Date=@d ORDER BY Periode",
            new { d = date.ToString("yyyy-MM-dd") })).AsList();
    }

    public async Task<IReadOnlyList<Session>> ListByPeriodeAsync(DateOnly debut, DateOnly fin)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        return (await cn.QueryAsync<Session>(
            "SELECT * FROM Sessions WHERE Date BETWEEN @d0 AND @d1 ORDER BY Date, Periode",
            new { d0 = debut.ToString("yyyy-MM-dd"), d1 = fin.ToString("yyyy-MM-dd") })).AsList();
    }

    public async Task<bool> UpdateAsync(Session s)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        var rows = await cn.ExecuteAsync(
            @"UPDATE Sessions SET Date=@Date, Periode=@Periode, HeureDebut=@HeureDebut,
              AnneeScolaire=@AnneeScolaire WHERE Id=@Id", s);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        await cn.ExecuteAsync("DELETE FROM GroupesExamen    WHERE SessionId=@id", new { id });
        await cn.ExecuteAsync("DELETE FROM RolesSurveillance WHERE SessionId=@id", new { id });
        var rows = await cn.ExecuteAsync("DELETE FROM Sessions WHERE Id=@id", new { id });
        return rows > 0;
    }
}
