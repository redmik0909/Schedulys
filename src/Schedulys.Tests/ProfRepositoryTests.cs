using Schedulys.Core.Models;
using Schedulys.Tests.Helpers;

namespace Schedulys.Tests;

public sealed class ProfRepositoryTests : IAsyncLifetime
{
    private TestDb _tdb = null!;

    public async Task InitializeAsync() => _tdb = await TestDb.CreateAsync();
    public async Task DisposeAsync()    => await _tdb.DisposeAsync();

    private static Prof MakeProf(string nom = "Marie Tremblay") =>
        new() { Nom = nom, Role = "Surveillant", Annee = "2025-2026" };

    // ── CRUD de base ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Create_And_GetById_RoundTrip()
    {
        var db  = _tdb.Db;
        var id  = await db.Profs.CreateAsync(MakeProf("Alice Dupont"));
        var got = await db.Profs.GetAsync(id);

        Assert.NotNull(got);
        Assert.Equal(id,           got.Id);
        Assert.Equal("Alice Dupont", got.Nom);
        Assert.Equal("Surveillant",  got.Role);
        Assert.Equal("2025-2026",    got.Annee);
    }

    [Fact]
    public async Task GetById_NonExistent_ReturnsNull()
    {
        var got = await _tdb.Db.Profs.GetAsync(9999);
        Assert.Null(got);
    }

    [Fact]
    public async Task Create_ReturnsPositiveId()
    {
        var id = await _tdb.Db.Profs.CreateAsync(MakeProf());
        Assert.True(id > 0);
    }

    [Fact]
    public async Task Create_TwoProfs_HaveDifferentIds()
    {
        var db = _tdb.Db;
        var id1 = await db.Profs.CreateAsync(MakeProf("Prof A"));
        var id2 = await db.Profs.CreateAsync(MakeProf("Prof B"));
        Assert.NotEqual(id1, id2);
    }

    // ── ListAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task List_ReturnsAllProfs()
    {
        var db = _tdb.Db;
        await db.Profs.CreateAsync(MakeProf("Alice"));
        await db.Profs.CreateAsync(MakeProf("Bob"));
        var list = await db.Profs.ListAsync();
        Assert.Equal(2, list.Count);
    }

    [Fact]
    public async Task List_OrderedByNom()
    {
        var db = _tdb.Db;
        await db.Profs.CreateAsync(MakeProf("Zara"));
        await db.Profs.CreateAsync(MakeProf("Alice"));
        var list = await db.Profs.ListAsync();
        Assert.Equal("Alice", list[0].Nom);
        Assert.Equal("Zara",  list[1].Nom);
    }

    [Fact]
    public async Task List_WithSearch_FiltersOnNom()
    {
        var db = _tdb.Db;
        await db.Profs.CreateAsync(MakeProf("Marie Tremblay"));
        await db.Profs.CreateAsync(MakeProf("Jean Tremblay"));
        await db.Profs.CreateAsync(MakeProf("Paul Gagnon"));
        var result = await db.Profs.ListAsync(search: "Tremblay");
        Assert.Equal(2, result.Count);
        Assert.All(result, p => Assert.Contains("Tremblay", p.Nom));
    }

