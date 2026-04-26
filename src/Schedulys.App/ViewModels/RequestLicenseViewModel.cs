using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Schedulys.App.ViewModels;

public sealed partial class RequestLicenseViewModel : ObservableObject
{
    public event Action? RequestSucceeded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _schoolName = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _contactName = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    private string _email = "";

    [ObservableProperty] private string _erreur  = "";
    [ObservableProperty] private string _status  = "";
    [ObservableProperty] private bool   _enCours = false;
    [ObservableProperty] private bool   _done    = false;

    public string MachineId => LicenseService.GetMachineId();

    private bool PeutEnvoyer() => !EnCours && !Done
        && SchoolName.Trim().Length > 0
        && ContactName.Trim().Length > 0
        && Email.Contains('@');

    [RelayCommand(CanExecute = nameof(PeutEnvoyer))]
    private async Task SendAsync()
    {
        Erreur  = "";
        Status  = "Envoi en cours...";
        EnCours = true;
        SendCommand.NotifyCanExecuteChanged();
        try
        {
            await LicenseService.RequestLicenseAsync(
                SchoolName.Trim(), ContactName.Trim(), Email.Trim());
            Status = "";
            Done   = true;
            RequestSucceeded?.Invoke();
        }
        catch (Exception ex)
        {
            Erreur = $"Impossible d'envoyer la demande : {ex.Message}";
            Status = "";
        }
        finally
        {
            EnCours = false;
            SendCommand.NotifyCanExecuteChanged();
        }
    }
}
