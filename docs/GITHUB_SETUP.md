# 🚀 Guide GitHub Setup - Gaming Keypress Overlay

Guide rapide pour mettre le projet sur GitHub.

---

## 📋 Prérequis

- Compte GitHub
- Git installé
- Projet local prêt

---

## 🔧 Step 1: Initialiser Git

```bash
# Dans le dossier du projet
cd GamingKeypressOverlay

# Init git (si pas déjà fait)
git init

# Vérifier .gitignore existe
# (déjà créé dans le projet)
```

---

## 📝 Step 2: Premier Commit

```bash
# Ajouter tous les fichiers
git add .

# Commit initial
git commit -m "Initial commit - Gaming Keypress Overlay v1.1.0

- Raw Input API sur thread dédié
- Flash System + Event Buffer + Visual Latch
- Palette de couleurs entièrement personnalisable en HEX
- IDisposable Pattern complet (v1.1.0)
- Localization FR/EN (v1.1.0)
- Production-ready: crash reporting, télémétrie, fallback mode"
```

---

## 🌐 Step 3: Créer Repository GitHub

1. **Aller sur GitHub.com**
2. **Click "+" → "New repository"**
3. **Repository name**: `GamingKeypressOverlay`
4. **Description**: "Real-time keyboard/mouse overlay for gaming. Optimized for competitive gaming (COD, Fortnite)."
5. **Visibility**: Public (ou Private si tu veux)
6. **NE PAS** cocher "Initialize with README" (on a déjà un README)
7. **Click "Create repository"**

---

## 🔗 Step 4: Connecter Local → GitHub

```bash
# Ajouter remote (remplacer YOUR_USERNAME)
git remote add origin https://github.com/YOUR_USERNAME/GamingKeypressOverlay.git

# Renommer branche en main
git branch -M main

# Push initial
git push -u origin main
```

---

## 📦 Step 5: Créer Première Release

1. **Build release** (voir [DISTRIBUTION.md](DISTRIBUTION.md))
2. **Créer tag** :
   ```bash
   git tag v1.1.0
   git push origin v1.1.0
   ```
3. **Créer release sur GitHub** :
   - Aller sur `https://github.com/YOUR_USERNAME/GamingKeypressOverlay/releases`
   - Click "Create a new release"
   - Tag: `v1.1.0`
   - Title: `v1.1.0 - Memory Management Release`
   - Description: Copier section v1.1.0 du CHANGELOG.md
   - Upload `GamingKeypressOverlay_Setup_v1.1.0.exe`
   - Upload `checksums.txt`

---

## ✅ Checklist Post-Setup

- [ ] Repository créé sur GitHub
- [ ] Code pushé (git push)
- [ ] README visible sur GitHub
- [ ] LICENSE visible
- [ ] .gitignore fonctionne (pas de bin/obj dans repo)
- [ ] Première release créée (v1.1.0)
- [ ] Download link fonctionne

---

## 🔄 Workflow Continu

### Pour chaque nouvelle version :

```bash
# 1. Faire changements
# ... modifier code ...

# 2. Commit
git add .
git commit -m "Description des changements"

# 3. Push
git push origin main

# 4. Créer release (si version majeure)
git tag v1.2.0
git push origin v1.2.0
# Puis créer release sur GitHub avec installer
```

---

## 📚 Fichiers Importants

- **README.md** : Documentation principale (déjà complet ✅)
- **CHANGELOG.md** : Historique des versions
- **LICENSE** : MIT License (déjà présent)
- **.gitignore** : Exclut bin/, obj/, etc.
- **DISTRIBUTION.md** : Guide build et distribution

---

## 🎯 Prochaines Étapes

1. **GitHub Actions** : Setup CI/CD auto-build (voir DISTRIBUTION.md)
2. **Issues Templates** : Déjà créés dans `.github/ISSUE_TEMPLATE/`
3. **Contributing Guide** : Déjà créé (CONTRIBUTING.md)
4. **Screenshots** : Ajouter dans `docs/screenshots/` (optionnel)

---

**Ready to ship!** 🚀
