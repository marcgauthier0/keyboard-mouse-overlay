# Script pour debloquer les fichiers telecharges depuis Internet
# Supprime le "Mark of the Web" qui cause les alertes SmartScreen

param(
    [Parameter(Mandatory=$true)]
    [string]$Path,
    
    [switch]$Recursive,
    
    [switch]$DetailedOutput
)

Write-Host "Deblocage des fichiers..." -ForegroundColor Cyan

if (-not (Test-Path $Path)) {
    Write-Host "Erreur : Le chemin '$Path' n'existe pas." -ForegroundColor Red
    exit 1
}

$item = Get-Item $Path

if ($item.PSIsContainer) {
    # C'est un dossier
    Write-Host "Dossier detecte : $Path" -ForegroundColor Yellow
    
    if ($Recursive) {
        $files = Get-ChildItem -Path $Path -Recurse -File
        Write-Host "   Recherche recursive activee..." -ForegroundColor Gray
    } else {
        $files = Get-ChildItem -Path $Path -File
    }
    
    $count = 0
    foreach ($file in $files) {
        try {
            Unblock-File -Path $file.FullName -ErrorAction Stop
            $count++
            if ($DetailedOutput) {
                Write-Host "   Debloque : $($file.Name)" -ForegroundColor Green
            }
        } catch {
            Write-Host "   Erreur sur $($file.Name) : $_" -ForegroundColor Yellow
        }
    }
    
    Write-Host "OK : $count fichier(s) debloque(s)" -ForegroundColor Green
    
} else {
    # C'est un fichier
    Write-Host "Fichier detecte : $($item.Name)" -ForegroundColor Yellow
    
    try {
        Unblock-File -Path $Path -ErrorAction Stop
        Write-Host "Fichier debloque avec succes" -ForegroundColor Green
    } catch {
        Write-Host "Erreur : $_" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "Astuce : Les fichiers peuvent maintenant etre lances sans alerte SmartScreen." -ForegroundColor Cyan
