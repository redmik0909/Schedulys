using System.Linq;
using Dapper;
using Schedulys.Core.Interfaces;
using Schedulys.Core.Models;
using Schedulys.Data.Db;

namespace Schedulys.Data.Repositories;

public sealed class SalleRepository : ISalleRepository
{
    private readonly SqliteConnectionFactory _factory;
    public SalleRepository(SqliteConnectionFactory f) => _factory = f;

    public async Task<int> CreateAsync(Salle s)
    {
        const string sql = @"INSERT INTO Salles (Nom, Capacite, Type, Annee)
                             VALUES (@Nom, @Capacite, @Type, @Annee);
                             SELECT last_insert_rowid();";
        using var cn = _factory.Create();
        return (int) (long) await cn.ExecuteScalarAsync<long>(sql, s);
    }

    public async Task<Salle?> GetAsync(int id)
    {
        using var cn = _factory.Create();
        return await cn.QuerySingleOrDefaultAsync<Salle>(
            "SELECT Id, Nom, Capacite, Type, Annee FROM Salles WHERE Id=@id;", new { id });
    }

    public async Task<IReadOnlyList<Salle>> ListAsync(string? search = null, string? annee = null)
    {
        using var cn = _factory.Create();
        if (string.IsNullOrWhiteSpace(search))
        {
            var rows = await cn.QueryAsync<Salle>(
                "SELECT Id, Nom, Capacite, Type, Annee FROM Salles WHERE (@annee IS NULL OR Annee=@annee) ORDER BY Nom ASC",
                new { annee });
            return rows.ToList();
        }
        var r = await cn.QueryAsync<Salle>(
            "SELECT Id, Nom, Capacite, Type, Annee FROM Salles WHERE Nom LIKE @q AND (@annee IS NULL OR Annee=@annee) ORDER BY Nom ASC",
            new { q = $"%{search}%", annee });
        return r.ToList();
    }

    public async Task<bool> UpdateAsync(Salle s)
    {
        using var cn = _factory.Create();
        var n = await cn.ExecuteAsync(
            "UPDATE Salles SET Nom=@Nom, Capacite=@Capacite, Type=@Type, Annee=@Annee WHERE Id=@Id;", s);
        return n > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        using var tx = cn.BeginTransaction();
        await cn.ExecuteAsync("DELETE FROM Creneaux WHERE SalleId=@id", new { id }, tx);
        var n = await cn.ExecuteAsync("DELETE FROM Salles WHERE Id=@id", new { id }, tx);
        tx.Commit();
        return n > 0;
    }
}