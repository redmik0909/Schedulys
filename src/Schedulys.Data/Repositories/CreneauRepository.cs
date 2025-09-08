using System.Linq;
using Dapper;
using Schedulys.Core.Interfaces;
using Schedulys.Core.Models;
using Schedulys.Data.Db;

namespace Schedulys.Data.Repositories;

public sealed class CreneauRepository : ICreneauRepository
{
    private readonly SqliteConnectionFactory _factory;
    public CreneauRepository(SqliteConnectionFactory f) => _factory = f;

    public async Task<int> CreateAsync(Creneau c)
    {
        const string sql = @"INSERT INTO Creneaux (EpreuveId, SalleId, SurveillantId, Date, HeureDebut, HeureFin, Statut)
                             VALUES (@EpreuveId, @SalleId, @SurveillantId, @Date, @HeureDebut, @HeureFin, @Statut);
                             SELECT last_insert_rowid();";
        using var cn = _factory.Create();
        return (int)(long)await cn.ExecuteScalarAsync<long>(sql, c);
    }

    public async Task<Creneau?> GetAsync(int id)
    {
        using var cn = _factory.Create();
        return await cn.QuerySingleOrDefaultAsync<Creneau>(
            "SELECT Id, EpreuveId, SalleId, SurveillantId, Date, HeureDebut, HeureFin, Statut FROM Creneaux WHERE Id=@id;", new { id });
    }

    public async Task<IReadOnlyList<Creneau>> ListByDateAsync(DateOnly date, string? annee = null)
    {
        using var cn = _factory.Create();
        var rows = await cn.QueryAsync<Creneau>(
            "SELECT Id, EpreuveId, SalleId, SurveillantId, Date, HeureDebut, HeureFin, Statut FROM Creneaux WHERE Date=@d ORDER BY HeureDebut ASC",
            new { d = date.ToString("yyyy-MM-dd") });
        return rows.ToList();
    }

    public async Task<IReadOnlyList<Creneau>> ListBySemaineAsync(DateOnly startOfWeek, string? annee = null)
    {
        var d0 = startOfWeek;
        var d6 = startOfWeek.AddDays(6);
        using var cn = _factory.Create();
        var rows = await cn.QueryAsync<Creneau>(
            "SELECT Id, EpreuveId, SalleId, SurveillantId, Date, HeureDebut, HeureFin, Statut FROM Creneaux WHERE Date BETWEEN @d0 AND @d6 ORDER BY Date, HeureDebut",
            new { d0 = d0.ToString("yyyy-MM-dd"), d6 = d6.ToString("yyyy-MM-dd") });
        return rows.ToList();
    }

    public async Task<bool> UpdateAsync(Creneau c)
    {
        using var cn = _factory.Create();
        var n = await cn.ExecuteAsync(
            "UPDATE Creneaux SET EpreuveId=@EpreuveId, SalleId=@SalleId, SurveillantId=@SurveillantId, Date=@Date, HeureDebut=@HeureDebut, HeureFin=@HeureFin, Statut=@Statut WHERE Id=@Id;", c);
        return n > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var cn = _factory.Create();
        var n = await cn.ExecuteAsync("DELETE FROM Creneaux WHERE Id=@id;", new { id });
        return n > 0;
    }
}