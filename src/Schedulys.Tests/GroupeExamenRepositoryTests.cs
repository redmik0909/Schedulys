using Schedulys.Core.Models;
using Schedulys.Tests.Helpers;

namespace Schedulys.Tests;

public sealed class GroupeExamenRepositoryTests : IAsyncLifetime
{
    private TestDb _tdb = null!;

    public async Task InitializeAsync() => _tdb = await TestDb.CreateAsync();
    public async Task DisposeAsync()    => await _tdb.DisposeAsync();

    private async Task<int> MakeSessionAsync(string date = "2026-05-10")
    {
        return await _tdb.Db.Sessions.CreateAsync(new Session
        {
            Date = date, Periode = "AM", HeureDebut = "08:30", AnneeScolaire = "2025-2026"
        });
    }

    private async Task<int> MakeProfAsync(string nom = "Alice")
    {
        return await _tdb.Db.Profs.CreateAsync(new Prof { Nom = nom, Annee = "2025-2026" });
    }

    // ── CRUD de base ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_And_GetById_RoundTrip()
    {
        var db        = _tdb.Db;
        var sessionId = await MakeSessionAsync();
        var profId    = await MakeProfAsync();

        var g = new GroupeExamen
        {
            SessionId    = sessionId,
            SurveillantId = profId,
            CodeGroupe   = "GR-01",
            DureeMinutes = 90,
            Type         = "Standard",
            NbEleves     = 25,
            HeureFin     = "10:30",
        };
        var id  = await db.GroupesExamen.CreateAsync(g);
        var got = await db.GroupesExamen.GetAsync(id);

        Assert.NotNull(got);
        Assert.Equal(id,         got.Id);
        Assert.Equal(sessionId,  got.SessionId);
        Assert.Equal(profId,     got.SurveillantId);
        Assert.Equal("GR-01",    got.CodeGroupe);
        Assert.Equal(90,         got.DureeMinutes);
        Assert.Equal("Standard", got.Type);
        Assert.Equal(25,         got.NbEleves);
    }

    [Fact]
    public async Task GetById_NonExistent_ReturnsNull()
    {
        Assert.Null(await _tdb.Db.GroupesExamen.GetAsync(9999));
    }

    [Fact]
    public async Task ListBySession_ReturnsOnlyThatSession()
    {
        var db         = _tdb.Db;
        var sessionId1 = await MakeSessionAsync("2026-05-10");
        var sessionId2 = await MakeSessionAsync("2026-05-11");

        await db.GroupesExamen.CreateAsync(new GroupeExamen { SessionId = sessionId1, CodeGroupe = "GR-A", DureeMinutes = 90 });
        await db.GroupesExamen.CreateAsync(new GroupeExamen { SessionId = sessionId2, CodeGroupe = "GR-B", DureeMinutes = 90 });

        var result = await db.GroupesExamen.ListBySessionAsync(sessionId1);
        Assert.Single(result);
        Assert.Equal("GR-A", result[0].CodeGroupe);
    }

    [Fact]
    public async Task ListBySession_OrderedByCodeGroupe()
    {
        var db        = _tdb.Db;
        var sessionId = await MakeSessionAsync();
        await db.GroupesExamen.CreateAsync(new GroupeExamen { SessionId = sessionId, CodeGroupe = "ZZ", DureeMinutes = 90 });
        await db.GroupesExamen.CreateAsync(new GroupeExamen { SessionId = sessionId, CodeGroupe = "AA", DureeMinutes = 90 });

        var result = await db.GroupesExamen.ListBySessionAsync(sessionId);
        Assert.Equal("AA", result[0].CodeGroupe);
        Assert.Equal("ZZ", result[1].CodeGroupe);
    }

    // ── UpdateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ChangesFields()
    {
        var db        = _tdb.Db;
        var sessionId = await MakeSessionAsync();
        var id        = await db.GroupesExamen.CreateAsync(
            new GroupeExamen { SessionId = sessionId, CodeGroupe = "OLD", DureeMinutes = 60 });

        var ok = await db.GroupesExamen.UpdateAsync(new GroupeExamen
        {
            Id           = id,
            SessionId    = sessionId,
            CodeGroupe   = "NEW",
            DureeMinutes = 120,
            Type         = "SAI",
        });
        Assert.True(ok);

        var got = await db.GroupesExamen.GetAsync(id);
        Assert.Equal("NEW", got!.CodeGroupe);
        Assert.Equal(120,    got.DureeMinutes);
        Assert.Equal("SAI",  got.Type);
    }

