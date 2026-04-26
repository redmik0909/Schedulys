# ── Déploiement du système de demande de licence ───────────────────────────
# Prérequis : Node.js installé (pour npx)
#
# Utilisation :
#   .\deploy-license-functions.ps1 -SupabaseToken "ton-token-ici"
#
# Pour obtenir ton token : https://supabase.com/dashboard/account/tokens
# ---------------------------------------------------------------------------

param(
    [Parameter(Mandatory=$true)]
    [string]$SupabaseToken
)

$ErrorActionPreference = "Stop"
$env:SUPABASE_ACCESS_TOKEN = $SupabaseToken

Write-Host "`n── 1/4  Authentification Supabase..." -ForegroundColor Cyan
npx supabase projects list | Out-Null
Write-Host "OK" -ForegroundColor Green

Write-Host "`n── 2/4  Configuration des secrets..." -ForegroundColor Cyan
npx supabase secrets set `
    RESEND_API_KEY=re_dwnv3q27_7q8Gr3w7erWws8Sj9QDpD8XA `
    NTFY_TOPIC=schedulys-demandes-k9x2m7 `
    "APPROVE_FUNCTION_URL=https://kwdykfxrgiqqeskkogta.supabase.co/functions/v1/approve-license" `
    "FROM_EMAIL=Schedulys <noreply@revolvittech.com>"
Write-Host "OK" -ForegroundColor Green

Write-Host "`n── 3/4  Déploiement des fonctions..." -ForegroundColor Cyan
npx supabase functions deploy request-license --no-verify-jwt
npx supabase functions deploy approve-license --no-verify-jwt
Write-Host "OK" -ForegroundColor Green

Write-Host "`n── 4/4  Migration SQL..." -ForegroundColor Cyan
Write-Host "Exécute ce SQL dans le dashboard Supabase (SQL Editor) :" -ForegroundColor Yellow
Write-Host "  https://supabase.com/dashboard/project/kwdykfxrgiqqeskkogta/sql" -ForegroundColor Yellow
Get-Content "supabase\migrations\002_license_requests.sql"

Write-Host "`n✅ Déploiement terminé !" -ForegroundColor Green
Write-Host "   Abonne-toi au topic ntfy.sh : schedulys-demandes-k9x2m7" -ForegroundColor White
Write-Host "   App mobile : https://ntfy.sh" -ForegroundColor White
