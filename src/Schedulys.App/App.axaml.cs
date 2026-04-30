using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Data.Sqlite;
using Sentry;
using Schedulys.Data;
using Schedulys.Data.Db;
using Schedulys.App.ViewModels;
using Schedulys.App.Views;

namespace Schedulys.App;

public partial class App : Application
{
    // ── Remplace par ton DSN Sentry (sentry.io → projet → Settings → DSN) ──
    private static readonly string SentryDsn = "https://2c92a958212954ed66ad795a4e04a88d@o4511296069566464.ingest.us.sentry.io/4511296091586560";

    public static DataContext   Db      { get; private set; } = null!;
    public static LicenseInfo?  License { get; set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Empêche WPF de fermer l'app quand l'ActivationWindow se ferme avant la MainWindow
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Initialise le logger fichier le plus tôt possible — tous les flux suivants
        // (gestionnaires d'exceptions, licence, Sentry) peuvent ainsi logger.
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Schedulys");
        AppLogger.Init(appData);

        DispatcherUnhandledException += (_, args) =>
        {
            AppLogger.Error("DISPATCHER", "Exception non gérée", args.Exception);
            MessageBox.Show(
                $"{args.Exception.GetType().Name}: {args.Exception.Message}\n\nLog: {AppLogger.LogPath}",
                "Erreur inattendue", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                AppLogger.Error("APPDOMAIN", "Exception non gérée", ex);
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            AppLogger.Error("TASK", "Tâche async non observée", args.Exception);
            args.SetObserved();
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
            _ = Task.Run(() => LicenseService.RefreshAsync(License.LicenseKey));
        }

        // ── Sentry ───────────────────────────────────────────────────────
        if (SentryDsn != "SENTRY_DSN_ICI")
        {
            SentrySdk.Init(options =>
            {
                options.Dsn                 = SentryDsn;
                options.Release             = $"schedulys@{UpdateChecker.CurrentVersion}";
                options.Environment         = "production";
                options.AutoSessionTracking = true;
                options.IsGlobalModeEnabled = true;
                options.AttachStacktrace    = true;
                options.SendDefaultPii      = false;
            });

            SentrySdk.ConfigureScope(scope =>
            {
                scope.User = new SentryUser
                {
                    Username = License?.SchoolName ?? "inconnu",
                    Id       = LicenseService.GetMachineId(),
                };
                scope.SetTag("version", UpdateChecker.CurrentVersion);
                scope.SetTag("school",  License?.SchoolName ?? "inconnu");
                var keyPreview = License?.LicenseKey is { Length: > 0 } k
                    ? k[..Math.Min(8, k.Length)] + "…"
                    : "N/A";
                scope.SetTag("licence_key", keyPreview);
            });

            AppLogger.InitSentry();
        }

        // ── Démarrage normal ─────────────────────────────────────────────
        var dbPath    = Path.Combine(appData, "data.db");
        var backupDir = Path.Combine(appData, "backups");

        AppLogger.Info("STARTUP", $"=== Schedulys v{UpdateChecker.CurrentVersion} démarré ===");
        AppLogger.Info("STARTUP", $"École : {License?.SchoolName ?? "inconnue"} | Licence expire : {License?.ExpiresAt:yyyy-MM-dd}");
        AppLogger.Info("STARTUP", $"DB : {dbPath} | Existe : {File.Exists(dbPath)} | Taille : {(File.Exists(dbPath) ? $"{new FileInfo(dbPath).Length / 1024.0:F1} KB" : "N/A")}");

        // Détection de base de données manquante avec backups disponibles
        if (!File.Exists(dbPath) && Directory.Exists(backupDir))
        {
            var latestBackup = Directory.GetFiles(backupDir, "data_*.db")
                                        .OrderByDescending(f => f)
                                        .FirstOrDefault();
            if (latestBackup is not null)
            {
                AppLogger.Error("STARTUP", $"data.db manquante — backup trouvé : {latestBackup}");
                var res = MessageBox.Show(
                    $"La base de données est introuvable.\n\n" +
                    $"Une sauvegarde du {Path.GetFileNameWithoutExtension(latestBackup).Replace("data_", "")} est disponible.\n\n" +
                    "Restaurer depuis cette sauvegarde ?\n(Non = démarrer avec une base vide)",
                    "Base de données manquante",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                if (res == MessageBoxResult.Yes)
                {
                    File.Copy(latestBackup, dbPath);
                    AppLogger.Warn("RESTORE", $"Base restaurée depuis : {Path.GetFileName(latestBackup)}");
                }
            }
        }

        BackupDatabase(dbPath, appData);

        var factory = new SqliteConnectionFactory(dbPath);
        AppLogger.Info("STARTUP", "Initialisation du schéma...");
        await SchemaInitializer.InitAsync(factory, msg => AppLogger.Info("MIGRATION", msg));
        AppLogger.Info("STARTUP", "Schéma initialisé.");
        Db = new DataContext(dbPath);

        var window = new MainWindow
        {
            DataContext = new MainShellViewModel(Db)
        };
        window.Closed += (_, _) => Shutdown();
        window.Show();

        _ = UpdateChecker.CheckAndPromptAsync();
    }

    private static void BackupDatabase(string dbPath, string appData)
    {
        try
        {
            if (!File.Exists(dbPath)) return;

            var backupDir = Path.Combine(appData, "backups");
            Directory.CreateDirectory(backupDir);

            var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var dest  = Path.Combine(backupDir, $"data_{stamp}.db");

            // API SQLite native — checkpoint WAL + copie cohérente, safe sur une DB ouverte
            using var src = new SqliteConnection($"Data Source={dbPath}");
            src.Open();
            using var bak = new SqliteConnection($"Data Source={dest}");
            bak.Open();
            src.BackupDatabase(bak);

            AppLogger.Info("BACKUP", $"Sauvegarde créée : {dest}");

            var files = Directory.GetFiles(backupDir, "data_*.db")
                                 .OrderByDescending(f => f)
                                 .Skip(10)
                                 .ToArray();
            foreach (var old in files)
            {
                File.Delete(old);
                AppLogger.Info("BACKUP", $"Ancienne sauvegarde supprimée : {Path.GetFileName(old)}");
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error("BACKUP", "Échec de la sauvegarde", ex);
        }
    }
}
