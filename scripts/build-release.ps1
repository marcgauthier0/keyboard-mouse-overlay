# Script de build complet pour release
# Automatise : build, déblocage, création installer, signature (optionnel)

param(
    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release",
    
    [Parameter(Mandatory=$false)]
    [string]$Runtime = "win-x64",
    
    [Parameter(Mandatory=$false)]
    [switch]$SelfContained = $true,
    
    [Parameter(Mandatory=$false)]
    [switch]$UnblockFiles = $true,
    
    [Parameter(Mandatory=$false)]
    [switch]$BuildInstaller = $true,
    
    [Parameter(Mandatory=$false)]
    [switch]$SignInstaller = $false,
    
    [Parameter(Mandatory=$false)]
    [string]$CertPath = "",
    
    [Parameter(Mandatory=$false)]
    [string]$CertPassword = "",
    
    [Parameter(Mandatory=$false)]
    [switch]$DetailedOutput
)

$ErrorActionPreference = "Stop"

Write-Host "🚀 Build Release - Keyboard & Mouse Overlay" -ForegroundColor Cyan
Write-Host ""

# Variables
$projectFile = "GamingKeypressOverlay.csproj"
$publishDir = "bin/$Configuration/net8.0-windows/$Runtime/publish"
$installerScript = "installer.nsi"

# Vérifier que le projet existe
if (-not (Test-Path $projectFile)) {
    Write-Host "❌ Erreur : $projectFile introuvable." -ForegroundColor Red
    exit 1
}

# Step 1: Build l'application
Write-Host "📦 Step 1: Build de l'application..." -ForegroundColor Yellow
Write-Host "   Configuration : $Configuration" -ForegroundColor Gray
Write-Host "   Runtime       : $Runtime" -ForegroundColor Gray
Write-Host "   Self-Contained : $SelfContained" -ForegroundColor Gray
Write-Host ""

$publishArgs = @(
    "publish",
    $projectFile,
    "-c", $Configuration,
    "-r", $Runtime
)

if ($SelfContained) {
    $publishArgs += "--self-contained", "true"
    $publishArgs += "-p:PublishSingleFile=true"
    $publishArgs += "-p:IncludeNativeLibrariesForSelfExtract=true"
    $publishArgs += "-p:EnableCompressionInSingleFile=true"
} else {
    $publishArgs += "--self-contained", "false"
}

try {
    & dotnet $publishArgs
    
    if ($LASTEXITCODE -ne 0) {
        throw "Build échoué avec le code $LASTEXITCODE"
    }
    
    Write-Host "✅ Build réussi" -ForegroundColor Green
    Write-Host "   Output : $publishDir" -ForegroundColor Gray
    Write-Host ""
} catch {
    Write-Host "❌ Erreur lors du build : $_" -ForegroundColor Red
    exit 1
}

# Step 2: Débloquer les fichiers (si demandé)
if ($UnblockFiles) {
    Write-Host "🔓 Step 2: Déblocage des fichiers..." -ForegroundColor Yellow
    
    if (Test-Path $publishDir) {
        try {
            & "$PSScriptRoot\unblock-files.ps1" -Path $publishDir -Recursive
            Write-Host "✅ Fichiers débloqués" -ForegroundColor Green
            Write-Host ""
        } catch {
            Write-Host "⚠️  Erreur lors du déblocage : $_" -ForegroundColor Yellow
            Write-Host "   Continuation du build..." -ForegroundColor Gray
            Write-Host ""
        }
    } else {
        Write-Host "⚠️  Dossier $publishDir introuvable, déblocage ignoré" -ForegroundColor Yellow
        Write-Host ""
    }
}