    [Fact]
    public async Task List_WithSearch_EmptyString_ReturnsAll()
    {
        var db = _tdb.Db;
        await db.Profs.CreateAsync(MakeProf("Alice"));
        await db.Profs.CreateAsync(MakeProf("Bob"));
        var result = await db.Profs.ListAsync(search: "");
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task List_WithAnnee_FiltersOnAnnee()
    {
        var db = _tdb.Db;
        await db.Profs.CreateAsync(new Prof { Nom = "Alice", Annee = "2025-2026" });
        await db.Profs.CreateAsync(new Prof { Nom = "Bob",   Annee = "2026-2027" });
        var result = await db.Profs.ListAsync(annee: "2025-2026");
        Assert.Single(result);
        Assert.Equal("Alice", result[0].Nom);
    }

    [Fact]
    public async Task List_WithNullAnnee_ReturnsAll()
    {
        var db = _tdb.Db;
        await db.Profs.CreateAsync(new Prof { Nom = "Alice", Annee = "2025-2026" });
        await db.Profs.CreateAsync(new Prof { Nom = "Bob",   Annee = "2026-2027" });
        var result = await db.Profs.ListAsync(annee: null);
        Assert.Equal(2, result.Count);
    }

    // ── UpdateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ChangesNom()
    {
        var db  = _tdb.Db;
        var id  = await db.Profs.CreateAsync(MakeProf("Ancien Nom"));
        var ok  = await db.Profs.UpdateAsync(new Prof { Id = id, Nom = "Nouveau Nom", Role = "Surveillant", Annee = "2025-2026" });
        Assert.True(ok);
        var got = await db.Profs.GetAsync(id);
        Assert.Equal("Nouveau Nom", got!.Nom);
    }

    [Fact]
    public async Task Update_NonExistentId_ReturnsFalse()
    {
        var ok = await _tdb.Db.Profs.UpdateAsync(new Prof { Id = 9999, Nom = "X", Role = "Surveillant", Annee = "2025-2026" });
        Assert.False(ok);
    }

    // ── DeleteAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Delete_RemovesProf()
    {
        var db = _tdb.Db;
        var id = await db.Profs.CreateAsync(MakeProf());
        var ok = await db.Profs.DeleteAsync(id);
        Assert.True(ok);
        Assert.Null(await db.Profs.GetAsync(id));
    }

    [Fact]
    public async Task Delete_NonExistentId_ReturnsFalse()
    {
        var ok = await _tdb.Db.Profs.DeleteAsync(9999);
        Assert.False(ok);
    }

    [Fact]
    public async Task Delete_CleansQuotasMinutes()
    {
        var db    = _tdb.Db;
        var profId = await db.Profs.CreateAsync(MakeProf());
        await db.Quotas.CreateAsync(new QuotaMinutes { ProfId = profId, MinutesMax = 120, AnneeScolaire = "2025-2026" });

        await db.Profs.DeleteAsync(profId);

        var quotas = await db.Quotas.GetAllByProfAsync(profId);
        Assert.Empty(quotas);
    }

    [Fact]
    public async Task Delete_CleansRolesSurveillance()
    {
        var db        = _tdb.Db;
        var profId    = await db.Profs.CreateAsync(MakeProf());
        var sessionId = await db.Sessions.CreateAsync(new Session
            { Date = "2026-05-10", Periode = "AM", HeureDebut = "08:30", AnneeScolaire = "2025-2026" });
        await db.RolesSurveillance.CreateAsync(new RoleSurveillance
            { SessionId = sessionId, TypeRole = "Régulateur", SurveillantId = profId, DureeMinutes = 60 });

        await db.Profs.DeleteAsync(profId);

        var roles = await db.RolesSurveillance.ListBySessionAsync(sessionId);
        Assert.Empty(roles);
    }

    [Fact]
    public async Task Delete_CleansGroupesExamen_WhereSurveillant()
    {
        var db        = _tdb.Db;
        var profId    = await db.Profs.CreateAsync(MakeProf());
        var sessionId = await db.Sessions.CreateAsync(new Session
            { Date = "2026-05-10", Periode = "AM", HeureDebut = "08:30", AnneeScolaire = "2025-2026" });
        await db.GroupesExamen.CreateAsync(new GroupeExamen
            { SessionId = sessionId, SurveillantId = profId, CodeGroupe = "GR-01", DureeMinutes = 90 });

        await db.Profs.DeleteAsync(profId);

        var groupes = await db.GroupesExamen.ListBySessionAsync(sessionId);
        Assert.Empty(groupes);
    }

    [Fact]
    public async Task Delete_CleansGroupesExamen_WhereEnseignant()
    {
        var db        = _tdb.Db;
        var profId    = await db.Profs.CreateAsync(MakeProf());
        var sessionId = await db.Sessions.CreateAsync(new Session
            { Date = "2026-05-10", Periode = "AM", HeureDebut = "08:30", AnneeScolaire = "2025-2026" });
        await db.GroupesExamen.CreateAsync(new GroupeExamen
            { SessionId = sessionId, EnseignantId = profId, CodeGroupe = "GR-02", DureeMinutes = 90 });

        await db.Profs.DeleteAsync(profId);

        var groupes = await db.GroupesExamen.ListBySessionAsync(sessionId);
        Assert.Empty(groupes);
    }

    [Fact]
    public async Task Delete_CleansCreneaux()
    {
        var db        = _tdb.Db;
        var profId    = await db.Profs.CreateAsync(MakeProf());
        var salleId   = await db.Salles.CreateAsync(new Salle { Nom = "101", Capacite = 30, Annee = "2025-2026" });
        var classeId  = await db.Classes.CreateAsync(new Classe { Nom = "GR-01", Effectif = 20, Annee = "2025-2026" });
        var epreuveId = await db.Epreuves.CreateAsync(new Epreuve
            { Nom = "Math", ClasseId = classeId, DureeMinutes = 90, Annee = "2025-2026" });
        await db.Creneaux.CreateAsync(new Creneau
        {
            EpreuveId    = epreuveId,
            SalleId      = salleId,
            SurveillantId = profId,
            Date         = "2026-05-10",
            HeureDebut   = "08:30",
            HeureFin     = "10:30",
        });

        await db.Profs.DeleteAsync(profId);

        var creneaux = await db.Creneaux.ListByDateAsync(new DateOnly(2026, 5, 10));
        Assert.Empty(creneaux);
    }

    [Fact]
    public async Task Delete_DetachesFromClasses_NotDeletesClasse()
    {
        var db       = _tdb.Db;
        var profId   = await db.Profs.CreateAsync(MakeProf());
        var classeId = await db.Classes.CreateAsync(new Classe
            { Nom = "GR-01", Effectif = 20, Annee = "2025-2026", ProfId = profId });

        await db.Profs.DeleteAsync(profId);

        // La classe doit encore exister
        var classes = await db.Classes.ListAsync(annee: "2025-2026");
        Assert.Single(classes);
        // ProfId doit être remis à 0
        Assert.Equal(0, classes[0].ProfId);
    }
}
