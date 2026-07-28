# Script helper pour vérifier et configurer la politique d'exécution PowerShell
# Peut être exécuté même si la politique bloque les scripts (via -ExecutionPolicy Bypass)

param(
    [switch]$AutoFix,
    [switch]$Verbose
)

Write-Host "🔒 Vérification de la politique d'exécution PowerShell..." -ForegroundColor Cyan
Write-Host ""

# Vérifier la politique actuelle
$currentPolicy = Get-ExecutionPolicy
$userPolicy = Get-ExecutionPolicy -Scope CurrentUser

Write-Host "📋 Politique actuelle :" -ForegroundColor Yellow
Write-Host "   Process      : $currentPolicy" -ForegroundColor Gray
Write-Host "   CurrentUser  : $userPolicy" -ForegroundColor Gray
Write-Host ""

# Vérifier si les scripts peuvent s'exécuter
$canExecute = $false
if ($currentPolicy -eq "Unrestricted" -or $currentPolicy -eq "RemoteSigned" -or $currentPolicy -eq "Bypass") {
    $canExecute = $true
}

if ($canExecute) {
    Write-Host "✅ Les scripts peuvent s'exécuter" -ForegroundColor Green
    Write-Host ""
    Write-Host "💡 Vous pouvez maintenant exécuter les scripts normalement :" -ForegroundColor Cyan
    Write-Host "   .\scripts\sign-application.ps1 ..." -ForegroundColor Gray
    Write-Host "   .\scripts\unblock-files.ps1 ..." -ForegroundColor Gray
    exit 0
}

Write-Host "❌ Les scripts sont bloqués par la politique d'exécution" -ForegroundColor Red
Write-Host ""

# Proposer une solution
Write-Host "💡 Solutions disponibles :" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Changer la politique pour l'utilisateur actuel (RECOMMANDÉ)" -ForegroundColor Yellow
Write-Host "   Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser" -ForegroundColor Gray
Write-Host ""
Write-Host "2. Exécuter avec Bypass (temporaire)" -ForegroundColor Yellow
Write-Host "   powershell -ExecutionPolicy Bypass -File .\scripts\sign-application.ps1 ..." -ForegroundColor Gray
Write-Host ""

if ($AutoFix) {
    Write-Host "🔧 Tentative de correction automatique..." -ForegroundColor Yellow
    Write-Host ""
    
    try {
        Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser -Force -ErrorAction Stop
        Write-Host "✅ Politique changée avec succès !" -ForegroundColor Green
        Write-Host "   Nouvelle politique : $(Get-ExecutionPolicy -Scope CurrentUser)" -ForegroundColor Gray
        Write-Host ""
        Write-Host "💡 Vous pouvez maintenant exécuter les scripts normalement." -ForegroundColor Cyan
    } catch {
        Write-Host "❌ Erreur lors du changement de politique : $_" -ForegroundColor Red
        Write-Host ""
        Write-Host "💡 Essayez d'exécuter manuellement :" -ForegroundColor Yellow
        Write-Host "   Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser" -ForegroundColor Gray
        exit 1
    }
} else {
    Write-Host "💡 Pour corriger automatiquement, exécutez :" -ForegroundColor Cyan
    Write-Host "   powershell -ExecutionPolicy Bypass -File .\scripts\check-execution-policy.ps1 -AutoFix" -ForegroundColor Gray
    Write-Host ""
    Write-Host "   Ou manuellement :" -ForegroundColor Yellow
    Write-Host "   Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser" -ForegroundColor Gray
}
