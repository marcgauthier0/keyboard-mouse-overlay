# 🚨 Guide Complet - Résolution SmartScreen / Windows Defender

Ce guide explique pourquoi Windows bloque votre application et comment résoudre le problème.

---

## 🔍 Pourquoi Windows Bloque l'Application

Windows affiche un blocage **sans bouton "Passer quand même"** quand :

1. ✅ L'exécutable vient d'Internet (zone "Mark of the Web")
2. ✅ L'app **n'est pas signée numériquement**
3. ✅ SmartScreen est configuré en **mode blocage dur** (au lieu d'avertir)

Dans ce cas, **le bouton n'apparaît même pas**.

---

## 🚫 « Block executable... prevalence, age or trusted list »

Si Windows affiche :

> **Block executable file from running unless they meet a prevalence, age or trusted list criteria**

c’est en général **Windows Defender Application Control** ou **SmartScreen** en mode blocage : l’app n’a pas de **réputation** (peu de téléchargements), pas d’**ancienneté**, ni de **signature** reconnue.

### ✅ Sur la machine où l’app est bloquée (contournement rapide)

#### 1. Débloquer le fichier (si téléchargé / copié)

1. **Clic droit** sur l’installer ou l’exe
2. **Propriétés**
3. En bas de l’onglet **Général**, cocher **« Débloquer »**
4. **Appliquer** → **OK**

Ou en PowerShell (en tant qu’admin si besoin) :

```powershell
Unblock-File -Path "C:\chemin\vers\GamingKeypressOverlay_Setup_v1.0.0.exe"
```

#### 2. Exclusion Windows Defender (temporaire, pour tester)

1. **Paramètres** → **Confidentialité et sécurité** → **Sécurité Windows** → **Protection contre les virus et menaces**
2. **Gérer les paramètres** (sous « Paramètres de protection contre les virus et menaces »)
3. **Exclusions** → **Ajouter une exclusion** → **Dossier** (ou **Fichier**)
4. Choisir le dossier d’installation (ex. `C:\Program Files\Gaming Keypress Overlay`) ou l’exe directement

#### 3. Lancer depuis un autre emplacement

- Copier l’installer sur une **clé USB**, puis l’exécuter depuis la clé (sans passer par Téléchargements).
- Ou copier dans un dossier local (ex. `C:\Temp\`) puis exécuter.

### ✅ Solution durable : signer l’application

Pour ne plus dépendre des exclusions et du déblocage manuel :

- **Certificat commercial** (DigiCert, Sectigo, etc.) : l’app est considérée comme **trusted** → plus de blocage « prevalence / age / trusted list » sur les PCs normaux.
- Voir `docs/SIGN_INSTALLER.md` pour la signature avec `signtool` ou `osslsigncode`.
- **Microsoft Trusted Signing** (signature cloud) peut aussi convenir selon ton cas.

Résumé : **débloquer + exclusion** = correct pour tests / proches ; **signature de code** = solution propre pour distribuer à d’autres machines.

---

## ✅ SOLUTION IMMÉDIATE (Distribution Privée)

### ➜ Supprimer le marquage "Internet"

C'est **LA solution** pour distribution privée.

#### Méthode 1 — Interface Graphique

1. **Clic droit** sur le `.exe` ou `.zip`
2. **Propriétés**
3. Cocher **"Débloquer"** (en bas de l'onglet Général)
4. **Appliquer** → **OK**

➡️ Ensuite l'exe se lance **sans alerte**.

⚠️ **Important** : Si le fichier est dans un `.zip`, il faut **débloquer le ZIP AVANT d'extraire**.

#### Méthode 2 — PowerShell (Recommandé)

```powershell
# Pour un fichier unique
Unblock-File -Path "GamingKeypressOverlay.exe"

# Pour un dossier entier (récursif)
Get-ChildItem -Path "publish" -Recurse | Unblock-File

# Pour un ZIP
Unblock-File -Path "GamingKeypressOverlay.zip"
```

#### Méthode 3 — Script Automatique

Utilisez le script `scripts/unblock-files.ps1` fourni dans ce projet :

```powershell
.\scripts\unblock-files.ps1 -Path "publish"
```

---

## ✅ SOLUTION PROPRE : Installateur NSIS

Au lieu de distribuer juste un `.exe`, utilisez l'**installateur NSIS** déjà configuré.

### Avantages

- ✅ Moins bloqué par SmartScreen
- ✅ Affiche au moins **"Informations complémentaires → Exécuter quand même"**
- ✅ Installation propre dans Program Files
- ✅ Gestion des mises à jour

### Build de l'Installer

```powershell
# 1. Build l'application
dotnet publish GamingKeypressOverlay.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true

# 2. Copier les fichiers nécessaires
Copy-Item "bin/Release/net8.0-windows/win-x64/publish/GamingKeypressOverlay.exe" "."

# 3. Compiler l'installer
makensis installer.nsi

# Résultat : GamingKeypressOverlay_Setup_v1.0.0.exe
```

Voir `docs/BUILD_INSTALLER.md` pour plus de détails.

---

## ⚠️ IMPORTANT : Éviter le Marquage Internet

### ❌ À ÉVITER

- Télécharger via **navigateur web** → marque Internet automatique
- Lancer depuis **Downloads**
- Envoyer l'exe directement par **mail**

### ✅ PRÉFÉRER

- **Clé USB** (pas de marquage Internet)
- **Dossier réseau interne** (partage local)
- **Archive `.zip` débloquée** avant extraction
- **Installateur NSIS** (moins bloqué)

---

## 🔒 SOLUTION AVANCÉE : Certificat Auto-Signé (Distribution Privée)

Pour une distribution **interne uniquement**, vous pouvez créer un certificat auto-signé.

### ⚠️ Limitations

- Ne fonctionne **QUE** sur les PCs où le certificat est installé
- **Ne marche PAS** pour distribution publique
- Nécessite installation manuelle sur chaque PC testeur

### Étapes

#### 1. Créer le Certificat Auto-Signé

```powershell
# Créer un certificat auto-signé (PowerShell en Admin)
$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject "CN=Gaming Keypress Overlay" `
    -KeyUsage DigitalSignature `
    -FriendlyName "Gaming Keypress Overlay Code Signing" `
    -CertStoreLocation Cert:\CurrentUser\My `
    -KeyExportPolicy Exportable `
    -KeySpec Signature `
    -KeyLength 2048 `
    -KeyAlgorithm RSA `
    -HashAlgorithm SHA256 `
    -ValidityPeriod Years `
    -ValidityPeriodUnits 5

# Exporter en PFX (avec mot de passe)
$password = ConvertTo-SecureString -String "VotreMotDePasse123!" -Force -AsPlainText
Export-PfxCertificate -Cert $cert -FilePath "GamingOverlay_CodeSigning.pfx" -Password $password

Write-Host "Certificat créé : GamingOverlay_CodeSigning.pfx"
Write-Host "Thumbprint : $($cert.Thumbprint)"
```

#### 2. Installer le Certificat sur les PCs Testeurs

Sur **chaque PC testeur**, installer le certificat dans **Autorités de certification approuvées** :

```powershell
# Méthode 1 : Via PowerShell (Admin)
Import-Certificate -FilePath "GamingOverlay_CodeSigning.cer" -CertStoreLocation Cert:\LocalMachine\Root

# Méthode 2 : Interface graphique
# 1. Double-clic sur GamingOverlay_CodeSigning.cer
# 2. "Installer le certificat"
# 3. "Placer tous les certificats dans le magasin suivant"
# 4. "Autorités de certification racines de confiance" → Suivant → Terminer
```

#### 3. Signer l'Application

```powershell
# Trouver signtool.exe (Windows SDK)
$signtool = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe"

# Signer l'exe
& $signtool sign `
    /f "GamingOverlay_CodeSigning.pfx" `
    /p "VotreMotDePasse123!" `
    /t "http://timestamp.digicert.com" `
    /d "Gaming Keypress Overlay" `
    "GamingKeypressOverlay.exe"

# Signer l'installer
& $signtool sign `
    /f "GamingOverlay_CodeSigning.pfx" `
    /p "VotreMotDePasse123!" `
    /t "http://timestamp.digicert.com" `
    /d "Gaming Keypress Overlay Setup" `
    "GamingKeypressOverlay_Setup_v1.0.0.exe"
```

#### 4. Vérifier la Signature

```powershell
# Vérifier la signature
Get-AuthenticodeSignature GamingKeypressOverlay.exe

# Devrait afficher : Status = Valid
```

➡️ **Résultat** : **Aucune alerte** sur les PCs internes où le certificat est installé.

---

## 🔐 SOLUTION PROFESSIONNELLE : Certificat Commercial

Pour une **distribution publique**, il faut un **certificat de signature de code** commercial.

### Fournisseurs Recommandés

- **DigiCert** : https://www.digicert.com/code-signing/
- **Sectigo** : https://sectigo.com/ssl-certificates-tls/code-signing
- **GlobalSign** : https://www.globalsign.com/en/code-signing-certificate

### Prix

- **Standard** : ~$200-400/an
- **EV (Extended Validation)** : ~$400-600/an (plus de confiance)

### Avantages

- ✅ **Aucune alerte** sur tous les PCs Windows
- ✅ **Réputation** auprès de SmartScreen
- ✅ **Aspect professionnel**
- ✅ **Requis** pour certaines distributions (Microsoft Store, etc.)

Voir `docs/SIGN_INSTALLER.md` pour instructions complètes.

---

## 📋 Checklist de Distribution

### Pour Distribution Privée (Tests)

- [ ] Build l'application (`dotnet publish`)
- [ ] Créer l'installer NSIS (`makensis installer.nsi`)
- [ ] **Débloquer les fichiers** (`Unblock-File`)
- [ ] Tester sur machine propre
- [ ] Distribuer via clé USB / réseau interne

### Pour Distribution Publique

- [ ] Build l'application
- [ ] Créer l'installer NSIS
- [ ] **Obtenir certificat commercial**
- [ ] **Signer l'installer** (`signtool`)
- [ ] Tester sur machine propre
- [ ] Upload sur GitHub Releases / site web
- [ ] Calculer checksums SHA256

---

## 🟢 Recommandation Finale

### Pour Maintenant (Distribution Privée)

1. ✅ Utiliser l'**installer NSIS** (déjà configuré)
2. ✅ **Débloquer les fichiers** avant distribution
3. ✅ Distribuer via **clé USB** ou **réseau interne**

### Pour Plus Tard (Distribution Publique)

1. ✅ **Certificat commercial** (DigiCert, Sectigo, etc.)
2. ✅ **Signer l'installer** avec `signtool`
3. ✅ Distribution via **GitHub Releases** ou site web

---

## 🛠️ Scripts Utiles

Ce projet inclut des scripts PowerShell pour automatiser :

- `scripts/unblock-files.ps1` : Débloquer automatiquement les fichiers
- `scripts/create-self-signed-cert.ps1` : Créer un certificat auto-signé
- `scripts/sign-application.ps1` : Signer l'application avec un certificat
- `scripts/check-execution-policy.ps1` : Vérifier/configurer la politique d'exécution PowerShell

**⚠️ Important** : Si PowerShell bloque l'exécution des scripts, voir `scripts/README_EXECUTION_POLICY.md` pour la solution.

**Quick Fix** :
```powershell
# Exécuter avec Bypass
powershell -ExecutionPolicy Bypass -File .\scripts\unblock-files.ps1 -Path "publish" -Recursive
```

Voir le dossier `scripts/` pour plus de détails.

---

## 📚 Ressources

- [Windows SmartScreen](https://docs.microsoft.com/en-us/windows/security/threat-protection/microsoft-defender-smartscreen/microsoft-defender-smartscreen-overview)
- [Code Signing Best Practices](https://docs.microsoft.com/en-us/windows/win32/seccrypto/cryptography-tools)
- [NSIS Documentation](https://nsis.sourceforge.io/Docs/)

---

**Made for gamers, by gamers** 🎮
