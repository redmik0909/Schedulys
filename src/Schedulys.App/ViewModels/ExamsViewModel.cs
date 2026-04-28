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

public sealed partial class GroupeCheckDisplay : ObservableObject
{
    public Classe Classe { get; }
    public string Label  => Classe.Niveau > 0
        ? $"[Sec.{Classe.Niveau}] {Classe.Code} — {Classe.Description}"
        : Classe.Label;

    [ObservableProperty] private bool _isChecked;

    public GroupeCheckDisplay(Classe c, bool isChecked = false)
    {
        Classe     = c;
        _isChecked = isChecked;
    }
}

public sealed class NiveauEpreuveGroup
{
    public int    Niveau      { get; init; }
    public string NiveauLabel => Niveau > 0 ? $"Sec. {Niveau}" : "Niveau non défini";
    public ObservableCollection<EpreuveDisplay> Items { get; } = new();
}

public sealed partial class ExamsViewModel : ViewModelBase
{
    private readonly DataContext _db;

    public ObservableCollection<EpreuveDisplay>     Epreuves      { get; } = new();
    public ObservableCollection<NiveauEpreuveGroup> GroupedEpreuves { get; } = new();
    public ObservableCollection<GroupeCheckDisplay>  GroupesCheck  { get; } = new();

    public static IReadOnlyList<GroupesViewModel.NiveauItem> NiveauxScolaires
        => GroupesViewModel.NiveauxScolaires;