# Step 3: Créer l'installer (si demandé)
if ($BuildInstaller) {
    Write-Host "📦 Step 3: Création de l'installer..." -ForegroundColor Yellow
    
    if (-not (Test-Path $installerScript)) {
        Write-Host "❌ Erreur : $installerScript introuvable." -ForegroundColor Red
        exit 1
    }
    
    # Vérifier que NSIS est installé
    $makensisCommand = Get-Command makensis -ErrorAction SilentlyContinue
    $makensisPath = if ($makensisCommand) { $makensisCommand.Source } else { $null }
    if (-not $makensisPath) {
        $knownNsisPaths = @(
            "$env:ProgramFiles\NSIS\makensis.exe",
            "${env:ProgramFiles(x86)}\NSIS\makensis.exe"
        )
        $makensisPath = $knownNsisPaths | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    }
    if (-not $makensisPath) {
        Write-Host "❌ Erreur : NSIS (makensis) introuvable." -ForegroundColor Red
        Write-Host "   Installez NSIS depuis : https://nsis.sourceforge.io/Download" -ForegroundColor Yellow
        exit 1
    }
    
    # Copier les fichiers nécessaires
    Write-Host "   Copie des fichiers..." -ForegroundColor Gray
    
    $exePath = Join-Path $publishDir "GamingKeypressOverlay.exe"
    if (-not (Test-Path $exePath)) {
        Write-Host "❌ Erreur : $exePath introuvable." -ForegroundColor Red
        exit 1
    }
    
    Copy-Item $exePath "." -Force
    
    # Copier les dépendances si elles existent
    $deps = @(
        "GamingKeypressOverlay.dll",
        "GamingKeypressOverlay.deps.json",
        "GamingKeypressOverlay.runtimeconfig.json"
    )
    
    foreach ($dep in $deps) {
        $depPath = Join-Path $publishDir $dep
        if (Test-Path $depPath) {
            Copy-Item $depPath "." -Force
        }
    }
    
    # Compiler l'installer
    Write-Host "   Compilation de l'installer..." -ForegroundColor Gray
    
    try {
        & $makensisPath $installerScript
        
        if ($LASTEXITCODE -ne 0) {
            throw "Compilation de l'installer échouée avec le code $LASTEXITCODE"
        }
        
        # Trouver le fichier installer créé
        $installerFiles = Get-ChildItem -Filter "KeyboardMouseOverlay_Setup_v*.exe" | Sort-Object LastWriteTime -Descending
        if ($installerFiles) {
            $installerPath = $installerFiles[0].FullName
            Write-Host "✅ Installer créé : $installerPath" -ForegroundColor Green
            Write-Host ""
            
            # Step 4: Signer l'installer (si demandé)
            if ($SignInstaller) {
                Write-Host "🔐 Step 4: Signature de l'installer..." -ForegroundColor Yellow
                
                if ([string]::IsNullOrEmpty($CertPath) -or [string]::IsNullOrEmpty($CertPassword)) {
                    Write-Host "❌ Erreur : CertPath et CertPassword requis pour la signature." -ForegroundColor Red
                    exit 1
                }
                
                try {
                    & "$PSScriptRoot\sign-application.ps1" `
                        -CertPath $CertPath `
                        -Password $CertPassword `
                        -FileToSign $installerPath `
                        -Description "Keyboard & Mouse Overlay Setup"
                    
                    Write-Host "✅ Installer signé" -ForegroundColor Green
                    Write-Host ""
                } catch {
                    Write-Host "❌ Erreur lors de la signature : $_" -ForegroundColor Red
                    exit 1
                }
            }
        } else {
            Write-Host "⚠️  Installer créé mais fichier introuvable" -ForegroundColor Yellow
        }
    } catch {
        Write-Host "❌ Erreur lors de la création de l'installer : $_" -ForegroundColor Red
        exit 1
    }
}

Write-Host "🎉 Build terminé avec succès !" -ForegroundColor Green
Write-Host ""
Write-Host "📋 Résumé :" -ForegroundColor Cyan
Write-Host "   - Application buildée : $publishDir" -ForegroundColor Gray
if ($BuildInstaller) {
    Write-Host "   - Installer créé : KeyboardMouseOverlay_Setup_v*.exe" -ForegroundColor Gray
}
Write-Host ""
Write-Host "💡 Prochaines étapes :" -ForegroundColor Cyan
Write-Host "   - Tester l'application sur une machine propre" -ForegroundColor Gray
if (-not $SignInstaller) {
    Write-Host "   - (Optionnel) Signer l'installer : .\scripts\sign-application.ps1" -ForegroundColor Gray
}
Write-Host "   - Distribuer via GitHub Releases ou autre méthode" -ForegroundColor Gray
Write-Host ""
