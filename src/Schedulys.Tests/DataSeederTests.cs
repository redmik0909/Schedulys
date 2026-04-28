using Schedulys.Core.Models;
using Schedulys.Data;
using Schedulys.Tests.Helpers;

namespace Schedulys.Tests;

public sealed class DataSeederTests : IAsyncLifetime
{
    private TestDb _tdb = null!;
    private DataContext Db => _tdb.Db;

    public async Task InitializeAsync() => _tdb = await TestDb.CreateAsync();
    public async Task DisposeAsync()    => await _tdb.DisposeAsync();

    // ── NeedsProfsResetAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task NeedsProfsReset_WhenNoProfs_ReturnsFalse()
    {
        Assert.False(await DataSeeder.NeedsProfsResetAsync(Db));
    }

    [Fact]
    public async Task NeedsProfsReset_WhenOnlyRegularProfs_ReturnsFalse()
    {
        await Db.Profs.CreateAsync(new Prof { Nom = "Marie Tremblay", Annee = "2025-2026" });
        await Db.Profs.CreateAsync(new Prof { Nom = "Paul Gagnon",    Annee = "2025-2026" });
        Assert.False(await DataSeeder.NeedsProfsResetAsync(Db));
    }

    [Fact]
    public async Task NeedsProfsReset_WhenAlmeidaFariasPresent_ReturnsTrue()
    {
        await Db.Profs.CreateAsync(new Prof { Nom = "Jean Almeida-Farias", Annee = "2025-2026" });
        Assert.True(await DataSeeder.NeedsProfsResetAsync(Db));
    }

    [Fact]
    public async Task NeedsProfsReset_WhenVoneschPresent_ReturnsTrue()
    {
        await Db.Profs.CreateAsync(new Prof { Nom = "Pierre Vonesch", Annee = "2025-2026" });
        Assert.True(await DataSeeder.NeedsProfsResetAsync(Db));
    }

    [Fact]
    public async Task NeedsProfsReset_CaseInsensitive_Uppercase()
    {
        await Db.Profs.CreateAsync(new Prof { Nom = "ALMEIDA-FARIAS Test", Annee = "2025-2026" });
        Assert.True(await DataSeeder.NeedsProfsResetAsync(Db));
    }

    [Fact]
    public async Task NeedsProfsReset_CaseInsensitive_Mixed()
    {
        await Db.Profs.CreateAsync(new Prof { Nom = "Test VONESCH", Annee = "2025-2026" });
        Assert.True(await DataSeeder.NeedsProfsResetAsync(Db));
    }

    // ── ResetProfsAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task ResetProfs_DeletesLegacyAlmeidaFarias()
    {
        await Db.Profs.CreateAsync(new Prof { Nom = "Jean Almeida-Farias", Annee = "2025-2026" });
        await DataSeeder.ResetProfsAsync(Db);
        var profs = await Db.Profs.ListAsync();
        Assert.DoesNotContain(profs, p => p.Nom.Contains("Almeida-Farias", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ResetProfs_DeletesLegacyVonesch()
    {
        await Db.Profs.CreateAsync(new Prof { Nom = "Pierre Vonesch", Annee = "2025-2026" });
        await DataSeeder.ResetProfsAsync(Db);
        var profs = await Db.Profs.ListAsync();
        Assert.DoesNotContain(profs, p => p.Nom.Contains("Vonesch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ResetProfs_PreservesNonLegacyProfs()
    {
        await Db.Profs.CreateAsync(new Prof { Nom = "Marie Tremblay", Annee = "2025-2026" });
        await Db.Profs.CreateAsync(new Prof { Nom = "Jean Vonesch",   Annee = "2025-2026" }); // legacy
        await DataSeeder.ResetProfsAsync(Db);
        var profs = await Db.Profs.ListAsync();
        Assert.Single(profs);
        Assert.Equal("Marie Tremblay", profs[0].Nom);
    }

    [Fact]
    public async Task ResetProfs_WhenNothingToDelete_LogsAndSkips()
    {
        await Db.Profs.CreateAsync(new Prof { Nom = "Marie Tremblay", Annee = "2025-2026" });
        var logs = new List<string>();
        await DataSeeder.ResetProfsAsync(Db, logs.Add);
        Assert.Contains(logs, m => m.Contains("aucun prof legacy", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ResetProfs_CleansQuotasMinutes_ForLegacyProf()
    {
        var profId = await Db.Profs.CreateAsync(new Prof { Nom = "Jean Vonesch", Annee = "2025-2026" });
        await Db.Quotas.CreateAsync(new QuotaMinutes { ProfId = profId, MinutesMax = 120, AnneeScolaire = "2025-2026" });

        await DataSeeder.ResetProfsAsync(Db);

        var quotas = await Db.Quotas.GetAllByProfAsync(profId);
        Assert.Empty(quotas); // quotas doivent être supprimés
    }

    // ── BUG: ResetProfsAsync ne nettoie pas les tables liées ────────────────

    [Fact(DisplayName = "BUG-001: ResetProfs laisse des Creneaux orphelins pour les profs legacy")]
    public async Task ResetProfs_Bug001_LeavesOrphanedCreneaux()
    {
        // Arrange
        var profId    = await Db.Profs.CreateAsync(new Prof { Nom = "Jean Vonesch", Annee = "2025-2026" });
        var salleId   = await Db.Salles.CreateAsync(new Salle { Nom = "101", Capacite = 30, Annee = "2025-2026" });
        var classeId  = await Db.Classes.CreateAsync(new Classe { Nom = "GR-01", Effectif = 20, Annee = "2025-2026" });
        var epreuveId = await Db.Epreuves.CreateAsync(new Epreuve
            { Nom = "Math", ClasseId = classeId, DureeMinutes = 90, Annee = "2025-2026" });
        await Db.Creneaux.CreateAsync(new Creneau
        {
            EpreuveId    = epreuveId,
            SalleId      = salleId,
            SurveillantId = profId,
            Date         = "2026-05-10",
            HeureDebut   = "08:30",
            HeureFin     = "10:30",
        });

        // Act
        await DataSeeder.ResetProfsAsync(Db);

        // Assert: le créneau devrait être supprimé — mais ce test va ÉCHOUER (bug)
        var creneaux = await Db.Creneaux.ListByDateAsync(new DateOnly(2026, 5, 10));
        Assert.Empty(creneaux);
    }

    [Fact(DisplayName = "BUG-002: ResetProfs laisse des RolesSurveillance orphelins pour les profs legacy")]
    public async Task ResetProfs_Bug002_LeavesOrphanedRolesSurveillance()
    {
        // Arrange
        var profId    = await Db.Profs.CreateAsync(new Prof { Nom = "Jean Almeida-Farias", Annee = "2025-2026" });
        var sessionId = await Db.Sessions.CreateAsync(new Session
            { Date = "2026-05-10", Periode = "AM", HeureDebut = "08:30", AnneeScolaire = "2025-2026" });
        await Db.RolesSurveillance.CreateAsync(new RoleSurveillance
            { SessionId = sessionId, TypeRole = "Régulateur", SurveillantId = profId, DureeMinutes = 60 });

        // Act
        await DataSeeder.ResetProfsAsync(Db);

        // Assert: le rôle devrait être supprimé — mais ce test va ÉCHOUER (bug)
        var roles = await Db.RolesSurveillance.ListBySessionAsync(sessionId);
        Assert.Empty(roles);
    }

    [Fact(DisplayName = "BUG-003: ResetProfs laisse des GroupesExamen orphelins (EnseignantId) pour les profs legacy")]
    public async Task ResetProfs_Bug003_LeavesOrphanedGroupesExamen_Enseignant()
    {
        // Arrange
        var profId    = await Db.Profs.CreateAsync(new Prof { Nom = "Jean Vonesch", Annee = "2025-2026" });
        var sessionId = await Db.Sessions.CreateAsync(new Session
            { Date = "2026-05-10", Periode = "AM", HeureDebut = "08:30", AnneeScolaire = "2025-2026" });
        await Db.GroupesExamen.CreateAsync(new GroupeExamen
        {
            SessionId    = sessionId,
            EnseignantId = profId,  // prof legacy comme enseignant
            CodeGroupe   = "GR-01",
            DureeMinutes = 90,
        });

        // Act
        await DataSeeder.ResetProfsAsync(Db);

        // Assert: le groupe devrait être nettoyé — mais ce test va ÉCHOUER (bug)
        var groupes = await Db.GroupesExamen.ListBySessionAsync(sessionId);
        Assert.Empty(groupes);
    }
}
