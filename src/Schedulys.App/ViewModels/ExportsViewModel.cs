using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;
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

    // ── Export planification Excel ─────────────────────────────────────────────

    [RelayCommand]
    private async Task ExporterPlanificationAsync()
    {
        Message = "";
        Succes  = false;

        var dlg = new SaveFileDialog
        {
            Title      = "Enregistrer la feuille de planification",
            Filter     = "Fichier Excel (*.xlsx)|*.xlsx",
            FileName   = $"Planification_{DateDebut:yyyy-MM-dd}_{DateFin:yyyy-MM-dd}.xlsx",
            DefaultExt = ".xlsx"
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

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Planification");

            ws.Column(1).Width = 36;  // Description
            ws.Column(2).Width = 17;  // Code
            ws.Column(3).Width = 23;  // Enseignant
            ws.Column(4).Width =  9;  // # Élèves
            ws.Column(5).Width =  9;  // Durée
            ws.Column(6).Width = 12;  // 1er départ
            ws.Column(7).Width = 23;  // Surveillant
            ws.Column(8).Width =  8;  // Local

            var cNavy   = XLColor.FromHtml("#1E3A5F");
            var cBlue   = XLColor.FromHtml("#2D6A9F");
            var cLightBg = XLColor.FromHtml("#E8F0F8");
            var cAlt    = XLColor.FromHtml("#F5F8FC");
            var cBorder = XLColor.FromHtml("#C8D4E0");

            string[] colHeaders = { "Description de l'épreuve", "Code groupe", "Enseignant-e",
                                    "# Élèves", "Durée (min)", "1er départ", "Surveillant", "Local" };

            int row = 1;
            int totalGroupes = 0;

            foreach (var s in sessions.OrderBy(x => x.Date).ThenBy(x => x.Periode))
            {
                var date    = DateOnly.ParseExact(s.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                var jourNom = date.ToString("dddd d MMMM yyyy", culture);
                jourNom     = char.ToUpper(jourNom[0]) + jourNom[1..];
                var periode = s.Periode == "AM" ? "Matin" : "Après-midi";
                var cycleLabel = s.JourCycle > 0 ? $"  ·  Jour de cycle {s.JourCycle}" : "";

                // ── Ligne titre de session ──────────────────────────────────
                var hRange = ws.Range(row, 1, row, 8);
                hRange.Merge();
                hRange.Value = $"{jourNom}  —  {periode}  —  Début : {s.HeureDebut}{cycleLabel}";
                hRange.Style.Font.Bold      = true;
                hRange.Style.Font.FontSize  = 11;
                hRange.Style.Font.FontColor = XLColor.White;
                hRange.Style.Fill.BackgroundColor = cNavy;
                hRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                hRange.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
                hRange.Style.Alignment.Indent     = 1;
                ws.Row(row).Height = 22;
                row++;

                // ── En-têtes colonnes ───────────────────────────────────────
                for (int c = 0; c < colHeaders.Length; c++)
                {
                    var cell = ws.Cell(row, c + 1);
                    cell.Value = colHeaders[c];
                    cell.Style.Font.Bold      = true;
                    cell.Style.Font.FontSize  = 9;
                    cell.Style.Font.FontColor = cNavy;
                    cell.Style.Fill.BackgroundColor = cLightBg;
                    cell.Style.Border.BottomBorder      = XLBorderStyleValues.Thin;
                    cell.Style.Border.BottomBorderColor = cBlue;
                    cell.Style.Alignment.Horizontal     = c >= 3
                        ? XLAlignmentHorizontalValues.Center
                        : XLAlignmentHorizontalValues.Left;
                    cell.Style.Alignment.Indent = c < 3 ? 1 : 0;
                }
                ws.Row(row).Height = 17;
                row++;

                // ── Lignes de données ───────────────────────────────────────
                var groupes = await _db.GroupesExamen.ListBySessionAsync(s.Id);

                var ordonnes = groupes
                    .OrderBy(g =>
                    {
                        if (g.ClasseId.HasValue)
                        {
                            var cl = classes.FirstOrDefault(c => c.Id == g.ClasseId);
                            return cl?.Description ?? cl?.Code ?? g.CodeGroupe;
                        }
                        return epreuves.FirstOrDefault(e => e.Id == g.EpreuveId)?.Nom ?? g.CodeGroupe;
                    })
                    .ThenBy(g => g.CodeGroupe)
                    .ToList();

                bool alt = false;
                foreach (var g in ordonnes)
                {
                    string desc;
                    if (g.ClasseId.HasValue)
                    {
                        var cl = classes.FirstOrDefault(c => c.Id == g.ClasseId);
                        desc = cl is not null
                            ? (string.IsNullOrWhiteSpace(cl.Description) ? cl.Code : cl.Description)
                            : g.CodeGroupe;
                    }
                    else
                    {
                        var ep = epreuves.FirstOrDefault(e => e.Id == g.EpreuveId);
                        var cl = ep is not null ? classes.FirstOrDefault(c => c.Id == ep.ClasseId) : null;
                        desc   = ep is not null
                            ? (cl is not null ? $"{ep.Nom} ({cl.Nom})" : ep.Nom)
                            : g.CodeGroupe;
                    }

                    var ens    = profs.FirstOrDefault(p => p.Id == g.EnseignantId)?.Nom ?? "—";
                    var surv   = g.SurveillantId.HasValue ? profs.FirstOrDefault(p => p.Id == g.SurveillantId)?.Nom ?? "—" : "—";
                    var salle  = g.SalleId.HasValue ? salles.FirstOrDefault(x => x.Id == g.SalleId)?.Nom ?? "—" : "—";
                    var depart = string.IsNullOrWhiteSpace(g.PremierDepart) ? "—" : g.PremierDepart;

                    var bg = alt ? cAlt : XLColor.White;

                    void SetCell(int col, string val, bool center = false)
                    {
                        var cell = ws.Cell(row, col);
                        cell.Value = val;
                        cell.Style.Fill.BackgroundColor    = bg;
                        cell.Style.Font.FontSize           = 9;
                        cell.Style.Border.BottomBorder     = XLBorderStyleValues.Hair;
                        cell.Style.Border.BottomBorderColor = cBorder;
                        cell.Style.Alignment.Horizontal    = center
                            ? XLAlignmentHorizontalValues.Center
                            : XLAlignmentHorizontalValues.Left;
                        cell.Style.Alignment.Indent = (!center) ? 1 : 0;
                    }

                    void SetCellInt(int col, int val)
                    {
                        var cell = ws.Cell(row, col);
                        cell.Value = val;
                        cell.Style.Fill.BackgroundColor    = bg;
                        cell.Style.Font.FontSize           = 9;
                        cell.Style.Border.BottomBorder     = XLBorderStyleValues.Hair;
                        cell.Style.Border.BottomBorderColor = cBorder;
                        cell.Style.Alignment.Horizontal    = XLAlignmentHorizontalValues.Center;
                    }

                    SetCell(1, desc);
                    SetCell(2, g.CodeGroupe, center: true);
                    SetCell(3, ens);
                    SetCellInt(4, g.NbEleves);
                    SetCellInt(5, g.DureeMinutes);
                    SetCell(6, depart, center: true);
                    SetCell(7, surv);
                    SetCell(8, salle, center: true);

                    ws.Row(row).Height = 15;
                    alt = !alt;
                    row++;
                    totalGroupes++;
                }

                row++; // ligne vide entre sessions
            }

            // Freeze la première ligne de chaque session ne marche pas bien
            // → on freeze simplement les 2 premières lignes si au moins une session
            ws.SheetView.FreezeRows(0);

            wb.SaveAs(dlg.FileName);

            Succes  = true;
            Message = $"✓ {totalGroupes} groupe(s) exporté(s) → {Path.GetFileName(dlg.FileName)}";
        }
        catch (Exception ex)
        {
            Message = $"Erreur : {ex.Message}";
        }
    }

    // ── Export CSV brut ────────────────────────────────────────────────────────

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

            sb.AppendLine("Date;Jour;Période;Heure début;Épreuve;Code groupe;Enseignant;Surveillant;Salle;# élèves;Durée (min);1er départ;Type");

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
                    string desc;
                    if (g.ClasseId.HasValue)
                    {
                        var cl = classes.FirstOrDefault(c => c.Id == g.ClasseId);
                        desc = cl is not null
                            ? (string.IsNullOrWhiteSpace(cl.Description) ? cl.Code : cl.Description)
                            : g.CodeGroupe;
                    }
                    else
                    {
                        var ep = epreuves.FirstOrDefault(e => e.Id == g.EpreuveId);
                        var cl = ep is not null ? classes.FirstOrDefault(c => c.Id == ep.ClasseId) : null;
                        desc   = ep is not null
                            ? (cl is not null ? $"{ep.Nom} ({cl.Nom})" : ep.Nom)
                            : g.CodeGroupe;
                    }

                    var ens    = profs.FirstOrDefault(p => p.Id == g.EnseignantId)?.Nom ?? "—";
                    var surv   = g.SurveillantId.HasValue ? profs.FirstOrDefault(p => p.Id == g.SurveillantId)?.Nom ?? "—" : "—";
                    var sal    = g.SalleId.HasValue ? salles.FirstOrDefault(x => x.Id == g.SalleId)?.Nom ?? "—" : "—";
                    var depart = string.IsNullOrWhiteSpace(g.PremierDepart) ? "—" : g.PremierDepart;

                    sb.AppendLine(string.Join(";",
                        s.Date, jour, periode, s.HeureDebut,
                        Esc(desc), Esc(g.CodeGroupe), Esc(ens), Esc(surv), Esc(sal),
                        g.NbEleves, g.DureeMinutes, depart, g.Type));
                    totalGroupes++;
                }

                var roles = await _db.RolesSurveillance.ListBySessionAsync(s.Id);
                foreach (var r in roles)
                {
                    var surv  = profs.FirstOrDefault(p => p.Id == r.SurveillantId)?.Nom ?? "—";
                    var plage = (!string.IsNullOrWhiteSpace(r.HeureDebut) && !string.IsNullOrWhiteSpace(r.HeureFin))
                        ? $"{r.HeureDebut}-{r.HeureFin}"
                        : "—";
                    sb.AppendLine(string.Join(";",
                        s.Date, jour, periode, s.HeureDebut,
                        Esc(r.TypeRole), "—", "—", Esc(surv), Esc(plage),
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
