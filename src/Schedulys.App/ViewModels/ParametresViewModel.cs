using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Schedulys.Core.Models;
using Schedulys.Data;

namespace Schedulys.App.ViewModels;

public sealed partial class ParametresViewModel : ViewModelBase
{
    private readonly DataContext _db;

    public ObservableCollection<ZoneSurveillance> Zones { get; } = new();

    [ObservableProperty] private string             _nomZoneInput        = "";
    [ObservableProperty] private ZoneSurveillance?  _zoneSelectionnee;
    [ObservableProperty] private string             _erreur              = "";
    [ObservableProperty] private string             _message             = "";

    public ParametresViewModel(DataContext db)
    {
        _db = db;
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        var zones = await _db.Zones.ListAsync();
        Zones.Clear();
        foreach (var z in zones) Zones.Add(z);
    }

    [RelayCommand]
    private async Task AjouterZoneAsync()
    {
        Erreur  = "";
        Message = "";
        var nom = NomZoneInput.Trim();
        if (string.IsNullOrEmpty(nom)) { Erreur = "Le nom de la zone est requis."; return; }

        await _db.Zones.CreateAsync(new ZoneSurveillance { Nom = nom, Ordre = Zones.Count + 1 });
        NomZoneInput = "";
        Message = "✓ Zone ajoutée.";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task SupprimerZoneAsync(ZoneSurveillance? z)
    {
        if (z is null) return;
        Erreur  = "";
        Message = "";
        await _db.Zones.DeleteAsync(z.Id);
        Message = "✓ Zone supprimée.";
        await LoadAsync();
    }
}
