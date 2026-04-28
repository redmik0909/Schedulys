using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Schedulys.Core.Models;
using Schedulys.Data;

namespace Schedulys.App.ViewModels;

public sealed partial class ClasseDisplay : ObservableObject
{
    public Classe Classe     { get; }
    public string PrefixCode => Classe.Code.Length >= 6 ? Classe.Code[..6] : Classe.Code;
    public string GroupKey   => $"{PrefixCode} — {Classe.Description}";

    [ObservableProperty] private Prof?  _selectedProf;
    [ObservableProperty] private string _effectifInput    = "";
    [ObservableProperty] private string _codeInput        = "";
    [ObservableProperty] private string _descriptionInput = "";
    [ObservableProperty] private int    _niveauInput;

    public ClasseDisplay(Classe c, System.Collections.Generic.IReadOnlyDictionary<int, Prof> profMap)
    {
        Classe             = c;
        _selectedProf      = c.ProfId > 0 && profMap.TryGetValue(c.ProfId, out var p) ? p : null;
        _effectifInput     = c.Effectif > 0 ? c.Effectif.ToString() : "";
        _codeInput         = c.Code;
        _descriptionInput  = c.Description;
        _niveauInput       = c.Niveau;
    }

    partial void OnDescriptionInputChanged(string value)
    {
        if (value.Length == 1 && char.IsLower(value[0]))
            DescriptionInput = char.ToUpper(value[0]) + value[1..];
    }
}

public sealed class GroupeMatiere
{
    public string PrefixCode  { get; init; } = "";
    public string Description { get; init; } = "";
    public int    Niveau      { get; init; }
    public string Header
    {
        get
        {
            var niveauLabel = Niveau > 0 ? $" Sec. {Niveau}" : "";
            return string.IsNullOrWhiteSpace(Description)
                ? $"{PrefixCode}{niveauLabel}"
                : $"{PrefixCode} — {Description}{niveauLabel}";
        }
    }
    public ObservableCollection<ClasseDisplay> Items { get; } = new();
}

public sealed partial class GroupesViewModel : ViewModelBase
{
    private readonly DataContext _db;

    public ObservableCollection<GroupeMatiere> GroupedClasses { get; } = new();
    public ObservableCollection<Prof>          Profs          { get; } = new();

    public record NiveauItem(int Valeur, string Libelle);
    public static IReadOnlyList<NiveauItem> NiveauxScolaires { get; } = new[]
    {
        new NiveauItem(0, "—"),
        new NiveauItem(1, "Sec. 1"),
        new NiveauItem(2, "Sec. 2"),
        new NiveauItem(3, "Sec. 3"),
        new NiveauItem(4, "Sec. 4"),
        new NiveauItem(5, "Sec. 5"),
    };

    [ObservableProperty] private string    _codeMatiereInput    = "";
    [ObservableProperty] private string    _niveauInput         = "";
    [ObservableProperty] private string    _nbGroupesInput      = "";
    [ObservableProperty] private string    _descriptionInput    = "";
    [ObservableProperty] private NiveauItem _niveauScolaireInput = NiveauxScolaires[0];
    [ObservableProperty] private string    _erreur              = "";
    [ObservableProperty] private string    _message             = "";

    partial void OnDescriptionInputChanged(string value)
    {
        if (value.Length == 1 && char.IsLower(value[0]))
            DescriptionInput = char.ToUpper(value[0]) + value[1..];
    }

    public GroupesViewModel(DataContext db)
    {
        _db = db;
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        var profs   = await _db.Profs.ListAsync();
        var profMap = profs.ToDictionary(p => p.Id);

        Profs.Clear();
        foreach (var p in profs) Profs.Add(p);

        var list = await _db.Classes.ListAsync();

        GroupedClasses.Clear();
        var groups = list
            .OrderBy(c => c.Niveau == 0 ? 99 : c.Niveau)
            .ThenBy(c => c.Code)
            .GroupBy(c => c.Code.Length >= 6 ? c.Code[..6] : c.Code);

        foreach (var g in groups)
        {
            var first = g.First();
            var gm    = new GroupeMatiere
            {
                PrefixCode  = g.Key,
                Description = first.Description,
                Niveau      = first.Niveau
            };
            foreach (var c in g)
                gm.Items.Add(new ClasseDisplay(c, profMap));
            GroupedClasses.Add(gm);
        }
    }

