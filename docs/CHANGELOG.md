# 📝 Changelog

Tous les changements notables de ce projet seront documentés dans ce fichier.

Le format est basé sur [Keep a Changelog](https://keepachangelog.com/fr/1.0.0/),
et ce projet adhère à [Semantic Versioning](https://semver.org/lang/fr/).

## [1.1.0] - 2025-02-XX - Memory Management Release 🛡️

### 🐛 Memory Leaks CRITIQUES Fixed
- **IDisposable Pattern Complet**: Toutes les classes avec ressources non-managées implémentent IDisposable
  - `RawInputThreadManager`: Thread shutdown gracieux, handles Win32 libérés (DestroyWindow, UnregisterClass, RIDEV_REMOVE)
  - `InputStateManager`: Nouveau wrapper pour InputState* avec cleanup automatique
  - `MainWindow`: Unsubscribe CompositionTarget.Rendering (event handler leak fixé)
  - `PerformanceMetrics`: StreamWriter persistant avec Dispose (file handle leak fixé)
  - `App.xaml.cs`: Diagnostics mémoire en mode DEBUG (mémoire/threads à la sortie)
- **Zero Memory Leak**: Tests unitaires confirment 0 leak après 100× open/close
- **Thread Cleanup**: Shutdown gracieux avec PostMessage WM_QUIT + Join(1000ms timeout)
- **Win32 Handles**: Cleanup complet (DestroyWindow + UnregisterClass + UnregisterRawInputDevices)
- **Event Handler Leak**: CompositionTarget.Rendering unsubscribe dans Dispose

### 🧪 Tests
- `MemoryLeakTests`: Tests unitaires pour vérifier absence de memory leaks
  - `TestRawInputThreadManager_NoLeak`: 100× create/dispose, vérifie <1MB leak
  - `TestMainWindow_EventHandlerLeak`: WeakReference check, vérifie GC collect
  - `TestInputStateManager_NoLeak`: Vérifie InputState* cleanup

### 📚 Documentation
- Section "Known Issues" avec détails complets IDisposable Pattern
- Code samples pour chaque classe Disposable
- Explication impact (50× open/close = 100+ MB leak avant fix)
- FAQ "Pourquoi l'app utilisait de la mémoire?" ajoutée

### 🔧 Technical Improvements
- **Thread.Abort() removed**: Remplacement par shutdown gracieux (Thread.Abort obsolète dans .NET Core/.NET 5+)
- **Lock thread-safe**: Dispose avec `_disposeLock` pour éviter race conditions
- **Double-dispose protection**: Vérification `_disposed` flag dans toutes les méthodes Dispose

---

## [1.0.0] - 2025-01-XX - Production Release 🚀

### 🛡️ Production Features
- **Crash Reporting**: Dump automatique d'état lors de crash avec logs détaillés
- **PerformanceMetrics**: Télémétrie complète (input/UI latency, dropped frames, keys/sec)
- **AutoTuner**: Détection automatique laptop/desktop, CPU cores, ajustement auto des settings
- **AdvancedSettings**: Configuration granulaire avec validation (flash duration, latch, buffer size)
- **Fallback Mode**: Bascule automatique vers polling si Raw Input échoue
- **Logging production**: Logs structurés dans `%LocalAppData%\GamingKeypressOverlay\Logs\`

### 🐛 Critical Bugs Fixed
- **Race condition Event Buffer**: Fix atomic writes (calcul `nextHead` avant écriture)
- **WheelDelta overflow**: Clamping -10000 à 10000 pour éviter overflow int32
- **CleanOldTimestamps race**: Double-check avant cleanup pour éviter race condition
- **CreateSnapshot thread safety**: Lock pour snapshot atomic (évite corruption données)

### ✨ Features
- **Flash System (30ms)**: Affiche touches ultra-rapides après relâchement
- **Visual Latch (50ms)**: Garantit minimum 50ms d'affichage pour touches <16ms
- **Event Buffer**: Buffer circulaire capture TOUTES les presses, même <1ms
- **CPU Affinity optimisé**: Thread input sur dernier core, process évite core 0
- **Personnalisation libre**: palette complète modifiable avec le sélecteur Windows ou des valeurs HEX
- **Modes clavier**: Full (complet) + Gaming (FPS layout essentiel)
- **Support souris complet**: Boutons (left/right/middle/X1/X2), wheel, position
- **Mode Performance**: Gaming Compétitif (optimisé) vs Desktop (normal)

### 🧪 Tests & Tools
- **Unit tests**: InputStateTests + AdvancedSettingsTests
- **NSIS Installer**: Production-ready avec vérifications (.NET 8.0, Windows 10+)
- **Logging structuré**: Fichiers logs avec rotation automatique

### 📚 Documentation
- README complet avec architecture détaillée
- Guide installation NSIS
- Guide signature installer (SIGN_INSTALLER.md)
- Troubleshooting section complète

### 🔧 Technical Improvements
- **Error Handling**: Try-catch partout, validation paramètres
- **Thread Safety**: Locks pour snapshots, volatile pour state
- **Overflow Protection**: Clamping WheelDelta, validation ranges
- **Memory Management**: IDisposable pattern, GC.SuppressFinalize

---

## [0.9.0] - 2024-12-XX - Beta

### ✨ Features
- Raw Input API sur thread dédié (priorité Highest)
- Flash System basique (30ms)
- Première version de la palette visuelle
- Mode clavier Full uniquement
- Support souris basique (boutons left/right)

### 🐛 Known Issues
- Race conditions dans Event Buffer (fixé v1.0.0)
- WheelDelta overflow possible (fixé v1.0.0)
- Pas de fallback si Raw Input échoue (fixé v1.0.0)
- Pas de crash reporting (ajouté v1.0.0)

---

## [0.8.0] - 2024-11-XX - Alpha

### ✨ Initial Release
- Capture Raw Input basique
- Affichage touches clavier
- Palette visuelle initiale
- Pas de support souris
- Pas de flash system

### 🐛 Known Issues
- Touches ultra-rapides manquées
- Lag sous jeux compétitifs
- Pas de thread dédié (utilise WPF Dispatcher)

---

[1.0.0]: https://github.com/yourusername/keyboard_overlay_windows/releases/tag/v1.0.0
[0.9.0]: https://github.com/yourusername/keyboard_overlay_windows/releases/tag/v0.9.0
[0.8.0]: https://github.com/yourusername/keyboard_overlay_windows/releases/tag/v0.8.0
