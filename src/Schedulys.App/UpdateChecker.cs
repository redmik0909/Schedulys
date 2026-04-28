using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace Schedulys.App;

public static class UpdateChecker
{
    public const string CurrentVersion = "1.9";

    private const string ApiUrl = "https://api.github.com/repos/redmik0909/Schedulys/releases/latest";

    public static async Task CheckAndPromptAsync()
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Add("User-Agent", "Schedulys-App");
            http.Timeout = TimeSpan.FromSeconds(8);

            var json = await http.GetStringAsync(ApiUrl);
            var doc  = JsonDocument.Parse(json);

            var tag      = doc.RootElement.GetProperty("tag_name").GetString()?.TrimStart('v') ?? "";
            var assetUrl = doc.RootElement
                .GetProperty("assets")
                .EnumerateArray()
                .Select(a => a.GetProperty("browser_download_url").GetString())
                .FirstOrDefault(u => u?.EndsWith(".exe") == true);

            if (!Version.TryParse(tag, out var latest)) return;
            if (!Version.TryParse(CurrentVersion, out var current)) return;
            if (latest <= current) return;

            var result = MessageBox.Show(
                $"Une nouvelle version est disponible : v{tag}\n\nVoulez-vous la télécharger et l'installer maintenant ?",
                "Mise à jour disponible",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result != MessageBoxResult.Yes || assetUrl is null) return;

            await DownloadAndInstallAsync(assetUrl, tag);
        }
        catch
        {
            // Silencieux — pas de connexion ou API indisponible
        }
    }

    private static Task DownloadAndInstallAsync(string url, string version)
    {
        MessageBox.Show(
            $"Le téléchargement va s'ouvrir dans votre navigateur.\n\nUne fois le fichier téléchargé, lancez-le pour installer Schedulys v{version}.",
            "Mise à jour — v" + version,
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        return Task.CompletedTask;
    }
}
