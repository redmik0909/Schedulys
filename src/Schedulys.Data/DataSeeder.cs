using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Schedulys.Core.Models;

namespace Schedulys.Data;

public static class DataSeeder
{
    private const string ANNEE = "2025-2026";

    public static async Task<bool> IsAlreadySeededAsync(DataContext db)
    {
        var salles = await db.Salles.ListAsync(annee: ANNEE);
        return salles.Count >= 5;
    }

    public static async Task SeedAsync(DataContext db)
    {
        await SeedSallesAsync(db);
    }

    // ── Salles ───────────────────────────────────────────────────────────────

    private static async Task SeedSallesAsync(DataContext db)
    {
        var rooms = new (string Nom, int Cap)[]
        {
            ("121", 30), ("122", 32), ("123", 32), ("124", 29), ("125", 31),
            ("126", 32), ("127", 29), ("127b", 15), ("128", 36),
            ("131", 30), ("132", 34), ("133", 35), ("134", 30), ("135", 30),
            ("321", 33), ("322", 29), ("323", 33), ("324", 30), ("325", 34),
            ("326", 32), ("327", 33), ("328", 33), ("329", 34), ("330", 32),
            ("332", 32), ("333", 30), ("334", 28), ("335", 28), ("336", 30),
        };

        foreach (var (nom, cap) in rooms)
            await db.Salles.CreateAsync(new Salle { Nom = nom, Capacite = cap, Annee = ANNEE });
    }

    // ── Migration : nettoyage enseignants legacy ─────────────────────────────

    public static async Task<bool> NeedsProfsResetAsync(DataContext db)
    {
        var profs = await db.Profs.ListAsync();
        return profs.Any(p =>
            p.Nom.Contains("Almeida-Farias", StringComparison.OrdinalIgnoreCase) ||
            p.Nom.Contains("Vonesch",        StringComparison.OrdinalIgnoreCase));
    }

    public static async Task ResetProfsAsync(DataContext db, Action<string>? log = null)
    {
        using var cn = db.Factory.Create();
        await cn.OpenAsync();

        // Récupère uniquement les profs legacy ciblés — JAMAIS un wipe global.
        var legacyIds = (await cn.QueryAsync<int>(@"
            SELECT Id FROM Profs
            WHERE LOWER(Nom) LIKE '%almeida-farias%'
               OR LOWER(Nom) LIKE '%vonesch%'")).ToList();

        if (legacyIds.Count == 0)
        {
            log?.Invoke("ResetProfsAsync : aucun prof legacy détecté, aucune suppression.");
            return;
        }

        log?.Invoke($"ResetProfsAsync : suppression de {legacyIds.Count} prof(s) legacy ciblé(s).");
        using var tx = cn.BeginTransaction();
        await cn.ExecuteAsync("DELETE FROM QuotasMinutes     WHERE ProfId      IN @ids",                            new { ids = legacyIds }, tx);
        await cn.ExecuteAsync("DELETE FROM Creneaux          WHERE SurveillantId IN @ids",                          new { ids = legacyIds }, tx);
        await cn.ExecuteAsync("DELETE FROM RolesSurveillance WHERE SurveillantId IN @ids",                          new { ids = legacyIds }, tx);
        await cn.ExecuteAsync("DELETE FROM GroupesExamen     WHERE EnseignantId IN @ids OR SurveillantId IN @ids",  new { ids = legacyIds }, tx);
        await cn.ExecuteAsync("UPDATE Classes SET ProfId=0   WHERE ProfId       IN @ids",                           new { ids = legacyIds }, tx);
        await cn.ExecuteAsync("DELETE FROM Profs             WHERE Id            IN @ids",                          new { ids = legacyIds }, tx);
        tx.Commit();
        log?.Invoke("ResetProfsAsync : terminé.");
    }
}
