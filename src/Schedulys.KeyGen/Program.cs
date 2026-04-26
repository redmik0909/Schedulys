using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

// ============================================================
//  Schedulys KeyGen — Générateur de licences
//  Usage : dotnet run -- --school "École X" --email "dir@ecole.ca" --months 12
// ============================================================

const string SUPABASE_URL    = "https://kwdykfxrgiqqeskkogta.supabase.co";
const string SUPABASE_SECRET = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Imt3ZHlrZnhyZ2lxcWVza2tvZ3RhIiwicm9sZSI6InNlcnZpY2Vfcm9sZSIsImlhdCI6MTc3NzEzNzc4OSwiZXhwIjoyMDkyNzEzNzg5fQ.qxCOKISr8ZYuvEVns-sZSusJl-_xz2DWIHMw9fvt9tE";
const string HMAC_SECRET     = "ScH3duLys!L1c3ns3@2026#Qu3b3c$pX9mK2vL8nQ4rT6w";

var args2 = new Dictionary<string, string>();
for (int i = 0; i < args.Length - 1; i += 2)
    args2[args[i].TrimStart('-')] = args[i + 1];

string school = args2.GetValueOrDefault("school", "");
string email  = args2.GetValueOrDefault("email",  "");
int months    = int.TryParse(args2.GetValueOrDefault("months", "12"), out var m) ? m : 12;
int maxAct    = int.TryParse(args2.GetValueOrDefault("activations", "2"), out var a) ? a : 2;

if (string.IsNullOrWhiteSpace(school))
{
    Console.Write("Nom de l'école : ");
    school = Console.ReadLine()!.Trim();
}
if (string.IsNullOrWhiteSpace(email))
{
    Console.Write("Email de contact : ");
    email = Console.ReadLine()!.Trim();
}

var key    = GenerateKey(school);
var expiry = DateTime.UtcNow.AddMonths(months).ToString("yyyy-MM-dd");

Console.WriteLine();
Console.WriteLine($"  Clé            : {key}");
Console.WriteLine($"  École          : {school}");
Console.WriteLine($"  Email          : {email}");
Console.WriteLine($"  Expiration     : {expiry}");
Console.WriteLine($"  Activations max: {maxAct}");
Console.WriteLine();
Console.Write("Enregistrer dans Supabase ? (o/n) : ");
if (Console.ReadLine()?.Trim().ToLower() != "o") { Console.WriteLine("Annulé."); return; }

using var http = new HttpClient();
http.DefaultRequestHeaders.Add("apikey", SUPABASE_SECRET);
http.DefaultRequestHeaders.Add("Authorization", $"Bearer {SUPABASE_SECRET}");

var payload = new
{
    license_key     = key,
    school_name     = school,
    email           = email,
    expires_at      = expiry,
    max_activations = maxAct,
    activations     = Array.Empty<string>()
};

var resp = await http.PostAsJsonAsync($"{SUPABASE_URL}/rest/v1/licenses", payload);
if (resp.IsSuccessStatusCode)
{
    Console.WriteLine("Licence enregistree dans Supabase.");
    Console.WriteLine($"\n  Cle a envoyer au client :\n\n  {key}\n");
}
else
{
    var body = await resp.Content.ReadAsStringAsync();
    Console.WriteLine($"Erreur Supabase : {resp.StatusCode} — {body}");
}

static string GenerateKey(string school)
{
    // Format : SCHEDULYS-XXXX-XXXX-XXXX-XXXX
    using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(HMAC_SECRET));
    var seed = hmac.ComputeHash(Encoding.UTF8.GetBytes(school + DateTime.UtcNow.Ticks));
    var hex  = Convert.ToHexString(seed)[..16];
    return $"SCHEDULYS-{hex[..4]}-{hex[4..8]}-{hex[8..12]}-{hex[12..16]}";
}
