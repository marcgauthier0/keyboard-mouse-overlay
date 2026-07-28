# Pull Request

## 📝 Description

[Description claire du problème résolu / feature ajoutée]

**Fixes**: #[numéro issue] (si applicable)

## 🔄 Type de Changement

- [ ] 🐛 Bug fix (changement non-breaking qui corrige un problème)
- [ ] ✨ Nouvelle feature (changement non-breaking qui ajoute une fonctionnalité)
- [ ] 💥 Breaking change (fix ou feature qui causerait un changement existant)
- [ ] 📚 Documentation (changements à la documentation uniquement)
- [ ] 🔧 Refactoring (changements de code qui ne corrigent pas de bug ni n'ajoutent de feature)
- [ ] ⚡ Performance (amélioration de performance)
- [ ] 🧪 Tests (ajout ou modification de tests)

## 🧪 Tests

- [ ] Tests unitaires ajoutés/modifiés
- [ ] Tous les tests passent : `dotnet test`
- [ ] Testé manuellement sur Windows 10
- [ ] Testé manuellement sur Windows 11
- [ ] Testé en mode Gaming Compétitif
- [ ] Testé en mode Desktop

**Résultats des tests** :
```
[Coller ici la sortie de `dotnet test`]
```

## ⚡ Performance

- [ ] Impact mesuré (avant/après)
- [ ] Pas de régression de performance
- [ ] Métriques incluses dans description (si changement impact performance)

**Métriques** (si applicable) :
- Latence input : [avant] → [après]
- Latence UI : [avant] → [après]
- CPU usage : [avant] → [après]
- Memory : [avant] → [après]

## 📋 Logs

- [ ] Logs structurés utilisés (`CrashReporter.Log*`)
- [ ] Pas de `Console.WriteLine` ou `Debug.WriteLine`
- [ ] Niveaux de log appropriés (Info/Warning/Error/Critical)

## ✅ Checklist

- [ ] Code style respecté (PascalCase publics, `_camelCase` privés)
- [ ] Commentaires ajoutés pour code complexe (surtout unsafe)
- [ ] Error handling ajouté (try-catch dans méthodes critiques)
- [ ] Validation des paramètres (vkey range, buffer size, etc.)
- [ ] Thread safety vérifiée (Volatile reads/writes ou locks)
- [ ] Overflow protection (clamping, validation ranges)
- [ ] Documentation mise à jour (README, code comments)
- [ ] CHANGELOG.md mis à jour (si changement notable)
- [ ] Pas de warnings de compilation
- [ ] Pas de code mort (unused variables, methods)

## 📸 Screenshots / Demo

[Si applicable, ajouter des screenshots ou GIFs pour démontrer les changements]

## 🔗 Related Issues

- Closes #[numéro]
- Relates to #[numéro]

## 📚 Documentation

**Changements de documentation** :
- [ ] README.md mis à jour
- [ ] CONTRIBUTING.md mis à jour (si guidelines changées)
- [ ] CHANGELOG.md mis à jour
- [ ] Code comments ajoutés/modifiés

## 🔍 Code Review Notes

[Notes pour les reviewers : points d'attention, décisions techniques, etc.]

**Exemples** :
- "J'ai utilisé un lock ici car CreateSnapshot doit être atomic"
- "J'ai choisi Volatile.Read au lieu de lock pour performance (lock-free path)"
- "Cette méthode unsafe est justifiée car zero-allocation est critique pour latence"

## ✅ Self-Review Checklist

- [ ] J'ai relu mon propre code
- [ ] J'ai commenté les parties complexes
- [ ] J'ai testé les edge cases
- [ ] J'ai vérifié qu'il n'y a pas de memory leaks
- [ ] J'ai vérifié la thread safety
- [ ] J'ai vérifié les performances (si applicable)

---

**Merci pour ta contribution ! 🎮**
