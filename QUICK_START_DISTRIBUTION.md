# 🚀 Guide Rapide - Créer un Installer pour Tes Amis

Guide simple pour créer un installer que tu peux donner à tes amis.

---

## ✅ Étape 1 : Installer NSIS (une seule fois)

1. Télécharge NSIS : https://nsis.sourceforge.io/Download
2. Installe-le (garde les options par défaut)
3. C'est tout ! NSIS sera ajouté au PATH automatiquement

---

## ✅ Étape 2 : Build l'Application

```powershell
# Build en mode Release (self-contained = tout inclus)
dotnet publish GamingKeypressOverlay.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

Les fichiers seront dans : `bin\Release\net8.0-windows\win-x64\publish\`

---

## ✅ Étape 3 : Créer l'Installer

### Option A : Script Automatique (Recommandé)

```powershell
# Build + créer installer en une commande
powershell -ExecutionPolicy Bypass -File .\scripts\build-release.ps1
```

### Option B : Manuel

```powershell
# 1. Copier l'exe à la racine du projet
Copy-Item "bin\Release\net8.0-windows\win-x64\publish\GamingKeypressOverlay.exe" "."

# 2. Créer l'installer
makensis installer.nsi
```

**Résultat** : `GamingKeypressOverlay_Setup_v1.0.0.exe` sera créé à la racine du projet.

---

## ✅ Étape 4 : Débloquer l'Installer (Important pour SmartScreen)

```powershell
# Débloquer l'installer pour éviter SmartScreen
Unblock-File -Path "GamingKeypressOverlay_Setup_v1.0.0.exe"
```

---

## 📦 Étape 5 : Distribuer

Tu peux maintenant donner **`GamingKeypressOverlay_Setup_v1.0.0.exe`** à tes amis !

### Méthodes de Distribution

1. **Clé USB** (meilleur, pas de SmartScreen)
2. **Google Drive / OneDrive** (partage de fichier)
3. **Email** (mais débloquer le fichier avant)
4. **Réseau local** (partage de dossier)

---

## ⚠️ Important pour Tes Amis

### Si Windows Bloque l'Installer (SmartScreen)

Tes amis devront :

1. **Clic droit** sur `GamingKeypressOverlay_Setup_v1.0.0.exe`
2. **Propriétés**
3. Cocher **"Débloquer"** (en bas)
4. **Appliquer** → **OK**
5. Lancer l'installer

Ou en PowerShell :
```powershell
Unblock-File -Path "GamingKeypressOverlay_Setup_v1.0.0.exe"
```

### Si le message dit « Block... prevalence, age or trusted list »

Windows bloque **sans** proposer « Exécuter quand même ». Sur la **machine où c’est bloqué** :

1. **Débloquer** le fichier (Propriétés → Débloquer), puis réessayer.
2. Si ça bloque encore : **Paramètres** → **Sécurité Windows** → **Protection contre les virus** → **Exclusions** → ajouter le dossier d’installation ou l’exe.
3. Ou copier l’installer sur une **clé USB** et l’exécuter depuis la clé.

Pour une **solution durable** (distribution à d’autres PCs) : **signer** l’installer avec un certificat de code (voir `docs/SMARTSCREEN_SOLUTION.md` et `docs/SIGN_INSTALLER.md`).

---

## 🎯 Workflow Complet (Copier-Coller)

```powershell
# 1. Build
dotnet publish GamingKeypressOverlay.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# 2. Copier l'exe
Copy-Item "bin\Release\net8.0-windows\win-x64\publish\GamingKeypressOverlay.exe" "."

# 3. Créer l'installer
makensis installer.nsi

# 4. Débloquer l'installer
Unblock-File -Path "GamingKeypressOverlay_Setup_v1.0.0.exe"

# 5. C'est prêt ! Le fichier GamingKeypressOverlay_Setup_v1.0.0.exe est prêt à distribuer
```

---

## 📝 Notes

- L'installer vérifie automatiquement si .NET 8.0 est installé
- Si .NET n'est pas installé, l'installer propose de le télécharger
- L'installer installe dans `Program Files` (propre)
- L'installer crée un raccourci dans le menu Démarrer
- L'application peut être désinstallée via "Ajouter/Supprimer des programmes"

---

## 🔧 Personnaliser la Version

Si tu veux changer la version de l'installer, modifie la ligne 5 dans `installer.nsi` :

```nsis
!define APP_VERSION "1.0.0"  ← Change ici
```

Puis recompile l'installer avec `makensis installer.nsi`

---

**C'est tout ! Tu as maintenant un installer professionnel à distribuer** 🎮
