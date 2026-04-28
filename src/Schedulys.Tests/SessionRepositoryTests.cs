using Schedulys.Core.Models;
using Schedulys.Tests.Helpers;

namespace Schedulys.Tests;

public sealed class SessionRepositoryTests : IAsyncLifetime
{
    private TestDb _tdb = null!;

    public async Task InitializeAsync() => _tdb = await TestDb.CreateAsync();
    public async Task DisposeAsync()    => await _tdb.DisposeAsync();

    private static Session MakeSession(string date = "2026-05-10", string periode = "AM") => new()
    {
        Date          = date,
        Periode       = periode,
        HeureDebut    = "08:30",
        AnneeScolaire = "2025-2026",
        JourCycle     = 1,
    };

    // ── CRUD de base ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_And_GetById_RoundTrip()
    {
        var db  = _tdb.Db;
        var id  = await db.Sessions.CreateAsync(MakeSession());
        var got = await db.Sessions.GetAsync(id);

        Assert.NotNull(got);
        Assert.Equal(id,          got.Id);
        Assert.Equal("2026-05-10", got.Date);
        Assert.Equal("AM",         got.Periode);
        Assert.Equal("08:30",      got.HeureDebut);
        Assert.Equal("2025-2026",  got.AnneeScolaire);
        Assert.Equal(1,            got.JourCycle);
    }

    [Fact]
    public async Task GetById_NonExistent_ReturnsNull()
    {
        Assert.Null(await _tdb.Db.Sessions.GetAsync(9999));
    }

    [Fact]
    public async Task Create_ReturnsPositiveId()
    {
        Assert.True(await _tdb.Db.Sessions.CreateAsync(MakeSession()) > 0);
    }

    // ── ListAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_WithAnnee_FiltersCorrectly()
    {
        var db = _tdb.Db;
        await db.Sessions.CreateAsync(new Session { Date = "2026-05-10", Periode = "AM", HeureDebut = "08:30", AnneeScolaire = "2025-2026" });
        await db.Sessions.CreateAsync(new Session { Date = "2026-05-10", Periode = "AM", HeureDebut = "08:30", AnneeScolaire = "2026-2027" });

        var result = await db.Sessions.ListAsync(annee: "2025-2026");
        Assert.Single(result);
        Assert.Equal("2025-2026", result[0].AnneeScolaire);
    }

    [Fact]
    public async Task List_WithNullAnnee_ReturnsAll()
    {
        var db = _tdb.Db;
        await db.Sessions.CreateAsync(new Session { Date = "2026-05-10", Periode = "AM", HeureDebut = "08:30", AnneeScolaire = "2025-2026" });
        await db.Sessions.CreateAsync(new Session { Date = "2026-05-10", Periode = "AM", HeureDebut = "08:30", AnneeScolaire = "2026-2027" });

        var result = await db.Sessions.ListAsync(annee: null);
        Assert.Equal(2, result.Count);
    }

    // ── ListByDateAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task ListByDate_ReturnsOnlyThatDate()
    {
        var db = _tdb.Db;
        await db.Sessions.CreateAsync(MakeSession("2026-05-10"));
        await db.Sessions.CreateAsync(MakeSession("2026-05-11")); // autre date
        await db.Sessions.CreateAsync(MakeSession("2026-05-10", "PM"));

        var result = await db.Sessions.ListByDateAsync(new DateOnly(2026, 5, 10));
        Assert.Equal(2, result.Count);
        Assert.All(result, s => Assert.Equal("2026-05-10", s.Date));
    }

    [Fact]
    public async Task ListByDate_NoResults_ReturnsEmptyList()
    {
        var result = await _tdb.Db.Sessions.ListByDateAsync(new DateOnly(2026, 1, 1));
        Assert.Empty(result);
    }

    // ── ListByPeriodeAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task ListByPeriode_InclusiveBoundaries()
    {
        var db = _tdb.Db;
        await db.Sessions.CreateAsync(MakeSession("2026-05-01")); // début exact
        await db.Sessions.CreateAsync(MakeSession("2026-05-15")); // milieu
        await db.Sessions.CreateAsync(MakeSession("2026-05-31")); // fin exacte

        var result = await db.Sessions.ListByPeriodeAsync(
            new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31));
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task ListByPeriode_ExcludesOutsideRange()
    {
        var db = _tdb.Db;
        await db.Sessions.CreateAsync(MakeSession("2026-04-30")); // avant
        await db.Sessions.CreateAsync(MakeSession("2026-05-15")); // dans
        await db.Sessions.CreateAsync(MakeSession("2026-06-01")); // après

        var result = await db.Sessions.ListByPeriodeAsync(
            new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31));
        Assert.Single(result);
        Assert.Equal("2026-05-15", result[0].Date);
    }

    [Fact]
    public async Task ListByPeriode_OrderedByDateThenPeriode()
    {
        var db = _tdb.Db;
        await db.Sessions.CreateAsync(MakeSession("2026-05-10", "PM"));
        await db.Sessions.CreateAsync(MakeSession("2026-05-10", "AM"));
        await db.Sessions.CreateAsync(MakeSession("2026-05-09", "AM"));

        var result = await db.Sessions.ListByPeriodeAsync(
            new DateOnly(2026, 5, 1), new DateOnly(2026, 5, 31));
        Assert.Equal("2026-05-09", result[0].Date);
        Assert.Equal("2026-05-10", result[1].Date);
        Assert.Equal("AM",          result[1].Periode);
        Assert.Equal("PM",          result[2].Periode);
    }

    // ── UpdateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ChangesFields()
    {
        var db = _tdb.Db;
        var id = await db.Sessions.CreateAsync(MakeSession());

        var ok = await db.Sessions.UpdateAsync(new Session
        {
            Id            = id,
            Date          = "2026-06-01",
            Periode       = "PM",
            HeureDebut    = "13:00",
            AnneeScolaire = "2025-2026",
            JourCycle     = 3,
        });
        Assert.True(ok);

        var got = await db.Sessions.GetAsync(id);
        Assert.Equal("2026-06-01", got!.Date);
        Assert.Equal("PM",         got.Periode);
        Assert.Equal("13:00",      got.HeureDebut);
        Assert.Equal(3,            got.JourCycle);
    }

    [Fact]
    public async Task Update_NonExistentId_ReturnsFalse()
    {
        var s = MakeSession(); // Id = 0 par défaut → aucune ligne affectée
        var ok = await _tdb.Db.Sessions.UpdateAsync(s);
        Assert.False(ok);
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_RemovesSession()
    {
        var db = _tdb.Db;
        var id = await db.Sessions.CreateAsync(MakeSession());
        var ok = await db.Sessions.DeleteAsync(id);
        Assert.True(ok);
        Assert.Null(await db.Sessions.GetAsync(id));
    }

    [Fact]
    public async Task Delete_NonExistentId_ReturnsFalse()
    {
        Assert.False(await _tdb.Db.Sessions.DeleteAsync(9999));
    }

    [Fact]
    public async Task Delete_CascadesGroupesExamen()
    {
        var db        = _tdb.Db;
        var sessionId = await db.Sessions.CreateAsync(MakeSession());
        await db.GroupesExamen.CreateAsync(new GroupeExamen
            { SessionId = sessionId, CodeGroupe = "GR-01", DureeMinutes = 90 });

        await db.Sessions.DeleteAsync(sessionId);

        var groupes = await db.GroupesExamen.ListBySessionAsync(sessionId);
        Assert.Empty(groupes);
    }

    [Fact]
    public async Task Delete_CascadesRolesSurveillance()
    {
        var db        = _tdb.Db;
        var profId    = await db.Profs.CreateAsync(new Prof { Nom = "Alice", Annee = "2025-2026" });
        var sessionId = await db.Sessions.CreateAsync(MakeSession());
        await db.RolesSurveillance.CreateAsync(new RoleSurveillance
            { SessionId = sessionId, TypeRole = "Régulateur", SurveillantId = profId, DureeMinutes = 60 });

        await db.Sessions.DeleteAsync(sessionId);

        var roles = await db.RolesSurveillance.ListBySessionAsync(sessionId);
        Assert.Empty(roles);
    }

    [Fact]
    public async Task Delete_DoesNotDeleteOtherSessions()
    {
        var db  = _tdb.Db;
        var id1 = await db.Sessions.CreateAsync(MakeSession("2026-05-10"));
        var id2 = await db.Sessions.CreateAsync(MakeSession("2026-05-11"));

        await db.Sessions.DeleteAsync(id1);

        Assert.NotNull(await db.Sessions.GetAsync(id2));
    }
}
