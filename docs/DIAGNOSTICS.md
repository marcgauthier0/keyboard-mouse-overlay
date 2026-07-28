# Latency Diagnostics Guide

## Mesurer la latence Input → Render

Pour diagnostiquer le lag dans COD ou autres jeux, activez les diagnostics de latence:

### Activation

Dans `MainWindow.xaml.cs`, décommentez cette ligne dans le constructeur:

```csharp
// DIAGNOSTIC: Uncomment to enable latency measurement
EnableLatencyDiagnostics = true;
```

Ou activez-le programmatiquement:

```csharp
mainWindow.EnableLatencyDiagnostics = true;
```

### Résultats

Les statistiques sont loggées toutes les secondes (~60 frames) dans:
- **Debug Output** (Visual Studio Output window)
- **Console** (si disponible)

Format:
```
[LATENCY DIAGNOSTIC] Input→Render: Avg=18.45ms, Min=12.30ms, Max=45.20ms, Samples=100
```

### Interprétation

- **<16ms**: Excellent, pas de lag perceptible
- **16-33ms**: Acceptable pour casual, peut être problématique pour compétitif
- **>33ms**: XAML/WPF est probablement le bottleneck

### Tests supplémentaires

#### 1. Test sans VSync lock

Dans `MainWindow.xaml.cs`, remplacez:

```csharp
CompositionTarget.Rendering += UpdateTimer_Tick;
```

Par:

```csharp
// ALTERNATIVE: Test without VSync (may reduce latency but less smooth)
var timer = new DispatcherTimer(DispatcherPriority.Render);
timer.Interval = TimeSpan.FromMilliseconds(8); // ~120fps, not locked to VSync
timer.Tick += UpdateTimer_Tick;
timer.Start();
```

#### 2. Test avec Software Rendering

Ajoutez dans le constructeur:

```csharp
// Force software rendering (may paradoxically be faster if GPU is saturated)
RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
```

#### 3. Test Console App (bypass XAML)

Créez un test minimal sans UI pour vérifier si le problème vient de XAML:

```csharp
// Test.cs - Console app pure
static void Main() {
    // Setup raw input (Win32)
    while(true) {
        // Process messages
        if (keyPressed) {
            Console.SetCursorPosition(0, 0);
            Console.Write($"Key: {key} - {DateTime.Now:HH:mm:ss.fff}");
        }
    }
}
```

Si la console app ne lag PAS → XAML est confirmé comme bottleneck.

### Solutions si latence >33ms

1. **Win32 + Direct2D** (le plus rapide, <1ms latency)
2. **WinUI 3 Islands** (compromis, plus léger que WPF)
3. **Overlay externe** (2e moniteur, OBS overlay)
4. **Accepter le lag** (10-20ms acceptable pour casual)

### Accès programmatique aux stats

```csharp
var (avg, min, max, samples) = mainWindow.GetLatencyStats();
Console.WriteLine($"Avg: {avg:F2}ms, Min: {min:F2}ms, Max: {max:F2}ms");
```
