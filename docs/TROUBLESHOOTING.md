# 🔧 Guide de Dépannage - Gaming Keypress Overlay

Guide pour résoudre les problèmes courants après installation.

---

## 🚨 Application Crash au Démarrage

### Étape 1 : Vérifier les Logs de Crash

L'application enregistre automatiquement les crashs dans :

```
%LocalAppData%\GamingKeypressOverlay\Logs\
```

**Chemin complet** :
- Windows 10/11 : `C:\Users\TON_NOM\AppData\Local\GamingKeypressOverlay\Logs\`

**Fichiers à vérifier** :
- `crash_YYYY-MM-DD_HH-mm-ss.txt` : Rapport de crash détaillé
- `app_YYYY-MM-DD.log` : Logs de l'application

### Étape 2 : Ouvrir les Logs

```powershell
# Ouvrir le dossier des logs
explorer "$env:LOCALAPPDATA\GamingKeypressOverlay\Logs"

# Ou via PowerShell
Get-ChildItem "$env:LOCALAPPDATA\GamingKeypressOverlay\Logs" | Sort-Object LastWriteTime -Descending | Select-Object -First 5
```

### Étape 3 : Analyser le Crash Report

Le fichier `crash_*.txt` contient :
- **Type d'exception** : Quel type d'erreur
- **Message** : Description de l'erreur
- **Stack Trace** : Où le crash s'est produit
- **System Info** : OS, .NET version, etc.

---

## 🔍 Causes Courantes

### 1. Problème de Permissions (Program Files)

**Symptôme** : Crash immédiat au démarrage, erreur d'accès refusé

**Solution** :
- L'application ne devrait PAS nécessiter d'admin pour fonctionner
- Vérifier que le dossier d'installation n'est pas en lecture seule
- Essayer de lancer l'exe directement depuis `Program Files`

### 2. .NET Runtime Manquant ou Incompatible

**Symptôme** : Erreur "Could not load file or assembly" ou "Framework not found"

**Solution** :
```powershell
# Vérifier .NET 8.0
dotnet --list-runtimes

# Devrait afficher : Microsoft.WindowsDesktop.App 8.0.x
# Si manquant, télécharger depuis : https://dotnet.microsoft.com/download/dotnet/8.0
```

### 3. Raw Input API Non Disponible

**Symptôme** : Crash avec "Failed to initialize Raw Input"

**Causes possibles** :
- Windows version trop ancienne (< Windows 10)
- Permissions insuffisantes
- Conflit avec un autre logiciel

**Solution** :
- Vérifier Windows 10+ (64-bit)
- Lancer en tant qu'administrateur (temporaire, pour test)
- Désactiver temporairement antivirus/anti-cheat

### 4. Problème avec les Settings

**Symptôme** : Crash lors du chargement des paramètres sauvegardés

**Solution** :
```powershell
# Supprimer les settings corrompus
Remove-Item "$env:LOCALAPPDATA\GamingKeypressOverlay\settings.json" -ErrorAction SilentlyContinue

# Relancer l'application
```

### 5. Conflit avec Antivirus/Anti-Cheat

**Symptôme** : Application bloquée ou crash immédiat

**Solution** :
- Ajouter exception dans Windows Defender (fait automatiquement par l'installer si option sélectionnée)
- Vérifier que EAC/BattlEye ne bloque pas l'application
- Désactiver temporairement pour tester

---

## 🛠️ Solutions Rapides

### Solution 1 : Mode Portable (Test)

Au lieu d'utiliser l'installer, tester avec l'exe directement :

```powershell
# Copier l'exe depuis publish
Copy-Item "bin\Release\net8.0-windows\win-x64\publish\GamingKeypressOverlay.exe" "C:\Temp\"

# Lancer depuis C:\Temp (pas Program Files)
C:\Temp\GamingKeypressOverlay.exe
```

Si ça fonctionne en portable mais pas après installation → problème de permissions/chemin.

### Solution 2 : Lancer en Mode Debug

```powershell
# Lancer avec sortie console pour voir les erreurs
Start-Process "C:\Program Files\Gaming Keypress Overlay\GamingKeypressOverlay.exe" -NoNewWindow
```

### Solution 3 : Vérifier l'Événement Viewer

Windows enregistre les crashes dans l'Événement Viewer :

1. Ouvrir **Événement Viewer** (`eventvwr.msc`)
2. **Windows Logs** → **Application**
3. Chercher les erreurs récentes pour "GamingKeypressOverlay"

---

## 📋 Checklist de Diagnostic

- [ ] Vérifier les logs dans `%LocalAppData%\GamingKeypressOverlay\Logs\`
- [ ] Vérifier .NET 8.0 Runtime installé (`dotnet --list-runtimes`)
- [ ] Vérifier Windows 10+ (64-bit)
- [ ] Tester en mode portable (exe directement)
- [ ] Vérifier permissions du dossier d'installation
- [ ] Vérifier Événement Viewer pour erreurs Windows
- [ ] Désactiver temporairement antivirus
- [ ] Supprimer settings.json et relancer

---

## 🆘 Si Rien ne Fonctionne

### Informations à Fournir

Si tu demandes de l'aide, fournis :

1. **Crash Report** : Contenu de `crash_*.txt`
2. **Logs** : Contenu de `app_*.log`
3. **Windows Version** : `winver` dans CMD
4. **.NET Version** : `dotnet --version`
5. **Chemin d'installation** : Où l'application est installée
6. **Message d'erreur** : Message exact affiché (si visible)

### Commandes Utiles

```powershell
# Informations système complètes
systeminfo | Select-String "OS Name", "OS Version", "System Type"

# Version .NET
dotnet --version
dotnet --list-runtimes

# Vérifier l'exe
Get-Item "C:\Program Files\Gaming Keypress Overlay\GamingKeypressOverlay.exe" | Select-Object VersionInfo, Length, LastWriteTime

# Vérifier les logs
Get-ChildItem "$env:LOCALAPPDATA\GamingKeypressOverlay\Logs" | Sort-Object LastWriteTime -Descending | Select-Object -First 3
```

---

## 🔄 Rebuild et Réinstaller

Si le problème persiste après correction :

```powershell
# 1. Désinstaller complètement
# Via "Ajouter/Supprimer des programmes"

# 2. Supprimer les données utilisateur (optionnel)
Remove-Item "$env:LOCALAPPDATA\GamingKeypressOverlay" -Recurse -Force

# 3. Rebuild
dotnet publish GamingKeypressOverlay.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# 4. Recréer l'installer
Copy-Item "bin\Release\net8.0-windows\win-x64\publish\GamingKeypressOverlay.exe" "."
& "C:\Program Files (x86)\NSIS\makensis.exe" installer.nsi

# 5. Réinstaller
.\GamingKeypressOverlay_Setup_v1.0.0.exe
```

---

**Made for gamers, by gamers** 🎮
