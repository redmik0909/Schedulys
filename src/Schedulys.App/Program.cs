using Avalonia;
using System;
using System.IO;
using System.Threading.Tasks;
using Schedulys.Data.Db;

namespace Schedulys.App;

sealed class Program
{
    [STAThread]
    public static async Task Main(string[] args)
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Schedulys");
        var dbPath = Path.Combine(appData, "data.db");

        Console.WriteLine($"[Schedulys] AppData = {appData}");
        Console.WriteLine($"[Schedulys] DB Path = {dbPath}");

        try
        {
            // en haut : tu peux retirer `using Schedulys.Data.Db;` (optionnel)

            var factory = new global::Schedulys.Data.Db.SqliteConnectionFactory(dbPath);
            await global::Schedulys.Data.Db.SchemaInitializer.InitAsync(factory);
            Console.WriteLine("[Schedulys] Schema initialized OK.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("[Schedulys] DB init ERROR: " + ex);
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}