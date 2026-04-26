using System;
using System.Collections.Generic;
using System.Text;
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

    public static async Task<bool> AreQuotasParJourSeededAsync(DataContext db)
    {
        var quotas = await db.Quotas.ListAsync(annee: ANNEE);
        return quotas.Any(q => q.JourCycle > 0);
    }

    public static async Task SeedAsync(DataContext db)
    {
        var salleIds    = await SeedSallesAsync(db);
        var profIds     = await SeedProfsAsync(db);
        await SeedQuotasAsync(db, profIds);
        var classeIds   = await SeedClassesAsync(db);
        var epreuveIds  = await SeedEpreuvesAsync(db, classeIds);
        await SeedSessionsAsync(db, salleIds, profIds, epreuveIds);
    }

    // ─── Salles ─────────────────────────────────────────────────────────────

    private static async Task<Dictionary<string, int>> SeedSallesAsync(DataContext db)
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

        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (nom, cap) in rooms)
        {
            var id = await db.Salles.CreateAsync(new Salle { Nom = nom, Capacite = cap, Annee = ANNEE });
            dict[nom] = id;
        }
        return dict;
    }

    // ─── Profs ──────────────────────────────────────────────────────────────

    // ─── Vérification / reset enseignants ──────────────────────────────────

    public static async Task<bool> NeedsProfsResetAsync(DataContext db)
    {
        var profs = await db.Profs.ListAsync();
        return profs.Any(p =>
            p.Nom.Contains("Almeida-Farias", StringComparison.OrdinalIgnoreCase) ||
            p.Nom.Contains("Vonesch",        StringComparison.OrdinalIgnoreCase));
    }

    public static async Task ResetProfsAsync(DataContext db)
    {
        using var cn = db.Factory.Create();
        await cn.OpenAsync();
        await cn.ExecuteAsync("DELETE FROM QuotasMinutes");
        await cn.ExecuteAsync("DELETE FROM Profs");

        var profIds = await SeedProfsAsync(db, includeExtras: false);

        foreach (var (htmlName, jours) in QuotasParJourData)
        {
            var key = ToTitleCase(htmlName).ToLowerInvariant();
            if (!profIds.TryGetValue(key, out var profId)) continue;
            for (int j = 1; j <= 9; j++)
                await db.Quotas.UpsertAsync(new QuotaMinutes
                {
                    ProfId        = profId,
                    JourCycle     = j,
                    MinutesMax    = jours[j - 1],
                    AnneeScolaire = ANNEE,
                });
        }
    }

    private static async Task<Dictionary<string, int>> SeedProfsAsync(DataContext db, bool includeExtras = true)
    {
        var profs = new (string HtmlName, string Role)[]
        {
            ("ADAM, NADIA",              "Ortho. SAI"),
            ("ATAMNIA, BACHIR",          "Enseignant"),
            ("AYARI, RAFIKA",            "Enseignant"),
            ("BASSOWOU, KODJO",          "Enseignant"),
            ("BEAUSOLEIL-HUNEAULT, S.",  "Enseignant"),
            ("BELAL, CHERIFA",           "Enseignant"),
            ("BENSIALI, LINDA",          "Enseignant"),
            ("BENZENATI, FARID",         "Enseignant"),
            ("BENZINE, LAMIA",           "Enseignant"),
            ("BISAILLON, ELISABETH",     "Enseignant"),
            ("BONHOMME, CATHERINE",      "Enseignant"),
            ("BOUAICHE, FADILA",         "Enseignant"),
            ("BOUCHER, MARGUERITE",      "Enseignant"),
            ("BOUMAZA, WALID",           "Enseignant"),
            ("BROCHU, JENNIFER",         "Enseignant"),
            ("CHANTAL, SYLVIANNE",       "Enseignant"),
            ("CHARLES, KISCHNER",        "Enseignant"),
            ("CHAUVIN, M-O.",            "Enseignant"),
            ("CIOLPAN, GRATIELA",        "Enseignant"),
            ("CORDEAU, PASCALE",         "Enseignant"),
            ("COUTURE, PHILIPPE",        "Enseignant"),
            ("DESTROISMAISONS, N.",      "Enseignant"),
            ("DIEDHIOU, NFALLY",         "Enseignant"),
            ("DIONNE, PIERRE-LUC",       "Enseignant"),
            ("DJEDDI, REZIKA",           "Enseignant"),
            ("DUCHESNE, GENEVIÈVE",      "Enseignant"),
            ("DUMAY, MARTINE",           "Coordonnateur EHDAA"),
            ("FERAZ, FATIMA",            "Enseignant"),
            ("FLEIFEL, VINCENT",         "Enseignant"),
            ("FOURNIER, M-F.",           "Enseignant"),
            ("GAGNON, ALEXIS",           "Enseignant"),
            ("GHOWIL, AMIR",             "Enseignant"),
            ("GIRARD, FRANCIS",          "Enseignant"),
            ("GIRAUD, SHEIRLEY",         "Enseignant"),
            ("GRÉGOIRE, FRANCIS",        "Enseignant"),
            ("GUEGUEN-SOULIER, N.",      "Enseignant"),
            ("HIDJA, HAYETTE",           "Enseignant"),
            ("HOCHEREAU, ALAIN",         "Enseignant"),
            ("HOUARI, YAZID",            "Enseignant"),
            ("HOWA, SONIA",              "Enseignant"),
            ("JOLIN, JÉRÔME",            "Enseignant"),
            ("JONCAS, CINDY",            "Enseignant"),
            ("JOURNÉ, LUC",              "Enseignant"),
            ("KARANGWA, MIREILLE",       "Enseignant"),
            ("KHELIFI, MOHSEN",          "Enseignant"),
            ("LACOMBE, DAVID",           "Enseignant"),
            ("LALONGE, CHANTAL",         "Enseignant"),
            ("LANTHIER, SARAH",          "Enseignant"),
            ("LAOUINA, SAID",            "Enseignant"),
            ("LARIVIÈRE TURCOTTE, K.",   "Enseignant"),
            ("LAURENDEAU, ETHEL",        "Enseignant"),
            ("LAVOIE, CLAUDIA",          "Enseignant"),
            ("LECONTE, ELVIS",           "Ortho. SAI"),
            ("MABSOUT, MINA",            "Enseignant"),
            ("MANÉ, SOULEYMANE",         "Enseignant"),
            ("MERCIER, VÉRONIQUE",       "Enseignant"),
            ("MICHAUD-GUILBAULT, É.",    "Enseignant"),
            ("MJIDOU, MOHAMED OMAR",     "Enseignant"),
            ("MONTALVO, JHEINSEN",       "Enseignant"),
            ("MORIN, KEVIN",             "Enseignant"),
            ("MORRIS, BRIGITTE",         "Enseignant"),
            ("PAQUETTE, ANNE",           "Coordonnateur EHDAA"),
            ("PELLAND, MARYLÈNE",        "Enseignant"),
            ("PILOTE, ANDREA",           "Enseignant"),
            ("POITRAS-QUINIOU, C.",      "Ortho. SAI"),
            ("PROPHETE, JOCELYN",        "Enseignant"),
            ("PROVOST, CHRISTIAN",       "Enseignant"),
            ("RACHEDI, KARIMA",          "Enseignant"),
            ("ROBERGE, KARINE",          "Enseignant"),
            ("ROBIDAS, PHILIPPE",        "Enseignant"),
            ("ROUSSELLE, NICHOLAS",      "Enseignant"),
            ("RUBIO, ANGELINA",          "Enseignant"),
            ("RUIZ, MAURICIO",           "Enseignant"),
            ("SAHLI, ADEL KHEMAIS",      "Enseignant"),
            ("SANSAL, AMEL",             "Enseignant"),
            ("SAVARD-GOURDE, A-A.",      "Enseignant"),
            ("SAÏDI, NABIL",             "Enseignant"),
            ("ST-AMANT-PROULX, A.",      "Enseignant"),
            ("TAALOUCHT, SOUAD",         "Enseignant"),
            ("TORRES-BASCOUR, N.",       "Enseignant"),
            ("TOSKA, GLITIANA",          "Enseignant"),
            ("TREMBLAY, N-J.",           "Enseignant"),
            ("XHERAJ, ZHANKLINA",        "Enseignant"),
        };

        var extras = new (string Nom, string Role)[]
        {
            ("Almeida-Farias, N.", "Enseignant"),
            ("Boucher, Gabriel",   "Enseignant"),
            ("Vonesch, Ariane",    "Enseignant"),
        };

        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var (htmlName, role) in profs)
        {
            var nom = ToTitleCase(htmlName);
            var id  = await db.Profs.CreateAsync(new Prof { Nom = nom, Role = role, Annee = ANNEE });
            dict[nom.ToLowerInvariant()] = id;
        }

        if (includeExtras)
        {
            foreach (var (nom, role) in extras)
            {
                var id = await db.Profs.CreateAsync(new Prof { Nom = nom, Role = role, Annee = ANNEE });
                dict[nom.ToLowerInvariant()] = id;
            }
        }

        return dict;
    }

    // ─── Quotas minutes/jour ─────────────────────────────────────────────────

    // Données brutes HTML : Jour1..Jour9 par enseignant
    private static readonly (string HtmlName, int[] Jours)[] QuotasParJourData = new[]
    {
        ("ADAM, NADIA",             new[]{  6,  20,  16,   6,  18,  21,   8,  18,   6}),
        ("ATAMNIA, BACHIR",         new[]{240, 150, 300, 300, 225, 150, 225, 300, 240}),
        ("AYARI, RAFIKA",           new[]{150, 300, 205, 225, 225, 270, 235, 235, 225}),
        ("BASSOWOU, KODJO",         new[]{150, 265, 235, 150, 240, 290, 225, 150, 225}),
        ("BEAUSOLEIL-HUNEAULT, S.", new[]{240, 247, 210, 245, 195, 225, 237, 235, 270}),
        ("BELAL, CHERIFA",          new[]{175, 235, 225, 225, 225,  75, 240, 255, 225}),
        ("BENSIALI, LINDA",         new[]{250, 165, 238, 225, 160, 225, 238, 240, 178}),
        ("BENZENATI, FARID",        new[]{256, 231, 150, 285, 175, 285, 210, 231, 231}),
        ("BENZINE, LAMIA",          new[]{300, 173, 225, 173, 225, 238, 150, 240, 238}),
        ("BISAILLON, ELISABETH",    new[]{ 75,  75, 150,  75, 225,  75, 225,  75,   0}),
        ("BONHOMME, CATHERINE",     new[]{190, 225, 225, 225, 235, 175, 235, 210, 300}),
        ("BOUAICHE, FADILA",        new[]{225, 235, 170, 300,  75, 300, 170, 300, 235}),
        ("BOUCHER, MARGUERITE",     new[]{300,  75, 300, 190, 225, 225, 165, 300, 175}),
        ("BOUMAZA, WALID",          new[]{225, 150, 250, 225, 163, 238, 173, 225, 240}),
        ("BROCHU, JENNIFER",        new[]{225, 150, 240, 225, 240, 280, 172, 257, 190}),
        ("CHANTAL, SYLVIANNE",      new[]{235, 300, 300, 150,  75, 285, 242, 235, 242}),
        ("CHARLES, KISCHNER",       new[]{225, 178, 300,  75, 300, 100, 300, 163, 235}),
        ("CHAUVIN, M-O.",           new[]{235, 225, 370, 190, 165, 262, 247, 180, 150}),
        ("CIOLPAN, GRATIELA",       new[]{250, 150, 261, 257, 150, 262, 165, 225, 261}),
        ("CORDEAU, PASCALE",        new[]{300, 300, 300, 300, 150, 300, 300, 300, 300}),
        ("COUTURE, PHILIPPE",       new[]{245,  75, 150, 150,  75, 170,   0,  75,  75}),
        ("DESTROISMAISONS, N.",     new[]{295, 150, 225, 165, 300, 165, 240, 160, 235}),
        ("DIEDHIOU, NFALLY",        new[]{160, 241, 225, 175, 256, 225, 150, 241, 225}),
        ("DIONNE, PIERRE-LUC",      new[]{225, 300, 225, 225, 150, 252, 250, 242, 105}),
        ("DJEDDI, REZIKA",          new[]{255, 225, 225, 225, 300, 150, 300, 225, 225}),
        ("DUCHESNE, GENEVIÈVE",     new[]{300, 250, 165, 270, 150, 250, 295, 225, 225}),
        ("DUMAY, MARTINE",          new[]{  0,   0,   0,   0,  10,   0,  10,  10,  10}),
        ("FERAZ, FATIMA",           new[]{150, 225, 231, 225, 160, 231, 246, 150, 256}),
        ("FLEIFEL, VINCENT",        new[]{247, 225, 280, 225, 150, 247, 235, 270, 235}),
        ("FOURNIER, M-F.",          new[]{150, 225, 195, 225, 165, 245, 225, 160, 300}),
        ("GAGNON, ALEXIS",          new[]{300, 170, 250,  85, 170, 300, 245, 285, 300}),
        ("GHOWIL, AMIR",            new[]{165, 300, 225, 210, 225, 165, 225, 235, 160}),
        ("GIRARD, FRANCIS",         new[]{225, 175, 225, 267, 225, 237, 225, 175, 150}),
        ("GIRAUD, SHEIRLEY",        new[]{235, 225, 246, 150, 301, 210, 231, 171, 225}),
        ("GRÉGOIRE, FRANCIS",       new[]{150, 300, 315, 165, 270, 115, 240, 360, 160}),
        ("GUEGUEN-SOULIER, N.",     new[]{165, 225, 225, 285, 225, 150, 240, 210, 225}),
        ("HIDJA, HAYETTE",          new[]{300, 176,  85, 251, 300, 150, 246,  75, 300}),
        ("HOCHEREAU, ALAIN",        new[]{225, 225, 160, 240, 225, 160, 265, 225, 150}),
        ("HOUARI, YAZID",           new[]{225, 225, 235, 225, 150, 245, 300, 245, 165}),
        ("HOWA, SONIA",             new[]{225, 250, 195, 235, 150, 225, 250, 225, 170}),
        ("JOLIN, JÉRÔME",           new[]{270, 237, 240, 267, 240, 225, 235, 150, 160}),
        ("JONCAS, CINDY",           new[]{235, 240, 150, 225, 240, 250, 225, 240, 225}),
        ("JOURNÉ, LUC",             new[]{150, 185, 245, 300, 235, 235, 185, 305, 150}),
        ("KARANGWA, MIREILLE",      new[]{240, 235, 300, 225, 105, 225, 115, 240, 235}),
        ("KHELIFI, MOHSEN",         new[]{231, 231, 195, 265, 186, 231, 150, 250, 240}),
        ("LACOMBE, DAVID",          new[]{105, 280, 225, 245, 225, 165, 300, 285, 200}),
        ("LALONGE, CHANTAL",        new[]{195, 235, 242, 250,  75, 225, 225, 300, 257}),
        ("LANTHIER, SARAH",         new[]{240, 150, 240, 150, 250, 255, 160, 235, 225}),
        ("LAOUINA, SAID",           new[]{165, 225, 243, 255, 150, 225, 225, 248, 168}),
        ("LARIVIÈRE TURCOTTE, K.",  new[]{242, 240, 150, 250, 177, 300, 225,  75, 225}),
        ("LAURENDEAU, ETHEL",       new[]{150, 235, 165, 300, 225, 300, 235, 150, 240}),
        ("LAVOIE, CLAUDIA",         new[]{240, 180, 247, 250, 225, 237, 185, 225, 180}),
        ("LECONTE, ELVIS",          new[]{  8,  21,   8,  19,  19,   8,  31,   6,   6}),
        ("MABSOUT, MINA",           new[]{175, 235, 225, 225, 182, 225, 225, 167, 375}),
        ("MANÉ, SOULEYMANE",        new[]{240, 207, 150, 150, 312, 235, 265, 259, 180}),
        ("MERCIER, VÉRONIQUE",      new[]{260, 210, 285, 237, 160, 250, 172, 300, 150}),
        ("MICHAUD-GUILBAULT, É.",   new[]{240, 300, 170, 150, 150, 300, 250, 150, 180}),
        ("MJIDOU, MOHAMED OMAR",    new[]{225, 240, 175, 225, 235, 225, 235, 255, 240}),
        ("MONTALVO, JHEINSEN",      new[]{ 95, 225, 175, 240, 240, 160, 235, 150, 150}),
        ("MORIN, KEVIN",            new[]{225, 225, 195, 235, 235, 225, 150, 240, 240}),
        ("MORRIS, BRIGITTE",        new[]{225, 225, 225, 300, 150, 225, 225, 150, 225}),
        ("PAQUETTE, ANNE",          new[]{310, 175, 235, 225, 150, 225, 250, 300, 150}),
        ("PELLAND, MARYLÈNE",       new[]{150, 190, 235, 235, 225, 300, 190, 300,  75}),
        ("PILOTE, ANDREA",          new[]{210, 262, 210, 237, 165, 225, 225, 235, 225}),
        ("POITRAS-QUINIOU, C.",     new[]{ 15,   0,  15,  15,   0,  15,   0,   0,  15}),
        ("PROPHETE, JOCELYN",       new[]{225, 100, 225, 225, 300, 175, 115, 255, 300}),
        ("PROVOST, CHRISTIAN",      new[]{300,  75, 160, 250, 150, 225,  75, 150, 165}),
        ("RACHEDI, KARIMA",         new[]{225, 188, 253, 150, 238, 177, 225, 240, 252}),
        ("ROBERGE, KARINE",         new[]{240,  75, 225, 225, 300, 235, 160, 300, 240}),
        ("ROBIDAS, PHILIPPE",       new[]{210, 240, 225, 150, 240, 225, 250, 325, 270}),
        ("ROUSSELLE, NICHOLAS",     new[]{225, 240, 225, 150, 255, 160, 260, 185, 225}),
        ("RUBIO, ANGELINA",         new[]{302, 235, 175, 240, 225, 257, 240, 120, 300}),
        ("RUIZ, MAURICIO",          new[]{225, 255, 225, 150, 275, 225, 150, 240, 160}),
        ("SAHLI, ADEL KHEMAIS",     new[]{275, 240, 150, 190, 225, 225, 240, 165, 225}),
        ("SANSAL, AMEL",            new[]{240, 150, 255, 225, 225, 250,  75, 245, 225}),
        ("SAVARD-GOURDE, A-A.",     new[]{237, 240, 225, 177, 235, 202, 237, 150, 225}),
        ("SAÏDI, NABIL",            new[]{ 75, 310, 240, 235, 160, 205, 265, 225, 235}),
        ("ST-AMANT-PROULX, A.",     new[]{300, 300,  90, 150, 285, 300, 300, 185, 150}),
        ("TAALOUCHT, SOUAD",        new[]{250, 225, 225, 195, 235, 164, 254, 270, 150}),
        ("TORRES-BASCOUR, N.",      new[]{245,  75, 235, 225, 240, 160, 225, 225, 260}),
        ("TOSKA, GLITIANA",         new[]{210, 251, 150, 285, 246, 241, 225, 150, 231}),
        ("TREMBLAY, N-J.",          new[]{190, 285, 210, 295, 225, 180, 225, 150, 315}),
        ("XHERAJ, ZHANKLINA",       new[]{ 75,   0, 116,   0,   0,   0,  75,   0,  75}),
    };

    public static async Task SeedQuotasParJourAsync(DataContext db)
    {
        var profs   = await db.Profs.ListAsync();
        var profDict = profs.ToDictionary(
            p => p.Nom.ToLowerInvariant(),
            p => p.Id,
            StringComparer.OrdinalIgnoreCase);

        foreach (var (htmlName, jours) in QuotasParJourData)
        {
            var key = ToTitleCase(htmlName).ToLowerInvariant();
            if (!profDict.TryGetValue(key, out var profId)) continue;

            for (int j = 1; j <= 9; j++)
            {
                await db.Quotas.UpsertAsync(new QuotaMinutes
                {
                    ProfId        = profId,
                    JourCycle     = j,
                    MinutesMax    = jours[j - 1],
                    AnneeScolaire = ANNEE,
                });
            }
        }
    }

    private static async Task SeedQuotasAsync(DataContext db, Dictionary<string, int> profIds)
    {
        // (HTML all-caps name, total minutes sur 9 jours) → avg/jour = total÷9
        var quotas = new (string HtmlName, int Total)[]
        {
            ("ADAM, NADIA",             119),
            ("ATAMNIA, BACHIR",        2130),
            ("AYARI, RAFIKA",          2070),
            ("BASSOWOU, KODJO",        1930),
            ("BEAUSOLEIL-HUNEAULT, S.",2104),
            ("BELAL, CHERIFA",         1880),
            ("BENSIALI, LINDA",        1919),
            ("BENZENATI, FARID",       2054),
            ("BENZINE, LAMIA",         1962),
            ("BISAILLON, ELISABETH",    975),
            ("BONHOMME, CATHERINE",    2020),
            ("BOUAICHE, FADILA",       2010),
            ("BOUCHER, MARGUERITE",    1955),
            ("BOUMAZA, WALID",         1889),
            ("BROCHU, JENNIFER",       1979),
            ("CHANTAL, SYLVIANNE",     2064),
            ("CHARLES, KISCHNER",      1876),
            ("CHAUVIN, M-O.",          2024),
            ("CIOLPAN, GRATIELA",      1981),
            ("CORDEAU, PASCALE",       2550),
            ("COUTURE, PHILIPPE",      1015),
            ("DESTROISMAISONS, N.",    1935),
            ("DIEDHIOU, NFALLY",       1898),
            ("DIONNE, PIERRE-LUC",     1974),
            ("DJEDDI, REZIKA",         2130),
            ("DUCHESNE, GENEVIÈVE",    2130),
            ("DUMAY, MARTINE",           40),
            ("FERAZ, FATIMA",          1874),
            ("FLEIFEL, VINCENT",       2114),
            ("FOURNIER, M-F.",         1890),
            ("GAGNON, ALEXIS",         2105),
            ("GHOWIL, AMIR",           1910),
            ("GIRARD, FRANCIS",        1904),
            ("GIRAUD, SHEIRLEY",       1994),
            ("GRÉGOIRE, FRANCIS",      2075),
            ("GUEGUEN-SOULIER, N.",    1950),
            ("HIDJA, HAYETTE",         1883),
            ("HOCHEREAU, ALAIN",       1875),
            ("HOUARI, YAZID",          2015),
            ("HOWA, SONIA",            1925),
            ("JOLIN, JÉRÔME",          2024),
            ("JONCAS, CINDY",          2030),
            ("JOURNÉ, LUC",            1990),
            ("KARANGWA, MIREILLE",     1920),
            ("KHELIFI, MOHSEN",        1979),
            ("LACOMBE, DAVID",         2030),
            ("LALONGE, CHANTAL",       2004),
            ("LANTHIER, SARAH",        1905),
            ("LAOUINA, SAID",          1904),
            ("LARIVIÈRE TURCOTTE, K.", 1884),
            ("LAURENDEAU, ETHEL",      2000),
            ("LAVOIE, CLAUDIA",        1969),
            ("LECONTE, ELVIS",          126),
            ("MABSOUT, MINA",          2034),
            ("MANÉ, SOULEYMANE",       1998),
            ("MERCIER, VÉRONIQUE",     2024),
            ("MICHAUD-GUILBAULT, É.",  1890),
            ("MJIDOU, MOHAMED OMAR",   2055),
            ("MONTALVO, JHEINSEN",     1670),
            ("MORIN, KEVIN",           1970),
            ("MORRIS, BRIGITTE",       1950),
            ("PAQUETTE, ANNE",         2020),
            ("PELLAND, MARYLÈNE",      1900),
            ("PILOTE, ANDREA",         1994),
            ("POITRAS-QUINIOU, C.",      75),
            ("PROPHETE, JOCELYN",      1920),
            ("PROVOST, CHRISTIAN",     1550),
            ("RACHEDI, KARIMA",        1948),
            ("ROBERGE, KARINE",        2000),
            ("ROBIDAS, PHILIPPE",      2135),
            ("ROUSSELLE, NICHOLAS",    1925),
            ("RUBIO, ANGELINA",        2094),
            ("RUIZ, MAURICIO",         1905),
            ("SAHLI, ADEL KHEMAIS",    1935),
            ("SANSAL, AMEL",           1890),
            ("SAVARD-GOURDE, A-A.",    1928),
            ("SAÏDI, NABIL",           1950),
            ("ST-AMANT-PROULX, A.",    2060),
            ("TAALOUCHT, SOUAD",       1968),
            ("TORRES-BASCOUR, N.",     1890),
            ("TOSKA, GLITIANA",        1989),
            ("TREMBLAY, N-J.",         2075),
            ("XHERAJ, ZHANKLINA",       341),
        };

        foreach (var (htmlName, total) in quotas)
        {
            var key = ToTitleCase(htmlName).ToLowerInvariant();
            if (profIds.TryGetValue(key, out var profId))
                await db.Quotas.UpsertAsync(new QuotaMinutes
                {
                    ProfId        = profId,
                    JourCycle     = 0,
                    MinutesMax    = (int)Math.Round(total / 9.0),
                    AnneeScolaire = ANNEE,
                });
        }
    }

    // ─── Classes & Épreuves ─────────────────────────────────────────────────

    private static async Task<Dictionary<string, int>> SeedClassesAsync(DataContext db)
    {
        var classes = new string[]
        {
            "Français Sec 1", "Français Sec 2", "Français Sec 3",
            "Français Sec 4", "Français Sec 5",
            "Mathématique Sec 2 C2", "Mathématique Sec 3 C2",
        };

        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var nom in classes)
        {
            var id = await db.Classes.CreateAsync(new Classe { Nom = nom, Effectif = 0, Annee = ANNEE });
            dict[nom] = id;
        }
        return dict;
    }

    private static async Task<Dictionary<string, int>> SeedEpreuvesAsync(
        DataContext db, Dictionary<string, int> classeIds)
    {
        var epreuves = new (string Code, string Classe, int Duree, bool Ministerielle, string Nom)[]
        {
            ("132108", "Français Sec 1",        120, false, "Français langue d'enseignement Sec 1"),
            ("132208", "Français Sec 2",        120, false, "Français langue d'enseignement Sec 2"),
            ("132308", "Français Sec 3",        150, false, "Français langue d'enseignement Sec 3"),
            ("132406", "Français Sec 4",        150, true,  "Français langue d'enseignement Sec 4"),
            ("132506", "Français Sec 5",        195, true,  "Français langue d'enseignement Sec 5"),
            ("063226", "Mathématique Sec 2 C2", 120, true,  "Mathématique C2 Sec 2"),
            ("063306", "Mathématique Sec 3 C2", 120, true,  "Mathématique C2 Sec 3"),
        };

        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (code, classeNom, duree, min, nom) in epreuves)
        {
            if (!classeIds.TryGetValue(classeNom, out var classeId)) continue;
            var id = await db.Epreuves.CreateAsync(new Epreuve
            {
                Nom          = nom,
                ClasseId     = classeId,
                DureeMinutes = duree,
                Ministerielle= min,
                Annee        = ANNEE,
            });
            dict[code] = id;
        }
        return dict;
    }

    // ─── Sessions & GroupesExamen ────────────────────────────────────────────

    private record G(string Code, string Ens, int Nb, string Surv, string Room,
                     int Dur, bool Tiers, string Type = "Standard");

    private static async Task SeedSessionsAsync(
        DataContext db,
        Dictionary<string, int> salleIds,
        Dictionary<string, int> profIds,
        Dictionary<string, int> epreuveIds)
    {
        await InsertSession(db, "2026-01-22", "AM", "08:30", salleIds, profIds, epreuveIds,
            Array.Empty<G>());

        await InsertSession(db, "2026-01-22", "PM", "12:40", salleIds, profIds, epreuveIds, new G[]
        {
            // Mathématique C2 Sec 2 (063226)
            new("063226-01","Rubio, Angelina",     27,"Ayari, Rafika",       "121",  160,true,"SAI"),
            new("063226-02","Rubio, Angelina",     24,"Joncas, Cindy",       "122",  120,false),
            new("063226-03","Rubio, Angelina",     26,"Savard-Gourde, A-A.", "123",  120,false),
            new("063226-04","Rubio, Angelina",     25,"Grégoire, Francis",   "124",  120,false),
            new("063226-05","Mercier, Véronique",  28,"Mané, Souleymane",    "125",  120,false),
            new("063226-06","Mercier, Véronique",  25,"Houari, Yazid",       "126",  120,false),
            new("063226-41","Mabsout, Mina",       26,"Journé, Luc",         "127",  120,false),
            new("063226-42","Mabsout, Mina",       30,"Lacombe, David",      "128",  120,false),
            new("063226-62","Benzine, Lamia",       9,"Benzine, Lamia",      "127b", 120,false),
            // Mathématique C2 Sec 3 (063306)
            new("063306-01","Mjidou, Mohamed Omar",11,"Bassowou, Kodjo",     "324",  160,true,"SAI"),
            new("063306-02","Mjidou, Mohamed Omar",29,"Almeida-Farias, N.",  "325",  120,false),
            new("063306-03","Hidja, Hayette",      21,"Gagnon, Alexis",      "327",  120,false),
            new("063306-04","Ghowil, Amir",        31,"Hidja, Hayette",      "328",  120,false),
            new("063306-05","Hidja, Hayette",      28,"Morin, Kevin",        "329",  120,false),
            new("063306-06","Hidja, Hayette",      14,"Prophete, Jocelyn",   "334",  120,false),
            new("063306-09","Hidja, Hayette",      30,"Tremblay, N-J.",      "336",  120,false),
            new("063306-41","Mercier, Véronique",  26,"Boucher, Gabriel",    "333",  120,false),
            new("063306-42","Mercier, Véronique",  32,"Howa, Sonia",         "330",  120,false),
            new("063306-43","Mabsout, Mina",       31,"Hochereau, Alain",    "332",  120,false),
        });

        await InsertSession(db, "2026-01-23", "AM", "08:30", salleIds, profIds, epreuveIds, new G[]
        {
            // Français Sec 1 (132108)
            new("132108-01","Beausoleil-Huneault, S.",25,"Hidja, Hayette",        "327",120,false),
            new("132108-02","Beausoleil-Huneault, S.",25,"Ayari, Rafika",         "328",120,false),
            new("132108-03","Boumaza, Walid",          26,"Diedhiou, Nfally",     "329",120,false),
            new("132108-04","Boumaza, Walid",          23,"Feraz, Fatima",        "330",120,false),
            new("132108-05","Rousselle, Nicholas",     27,"Bassowou, Kodjo",      "332",120,false),
            new("132108-41","Rousselle, Nicholas",     27,"Ghowil, Amir",         "333",120,false),
            new("132108-42","Rousselle, Nicholas",     25,"Grégoire, Francis",    "334",120,false),
            // Français Sec 2 (132208)
            new("132208-01","Pilote, Andrea",          27,"Provost, Christian",   "121",120,false),
            new("132208-02","Brochu, Jennifer",        24,"Gueguen-Soulier, N.",  "122",120,false),
            new("132208-03","Brochu, Jennifer",        26,"Djeddi, Rezika",       "123",120,false),
            new("132208-04","Brochu, Jennifer",        25,"Taaloucht, Souad",     "124",120,false),
            new("132208-05","Pilote, Andrea",          28,"Joncas, Cindy",        "125",120,false),
            new("132208-06","Pilote, Andrea",          25,"Girard, Francis",      "126",120,false),
            new("132208-41","Robidas, Philippe",       26,"Mané, Souleymane",     "127",120,false),
            new("132208-42","Robidas, Philippe",       30,"Morin, Kevin",         "128",120,false),
            new("132208-61","Benzine, Lamia",           9,"Benzine, Lamia",       "127b",120,false),
            // Français Sec 5 lecture (132506) – épreuve lecture du matin
            new("132506-01","Howa, Sonia",             28,"St-Amant-Proulx, A.", "321",260,true,"SAI"),
            new("132506-03","Howa, Sonia",             29,"Robidas, Philippe",   "323",195,false),
            new("132506-04","Gueguen-Soulier, N.",     21,"Destroismaisons, N.", "324",195,false),
            new("132506-05","Gueguen-Soulier, N.",     26,"Mjidou, Mohamed Omar","325",195,false),
            new("132506-06","Gueguen-Soulier, N.",     34,"Mabsout, Mina",       "326",195,false),
        });

        await InsertSession(db, "2026-01-23", "PM", "12:40", salleIds, profIds, epreuveIds, new G[]
        {
            // Français Sec 3 (132308)
            new("132308-01","Rousselle, Nicholas",25,"Vonesch, Ariane",      "321",150,false),
            new("132308-02","Girard, Francis",    30,"Almeida-Farias, N.",   "322",150,false),
            new("132308-03","Lanthier, Sarah",    28,"Journé, Luc",          "323",150,false),
            new("132308-04","Girard, Francis",    30,"Laurendeau, Ethel",    "324",150,false),
            new("132308-09","Lanthier, Sarah",    32,"Tremblay, N-J.",       "325",150,false),
            new("132308-41","Hochereau, Alain",   30,"Mabsout, Mina",        "326",150,false),
            new("132308-42","Hochereau, Alain",   31,"Savard-Gourde, A-A.",  "328",150,false),
            new("132308-43","Hochereau, Alain",   28,"Rubio, Angelina",      "329",150,false),
            // Français Sec 4 (132406)
            new("132406-01","Ciolpan, Gratiela",  32,"Roberge, Karine",      "122",150,false),
            new("132406-02","Ciolpan, Gratiela",  25,"Lanthier, Sarah",      "123",150,false),
            new("132406-03","Boumaza, Walid",     30,"Houari, Yazid",        "124",150,false),
            new("132406-04","Ciolpan, Gratiela",  25,"Sahli, Adel Khemais",  "125",150,false),
            new("132406-05","Bassowou, Kodjo",    23,"Prophete, Jocelyn",    "126",150,false),
            new("132406-06","Bassowou, Kodjo",    31,"Bouaiche, Fadila",     "127",150,false),
            new("132406-07","Bassowou, Kodjo",    30,"Djeddi, Rezika",       "128",150,false),
        });

        await InsertSession(db, "2026-05-07", "AM", "08:30", salleIds, profIds, epreuveIds, new G[]
        {
            // Français Sec 5 écriture (132506) — 7 mai
            new("132506-01","Howa, Sonia",          28,"Laurendeau, Ethel",    "335",215,true,"SAI"),
            new("132506-01","Howa, Sonia",          28,"Djeddi, Rezika",       "335", 60,false),
            new("132506-02","Howa, Sonia",          29,"Chauvin, M-O.",        "336", 90,false),
            new("132506-02","Howa, Sonia",          29,"Ghowil, Amir",         "336",120,false),
            new("132506-03","Howa, Sonia",          29,"Diedhiou, Nfally",     "333",210,false),
            new("132506-04","Gueguen-Soulier, N.",  21,"Roberge, Karine",      "334",210,false),
            new("132506-05","Gueguen-Soulier, N.",  26,"Feraz, Fatima",        "332",210,false),
            new("132506-06","Gueguen-Soulier, N.",  34,"Gueguen-Soulier, N.",  "329",275,true,"SAI"),
        });
    }

    private static async Task InsertSession(
        DataContext db,
        string date, string periode, string heureDebut,
        Dictionary<string, int> salleIds,
        Dictionary<string, int> profIds,
        Dictionary<string, int> epreuveIds,
        G[] groupes)
    {
        var sessionId = await db.Sessions.CreateAsync(new Session
        {
            Date          = date,
            Periode       = periode,
            HeureDebut    = heureDebut,
            AnneeScolaire = ANNEE,
        });

        foreach (var g in groupes)
        {
            var ensId  = LookupProf(profIds, g.Ens);
            var survId = LookupProf(profIds, g.Surv);
            var salleId= LookupSalle(salleIds, g.Room);
            var epId   = LookupEpreuve(epreuveIds, g.Code);

            await db.GroupesExamen.CreateAsync(new GroupeExamen
            {
                SessionId    = sessionId,
                EpreuveId    = epId,
                CodeGroupe   = g.Code,
                EnseignantId = ensId,
                NbEleves     = g.Nb,
                SurveillantId= survId > 0 ? survId : null,
                SalleId      = salleId > 0 ? salleId : null,
                TiersTemps   = g.Tiers,
                DureeMinutes = g.Dur,
                Type         = g.Type,
            });
        }
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private static int LookupProf(Dictionary<string, int> dict, string nom)
    {
        if (string.IsNullOrWhiteSpace(nom)) return 0;
        var key = nom.Trim().ToLowerInvariant();
        return dict.TryGetValue(key, out var id) ? id : 0;
    }

    private static int LookupSalle(Dictionary<string, int> dict, string room)
    {
        if (string.IsNullOrWhiteSpace(room)) return 0;
        return dict.TryGetValue(room.Trim(), out var id) ? id : 0;
    }

    private static int LookupEpreuve(Dictionary<string, int> dict, string code)
    {
        if (code == null || code.Length < 6) return dict.Values.FirstOrDefault();
        var prefix = code[..6];
        return dict.TryGetValue(prefix, out var id) ? id : dict.Values.FirstOrDefault();
    }

    private static string ToTitleCase(string s)
    {
        var sb = new StringBuilder(s.Length);
        bool cap = true;
        foreach (char c in s.ToLower())
        {
            sb.Append(cap && char.IsLetter(c) ? char.ToUpper(c) : c);
            cap = c is ' ' or '-';
        }
        return sb.ToString();
    }
}