    [ObservableProperty] private string _nomInput     = "";
    [ObservableProperty] private string _dureeInput   = "120";
    [ObservableProperty] private bool   _tiersTemps;
    [ObservableProperty] private bool   _ministerielle;
    [ObservableProperty] private GroupesViewModel.NiveauItem _niveauEpreuve
        = GroupesViewModel.NiveauxScolaires[0];
    [ObservableProperty] private string _erreur       = "";
    [ObservableProperty] private string _message      = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(SaveGroupesCommand))]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyPropertyChangedFor(nameof(AddButtonLabel))]
    [NotifyPropertyChangedFor(nameof(FormTitle))]
    private EpreuveDisplay? _selected;

    public bool   HasSelection  => Selected is not null;
    public string AddButtonLabel => Selected is not null ? "Modifier" : "Ajouter";
    public string FormTitle      => Selected is not null ? "Modifier l'épreuve" : "Nouvelle épreuve";

    partial void OnSelectedChanged(EpreuveDisplay? value)
    {
        foreach (var e in Epreuves) e.IsSelected = (e == value);
        Erreur  = "";
        Message = "";
        _ = LoadGroupesCheckAsync(value);

        if (value is not null)
        {
            NomInput      = value.Nom;
            DureeInput    = value.Duree.ToString();
            TiersTemps    = value.Epreuve.TiersTemps;
            Ministerielle = value.Epreuve.Ministerielle;
            NiveauEpreuve = NiveauxScolaires.FirstOrDefault(n => n.Valeur == value.Epreuve.Niveau)
                            ?? NiveauxScolaires[0];
        }
        else
        {
            NomInput      = "";
            DureeInput    = "120";
            TiersTemps    = false;
            Ministerielle = false;
            NiveauEpreuve = NiveauxScolaires[0];
        }
    }

    public ExamsViewModel(DataContext db)
    {
        _db = db;
        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task RefreshAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        var epreuves = await _db.Epreuves.ListAsync(annee: AppConstants.AnneeScolaire);
        var savedId  = Selected?.Epreuve.Id;

        Epreuves.Clear();
        foreach (var e in epreuves)
            Epreuves.Add(new EpreuveDisplay(e));

        var displayMap = Epreuves.ToDictionary(d => d.Epreuve.Id);
        GroupedEpreuves.Clear();
        foreach (var g in epreuves.GroupBy(e => e.Niveau).OrderBy(g => g.Key == 0 ? 99 : g.Key))
        {
            var group = new NiveauEpreuveGroup { Niveau = g.Key };
            foreach (var e in g.OrderBy(e => e.Nom))
                group.Items.Add(displayMap[e.Id]);
            GroupedEpreuves.Add(group);
        }

        if (savedId.HasValue)
            Selected = Epreuves.FirstOrDefault(e => e.Epreuve.Id == savedId);
    }

    private async Task LoadGroupesCheckAsync(EpreuveDisplay? ep)
    {
        GroupesCheck.Clear();
        var classes = await _db.Classes.ListAsync();

        if (ep is null)
        {
            foreach (var c in classes.OrderBy(c => c.Niveau == 0 ? 99 : c.Niveau).ThenBy(c => c.Code))
                GroupesCheck.Add(new GroupeCheckDisplay(c, false));
            return;
        }

        var checkedIds = (await _db.Epreuves.GetGroupeIdsAsync(ep.Epreuve.Id)).ToHashSet();
        var filtered   = ep.Epreuve.Niveau > 0
            ? classes.Where(c => c.Niveau == ep.Epreuve.Niveau)
            : classes;
        foreach (var c in filtered.OrderBy(c => c.Code))
            GroupesCheck.Add(new GroupeCheckDisplay(c, checkedIds.Contains(c.Id)));
    }

    // ── Ajouter / Modifier épreuve ───────────────────────────────────────────

    [RelayCommand]
    private async Task AddAsync()
    {
        Erreur  = "";
        Message = "";
        if (string.IsNullOrWhiteSpace(NomInput)) return;

        int.TryParse(DureeInput, out var duree);
        if (duree <= 0) duree = 60;

        if (Selected is not null)
        {
            try
            {
                Selected.Epreuve.Nom           = NomInput.Trim();
                Selected.Epreuve.DureeMinutes  = duree;
                Selected.Epreuve.TiersTemps    = TiersTemps;
                Selected.Epreuve.Ministerielle = Ministerielle;
                Selected.Epreuve.Niveau        = NiveauEpreuve.Valeur;
                await _db.Epreuves.UpdateAsync(Selected.Epreuve);
                AppLogger.Info("DATA", $"Épreuve modifiée : Id={Selected.Epreuve.Id} Nom='{Selected.Epreuve.Nom}'");
                Message = "✓ Épreuve modifiée.";
                await LoadAsync();
            }
            catch (Exception ex)
            {
                AppLogger.Error("DATA", $"Erreur modification épreuve Id={Selected.Epreuve.Id}", ex);
                Erreur = $"Impossible de modifier : {ex.Message}";
            }
        }
        else
        {
            var newId = await _db.Epreuves.CreateAsync(new Epreuve
            {
                Nom           = NomInput.Trim(),
                ClasseId      = 0,
                DureeMinutes  = duree,
                TiersTemps    = TiersTemps,
                Ministerielle = Ministerielle,
                Niveau        = NiveauEpreuve.Valeur,
                Annee         = AppConstants.AnneeScolaire
            });
            AppLogger.Info("DATA", $"Épreuve créée : Id={newId} Nom='{NomInput.Trim()}' Durée={duree}min");
            Message       = "✓ Épreuve créée.";
            NomInput      = "";
            DureeInput    = "120";
            TiersTemps    = false;
            Ministerielle = false;
            await LoadAsync();
        }
    }

    // ── Sauvegarder les groupes cochés ───────────────────────────────────────

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task SaveGroupesAsync()
    {
        if (Selected is null) return;
        Erreur  = "";
        Message = "";
        var ids = GroupesCheck.Where(g => g.IsChecked).Select(g => g.Classe.Id);
        await _db.Epreuves.SetGroupesAsync(Selected.Epreuve.Id, ids);
        Message = "✓ Groupes enregistrés.";
    }

    // ── Tout cocher / décocher ───────────────────────────────────────────────

    [RelayCommand]
    private void ToutCocher()
    {
        foreach (var g in GroupesCheck) g.IsChecked = true;
    }

    [RelayCommand]
    private void ToutDecocher()
    {
        foreach (var g in GroupesCheck) g.IsChecked = false;
    }

    // ── Nouvelle épreuve (vide le formulaire) ────────────────────────────────

    [RelayCommand]
    private void NouvelleEpreuve() => Selected = null;

    // ── Sélectionner épreuve (depuis la liste) ───────────────────────────────

    [RelayCommand]
    private void Select(EpreuveDisplay? ep) => Selected = ep;

    // ── Supprimer épreuve ────────────────────────────────────────────────────

    [RelayCommand]
    private async Task DeleteSelectedAsync(EpreuveDisplay? ep = null)
    {
        var target = ep ?? Selected;
        if (target is null) return;
        Erreur = "";
        try
        {
            var id   = target.Epreuve.Id;
            var nom  = target.Epreuve.Nom;
            AppLogger.Warn("DATA", $"Suppression épreuve : Id={id} Nom='{nom}'");
            if (Selected?.Epreuve.Id == id) Selected = null;
            await _db.Epreuves.DeleteAsync(id);
            AppLogger.Info("DATA", $"Épreuve Id={id} supprimée.");
            await LoadAsync();
        }
        catch (Exception ex)
        {
            AppLogger.Error("DATA", $"Erreur suppression épreuve Id={target.Epreuve.Id}", ex);
            Erreur = $"Impossible de supprimer : {ex.Message}";
        }
    }
}

public sealed partial class EpreuveDisplay : ObservableObject
{
    public EpreuveDisplay(Epreuve epreuve) => Epreuve = epreuve;

    public Epreuve Epreuve { get; }
    public string  Nom     => Epreuve.Nom;
    public int     Duree   => Epreuve.DureeMinutes;
    public string  Options => BuildOptions();

    [ObservableProperty] private bool _isSelected;

    private string BuildOptions()
    {
        var parts = new System.Collections.Generic.List<string>();
        if (Epreuve.TiersTemps)    parts.Add("Tiers-temps");
        if (Epreuve.Ministerielle) parts.Add("Ministérielle");
        return parts.Count > 0 ? string.Join(", ", parts) : "—";
    }
}
