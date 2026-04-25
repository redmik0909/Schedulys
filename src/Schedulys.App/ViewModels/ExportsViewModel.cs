using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Schedulys.Data;

namespace Schedulys.App.ViewModels;

public sealed partial class ExportsViewModel : ViewModelBase
{
    private readonly DataContext _db;

    [ObservableProperty] private DateTime _dateDebut = new DateTime(DateTime.Today.Year, 1, 1);
    [ObservableProperty] private DateTime _dateFin   = new DateTime(DateTime.Today.Year, 12, 31);
    [ObservableProperty] private string   _message   = "";
    [ObservableProperty] private bool     _succes;

    public ExportsViewModel(DataContext db) => _db = db;

    [RelayCommand]
    private async Task ExporterCsvAsync()
    {
        Message = "";
        Succes  = false;

        var dlg = new SaveFileDialog
        {
            Title      = "Enregistrer le fichier CSV",
            Filter     = "Fichier CSV (*.csv)|*.csv",
            FileName   = $"Schedulys_examens_{DateDebut:yyyy-MM-dd}_{DateFin:yyyy-MM-dd}.csv",
            DefaultExt = ".csv"
        };

        if (dlg.ShowDialog() != true) return;

        try
        {
            var debut    = DateOnly.FromDateTime(DateDebut);
            var fin      = DateOnly.FromDateTime(DateFin);
            var sessions = await _db.Sessions.ListByPeriodeAsync(debut, fin);
            var profs    = await _db.Profs.ListAsync();
            var salles   = await _db.Salles.ListAsync();
            var epreuves = await _db.Epreuves.ListAsync();
            var classes  = await _db.Classes.ListAsync();
            var culture  = new CultureInfo("fr-CA");

            var sb = new StringBuilder();
            sb.Append('﻿'); // BOM UTF-8 pour Excel

            // En-tête
            sb.AppendLine("Date;Jour;Période;Heure début;Épreuve;Code groupe;Enseignant;Surveillant;Salle;# élèves;Durée (min);Tiers-temps;Type");

            int totalGroupes = 0;
            int totalRoles   = 0;

            foreach (var s in sessions.OrderBy(x => x.Date).ThenBy(x => x.Periode))
            {
                var date    = DateOnly.ParseExact(s.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                var jour    = date.ToString("dddd", culture);
                jour        = char.ToUpper(jour[0]) + jour[1..];
                var periode = s.Periode == "AM" ? "Matin" : "Après-midi";

                var groupes = await _db.GroupesExamen.ListBySessionAsync(s.Id);
                foreach (var g in groupes)
                {
                    var ep  = epreuves.FirstOrDefault(e => e.Id == g.EpreuveId);
                    var cl  = ep is not null ? classes.FirstOrDefault(c => c.Id == ep.ClasseId)?.Nom ?? "—" : "—";
                    var ens = profs.FirstOrDefault(p => p.Id == g.EnseignantId)?.Nom ?? "—";
                    var sur = g.SurveillantId.HasValue ? profs.FirstOrDefault(p => p.Id == g.SurveillantId)?.Nom ?? "—" : "—";
                    var sal = g.SalleId.HasValue ? salles.FirstOrDefault(x => x.Id == g.SalleId)?.Nom ?? "—" : "—";
                    var tt  = g.TiersTemps ? "Oui" : "Non";
                    var epNom = ep is not null ? $"{ep.Nom} ({cl})" : "—";

                    sb.AppendLine(string.Join(";",
                        s.Date, jour, periode, s.HeureDebut,
                        Esc(epNom), Esc(g.CodeGroupe), Esc(ens), Esc(sur), Esc(sal),
                        g.NbEleves, g.DureeMinutes, tt, g.Type));
                    totalGroupes++;
                }

                // Rôles de surveillance
                var roles = await _db.RolesSurveillance.ListBySessionAsync(s.Id);
                foreach (var r in roles)
                {
                    var sur = profs.FirstOrDefault(p => p.Id == r.SurveillantId)?.Nom ?? "—";
                    var loc = r.Local ?? "—";
                    sb.AppendLine(string.Join(";",
                        s.Date, jour, periode, s.HeureDebut,
                        Esc(r.TypeRole), "—", "—", Esc(sur), Esc(loc),
                        "—", r.DureeMinutes, "—", "Surveillance"));
                    totalRoles++;
                }
            }

            await File.WriteAllTextAsync(dlg.FileName, sb.ToString(), new UTF8Encoding(true));

            Succes  = true;
            Message = $"✓ {totalGroupes} groupe(s) + {totalRoles} rôle(s) → {Path.GetFileName(dlg.FileName)}";
        }
        catch (Exception ex)
        {
            Message = $"Erreur : {ex.Message}";
        }
    }

    private static string Esc(string s) =>
        s.Contains(';') || s.Contains('"') || s.Contains('\n')
            ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
}
