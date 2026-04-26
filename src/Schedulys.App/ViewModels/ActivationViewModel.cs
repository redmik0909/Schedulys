using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Schedulys.App.ViewModels;

public sealed partial class ActivationViewModel : ObservableObject
{
    public event Action<LicenseInfo>? ActivationSucceeded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ActivateCommand))]
    private string _licenseKey = "";

    [ObservableProperty] private string  _erreur       = "";
    [ObservableProperty] private string  _status       = "";
    [ObservableProperty] private bool    _enCours      = false;

    private bool PeutActiver() => !EnCours && LicenseKey.Length >= 10;

    [RelayCommand(CanExecute = nameof(PeutActiver))]
    private async Task ActivateAsync()
    {
        Erreur  = "";
        Status  = "Vérification en cours...";
        EnCours = true;
        ActivateCommand.NotifyCanExecuteChanged();
        try
        {
            var info = await LicenseService.ActivateAsync(LicenseKey);
            Status = $"Licence activée pour {info.SchoolName} (expire le {info.ExpiresAt:d})";
            ActivationSucceeded?.Invoke(info);
        }
        catch (LicenseException ex)
        {
            Erreur = ex.Message;
            Status = "";
        }
        catch (Exception ex)
        {
            Erreur = $"Erreur inattendue : {ex.Message}";
            Status = "";
        }
        finally
        {
            EnCours = false;
            ActivateCommand.NotifyCanExecuteChanged();
        }
    }
}
