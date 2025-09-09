using System;
using System.Globalization;
using System.Linq;
using Schedulys.Core.Models;
using Schedulys.Data;
using Schedulys.Data.Db;
using Schedulys.Core.Services;

class Program
{
    static async Task Main()
    {
        Console.WriteLine("[TestConsole] Démarrage...");

        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Schedulys");
        var dbPath = Path.Combine(appData, "data.db");

        // Init DB
        var factory = new SqliteConnectionFactory(dbPath);
        await SchemaInitializer.InitAsync(factory);

        var ctx = new DataContext(dbPath);

        // 1. Créer un prof
        var prof = new Prof { Nom = "Dupont", Role = "Surveillant", Annee = "2025-2026" };
        var id = await ctx.Profs.CreateAsync(prof);
        Console.WriteLine($"[+] Prof inséré avec Id={id}");

        // 2. Lire ce prof
        var loaded = await ctx.Profs.GetAsync(id);
        Console.WriteLine($"[=] Prof chargé: {loaded?.Nom}, rôle={loaded?.Role}, année={loaded?.Annee}");

        // 3. Lister les profs
        var all = await ctx.Profs.ListAsync();
        Console.WriteLine("[*] Liste des profs:");
        foreach (var p in all) Console.WriteLine($"   - {p.Id}: {p.Nom}");

        // 4. Supprimer ce prof
        var ok = await ctx.Profs.DeleteAsync(id);
        Console.WriteLine(ok ? $"[-] Prof Id={id} supprimé" : "Erreur suppression");

        // 5. Tester les élèves (avec gestion d'une classe de test)
        Console.WriteLine("[Eleves] Début test...");

        // S'assurer qu'il existe au moins une Classe; sinon en créer une
        var classes = await ctx.Classes.ListAsync();
        int classeId;
        if (classes.Count == 0)
        {
            classeId = await ctx.Classes.CreateAsync(new Classe { Nom = "Classe Test A", Effectif = 24, Annee = "2025-2026" });
            Console.WriteLine($"[Eleves] Classe de test créée (Id={classeId})");
        }
        else
        {
            classeId = classes.First().Id;
            Console.WriteLine($"[Eleves] Utilisation de la classe existante Id={classeId}");
        }

        // Insérer deux élèves dont un en tiers-temps
        var e1 = new Eleve { Nom = "Alice", ClasseId = classeId, TiersTemps = false };
        var e2 = new Eleve { Nom = "Bob",   ClasseId = classeId, TiersTemps = true  };
        await ctx.Eleves.CreateAsync(e1);
        await ctx.Eleves.CreateAsync(e2);

        // Compter par statut TT
        var nbReg = await ctx.Eleves.CountForClasseAndTTAsync(classeId, false);
        var nbTT  = await ctx.Eleves.CountForClasseAndTTAsync(classeId, true);
        Console.WriteLine($"[Eleves] Classe {classeId} → réguliers={nbReg}, TT={nbTT}");

        Console.WriteLine("[Eleves] Fin test.");

        // === RÈGLES MÉTIER ===
        var rules = new PlanningRules();
        var settings = new PlanningSettings();

        // Créer ou réutiliser les salles (évite les doublons à chaque run)
        var existingSalles = await ctx.Salles.ListAsync();
        var salleReg = existingSalles.FirstOrDefault(s => s.Nom == "A-101")
                     ?? new Salle { Nom = "A-101", Capacite = 24, Type = "Standard", Annee = "2025-2026" };
        if (salleReg.Id == 0) salleReg.Id = await ctx.Salles.CreateAsync(salleReg);

        var salleTt = existingSalles.FirstOrDefault(s => s.Nom == "B-202")
                    ?? new Salle { Nom = "B-202", Capacite = 10, Type = "TT", Annee = "2025-2026" };
        if (salleTt.Id == 0) salleTt.Id = await ctx.Salles.CreateAsync(salleTt);

        var salleRegId = salleReg.Id;
        var salleTtId  = salleTt.Id;

        // Créer ou réutiliser un surveillant "Martin" (évite les doublons à chaque run)
        var existingProfs = await ctx.Profs.ListAsync();
        var martin = existingProfs.FirstOrDefault(p => p.Nom == "Martin" && p.Role == "Surveillant" && p.Annee == "2025-2026")
                  ?? new Prof { Nom = "Martin", Role = "Surveillant", Annee = "2025-2026" };
        if (martin.Id == 0) martin.Id = await ctx.Profs.CreateAsync(martin);
        var profId2 = martin.Id;

        // Épreuve (60 min) pour la classe utilisée plus haut (classeId)
        var epreuveId = await ctx.Epreuves.CreateAsync(new Epreuve {
            Nom = "Math - Devoir 1",
            ClasseId = classeId,
            DureeMinutes = 60,
            TiersTemps = false,
            Ministerielle = false,
            Annee = "2025-2026"
        });

        // Créneaux: le même jour (utiliser des chaînes compatibles avec le modèle: Date="yyyy-MM-dd", Heure*="HH:mm")
        var dateStr = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");
        var hDebStr = "09:00";

        // Calcul heure fin REG
        var startReg = DateTime.ParseExact($"{dateStr} {hDebStr}", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        var finRegDt = rules.CalcHeureFin(startReg, 60, false, settings);

        var cReg = new Creneau {
            EpreuveId = epreuveId, SalleId = salleRegId, SurveillantId = profId2,
            Date = dateStr, HeureDebut = hDebStr, HeureFin = finRegDt.ToString("HH:mm"),
            DureeMinutes = 60, TiersTemps = false, Statut = "brouillon"
        };
        var cRegId = await ctx.Creneaux.CreateAsync(cReg);

        // Calcul heure fin TT
        var finTtDt = rules.CalcHeureFin(startReg, 60, true, settings);
        var cTt = new Creneau {
            EpreuveId = epreuveId, SalleId = salleTtId, SurveillantId = profId2,
            Date = dateStr, HeureDebut = hDebStr, HeureFin = finTtDt.ToString("HH:mm"),
            DureeMinutes = (int)Math.Ceiling(60 * settings.TiersTempsMultiplier),
            TiersTemps = true, Statut = "brouillon"
        };

        // Vérifier conflits avec les existants du jour
        var dateOnly = DateOnly.ParseExact(dateStr, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var existantsJour = await ctx.Creneaux.ListByDateAsync(dateOnly);
        Console.WriteLine($"[Rules] Prof conflit TT ? {rules.ProfEnConflit(profId2, existantsJour, cTt)}");
        Console.WriteLine($"[Rules] Salle conflit TT ? {rules.SalleEnConflit(salleTtId, existantsJour, cTt)}");

        // Vérifier capacité (nb élèves)
        var nbReg2 = await ctx.Eleves.CountForClasseAndTTAsync(classeId, false);
        var nbTt2  = await ctx.Eleves.CountForClasseAndTTAsync(classeId, true);
        var salleRegEntity = await ctx.Salles.GetAsync(salleRegId);
        var salleTtEntity  = await ctx.Salles.GetAsync(salleTtId);

        Console.WriteLine($"[Rules] Cap REG ok ? {rules.CapaciteValide(salleRegEntity!.Capacite, nbReg2)}");
        Console.WriteLine($"[Rules] Cap TT  ok ? {rules.CapaciteValide(salleTtEntity!.Capacite,  nbTt2)}");

        // Si ok, on insère le créneau TT
        if (!rules.ProfEnConflit(profId2, existantsJour, cTt)
            && !rules.SalleEnConflit(salleTtId, existantsJour, cTt)
            && rules.CapaciteValide(salleTt!.Capacite, nbTt2))
        {
            var cTtId = await ctx.Creneaux.CreateAsync(cTt);
            Console.WriteLine($"[Creneaux] TT créé Id={cTtId} ({cTt.HeureDebut}→{cTt.HeureFin})");
        }
        // === Sélecteur de surveillant libre ===
        var availability = new AvailabilityService(
            ctx.Profs,             // IProfRepository via DataContext
            ctx.Salles,            // ISalleRepository via DataContext
            ctx.Creneaux,          // ICreneauRepository via DataContext
            new PlanningRules(),
            new PlanningSettings()
        );
var existingProfs2 = await ctx.Profs.ListAsync();
var durand = existingProfs2.FirstOrDefault(p => p.Nom == "Durand" && p.Role == "Surveillant" && p.Annee == "2025-2026")
         ?? new Prof { Nom = "Durand", Role = "Surveillant", Annee = "2025-2026" };
if (durand.Id == 0) durand.Id = await ctx.Profs.CreateAsync(durand);

var libres = await availability.GetAvailableProfsAsync(
    dateOnly,              // le DateOnly déjà calculé
    hDebStr,               // "09:00"
    60,                    // durée de l'épreuve
    tiersTemps: true,      // par ex. on cherche un surveillant pour le TT
    annee: "2025-2026",
    role: "Surveillant"
);

Console.WriteLine("[Availability] Surveillants libres pour TT :");
foreach (var profLibre in libres)
    Console.WriteLine($" - {profLibre.Nom}");

// === Sélecteur de salles libres ===
var sallesLibres = await availability.GetAvailableSallesAsync(
    dateOnly,
    hDebStr,
    60,
    tiersTemps: true,
    nbEleves: nbTt2,
    annee: "2025-2026"
);

Console.WriteLine("[Availability] Salles libres pour TT :");
foreach (var s in sallesLibres)
    Console.WriteLine($" - {s.Nom} (cap={s.Capacite})");

        Console.WriteLine("[TestConsole] Fini.");
    }
}