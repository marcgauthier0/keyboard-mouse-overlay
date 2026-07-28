# 🚨 Crash Après Installation - Guide de Diagnostic

Si l'application fonctionne en mode portable mais crash après installation via l'installer.

---

## 🔍 Diagnostic Immédiat

### Étape 1 : Vérifier les Logs de Crash

L'application crée automatiquement des logs même si elle crash immédiatement :

```powershell
# Ouvrir le dossier des logs
explorer "$env:LOCALAPPDATA\GamingKeypressOverlay\Logs"

# Voir les fichiers les plus récents
Get-ChildItem "$env:LOCALAPPDATA\GamingKeypressOverlay\Logs" | 
    Sort-Object LastWriteTime -Descending | 
    Select-Object -First 5 | 
    Format-Table Name, LastWriteTime, Length
```

**Fichiers à chercher** :
- `crash_*.txt` : Rapport de crash complet
- `startup_error_*.txt` : Erreur au démarrage
- `app_*.log` : Logs de l'application

### Étape 2 : Tester l'Exe Installé Directement

```powershell
# Lancer l'exe installé directement (pas via le raccourci)
& "C:\Program Files\Gaming Keypress Overlay\GamingKeypressOverlay.exe"
```

Si ça fonctionne directement mais pas via le raccourci → problème avec le raccourci.

---

## 🔧 Causes Probables

### 1. Problème de Permissions (Program Files)

**Symptôme** : Crash immédiat, pas de message d'erreur visible

**Test** :
```powershell
# Vérifier les permissions du dossier
Get-Acl "C:\Program Files\Gaming Keypress Overlay" | Format-List

# Tester l'écriture dans le dossier
Test-Path "C:\Program Files\Gaming Keypress Overlay" -PathType Container
```

**Solution** : L'application ne devrait PAS écrire dans Program Files. Tous les fichiers utilisateur vont dans `%LocalAppData%`.

### 2. Chemin d'Installation avec Espaces

**Symptôme** : Crash si le chemin contient des espaces

**Test** : Vérifier que le chemin d'installation est correct dans l'installer.

**Solution** : L'installer utilise déjà `"$PROGRAMFILES\${APP_NAME}"` qui gère les espaces correctement.

### 3. .NET Runtime Problème

**Symptôme** : Erreur "Could not load file or assembly"

**Test** :
```powershell
# Vérifier .NET depuis l'exe installé
cd "C:\Program Files\Gaming Keypress Overlay"
.\GamingKeypressOverlay.exe
```

**Solution** : L'application est self-contained, donc .NET est inclus. Mais vérifier quand même :
```powershell
dotnet --list-runtimes
```

### 4. Antivirus Bloque l'Exe

**Symptôme** : Crash immédiat, pas de logs créés

**Test** :
```powershell
# Vérifier si Windows Defender bloque
Get-MpPreference | Select-Object -ExpandProperty ExclusionPath

# Vérifier dans Événement Viewer
eventvwr.msc
# Windows Logs → Windows Defender → Operational
```

**Solution** : L'installer propose d'ajouter une exception Windows Defender. Cocher cette option lors de l'installation.

---

## 🛠️ Solutions

### Solution 1 : Vérifier les Logs Immédiatement

Après le crash, vérifie les logs **immédiatement** :

```powershell
# Commande complète pour voir les logs
$logDir = "$env:LOCALAPPDATA\GamingKeypressOverlay\Logs"
if (Test-Path $logDir) {
    Write-Host "Logs trouvés :" -ForegroundColor Green
    Get-ChildItem $logDir | Sort-Object LastWriteTime -Descending | Select-Object -First 5
    Write-Host "`nDernier crash report :" -ForegroundColor Yellow
    $latestCrash = Get-ChildItem "$logDir\crash_*.txt" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($latestCrash) {
        Get-Content $latestCrash.FullName
    }
} else {
    Write-Host "Aucun log trouvé - l'application crash probablement avant de créer les logs" -ForegroundColor Red
}
```

### Solution 2 : Tester en Mode Debug

Lancer l'exe avec sortie console pour voir les erreurs :

```powershell
# Créer un script de test
@"
cd "C:\Program Files\Gaming Keypress Overlay"
.\GamingKeypressOverlay.exe
pause
"@ | Out-File -FilePath "$env:TEMP\test_app.bat" -Encoding ASCII

# Lancer le script
& "$env:TEMP\test_app.bat"
```

### Solution 3 : Comparer avec Mode Portable

```powershell
# 1. Copier l'exe depuis publish vers un dossier simple
Copy-Item "bin\Release\net8.0-windows\win-x64\publish\GamingKeypressOverlay.exe" "C:\Temp\TestApp.exe"

# 2. Lancer depuis C:\Temp
C:\Temp\TestApp.exe

# Si ça fonctionne → problème avec Program Files ou l'installer
# Si ça crash aussi → problème avec l'exe lui-même
```

### Solution 4 : Vérifier l'Événement Viewer Windows

Windows enregistre les crashes système :

1. Ouvrir **Événement Viewer** : `Win+R` → `eventvwr.msc`
2. **Windows Logs** → **Application**
3. Chercher les erreurs récentes avec source "Application Error" ou ".NET Runtime"
4. Filtrer par nom : "GamingKeypressOverlay"

---

## 📋 Checklist de Diagnostic

- [ ] Vérifier les logs dans `%LocalAppData%\GamingKeypressOverlay\Logs\`
- [ ] Lancer l'exe installé directement (pas via raccourci)
- [ ] Comparer avec l'exe en mode portable
- [ ] Vérifier Événement Viewer pour erreurs Windows
- [ ] Vérifier permissions du dossier d'installation
- [ ] Vérifier que Windows Defender n'a pas bloqué l'exe
- [ ] Tester avec l'exe en mode debug (console)

---

## 🆘 Informations à Fournir

Si le problème persiste, fournis :

1. **Contenu du dernier crash report** : `crash_*.txt`
2. **Contenu du dernier log** : `app_*.log`
3. **Résultat de** :
   ```powershell
   Get-Item "C:\Program Files\Gaming Keypress Overlay\GamingKeypressOverlay.exe" | 
       Select-Object FullName, Length, LastWriteTime, VersionInfo
   ```
4. **Résultat de** :
   ```powershell
   Get-Acl "C:\Program Files\Gaming Keypress Overlay" | Format-List
   ```
5. **Messages d'erreur** de l'Événement Viewer

---

## 🔄 Rebuild avec Corrections

Après avoir corrigé le code, rebuild et recréer l'installer :

```powershell
# 1. Build
dotnet publish GamingKeypressOverlay.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# 2. Copier exe
Copy-Item "bin\Release\net8.0-windows\win-x64\publish\GamingKeypressOverlay.exe" "."

# 3. Créer installer
& "C:\Program Files (x86)\NSIS\makensis.exe" installer.nsi

# 4. Débloquer installer
Unblock-File -Path "GamingKeypressOverlay_Setup_v1.0.0.exe"

# 5. Désinstaller l'ancienne version
# Via "Ajouter/Supprimer des programmes"

# 6. Réinstaller
.\GamingKeypressOverlay_Setup_v1.0.0.exe
```

---

**Made for gamers, by gamers** 🎮
