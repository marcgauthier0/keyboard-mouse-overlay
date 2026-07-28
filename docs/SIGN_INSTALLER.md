# 🔐 Signer l'Installer (Post-Build)

## Pourquoi Signer ?

La signature digitale de l'installer :
- ✅ Élimine les avertissements "Unknown Publisher" de Windows
- ✅ Améliore la confiance des utilisateurs
- ✅ Requis pour certaines distributions (Microsoft Store, etc.)
- ✅ Réduit les faux positifs antivirus

## Méthode 1 : signtool.exe (Windows SDK)

### Prérequis
1. **Windows SDK** installé (contient `signtool.exe`)
2. **Certificat de code signing** (.pfx ou .p12)

### Commande
```bash
signtool sign /f certificate.pfx /p password /t http://timestamp.digicert.com GamingKeypressOverlay_Setup_v1.0.0.exe
```

### Options
- `/f` : Chemin vers le certificat
- `/p` : Mot de passe du certificat
- `/t` : URL du serveur de timestamp (DigiCert, Sectigo, etc.)
- `/d` : Description (optionnel)
- `/du` : URL de description (optionnel)

### Exemple Complet
```bash
signtool sign ^
    /f "C:\Certificates\MyCodeSigning.pfx" ^
    /p "MyPassword123" ^
    /t "http://timestamp.digicert.com" ^
    /d "Gaming Keypress Overlay" ^
    /du "https://github.com/yourusername/gaming-overlay" ^
    "GamingKeypressOverlay_Setup_v1.0.0.exe"
```

## Méthode 2 : osslsigncode (Cross-Platform)

### Installation
```bash
# Windows (via Chocolatey)
choco install osslsigncode

# Linux/Mac
# Download from: https://github.com/mtrojnar/osslsigncode
```

### Commande
```bash
osslsigncode sign ^
    -pkcs12 certificate.pfx ^
    -pass password ^
    -t http://timestamp.digicert.com ^
    -in GamingKeypressOverlay_Setup_v1.0.0.exe ^
    -out GamingKeypressOverlay_Setup_v1.0.0_Signed.exe
```

## Méthode 3 : Auto-Sign dans NSIS (Avancé)

Ajouter dans `installer.nsi` après compilation :

```nsis
!ifdef SIGN_TOOL
    !system '"${SIGN_TOOL}" sign /f "${CERT_FILE}" /p "${CERT_PASS}" /t http://timestamp.digicert.com "${OUT_FILE}"'
!endif
```

Puis compiler avec :
```bash
makensis /DSIGN_TOOL="C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe" /DCERT_FILE="cert.pfx" /DCERT_PASS="password" installer.nsi
```

## Obtenir un Certificat de Code Signing

### Options

1. **Certificat Commercial** (Recommandé pour production)
   - **DigiCert** : https://www.digicert.com/code-signing/
   - **Sectigo** : https://sectigo.com/ssl-certificates-tls/code-signing
   - **GlobalSign** : https://www.globalsign.com/en/code-signing-certificate
   - **Prix** : ~$200-400/an

2. **Certificat Auto-Signé** (Développement uniquement)
   - Ne sera pas reconnu par Windows par défaut
   - Utile pour tests internes
   - **Gratuit** mais nécessite configuration manuelle

### Créer un Certificat Auto-Signé (Test)
```bash
# Créer un certificat auto-signé
makecert -r -pe -n "CN=Gaming Overlay" -ss MY -len 2048 -sv GamingOverlay.pvk GamingOverlay.cer

# Convertir en PFX
pvk2pfx -pvk GamingOverlay.pvk -spc GamingOverlay.cer -pfx GamingOverlay.pfx -po password
```

## Vérifier la Signature

```bash
signtool verify /pa GamingKeypressOverlay_Setup_v1.0.0.exe
```

Ou via PowerShell :
```powershell
Get-AuthenticodeSignature GamingKeypressOverlay_Setup_v1.0.0.exe
```

## Notes Importantes

⚠️ **Sécurité** :
- Ne jamais commiter le certificat (.pfx) dans le repo
- Utiliser variables d'environnement pour le mot de passe
- Stocker le certificat dans un gestionnaire de secrets

⚠️ **Timestamp** :
- Toujours utiliser un serveur de timestamp
- Permet de signer une fois, valide même après expiration du certificat
- Serveurs recommandés :
  - `http://timestamp.digicert.com`
  - `http://timestamp.sectigo.com`
  - `http://timestamp.globalsign.com/tsa/r6advanced1`

## Intégration CI/CD

### GitHub Actions
```yaml
- name: Sign Installer
  run: |
    signtool sign /f ${{ secrets.CERT_FILE }} /p ${{ secrets.CERT_PASS }} /t http://timestamp.digicert.com GamingKeypressOverlay_Setup_v1.0.0.exe
  env:
    CERT_FILE: ${{ secrets.CODE_SIGNING_CERT }}
    CERT_PASS: ${{ secrets.CODE_SIGNING_PASSWORD }}
```

### Azure DevOps
```yaml
- task: CodeSigning@3
  inputs:
    signToolPath: 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe'
    files: 'GamingKeypressOverlay_Setup_v1.0.0.exe'
    certFile: '$(Build.SourcesDirectory)\cert.pfx'
    certPassword: '$(CodeSigningPassword)'
    timestampServer: 'http://timestamp.digicert.com'
```
