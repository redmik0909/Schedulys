using System.Collections.Generic;
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
            @"INSERT INTO GroupesExamen(SessionId, EpreuveId, CodeGroupe, EnseignantId,
              NbEleves, SurveillantId, SalleId, TiersTemps, DureeMinutes, Type)
              VALUES (@SessionId, @EpreuveId, @CodeGroupe, @EnseignantId,
              @NbEleves, @SurveillantId, @SalleId, @TiersTemps, @DureeMinutes, @Type);
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
        await cn.OpenAsync();

        var d = date?.ToString("yyyy-MM-dd");

        // Groupes d'examen (rôle de surveillant)
        var sqlG = (d, annee) switch
        {
            (not null, not null) =>
                @"SELECT COALESCE(SUM(g.DureeMinutes),0) FROM GroupesExamen g
                  JOIN Sessions s ON s.Id=g.SessionId
                  WHERE g.SurveillantId=@profId AND s.Date=@d AND s.AnneeScolaire=@annee",
            (not null, null) =>
                @"SELECT COALESCE(SUM(g.DureeMinutes),0) FROM GroupesExamen g
                  JOIN Sessions s ON s.Id=g.SessionId
                  WHERE g.SurveillantId=@profId AND s.Date=@d",
            (null, not null) =>
                @"SELECT COALESCE(SUM(g.DureeMinutes),0) FROM GroupesExamen g
                  JOIN Sessions s ON s.Id=g.SessionId
                  WHERE g.SurveillantId=@profId AND s.AnneeScolaire=@annee",
            _ =>
                @"SELECT COALESCE(SUM(g.DureeMinutes),0) FROM GroupesExamen g
                  WHERE g.SurveillantId=@profId"
        };
        var fromGroupes = await cn.ExecuteScalarAsync<int>(sqlG, new { profId, d, annee });

        // Rôles de surveillance
        var sqlR = (d, annee) switch
        {
            (not null, not null) =>
                @"SELECT COALESCE(SUM(r.DureeMinutes),0) FROM RolesSurveillance r
                  JOIN Sessions s ON s.Id=r.SessionId
                  WHERE r.SurveillantId=@profId AND s.Date=@d AND s.AnneeScolaire=@annee",
            (not null, null) =>
                @"SELECT COALESCE(SUM(r.DureeMinutes),0) FROM RolesSurveillance r
                  JOIN Sessions s ON s.Id=r.SessionId
                  WHERE r.SurveillantId=@profId AND s.Date=@d",
            (null, not null) =>
                @"SELECT COALESCE(SUM(r.DureeMinutes),0) FROM RolesSurveillance r
                  JOIN Sessions s ON s.Id=r.SessionId
                  WHERE r.SurveillantId=@profId AND s.AnneeScolaire=@annee",
            _ =>
                @"SELECT COALESCE(SUM(r.DureeMinutes),0) FROM RolesSurveillance r
                  WHERE r.SurveillantId=@profId"
        };
        var fromRoles = await cn.ExecuteScalarAsync<int>(sqlR, new { profId, d, annee });

        return fromGroupes + fromRoles;
    }

    public async Task<bool> UpdateAsync(GroupeExamen g)
    {
        using var cn = _factory.Create();
        await cn.OpenAsync();
        var rows = await cn.ExecuteAsync(
            @"UPDATE GroupesExamen SET SessionId=@SessionId, EpreuveId=@EpreuveId,
              CodeGroupe=@CodeGroupe, EnseignantId=@EnseignantId, NbEleves=@NbEleves,
              SurveillantId=@SurveillantId, SalleId=@SalleId, TiersTemps=@TiersTemps,
              DureeMinutes=@DureeMinutes, Type=@Type WHERE Id=@Id", g);
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
