using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Schedulys.App;

public enum LicenseStatus { Valid, Expired, NotActivated, Invalid }

public sealed class LicenseInfo
{
    public string        SchoolName { get; init; } = "";
    public string        LicenseKey { get; init; } = "";
    public DateTime      ExpiresAt  { get; init; }
    public LicenseStatus Status     { get; init; }
    public bool          IsTrial    { get; init; }
}

public static class LicenseService
{
    private const string SUPABASE_URL  = "https://kwdykfxrgiqqeskkogta.supabase.co";
    private const string SUPABASE_ANON = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Imt3ZHlrZnhyZ2lxcWVza2tvZ3RhIiwicm9sZSI6ImFub24iLCJpYXQiOjE3NzcxMzc3ODksImV4cCI6MjA5MjcxMzc4OX0.8ZmKijEKcaCtgMRBWw32ccu-85ubKmc8khKpa7pKkMw";
    private const string HMAC_SECRET   = "ScH3duLys!L1c3ns3@2026#Qu3b3c$pX9mK2vL8nQ4rT6w";

    private static readonly string TokenPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Schedulys", "license.dat");

    // HttpClient réutilisé — évite les fuites de socket
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    static LicenseService()
    {
        _http.DefaultRequestHeaders.Add("apikey", SUPABASE_ANON);
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {SUPABASE_ANON}");
    }

    // ── API publique ────────────────────────────────────────────────────────

    public static LicenseInfo? LoadLocal()
    {
        if (!File.Exists(TokenPath)) return null;
        try
        {
            // Déchiffrement DPAPI (lié au compte Windows courant)
            var encrypted = File.ReadAllBytes(TokenPath);
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            var token     = JsonSerializer.Deserialize<LocalToken>(decrypted);

            if (token is null || !VerifySignature(token)) return null;

            var status = token.ExpiresAt < DateTime.UtcNow
                ? LicenseStatus.Expired
                : LicenseStatus.Valid;

            return new LicenseInfo
            {
                SchoolName = token.SchoolName,
                LicenseKey = token.LicenseKey,
                ExpiresAt  = token.ExpiresAt,
                Status     = status,
                IsTrial    = token.IsTrial
            };
        }
        catch { return null; }
    }

    public static async Task<LicenseInfo> ActivateAsync(string licenseKey)
    {
        licenseKey = licenseKey.Trim().ToUpperInvariant();

        var payload = new { license_key = licenseKey, machine_id = GetMachineId() };
        HttpResponseMessage resp;
        try
        {
            resp = await _http.PostAsJsonAsync(
                $"{SUPABASE_URL}/functions/v1/validate-license", payload);
        }
        catch (Exception ex)
        {
            throw new LicenseException($"Impossible de joindre le serveur de licences : {ex.Message}");
        }

        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync();
            throw new LicenseException(ParseSupabaseError(err));
        }

        var result = await resp.Content.ReadFromJsonAsync<ValidationResult>()
            ?? throw new LicenseException("Réponse invalide du serveur.");

        if (!result.Valid)
            throw new LicenseException(result.Error ?? "Licence invalide.");

        var expiry = ParseExpiryUtc(result.ExpiresAt!);
        SaveToken(licenseKey, result.SchoolName!, expiry, result.IsTrial);