    // ── Création en lot ──────────────────────────────────────────────────────

    [RelayCommand]
    private async Task CreateBatchAsync()
    {
        Erreur  = "";
        Message = "";

        var code   = CodeMatiereInput.Trim();
        var niveau = NiveauInput.Trim();
        var desc   = DescriptionInput.Trim();

        if (code.Length == 0 || niveau.Length == 0)
        {
            Erreur = "Code matière et niveau requis.";
            return;
        }
        if (!int.TryParse(NbGroupesInput, out var n) || n < 1)
        {
            Erreur = "Nombre de groupes invalide.";
            return;
        }

        var prefix = code + niveau;
        for (int i = 1; i <= n; i++)
        {
            await _db.Classes.CreateAsync(new Classe
            {
                Code        = $"{prefix}-{i:D2}",
                Description = desc,
                ProfId      = 0,
                Effectif    = 0,
                Niveau      = NiveauScolaireInput.Valeur,
                Nom         = $"{prefix}-{i:D2}",
                Annee       = AppConstants.AnneeScolaire
            });
        }

        Message              = $"✓ {n} groupe(s) créés ({prefix}-01 à {prefix}-{n:D2})";
        CodeMatiereInput     = "";
        NiveauInput          = "";
        NbGroupesInput       = "";
        DescriptionInput     = "";
        NiveauScolaireInput  = NiveauxScolaires[0];
        await LoadAsync();
    }

    // ── Sauvegarde ───────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SaveAllAsync()
    {
        Erreur  = "";
        Message = "";
        foreach (var gm in GroupedClasses)
            foreach (var cd in gm.Items)
            {
                int.TryParse(cd.EffectifInput, out var eff);
                cd.Classe.Code        = cd.CodeInput.Trim();
                cd.Classe.Description = cd.DescriptionInput.Trim();
                cd.Classe.Nom         = cd.CodeInput.Trim();
                cd.Classe.ProfId      = cd.SelectedProf?.Id ?? 0;
                cd.Classe.Effectif    = eff;
                cd.Classe.Niveau      = cd.NiveauInput;
                await _db.Classes.UpdateAsync(cd.Classe);
            }
        Message = "✓ Modifications enregistrées.";
        await LoadAsync();
    }

    // ── Suppressions ─────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task DeleteGroupeAsync(GroupeMatiere? gm)
    {
        if (gm is null) return;
        Erreur = "";
        try
        {
            AppLogger.Warn("DATA", $"Suppression groupe matière '{gm.Header}' ({gm.Items.Count} classe(s)).");
            foreach (var cd in gm.Items.ToList())
            {
                AppLogger.Warn("DATA", $"  Suppression classe Id={cd.Classe.Id} Code={cd.Classe.Code}");
                await _db.Classes.DeleteAsync(cd.Classe.Id);
            }
            AppLogger.Info("DATA", $"Groupe '{gm.Header}' supprimé.");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Error("DATA", $"Erreur suppression groupe '{gm.Header}'", ex);
            Erreur = $"Impossible de supprimer : {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteClasseAsync(ClasseDisplay? cd)
    {
        if (cd is null) return;
        Erreur = "";
        try
        {
            AppLogger.Warn("DATA", $"Suppression classe Id={cd.Classe.Id} Code={cd.Classe.Code}");
            await _db.Classes.DeleteAsync(cd.Classe.Id);
            AppLogger.Info("DATA", $"Classe Id={cd.Classe.Id} supprimée.");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Error("DATA", $"Erreur suppression classe Id={cd.Classe.Id}", ex);
            Erreur = $"Impossible de supprimer : {ex.Message}";
        }
    }
}
