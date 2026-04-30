using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Schedulys.Core.Interfaces;
using Schedulys.Core.Models;
using Schedulys.Data.Db;

namespace Schedulys.Data.Repositories;

public sealed class GroupeExamenRepository : IGroupeExamenRepository
{
    private readonly SqliteConnectionFactory _factory;
    public GroupeExamenRepository(SqliteConnectionFactory factory) => _factory = factory;

    public async Task<int> CreateAsync(GroupeExamen g)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        return (int)await cn.ExecuteScalarAsync<long>(
            @"INSERT INTO GroupesExamen(SessionId, EpreuveId, ClasseId, CodeGroupe, EnseignantId,
              NbEleves, SurveillantId, SalleId, TiersTemps, DureeMinutes, Type, HeureFin, PremierDepart)
              VALUES (@SessionId, @EpreuveId, @ClasseId, @CodeGroupe, @EnseignantId,
              @NbEleves, @SurveillantId, @SalleId, @TiersTemps, @DureeMinutes, @Type, @HeureFin, @PremierDepart);
              SELECT last_insert_rowid();", g);
    }

    public async Task<GroupeExamen?> GetAsync(int id)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        return await cn.QueryFirstOrDefaultAsync<GroupeExamen>(
            "SELECT * FROM GroupesExamen WHERE Id=@id", new { id });
    }

    public async Task<IReadOnlyList<GroupeExamen>> ListBySessionAsync(int sessionId)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        return (await cn.QueryAsync<GroupeExamen>(
            "SELECT * FROM GroupesExamen WHERE SessionId=@sessionId ORDER BY CodeGroupe",
            new { sessionId })).AsList();
    }

    public async Task<IReadOnlyList<GroupeExamen>> ListByPeriodeAsync(DateOnly debut, DateOnly fin)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        return (await cn.QueryAsync<GroupeExamen>(
            @"SELECT g.* FROM GroupesExamen g
              JOIN Sessions s ON s.Id = g.SessionId
              WHERE s.Date BETWEEN @d0 AND @d1
              ORDER BY s.Date, s.Periode, g.CodeGroupe",
            new { d0 = debut.ToString("yyyy-MM-dd"), d1 = fin.ToString("yyyy-MM-dd") })).AsList();
    }

    // Minutes assignées à un prof pour une date précise (ou toutes les dates si null)
    public async Task<int> GetMinutesAssigneesAsync(int profId, DateOnly? date = null, string? annee = null)
    {
        using var cn = _factory.Create();
        var d = date?.ToString("yyyy-MM-dd");

        var whereG = "g.SurveillantId=@profId"
            + (d    != null ? " AND s.Date=@d"               : "")
            + (annee != null ? " AND s.AnneeScolaire=@annee" : "");
        var whereR = "r.SurveillantId=@profId"
            + (d    != null ? " AND r.Date=@d"               : "");

        var sql = $@"
            SELECT COALESCE(SUM(Minutes), 0) FROM (
                SELECT g.DureeMinutes AS Minutes
                FROM GroupesExamen g JOIN Sessions s ON s.Id=g.SessionId
                WHERE {whereG}
                UNION ALL
                SELECT r.DureeMinutes AS Minutes
                FROM RolesSurveillance r
                WHERE {whereR}
            )";

        return await cn.ExecuteScalarAsync<int>(sql, new { profId, d, annee });
    }

    // Batch : minutes assignées pour TOUS les profs à une date donnée (1 seule requête).
    // Utilisé par le tableau de quotas pour éviter le N+1.
    public async Task<IReadOnlyDictionary<int, int>> GetMinutesAssigneesByProfAsync(DateOnly date)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        var d = date.ToString("yyyy-MM-dd");

        var rows = await cn.QueryAsync<(int ProfId, int Minutes)>(@"
            SELECT ProfId, SUM(Minutes) AS Minutes FROM (
                SELECT g.SurveillantId AS ProfId, g.DureeMinutes AS Minutes
                FROM GroupesExamen g
                JOIN Sessions s ON s.Id = g.SessionId
                WHERE g.SurveillantId IS NOT NULL AND s.Date = @d
                UNION ALL
                SELECT r.SurveillantId AS ProfId, r.DureeMinutes AS Minutes
                FROM RolesSurveillance r
                WHERE r.SurveillantId IS NOT NULL AND r.Date = @d
            )
            GROUP BY ProfId", new { d });

        return rows.ToDictionary(r => r.ProfId, r => r.Minutes);
    }

    public async Task<bool> UpdateAsync(GroupeExamen g)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        var rows = await cn.ExecuteAsync(
            @"UPDATE GroupesExamen SET SessionId=@SessionId, EpreuveId=@EpreuveId,
              ClasseId=@ClasseId, CodeGroupe=@CodeGroupe, EnseignantId=@EnseignantId,
              NbEleves=@NbEleves, SurveillantId=@SurveillantId, SalleId=@SalleId,
              TiersTemps=@TiersTemps, DureeMinutes=@DureeMinutes, Type=@Type,
              HeureFin=@HeureFin, PremierDepart=@PremierDepart WHERE Id=@Id", g);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        var rows = await cn.ExecuteAsync(
            "DELETE FROM GroupesExamen WHERE Id=@id", new { id });
        return rows > 0;
    }
}