    [Fact]
    public async Task Update_NonExistentId_ReturnsFalse()
    {
        var ok = await _tdb.Db.GroupesExamen.UpdateAsync(
            new GroupeExamen { Id = 9999, SessionId = 1, CodeGroupe = "X", DureeMinutes = 0 });
        Assert.False(ok);
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_RemovesGroupe()
    {
        var db        = _tdb.Db;
        var sessionId = await MakeSessionAsync();
        var id        = await db.GroupesExamen.CreateAsync(
            new GroupeExamen { SessionId = sessionId, CodeGroupe = "GR-01", DureeMinutes = 90 });

        var ok = await db.GroupesExamen.DeleteAsync(id);
        Assert.True(ok);
        Assert.Null(await db.GroupesExamen.GetAsync(id));
    }

    [Fact]
    public async Task Delete_NonExistentId_ReturnsFalse()
    {
        Assert.False(await _tdb.Db.GroupesExamen.DeleteAsync(9999));
    }

    // ── GetMinutesAssigneesAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetMinutes_FromGroupes_WithSurveillant()
    {
        var db        = _tdb.Db;
        var profId    = await MakeProfAsync();
        var sessionId = await MakeSessionAsync("2026-05-10");

        await db.GroupesExamen.CreateAsync(new GroupeExamen
            { SessionId = sessionId, SurveillantId = profId, DureeMinutes = 90 });
        await db.GroupesExamen.CreateAsync(new GroupeExamen
            { SessionId = sessionId, SurveillantId = profId, DureeMinutes = 60 });

        var minutes = await db.GroupesExamen.GetMinutesAssigneesAsync(
            profId, new DateOnly(2026, 5, 10));
        Assert.Equal(150, minutes); // 90 + 60
    }

    [Fact]
    public async Task GetMinutes_FromRoles()
    {
        var db        = _tdb.Db;
        var profId    = await MakeProfAsync();
        var sessionId = await MakeSessionAsync("2026-05-10");
        await db.RolesSurveillance.CreateAsync(new RoleSurveillance
            { SessionId = sessionId, TypeRole = "Régulateur", SurveillantId = profId, DureeMinutes = 45 });

        var minutes = await db.GroupesExamen.GetMinutesAssigneesAsync(
            profId, new DateOnly(2026, 5, 10));
        Assert.Equal(45, minutes);
    }

    [Fact]
    public async Task GetMinutes_CombinesGroupesAndRoles()
    {
        var db        = _tdb.Db;
        var profId    = await MakeProfAsync();
        var sessionId = await MakeSessionAsync("2026-05-10");

        await db.GroupesExamen.CreateAsync(new GroupeExamen
            { SessionId = sessionId, SurveillantId = profId, DureeMinutes = 90 });
        await db.RolesSurveillance.CreateAsync(new RoleSurveillance
            { SessionId = sessionId, TypeRole = "Régulateur", SurveillantId = profId, DureeMinutes = 30 });

        var minutes = await db.GroupesExamen.GetMinutesAssigneesAsync(
            profId, new DateOnly(2026, 5, 10));
        Assert.Equal(120, minutes); // 90 + 30
    }

    [Fact]
    public async Task GetMinutes_DifferentDate_ReturnsZero()
    {
        var db        = _tdb.Db;
        var profId    = await MakeProfAsync();
        var sessionId = await MakeSessionAsync("2026-05-10");
        await db.GroupesExamen.CreateAsync(new GroupeExamen
            { SessionId = sessionId, SurveillantId = profId, DureeMinutes = 90 });

        var minutes = await db.GroupesExamen.GetMinutesAssigneesAsync(
            profId, new DateOnly(2026, 5, 11)); // autre date
        Assert.Equal(0, minutes);
    }

    [Fact]
    public async Task GetMinutes_NullSurveillant_NotCounted()
    {
        var db        = _tdb.Db;
        var profId    = await MakeProfAsync();
        var sessionId = await MakeSessionAsync("2026-05-10");
        // Groupe sans surveillant
        await db.GroupesExamen.CreateAsync(new GroupeExamen
            { SessionId = sessionId, SurveillantId = null, DureeMinutes = 90 });

        var minutes = await db.GroupesExamen.GetMinutesAssigneesAsync(
            profId, new DateOnly(2026, 5, 10));
        Assert.Equal(0, minutes);
    }

    [Fact]
    public async Task GetMinutes_WithAnnee_FiltersOnYear()
    {
        var db        = _tdb.Db;
        var profId    = await MakeProfAsync();
        var sessionId = await MakeSessionAsync("2026-05-10");
        await db.GroupesExamen.CreateAsync(new GroupeExamen
            { SessionId = sessionId, SurveillantId = profId, DureeMinutes = 90 });

        var minutes = await db.GroupesExamen.GetMinutesAssigneesAsync(
            profId, new DateOnly(2026, 5, 10), annee: "2025-2026");
        Assert.Equal(90, minutes);

        var minutesWrongYear = await db.GroupesExamen.GetMinutesAssigneesAsync(
            profId, new DateOnly(2026, 5, 10), annee: "2026-2027");
        Assert.Equal(0, minutesWrongYear);
    }

    // ── GetMinutesAssigneesByProfAsync (batch) ────────────────────────────────

    [Fact]
    public async Task GetMinutesBatch_ReturnsCorrectTotalsForMultipleProfs()
    {
        var db        = _tdb.Db;
        var prof1     = await MakeProfAsync("Alice");
        var prof2     = await MakeProfAsync("Bob");
        var sessionId = await MakeSessionAsync("2026-05-10");

        await db.GroupesExamen.CreateAsync(new GroupeExamen
            { SessionId = sessionId, SurveillantId = prof1, DureeMinutes = 90 });
        await db.GroupesExamen.CreateAsync(new GroupeExamen
            { SessionId = sessionId, SurveillantId = prof2, DureeMinutes = 60 });
        await db.RolesSurveillance.CreateAsync(new RoleSurveillance
            { SessionId = sessionId, TypeRole = "Reg", SurveillantId = prof1, DureeMinutes = 30 });

        var map = await db.GroupesExamen.GetMinutesAssigneesByProfAsync(new DateOnly(2026, 5, 10));

        Assert.True(map.ContainsKey(prof1));
        Assert.True(map.ContainsKey(prof2));
        Assert.Equal(120, map[prof1]); // 90 + 30
        Assert.Equal(60,  map[prof2]);
    }

    [Fact]
    public async Task GetMinutesBatch_DifferentDate_EmptyMap()
    {
        var db        = _tdb.Db;
        var profId    = await MakeProfAsync();
        var sessionId = await MakeSessionAsync("2026-05-10");
        await db.GroupesExamen.CreateAsync(new GroupeExamen
            { SessionId = sessionId, SurveillantId = profId, DureeMinutes = 90 });

        var map = await db.GroupesExamen.GetMinutesAssigneesByProfAsync(new DateOnly(2026, 5, 11));
        Assert.Empty(map);
    }

    [Fact]
    public async Task GetMinutesBatch_ExcludesNullSurveillant()
    {
        var db        = _tdb.Db;
        var sessionId = await MakeSessionAsync("2026-05-10");
        await db.GroupesExamen.CreateAsync(new GroupeExamen
            { SessionId = sessionId, SurveillantId = null, DureeMinutes = 90 });

        var map = await db.GroupesExamen.GetMinutesAssigneesByProfAsync(new DateOnly(2026, 5, 10));
        Assert.Empty(map);
    }

    [Fact]
    public async Task GetMinutesBatch_MatchesSingleQuery_Consistency()
    {
        // Vérifie que la requête batch donne le même résultat que la requête individuelle
        var db        = _tdb.Db;
        var prof1     = await MakeProfAsync("Alice");
        var prof2     = await MakeProfAsync("Bob");
        var sessionId = await MakeSessionAsync("2026-05-10");

        await db.GroupesExamen.CreateAsync(new GroupeExamen
            { SessionId = sessionId, SurveillantId = prof1, DureeMinutes = 75 });
        await db.GroupesExamen.CreateAsync(new GroupeExamen
            { SessionId = sessionId, SurveillantId = prof1, DureeMinutes = 45 });
        await db.RolesSurveillance.CreateAsync(new RoleSurveillance
            { SessionId = sessionId, TypeRole = "Reg", SurveillantId = prof2, DureeMinutes = 60 });

        var date  = new DateOnly(2026, 5, 10);
        var batch = await db.GroupesExamen.GetMinutesAssigneesByProfAsync(date);
        var p1    = await db.GroupesExamen.GetMinutesAssigneesAsync(prof1, date);
        var p2    = await db.GroupesExamen.GetMinutesAssigneesAsync(prof2, date);

        Assert.Equal(p1, batch.GetValueOrDefault(prof1));
        Assert.Equal(p2, batch.GetValueOrDefault(prof2));
    }
}
