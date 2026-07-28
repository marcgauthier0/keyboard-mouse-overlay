# Script pour signer l'application avec un certificat
# Supporte les certificats PFX (auto-signés ou commerciaux)

param(
    [Parameter(Mandatory=$true)]
    [string]$CertPath,
    
    [Parameter(Mandatory=$true)]
    [string]$Password,
    
    [Parameter(Mandatory=$false)]
    [string]$FileToSign = "GamingKeypressOverlay.exe",
    
    [Parameter(Mandatory=$false)]
    [string]$Description = "Gaming Keypress Overlay",
    
    [Parameter(Mandatory=$false)]
    [string]$TimestampServer = "http://timestamp.digicert.com",
    
    [Parameter(Mandatory=$false)]
    [string]$SignToolPath = ""
)

Write-Host "🔐 Signature de l'application..." -ForegroundColor Cyan
Write-Host ""

# Vérifier que le fichier existe
if (-not (Test-Path $FileToSign)) {
    Write-Host "❌ Erreur : Le fichier '$FileToSign' n'existe pas." -ForegroundColor Red
    exit 1
}

# Vérifier que le certificat existe
if (-not (Test-Path $CertPath)) {
    Write-Host "❌ Erreur : Le certificat '$CertPath' n'existe pas." -ForegroundColor Red
    exit 1
}

# Trouver signtool.exe
if ([string]::IsNullOrEmpty($SignToolPath)) {
    # Chercher dans les emplacements standards
    $possiblePaths = @(
        "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe",
        "C:\Program Files (x86)\Windows Kits\10\bin\10.0.*\x64\signtool.exe",
        "C:\Program Files\Windows Kits\10\bin\*\x64\signtool.exe"
    )
    
    $signtool = $null
    foreach ($pattern in $possiblePaths) {
        $found = Get-ChildItem -Path $pattern -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($found) {
            $signtool = $found.FullName
            break
        }
    }
    
    if (-not $signtool) {
        Write-Host "❌ Erreur : signtool.exe introuvable." -ForegroundColor Red
        Write-Host "   Installez Windows SDK ou spécifiez le chemin avec -SignToolPath" -ForegroundColor Yellow
        exit 1
    }
} else {
    $signtool = $SignToolPath
    if (-not (Test-Path $signtool)) {
        Write-Host "❌ Erreur : signtool.exe introuvable à '$signtool'." -ForegroundColor Red
        exit 1
    }
}

Write-Host "📝 Utilisation de : $signtool" -ForegroundColor Gray
Write-Host ""

# Vérifier la signature actuelle (si elle existe)
Write-Host "🔍 Vérification de la signature actuelle..." -ForegroundColor Yellow
$currentSig = Get-AuthenticodeSignature -FilePath $FileToSign

if ($currentSig.Status -eq "Valid") {
    Write-Host "⚠️  Le fichier est déjà signé." -ForegroundColor Yellow
    $response = Read-Host "   Voulez-vous re-signer ? (O/N)"
    if ($response -ne "O" -and $response -ne "o") {
        Write-Host "   Signature annulée." -ForegroundColor Gray
        exit 0
    }
}

# Signer le fichier
Write-Host "✍️  Signature du fichier..." -ForegroundColor Yellow

$arguments = @(
    "sign",
    "/f", "`"$CertPath`"",
    "/p", "`"$Password`"",
    "/t", "`"$TimestampServer`"",
    "/d", "`"$Description`"",
    "`"$FileToSign`""
)

try {
    $process = Start-Process -FilePath $signtool -ArgumentList $arguments -Wait -NoNewWindow -PassThru
    
    if ($process.ExitCode -eq 0) {
        Write-Host "✅ Fichier signé avec succès" -ForegroundColor Green
        Write-Host ""
        
        # Vérifier la signature
        Write-Host "🔍 Vérification de la signature..." -ForegroundColor Yellow
        $newSig = Get-AuthenticodeSignature -FilePath $FileToSign
        
        Write-Host "   Status      : $($newSig.Status)" -ForegroundColor Gray
        Write-Host "   Signer      : $($newSig.SignerCertificate.Subject)" -ForegroundColor Gray
        Write-Host "   Timestamp   : $($newSig.TimeStamperCertificate.Subject)" -ForegroundColor Gray
        
        if ($newSig.Status -eq "Valid") {
            Write-Host ""
            Write-Host "✅ Signature valide !" -ForegroundColor Green
        } else {
            Write-Host ""
            Write-Host "⚠️  Signature créée mais statut : $($newSig.Status)" -ForegroundColor Yellow
            Write-Host "   Cela peut être normal pour un certificat auto-signé." -ForegroundColor Gray
        }
    } else {
        Write-Host "❌ Erreur lors de la signature (code de sortie : $($process.ExitCode))" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "❌ Erreur : $_" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "💡 Le fichier est maintenant signé et prêt pour la distribution." -ForegroundColor Cyan
