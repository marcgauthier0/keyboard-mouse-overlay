# 🤝 Contributing to Gaming Keypress Overlay

Merci de votre intérêt pour contribuer au projet ! Ce document décrit les guidelines pour contribuer.

## 🚀 Quick Start

1. **Fork le projet** sur GitHub
2. **Clone votre fork** :
   ```bash
   git clone https://github.com/votre-username/keyboard_overlay_windows.git
   cd keyboard_overlay_windows
   ```
3. **Créer une branche** :
   ```bash
   git checkout -b feature/amazing-feature
   ```
4. **Faire vos modifications**
5. **Commit** :
   ```bash
   git commit -m 'Add amazing feature'
   ```
6. **Push** :
   ```bash
   git push origin feature/amazing-feature
   ```
7. **Créer une Pull Request** sur GitHub

## 📋 Guidelines

### Code Style

- **C# Conventions** :
  - PascalCase pour méthodes publiques : `CreateSnapshot()`
  - `_camelCase` pour champs privés : `_inputState`
  - camelCase pour variables locales : `currentTimestamp`
  
- **Commentaires** :
  - En français ou anglais (consistant dans un fichier)
  - Commenter code complexe, surtout unsafe
  - Justifier unsafe code : `// CRITICAL: Direct pointer access for zero-allocation`
  
- **Naming** :
  - Noms explicites : `inputState` pas `is`, `currentTimestamp` pas `ts`
  - Préfixer booléens : `isKeyPressed`, `hasError`

### Tests Requis

**Toutes nouvelles features doivent inclure des tests unitaires.**

```csharp
// Exemple: GamingKeypressOverlay.Tests/NewFeatureTests.cs
[Test]
public void NewFeature_ShouldWork()
{
    // Arrange
    var feature = new NewFeature();
    
    // Act
    var result = feature.DoSomething();
    
    // Assert
    Assert.IsTrue(result);
}
```

**Tests existants** :
- `InputStateTests.cs` : Tests InputState (SetKey, GetKey, EventBuffer, etc.)
- `AdvancedSettingsTests.cs` : Tests validation settings

**Run tests** :
```bash
dotnet test GamingKeypressOverlay.Tests/GamingKeypressOverlay.Tests.csproj
```

### Logging

**Utiliser `CrashReporter` pour tous les logs** :

```csharp
// ✅ Correct
CrashReporter.LogInfo("Feature initialized");
CrashReporter.LogWarning("Low memory detected");
CrashReporter.LogError("Failed to initialize", exception);

// ❌ Incorrect
Console.WriteLine("Feature initialized"); // Pas de logging structuré
Debug.WriteLine("Feature initialized"); // Pas capturé en production
```

**Niveaux de log** :
- `LogInfo` : Informations normales (initialisation, config)
- `LogWarning` : Situations suspectes mais non-critiques (fallback mode)
- `LogError` : Erreurs récupérables (try-catch)
- `LogCritical` : Erreurs critiques (crash imminent)

### Error Handling

**Toutes méthodes critiques doivent avoir try-catch** :

```csharp
// ✅ Correct
public void ProcessInput()
{
    try
    {
        // Code critique
        unsafe { /* ... */ }
    }
    catch (Exception ex)
    {
        CrashReporter.LogError("Failed to process input", ex);
        // Fail gracefully: continue ou fallback
    }
}

// ❌ Incorrect
public void ProcessInput()
{
    // Pas de try-catch = crash possible
    unsafe { /* ... */ }
}
```

**Validation des paramètres** :

```csharp
// ✅ Correct
public void SetKey(byte vkey, bool pressed)
{
    if (vkey > 255)
        throw new ArgumentOutOfRangeException(nameof(vkey));
    
    // ...
}

// ❌ Incorrect
public void SetKey(byte vkey, bool pressed)
{
    // Pas de validation = buffer overflow possible
    _keys[vkey] = pressed;
}
```

### Thread Safety

**Utiliser Volatile pour state lock-free** :

```csharp
// ✅ Correct
private bool _isRunning;
public bool IsRunning => Volatile.Read(ref _isRunning);

public void SetRunning(bool value)
{
    Volatile.Write(ref _isRunning, value);
}
```

**Utiliser locks pour opérations atomiques** :

```csharp
// ✅ Correct
private readonly object _snapshotLock = new object();

public InputStateSnapshot CreateSnapshot()
{
    lock (_snapshotLock)
    {
        // Copy atomic de tout l'état
        return new InputStateSnapshot(this);
    }
}
```

**Double-check pour cleanup** :

