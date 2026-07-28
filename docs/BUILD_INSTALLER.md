# 🚀 Guide de Build et Installation

## Build de l'Application

### Build Release
```bash
dotnet publish GamingKeypressOverlay.csproj -c Release -r win-x64 --self-contained
```

Les fichiers seront dans : `bin/Release/net8.0-windows/win-x64/publish/`

## Création de l'Installer NSIS

### Prérequis
1. Installer [NSIS](https://nsis.sourceforge.io/Download) (Nullsoft Scriptable Install System)
2. Ajouter NSIS au PATH ou utiliser le chemin complet

### Build de l'Installer

1. **Copier les fichiers nécessaires** :
   ```bash
   # Depuis le dossier publish
   copy GamingKeypressOverlay.exe .
   copy GamingKeypressOverlay.dll .
   copy GamingKeypressOverlay.deps.json .
   copy GamingKeypressOverlay.runtimeconfig.json .
   copy README.md README.txt  # Si vous voulez inclure README
   ```

2. **Compiler le script NSIS** :
   ```bash
   makensis installer.nsi
   ```

   Ou avec chemin complet :
   ```bash
   "C:\Program Files (x86)\NSIS\makensis.exe" installer.nsi
   ```

3. **Résultat** : `GamingKeypressOverlay_Setup_v1.0.0.exe` sera créé

### Vérifications Automatiques

L'installer vérifie automatiquement :
- ✅ **Windows 10+** (requis pour Raw Input API)
- ✅ **64-bit OS** (requis pour .NET 8.0)
- ✅ **.NET 8.0 Runtime** installé
- ✅ **Application déjà installée** (gère upgrade/downgrade)
- ✅ **Application en cours d'exécution** (propose de fermer)

### Personnalisation

- **Icône** : Modifier `MUI_ICON` et `MUI_UNICON` dans `installer.nsi`
- **Version** : Modifier `APP_VERSION` dans `installer.nsi`
- **License** : Créer `LICENSE.txt` et décommenter la ligne dans `installer.nsi`

### Signer l'Installer (Recommandé)

Voir `SIGN_INSTALLER.md` pour instructions complètes.

**Quick Start** :
```bash
signtool sign /f certificate.pfx /p password /t http://timestamp.digicert.com GamingKeypressOverlay_Setup_v1.0.0.exe
```

### Débloquer les Fichiers (SmartScreen)

Si Windows bloque l'application avec SmartScreen, voir **[SMARTSCREEN_SOLUTION.md](SMARTSCREEN_SOLUTION.md)**.

**Quick Fix** :
```powershell
# Débloquer les fichiers après build
.\scripts\unblock-files.ps1 -Path "bin/Release/net8.0-windows/win-x64/publish" -Recursive
```

## Tests Unitaires

### Exécuter les Tests
```bash
dotnet test GamingKeypressOverlay.Tests/GamingKeypressOverlay.Tests.csproj
```

### Avec Coverage
```bash
dotnet test --collect:"XPlat Code Coverage"
```

## Distribution

### Fichiers à distribuer
- `GamingKeypressOverlay_Setup_v1.0.0.exe` (installer signé)
- `README.md` (documentation)

### Checklist Production
- [ ] Version mise à jour dans `installer.nsi`
- [ ] Tests unitaires passent
- [ ] Build Release testé
- [ ] Installer testé (install + uninstall)
- [ ] Installer testé sur Windows 10/11
- [ ] Installer testé sans .NET (vérifie bien le message)
- [ ] Installer testé avec version déjà installée (upgrade)
- [ ] Installer signé (voir `SIGN_INSTALLER.md`)
- [ ] README à jour
- [ ] Logs d'installation vérifiés (si problème)

### Test de l'Installer

**Scénarios à tester** :
1. ✅ Installation fraîche (Windows 10/11, .NET installé)
2. ✅ Installation sans .NET (doit proposer téléchargement)
3. ✅ Installation sur Windows 8 (doit refuser)
4. ✅ Upgrade depuis version précédente
5. ✅ Downgrade vers version antérieure
6. ✅ Installation avec app déjà running (doit proposer fermeture)
7. ✅ Uninstall complet
8. ✅ Uninstall avec conservation des settings
9. ✅ Uninstall avec suppression des settings
