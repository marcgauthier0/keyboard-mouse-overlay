# Script pour créer un certificat auto-signé pour signature de code
# ⚠️ UTILISATION INTERNE UNIQUEMENT - Ne fonctionne que sur les PCs où le certificat est installé

param(
    [Parameter(Mandatory=$false)]
    [string]$Subject = "CN=Gaming Keypress Overlay",
    
    [Parameter(Mandatory=$false)]
    [string]$FriendlyName = "Gaming Keypress Overlay Code Signing",
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "GamingOverlay_CodeSigning.pfx",
    
    [Parameter(Mandatory=$false)]
    [string]$Password = "",
    
    [Parameter(Mandatory=$false)]
    [int]$ValidityYears = 5
)

# Vérifier les privilèges admin
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

if (-not $isAdmin) {
    Write-Host "❌ Erreur : Ce script nécessite des privilèges administrateur." -ForegroundColor Red
    Write-Host "   Relancez PowerShell en tant qu'administrateur." -ForegroundColor Yellow
    exit 1
}

Write-Host "🔐 Création d'un certificat auto-signé pour signature de code..." -ForegroundColor Cyan
Write-Host ""

# Générer un mot de passe si non fourni
if ([string]::IsNullOrEmpty($Password)) {
    $Password = -join ((48..57) + (65..90) + (97..122) | Get-Random -Count 16 | ForEach-Object {[char]$_})
    Write-Host "🔑 Mot de passe généré automatiquement (sauvegardez-le !)" -ForegroundColor Yellow
    Write-Host "   Mot de passe : $Password" -ForegroundColor Gray
    Write-Host ""
}

try {
    # Créer le certificat
    Write-Host "📝 Création du certificat..." -ForegroundColor Yellow
    
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject $Subject `
        -KeyUsage DigitalSignature `
        -FriendlyName $FriendlyName `
        -CertStoreLocation Cert:\CurrentUser\My `
        -KeyExportPolicy Exportable `
        -KeySpec Signature `
        -KeyLength 2048 `
        -KeyAlgorithm RSA `
        -HashAlgorithm SHA256 `
        -ValidityPeriod Years `
        -ValidityPeriodUnits $ValidityYears `
        -ErrorAction Stop
    
    Write-Host "✅ Certificat créé avec succès" -ForegroundColor Green
    Write-Host ""
    
    # Exporter en PFX
    Write-Host "💾 Export du certificat en PFX..." -ForegroundColor Yellow
    
    $securePassword = ConvertTo-SecureString -String $Password -Force -AsPlainText
    Export-PfxCertificate `
        -Cert $cert `
        -FilePath $OutputPath `
        -Password $securePassword `
        -ErrorAction Stop
    
    Write-Host "✅ Certificat exporté : $OutputPath" -ForegroundColor Green
    Write-Host ""
    
    # Exporter aussi en CER (pour installation sur autres PCs)
    $cerPath = $OutputPath -replace '\.pfx$', '.cer'
    Export-Certificate -Cert $cert -FilePath $cerPath -ErrorAction Stop
    Write-Host "✅ Certificat public exporté : $cerPath" -ForegroundColor Green
    Write-Host ""
    
    # Afficher les informations
    Write-Host "📋 Informations du certificat :" -ForegroundColor Cyan
    Write-Host "   Thumbprint : $($cert.Thumbprint)" -ForegroundColor Gray
    Write-Host "   Subject    : $($cert.Subject)" -ForegroundColor Gray
    Write-Host "   Valid From : $($cert.NotBefore)" -ForegroundColor Gray
    Write-Host "   Valid To   : $($cert.NotAfter)" -ForegroundColor Gray
    Write-Host ""
    
    Write-Host "📝 Prochaines étapes :" -ForegroundColor Cyan
    Write-Host "   1. Installer le certificat sur les PCs testeurs :" -ForegroundColor Yellow
    Write-Host "      Import-Certificate -FilePath `"$cerPath`" -CertStoreLocation Cert:\LocalMachine\Root" -ForegroundColor Gray
    Write-Host ""
    Write-Host "   2. Signer l'application :" -ForegroundColor Yellow
    Write-Host "      .\scripts\sign-application.ps1 -CertPath `"$OutputPath`" -Password `"$Password`"" -ForegroundColor Gray
    Write-Host ""
    
    Write-Host "⚠️  IMPORTANT :" -ForegroundColor Red
    Write-Host "   - Ce certificat fonctionne UNIQUEMENT sur les PCs où il est installé" -ForegroundColor Yellow
    Write-Host "   - Ne PAS utiliser pour distribution publique" -ForegroundColor Yellow
    Write-Host "   - Sauvegardez le fichier .pfx et le mot de passe en sécurité" -ForegroundColor Yellow
    Write-Host ""
    
} catch {
    Write-Host "❌ Erreur : $_" -ForegroundColor Red
    exit 1
}
