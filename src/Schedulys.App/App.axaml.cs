using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Schedulys.Data;
using Schedulys.Data.Db;
using Schedulys.App.ViewModels;
using Schedulys.App.Views;

namespace Schedulys.App;

public partial class App : Application
{
    public static DataContext  Db      { get; private set; } = null!;
    public static LicenseInfo? License { get; set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.Message, "Erreur inattendue",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        // ── Vérification licence ──────────────────────────────────────────
        License = LicenseService.LoadLocal();

        if (License is null || License.Status == LicenseStatus.Expired)
        {
            var activation = new ActivationWindow();
            if (activation.ShowDialog() != true)
            {
                Shutdown();
                return;
            }
            License = activation.Result!;
        }
        else
        {
            // Refresh silencieux en tâche de fond (1x/semaine)
            _ = Task.Run(() => LicenseService.RefreshAsync(License.LicenseKey));
        }

        // ── Démarrage normal ─────────────────────────────────────────────
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Schedulys");
        var dbPath = Path.Combine(appData, "data.db");

        var factory = new SqliteConnectionFactory(dbPath);
        await SchemaInitializer.InitAsync(factory);
        Db = new DataContext(dbPath);

        var window = new MainWindow
        {
            DataContext = new MainShellViewModel(Db)
        };
        window.Show();

        _ = UpdateChecker.CheckAndPromptAsync();
    }
}
