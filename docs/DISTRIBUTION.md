# 📦 Guide de Distribution - Gaming Keypress Overlay

Guide complet pour distribuer l'application via GitHub Releases.

> ⚠️ **Problème SmartScreen ?** Si Windows bloque votre application sans bouton "Passer quand même", consultez le guide **[SMARTSCREEN_SOLUTION.md](SMARTSCREEN_SOLUTION.md)** pour les solutions.

---

## 🚀 Build Release

### Step 1: Build Self-Contained Executable

```powershell
# Build self-contained (includes .NET runtime)
dotnet publish GamingKeypressOverlay.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true

# Output: bin/Release/net8.0-windows/win-x64/publish/GamingKeypressOverlay.exe
```

**Options** :
- `--self-contained true` : Inclut le runtime .NET (exe standalone)
- `-p:PublishSingleFile=true` : Un seul fichier .exe
- `-p:EnableCompressionInSingleFile=true` : Compression pour réduire la taille

### Step 2: Compiler Installer NSIS

```powershell
# Copier .exe vers installer/
Copy-Item "bin/Release/net8.0-windows/win-x64/publish/GamingKeypressOverlay.exe" "installer/"

# Compiler installer
cd installer
makensis installer.nsi

# Output: GamingKeypressOverlay_Setup_v1.1.0.exe
```

**Prérequis** : Installer [NSIS](https://nsis.sourceforge.io/Download)

---

## 📥 GitHub Releases

### Step 1: Créer Release Manuellement

1. **Aller sur GitHub** : `https://github.com/YOUR_USERNAME/GamingKeypressOverlay/releases`
2. **Click "Create a new release"**
3. **Tag** : `v1.1.0` (créer nouveau tag)
4. **Title** : `v1.1.0 - Memory Management Release`
5. **Description** : Copier section v1.1.0 du CHANGELOG.md
6. **Upload files** :
   - `GamingKeypressOverlay_Setup_v1.1.0.exe` (installer, ~15 MB)
   - `GamingKeypressOverlay.exe` (standalone, optionnel, ~50 MB)
   - `checksums.txt` (voir ci-dessous)

### Step 2: Créer Checksums

```powershell
# Windows PowerShell
Get-FileHash GamingKeypressOverlay_Setup_v1.1.0.exe -Algorithm SHA256 | Format-List
Get-FileHash GamingKeypressOverlay.exe -Algorithm SHA256 | Format-List

# Créer fichier checksums.txt
@"
SHA256 (GamingKeypressOverlay_Setup_v1.1.0.exe) = abc123def456...
SHA256 (GamingKeypressOverlay.exe) = def456abc123...
"@ | Out-File checksums.txt -Encoding UTF8
```

**Vérification utilisateur** :
```powershell
Get-FileHash GamingKeypressOverlay_Setup_v1.1.0.exe -Algorithm SHA256
```

---

## 🤖 GitHub Actions (Auto-Build)

Créer `.github/workflows/release.yml` :

```yaml
name: Build and Release

on:
  push:
    tags:
      - 'v*.*.*'

jobs:
  build:
    runs-on: windows-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'
    
    - name: Build
      run: |
        dotnet publish GamingKeypressOverlay.csproj `
          -c Release `
          -r win-x64 `
          --self-contained true `
          -p:PublishSingleFile=true `
          -p:IncludeNativeLibrariesForSelfExtract=true `
          -p:EnableCompressionInSingleFile=true
    
    - name: Setup NSIS
      run: |
        choco install nsis -y
    
    - name: Build Installer
      run: |
        Copy-Item "bin/Release/net8.0-windows/win-x64/publish/GamingKeypressOverlay.exe" "installer/"
        cd installer
        makensis installer.nsi
    
    - name: Calculate Checksums
      run: |
        Get-FileHash installer/GamingKeypressOverlay_Setup_v*.exe -Algorithm SHA256 | `
          Select-Object Hash,Path | Out-File checksums.txt
    
    - name: Create Release
      uses: softprops/action-gh-release@v1
      with:
        files: |
          installer/GamingKeypressOverlay_Setup_v*.exe
          checksums.txt
        body: |
          See [CHANGELOG.md](CHANGELOG.md) for details.
      env:
        GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

**Usage** :
```bash
# Tag version
git tag v1.1.0
git push origin v1.1.0

# GitHub Actions auto-build et crée la release!
```

---

## 📋 Checklist Distribution

### Avant Release
- [ ] Build Release (`dotnet publish`)
- [ ] Compiler installer NSIS
- [ ] Tester installer sur machine propre
- [ ] Calculer checksums SHA256
- [ ] Mettre à jour CHANGELOG.md
- [ ] Créer tag Git (`git tag v1.1.0`)

### Créer Release
- [ ] Créer release sur GitHub
- [ ] Upload installer .exe
- [ ] Upload checksums.txt
- [ ] Ajouter description (CHANGELOG)
- [ ] Marquer comme "Latest release"

### Après Release
- [ ] Tester download depuis GitHub
- [ ] Vérifier checksums
- [ ] Mettre à jour README avec lien download
- [ ] Annoncer sur social media (optionnel)

---

## 🔗 Alternatives d'Hébergement

### Option A: Google Drive / OneDrive
- Upload `GamingKeypressOverlay_Setup_v1.1.0.exe`
- Get shareable link
- Mettre lien dans README

### Option B: SourceForge
- Créer projet sur SourceForge
- Upload releases
- URLs stables pour téléchargements

### Option C: Self-Hosted
- Upload sur serveur personnel
- Direct download link
- Contrôle total

---

## 📊 Taille des Fichiers

**Avec .NET Runtime (Self-Contained)** :
- `GamingKeypressOverlay.exe` : ~50-60 MB
- `GamingKeypressOverlay_Setup_v1.1.0.exe` : ~15-20 MB (installer)

**Sans .NET Runtime (Framework-Dependent)** :
- `GamingKeypressOverlay.exe` : ~200 KB
- Nécessite .NET 8.0 Runtime installé

**Recommandation** : Utiliser self-contained pour distribution (pas besoin d'installer .NET séparément).

---

## ✅ Vérification Post-Release

1. **Tester download** : Télécharger depuis GitHub Releases
2. **Vérifier checksum** : `Get-FileHash file.exe -Algorithm SHA256`
3. **Installer** : Tester installation complète
4. **Lancer app** : Vérifier fonctionnement
5. **Vérifier langue** : Tester FR/EN

---

**Made for gamers, by gamers** 🎮
