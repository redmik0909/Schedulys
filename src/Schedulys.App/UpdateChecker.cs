using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Schedulys.App;

public static class UpdateChecker
{
    public const string CurrentVersion = "2.4";

    private const string Repo        = "redmik0909/Schedulys";
    private const string ApiUrl      = $"https://api.github.com/repos/{Repo}/releases/latest";
    private const string DownloadUrl = $"https://github.com/{Repo}/releases/latest/download/Schedulys-Setup-latest.exe";

    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    static UpdateChecker()
    {
        _http.DefaultRequestHeaders.Add("User-Agent", "Schedulys-App");
        _http.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
    }

    public static async Task CheckAndPromptAsync()
    {
        try
        {
            var release = await _http.GetFromJsonAsync<GhRelease>(ApiUrl);
            var latest  = release?.TagName?.TrimStart('v').Trim();
            if (string.IsNullOrEmpty(latest)) return;

            if (!Version.TryParse(latest, out var latestVer)) return;
            if (!Version.TryParse(CurrentVersion, out var current)) return;
            if (latestVer <= current) return;

            var result = MessageBox.Show(
                $"Une nouvelle version est disponible : v{latest}\n\nVoulez-vous la télécharger et l'installer maintenant ?",
                "Mise à jour disponible",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result != MessageBoxResult.Yes) return;

            await DownloadAndInstallAsync(DownloadUrl, latest);
        }
        catch
        {
            // Silencieux — pas de connexion ou serveur indisponible
        }
    }

    private static async Task DownloadAndInstallAsync(string url, string version)
    {
        var progressWin = BuildProgressWindow(version);
        progressWin.Show();

        var tmpPath = Path.Combine(Path.GetTempPath(), $"Schedulys-Setup-v{version}.exe");

        try
        {
            using var cts      = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            response.EnsureSuccessStatusCode();

            var total    = response.Content.Headers.ContentLength;
            var bar      = (ProgressBar)progressWin.FindName("ProgressBar");
            var lbl      = (TextBlock)progressWin.FindName("StatusLabel");

            if (total.HasValue)
                bar.IsIndeterminate = false;

            await using var src  = await response.Content.ReadAsStreamAsync(cts.Token);
            await using var dest = File.Create(tmpPath);

            var buffer    = new byte[81920];
            long received = 0;
            int  read;
            while ((read = await src.ReadAsync(buffer, cts.Token)) > 0)
            {
                await dest.WriteAsync(buffer.AsMemory(0, read), cts.Token);
                received += read;
                if (total.HasValue)
                {
                    var pct = (double)received / total.Value * 100;
                    bar.Value = pct;
                    lbl.Text  = $"Téléchargement… {pct:F0} %";
                }
            }

            progressWin.Close();
            Process.Start(new ProcessStartInfo(tmpPath) { UseShellExecute = true });

            await Task.Delay(600);
            Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown());
        }
        catch (Exception ex)
        {
            progressWin.Close();
            MessageBox.Show(
                $"Le téléchargement a échoué :\n{ex.Message}\n\nVeuillez télécharger manuellement depuis GitHub.",
                "Erreur de mise à jour",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private static Window BuildProgressWindow(string version)
    {
        var bar = new ProgressBar
        {
            Name            = "ProgressBar",
            IsIndeterminate = true,
            Height          = 18,
            Margin          = new Thickness(0, 8, 0, 0),
            Foreground      = new SolidColorBrush(Color.FromRgb(79, 70, 229)),
        };

        var lbl = new TextBlock
        {
            Name       = "StatusLabel",
            Text       = "Téléchargement en cours…",
            Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
            FontSize   = 13,
            Margin     = new Thickness(0, 0, 0, 4),
        };

        var title = new TextBlock
        {
            Text       = $"Mise à jour — v{version}",
            FontSize   = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
            Margin     = new Thickness(0, 0, 0, 12),
        };

        var panel = new StackPanel { Margin = new Thickness(28) };
        panel.Children.Add(title);
        panel.Children.Add(lbl);
        panel.Children.Add(bar);

        var win = new Window
        {
            Title           = "Mise à jour Schedulys",
            Width           = 380,
            SizeToContent   = SizeToContent.Height,
            ResizeMode      = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background      = new SolidColorBrush(Colors.White),
            Content         = panel,
        };

        // Enregistre les noms pour FindName()
        win.RegisterName(bar.Name, bar);
        win.RegisterName(lbl.Name, lbl);

        return win;
    }

    private sealed class GhRelease
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
    }
}
