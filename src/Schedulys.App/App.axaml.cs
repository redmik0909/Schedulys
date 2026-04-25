using System;
using System.IO;
using System.Windows;
using Schedulys.Data;
using Schedulys.Data.Db;
using Schedulys.App.ViewModels;
using Schedulys.App.Views;

namespace Schedulys.App;

public partial class App : Application
{
    public static DataContext Db { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show(args.Exception.ToString(), "Erreur inattendue",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

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