```csharp
// ✅ Correct (évite race condition)
bool isKeyPressed = Volatile.Read(ref keys[i]);
if (age > maxAgeTicks && !isKeyPressed && !currentKeyStates[i])
{
    long ts2 = Volatile.Read(ref timestamps[i]);
    if (ts2 == ts) // Timestamp unchanged = safe to clean
        Volatile.Write(ref timestamps[i], 0);
}
```

### Performance

**Mesurer impact avant/après** :

```csharp
// Utiliser PerformanceMetrics pour mesurer
var stopwatch = Stopwatch.StartNew();
// ... code ...
stopwatch.Stop();
PerformanceMetrics.RecordInputLatency(stopwatch.ElapsedMilliseconds);
```

**Éviter allocations inutiles** :

```csharp
// ✅ Correct (zero-allocation)
private IntPtr _rawInputBuffer = Marshal.AllocHGlobal(MAX_RAW_INPUT_SIZE);

// ❌ Incorrect (allocation à chaque appel)
byte[] buffer = new byte[MAX_RAW_INPUT_SIZE];
```

### Documentation

**Commenter code complexe** :

```csharp
// ✅ Correct
// CRITICAL: Calculate nextHead BEFORE writing to avoid race condition
// If we write then increment, UI thread might read invalid data
int head = Volatile.Read(ref *eventBufferHead);
int nextHead = (head + 1) % EVENT_BUFFER_SIZE;
Volatile.Write(ref eventBuffer[head], vkey);
Volatile.Write(ref *eventBufferHead, nextHead); // Atomic
```

**Documenter méthodes publiques** :

```csharp
/// <summary>
/// Crée un snapshot thread-safe de l'état actuel.
/// Inclut Event Buffer pour détection touches ultra-rapides.
/// </summary>
/// <returns>Snapshot de l'état (copie, pas référence)</returns>
public InputStateSnapshot CreateSnapshot()
{
    // ...
}
```

## 🔍 Pull Request Process

### Avant de soumettre

1. **Tests passent** :
   ```bash
   dotnet test
   ```

2. **Code style respecté** :
   - Pas de warnings
   - Commentaires ajoutés si nécessaire
   - Noms explicites

3. **Performance mesurée** (si changement impact performance) :
   - Inclure métriques avant/après dans PR description

### PR Description Template

```markdown
## Description
[Description claire du problème résolu / feature ajoutée]

## Type de changement
- [ ] Bug fix
- [ ] Nouvelle feature
- [ ] Breaking change
- [ ] Documentation

## Tests
- [ ] Tests unitaires ajoutés
- [ ] Tests passent
- [ ] Testé manuellement

## Performance
- [ ] Impact mesuré (avant/après)
- [ ] Pas de régression

## Logs
- [ ] Logs structurés utilisés (CrashReporter)
- [ ] Pas de Console.WriteLine

## Checklist
- [ ] Code style respecté
- [ ] Error handling ajouté
- [ ] Thread safety vérifiée
- [ ] Documentation mise à jour
```

### Review Process

1. **Automated checks** : Tests + linting
2. **Code review** : Au moins 1 approbation requise
3. **Merge** : Après approbation

## 🐛 Reporting Bugs

Utiliser [GitHub Issues](https://github.com/yourusername/keyboard_overlay_windows/issues) avec:

### Template Bug Report

```markdown
**Description**
[Description claire du bug]

**Steps to Reproduce**
1. [Étape 1]
2. [Étape 2]
3. [Étape 3]

**Comportement attendu**
[Ce qui devrait se passer]

**Comportement observé**
[Ce qui se passe réellement]

**Logs**
```
[Coller logs de %LocalAppData%\GamingKeypressOverlay\Logs\]
```

**System Info**
- Windows: [Version]
- .NET: [Version]
- CPU: [Cores]
- RAM: [GB]

**Screenshots**
[Si applicable]
```

## 💡 Feature Requests

Utiliser [GitHub Discussions](https://github.com/yourusername/keyboard_overlay_windows/discussions) pour:
- Proposer nouvelles features
- Discuter améliorations
- Poser questions

### Template Feature Request

```markdown
**Feature Description**
[Description claire de la feature]

**Use Case**
[Pourquoi cette feature est utile]

**Proposed Solution**
[Comment implémenter]

**Alternatives**
[Autres solutions considérées]
```

## 📚 Ressources

- **Architecture** : Voir README.md section "Architecture Technique"
- **Code examples** : Voir `RawInputThreadManager.cs`, `InputState.cs`
- **Tests examples** : Voir `GamingKeypressOverlay.Tests/`

## ❓ Questions?

- **GitHub Discussions** : Pour questions générales
- **GitHub Issues** : Pour bugs
- **Pull Request** : Pour code review questions

---

**Merci de contribuer ! 🎮**
