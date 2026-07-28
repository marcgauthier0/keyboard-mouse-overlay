# 🛠️ Scripts PowerShell - Gaming Keypress Overlay

Ce dossier contient des scripts PowerShell pour automatiser les tâches de build et distribution.

---

## 📋 Scripts Disponibles

### 1. `unblock-files.ps1` - Débloquer les Fichiers

Supprime le "Mark of the Web" qui cause les alertes SmartScreen.

**Usage** :
```powershell
# Débloquer un fichier
.\scripts\unblock-files.ps1 -Path "GamingKeypressOverlay.exe"

# Débloquer un dossier (récursif)
.\scripts\unblock-files.ps1 -Path "publish" -Recursive

# Mode verbose
.\scripts\unblock-files.ps1 -Path "publish" -Recursive -Verbose
```

**Quand l'utiliser** :
- Après avoir téléchargé l'application depuis Internet
- Avant de distribuer l'application à des testeurs
- Après avoir extrait un ZIP téléchargé

---

### 2. `create-self-signed-cert.ps1` - Créer un Certificat Auto-Signé

Crée un certificat auto-signé pour signature de code (distribution privée uniquement).

**⚠️ IMPORTANT** : Ce certificat fonctionne UNIQUEMENT sur les PCs où il est installé. Ne pas utiliser pour distribution publique.

**Prérequis** :
- PowerShell en tant qu'**administrateur**

**Usage** :
```powershell
# Avec paramètres par défaut
.\scripts\create-self-signed-cert.ps1

# Avec paramètres personnalisés
.\scripts\create-self-signed-cert.ps1 `
    -Subject "CN=Mon Application" `
    -FriendlyName "Mon App Code Signing" `
    -OutputPath "MonCert.pfx" `
    -Password "MonMotDePasse123!" `
    -ValidityYears 3
```

**Résultat** :
- `GamingOverlay_CodeSigning.pfx` : Certificat avec clé privée (pour signer)
- `GamingOverlay_CodeSigning.cer` : Certificat public (pour installer sur autres PCs)

**Prochaines étapes** :
1. Installer le `.cer` sur les PCs testeurs (voir `SMARTSCREEN_SOLUTION.md`)
2. Utiliser `sign-application.ps1` pour signer l'application

---

### 3. `sign-application.ps1` - Signer l'Application

Signe l'application avec un certificat (auto-signé ou commercial).

**Prérequis** :
- Windows SDK installé (contient `signtool.exe`)
- Certificat PFX disponible

**Usage** :
```powershell
# Avec certificat auto-signé
.\scripts\sign-application.ps1 `
    -CertPath "GamingOverlay_CodeSigning.pfx" `
    -Password "MonMotDePasse123!" `
    -FileToSign "GamingKeypressOverlay.exe"

# Avec certificat commercial
.\scripts\sign-application.ps1 `
    -CertPath "C:\Certificates\MyCodeSigning.pfx" `
    -Password "MonMotDePasse123!" `
    -FileToSign "GamingKeypressOverlay_Setup_v1.0.0.exe" `
    -Description "Gaming Keypress Overlay Setup" `
    -TimestampServer "http://timestamp.digicert.com"
```

**Paramètres** :
- `-CertPath` : Chemin vers le certificat PFX (requis)
- `-Password` : Mot de passe du certificat (requis)
- `-FileToSign` : Fichier à signer (défaut : `GamingKeypressOverlay.exe`)
- `-Description` : Description de l'application (défaut : "Gaming Keypress Overlay")
- `-TimestampServer` : Serveur de timestamp (défaut : DigiCert)
- `-SignToolPath` : Chemin vers signtool.exe (auto-détecté si non spécifié)

---

## 🔄 Workflow Complet

### Pour Distribution Privée (Tests)

```powershell
# 1. Build l'application
dotnet publish GamingKeypressOverlay.csproj -c Release -r win-x64 --self-contained

# 2. Débloquer les fichiers
.\scripts\unblock-files.ps1 -Path "bin/Release/net8.0-windows/win-x64/publish" -Recursive

# 3. (Optionnel) Créer et installer certificat auto-signé
.\scripts\create-self-signed-cert.ps1
# Installer le .cer sur les PCs testeurs

# 4. (Optionnel) Signer l'application
.\scripts\sign-application.ps1 -CertPath "GamingOverlay_CodeSigning.pfx" -Password "..."

# 5. Créer l'installer
makensis installer.nsi
```

### Pour Distribution Publique

```powershell
# 1. Build l'application
dotnet publish GamingKeypressOverlay.csproj -c Release -r win-x64 --self-contained

# 2. Créer l'installer
makensis installer.nsi

# 3. Signer l'installer avec certificat commercial
.\scripts\sign-application.ps1 `
    -CertPath "C:\Certificates\CommercialCert.pfx" `
    -Password "..." `
    -FileToSign "GamingKeypressOverlay_Setup_v1.0.0.exe"

# 4. Upload sur GitHub Releases
```

---

## ⚠️ Notes Importantes

### Sécurité

- **Ne jamais commiter** les certificats (.pfx) dans le repo
- Utiliser des **variables d'environnement** pour les mots de passe
- Stocker les certificats dans un **gestionnaire de secrets**

### Certificats Auto-Signés

- Fonctionnent **UNIQUEMENT** sur les PCs où le certificat est installé
- **Ne pas utiliser** pour distribution publique
- Utiles pour **tests internes** uniquement

### Certificats Commerciaux

- **Requis** pour distribution publique
- Coût : ~$200-400/an
- Fournisseurs : DigiCert, Sectigo, GlobalSign

---

## 📚 Documentation

- **[SMARTSCREEN_SOLUTION.md](../docs/SMARTSCREEN_SOLUTION.md)** : Guide complet sur SmartScreen
- **[SIGN_INSTALLER.md](../docs/SIGN_INSTALLER.md)** : Guide détaillé sur la signature
- **[BUILD_INSTALLER.md](../docs/BUILD_INSTALLER.md)** : Guide de build et installation

---

## 🐛 Dépannage

### "Impossible de charger le fichier... car l'exécution de scripts est désactivée"

C'est un problème de **politique d'exécution PowerShell**. Voir **[README_EXECUTION_POLICY.md](README_EXECUTION_POLICY.md)** pour la solution complète.

**Quick Fix** :
```powershell
# Option 1 : Bypass pour un script unique
powershell -ExecutionPolicy Bypass -File .\scripts\sign-application.ps1 -CertPath "..." -Password "..."

# Option 2 : Changer la politique (recommandé)
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser

# Option 3 : Vérifier et corriger automatiquement
powershell -ExecutionPolicy Bypass -File .\scripts\check-execution-policy.ps1 -AutoFix
```

### "signtool.exe introuvable"

Installez **Windows SDK** :
- Télécharger depuis : https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/
- Ou installer via Visual Studio Installer

### "Erreur : Ce script nécessite des privilèges administrateur"

Relancez PowerShell en tant qu'**administrateur** :
1. Clic droit sur PowerShell
2. "Exécuter en tant qu'administrateur"

### "Signature créée mais statut : NotSigned"

Vérifiez :
- Le certificat est valide
- Le mot de passe est correct
- Le serveur de timestamp est accessible

---

**Made for gamers, by gamers** 🎮