        return new LicenseInfo
        {
            LicenseKey = licenseKey,
            SchoolName = result.SchoolName!,
            ExpiresAt  = expiry,
            Status     = LicenseStatus.Valid,
            IsTrial    = result.IsTrial
        };
    }

    // Validation silencieuse en ligne — appelée en tâche de fond après démarrage
    public static async Task RefreshAsync(string licenseKey)
    {
        try
        {
            var payload = new { license_key = licenseKey, machine_id = GetMachineId() };
            var resp    = await _http.PostAsJsonAsync(
                $"{SUPABASE_URL}/functions/v1/validate-license", payload);
            if (!resp.IsSuccessStatusCode) return;

            var result = await resp.Content.ReadFromJsonAsync<ValidationResult>();
            if (result?.Valid == true)
                SaveToken(licenseKey, result.SchoolName!, ParseExpiryUtc(result.ExpiresAt!), result.IsTrial);
        }
        catch { /* silencieux — offline OK */ }
    }

    // Parse la date d'expiration en UTC consistant — accepte "YYYY-MM-DD" (date seule),
    // "YYYY-MM-DDTHH:MM:SSZ" ou avec offset. Évite le bug de fuseau horaire au Québec.
    private static DateTime ParseExpiryUtc(string raw)
    {
        var dt = DateTime.Parse(raw, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
        // Pour une date seule (YYYY-MM-DD), considère la fin de journée UTC pour éviter
        // une expiration prématurée juste avant minuit local.
        if (dt.TimeOfDay == TimeSpan.Zero && raw.Length == 10)
            dt = dt.AddDays(1).AddSeconds(-1);
        return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
    }

    // ── Helpers privés ──────────────────────────────────────────────────────

    private static void SaveToken(string key, string school, DateTime expiry, bool isTrial = false)
    {
        var token = new LocalToken
        {
            LicenseKey = key,
            SchoolName = school,
            ExpiresAt  = expiry,
            MachineId  = GetMachineId(),
            SavedAt    = DateTime.UtcNow,
            IsTrial    = isTrial
        };
        token.Signature = ComputeSignature(token);

        var json      = JsonSerializer.SerializeToUtf8Bytes(token);
        var encrypted = ProtectedData.Protect(json, null, DataProtectionScope.CurrentUser);

        Directory.CreateDirectory(Path.GetDirectoryName(TokenPath)!);
        File.WriteAllBytes(TokenPath, encrypted);
    }

    private static bool VerifySignature(LocalToken t)
        => ComputeSignature(t) == t.Signature;

    private static string ComputeSignature(LocalToken t)
    {
        var data = $"{t.LicenseKey}|{t.SchoolName}|{t.ExpiresAt:O}|{t.MachineId}|{t.SavedAt:O}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(HMAC_SECRET));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(data)));
    }

    public static string GetMachineId()
    {
        var raw = $"{Environment.MachineName}|{Environment.UserName}";
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(raw)))[..16];
    }

    // ── Demande de licence ──────────────────────────────────────────────────

    public static async Task RequestLicenseAsync(string schoolName, string contactName, string email)
    {
        var payload = new
        {
            school_name  = schoolName,
            contact_name = contactName,
            email,
            machine_id   = GetMachineId()
        };

        var resp = await _http.PostAsJsonAsync(
            $"{SUPABASE_URL}/functions/v1/request-license", payload);

        if (!resp.IsSuccessStatusCode)
        {
            var err = await resp.Content.ReadAsStringAsync();
            throw new LicenseException(ParseSupabaseError(err));
        }
    }

    private static string ParseSupabaseError(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("error",   out var e))   return e.GetString()!;
            if (doc.RootElement.TryGetProperty("message", out var msg)) return msg.GetString()!;
        }
        catch { }
        return "Erreur de licence inconnue.";
    }

    // ── Modèles internes ────────────────────────────────────────────────────

    private sealed class LocalToken
    {
        public string   LicenseKey { get; set; } = "";
        public string   SchoolName { get; set; } = "";
        public DateTime ExpiresAt  { get; set; }
        public string   MachineId  { get; set; } = "";
        public DateTime SavedAt    { get; set; }
        public string   Signature  { get; set; } = "";
        public bool     IsTrial    { get; set; }
    }

    private sealed class ValidationResult
    {
        [JsonPropertyName("valid")]       public bool    Valid      { get; set; }
        [JsonPropertyName("school_name")] public string? SchoolName { get; set; }
        [JsonPropertyName("expires_at")]  public string? ExpiresAt  { get; set; }
        [JsonPropertyName("error")]       public string? Error      { get; set; }
        [JsonPropertyName("is_trial")]    public bool    IsTrial    { get; set; }
    }
}

public sealed class LicenseException(string message) : Exception(message);
