# 🔒 Résolution : Politique d'Exécution PowerShell

Si vous voyez cette erreur :
```
Impossible de charger le fichier ... car l'exécution de scripts est désactivée sur ce système.
```

C'est normal : Windows bloque l'exécution de scripts PowerShell par défaut pour des raisons de sécurité.

---

## ✅ SOLUTION RAPIDE (Recommandée)

### Méthode 1 : Bypass pour un script unique

Exécutez le script avec `-ExecutionPolicy Bypass` :

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\sign-application.ps1 -CertPath "cert.pfx" -Password "pass"
```

Ou en une ligne :
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\unblock-files.ps1 -Path "publish" -Recursive
```

### Méthode 2 : Changer la politique pour l'utilisateur actuel

```powershell
# Vérifier la politique actuelle
Get-ExecutionPolicy

# Changer pour l'utilisateur actuel (recommandé)
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser

# Vérifier le changement
Get-ExecutionPolicy
```

**Explication** :
- `RemoteSigned` : Permet l'exécution de scripts locaux, mais nécessite une signature pour les scripts téléchargés
- `CurrentUser` : Affecte uniquement votre compte utilisateur (pas besoin d'admin)

---

## 🔧 AUTRES OPTIONS

### Option A : RemoteSigned (Recommandé)

```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

✅ **Avantages** :
- Scripts locaux fonctionnent
- Scripts téléchargés doivent être signés (sécurité)
- Pas besoin de privilèges admin

### Option B : Unrestricted (Moins sécurisé)

```powershell
Set-ExecutionPolicy -ExecutionPolicy Unrestricted -Scope CurrentUser
```

⚠️ **Attention** : Permet tous les scripts, même non signés.

### Option C : Bypass (Temporaire, pour session)

```powershell
Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process
```

✅ **Avantages** :
- Valide uniquement pour la session PowerShell actuelle
- Se réinitialise à la fermeture

---

## 🛠️ Script Helper

Utilisez `scripts/check-execution-policy.ps1` pour vérifier et configurer automatiquement :

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\check-execution-policy.ps1
```

---

## 📋 Vérification

Après avoir changé la politique, vérifiez :

```powershell
Get-ExecutionPolicy -List
```

Vous devriez voir :
```
        Scope ExecutionPolicy
        ----- ---------------
MachinePolicy       Undefined
   UserPolicy       Undefined
      Process       Undefined
  CurrentUser       RemoteSigned
 LocalMachine       Undefined
```

---

## ⚠️ IMPORTANT

- **Ne jamais** utiliser `Set-ExecutionPolicy Unrestricted -Scope LocalMachine` (nécessite admin et affecte tous les utilisateurs)
- **Préférer** `CurrentUser` scope pour éviter les problèmes de permissions
- **RemoteSigned** est le meilleur compromis sécurité/fonctionnalité

---

## 🔗 Références

- [about_Execution_Policies](https://docs.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_execution_policies)
- [Set-ExecutionPolicy](https://docs.microsoft.com/en-us/powershell/module/microsoft.powershell.security/set-executionpolicy)
