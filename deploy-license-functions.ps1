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

$ref = "kwdykfxrgiqqeskkogta"

Write-Host "`n── 1/4  Authentification Supabase..." -ForegroundColor Cyan
npx supabase projects list --project-ref $ref | Out-Null
Write-Host "OK" -ForegroundColor Green

Write-Host "`n── 2/4  Configuration des secrets..." -ForegroundColor Cyan
npx supabase secrets set --project-ref $ref `
    RESEND_API_KEY=re_dwnv3q27_7q8Gr3w7erWws8Sj9QDpD8XA `
    ADMIN_EMAIL=support@revolvittech.com `
    "APPROVE_FUNCTION_URL=https://kwdykfxrgiqqeskkogta.supabase.co/functions/v1/approve-license" `
    "FROM_EMAIL=Schedulys <noreply@send.revolvittech.com>"
Write-Host "OK" -ForegroundColor Green

Write-Host "`n── 3/4  Déploiement des fonctions..." -ForegroundColor Cyan
npx supabase functions deploy request-license  --project-ref $ref --no-verify-jwt
npx supabase functions deploy approve-license  --project-ref $ref --no-verify-jwt
npx supabase functions deploy validate-license --project-ref $ref --no-verify-jwt
Write-Host "OK" -ForegroundColor Green

Write-Host "`n── 4/4  Migration SQL..." -ForegroundColor Cyan
Write-Host "Exécute ces SQL dans le dashboard Supabase (SQL Editor) :" -ForegroundColor Yellow
Write-Host "  https://supabase.com/dashboard/project/kwdykfxrgiqqeskkogta/sql" -ForegroundColor Yellow
Write-Host "`n-- Migration 002 :" -ForegroundColor DarkGray
Get-Content "supabase\migrations\002_license_requests.sql"
Write-Host "`n-- Migration 003 (trial) :" -ForegroundColor DarkGray
Get-Content "supabase\migrations\003_licenses_trial.sql"

Write-Host "`n✅ Déploiement terminé !" -ForegroundColor Green
