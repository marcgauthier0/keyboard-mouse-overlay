# 🌐 Guide Localization - Gaming Keypress Overlay

Guide pour utiliser et étendre le support multilingue.

---

## 📁 Structure

```
Resources/
├── Strings.resx          # English (default, neutral language)
└── Strings.fr-CA.resx    # Français (Canada)
```

---

## 🔧 Utilisation dans XAML

### Exemple: MainWindow.xaml

```xaml
<Window x:Class="GamingKeypressOverlay.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:p="clr-namespace:GamingKeypressOverlay.Resources"
        Title="{x:Static p:Strings.WindowTitle}"
        Width="1400" Height="600">
    
    <Menu>
        <MenuItem Header="{x:Static p:Strings.MenuOptions}">
            <MenuItem Header="{x:Static p:Strings.MenuColors}" Click="OpenColorPicker"/>
            
            <MenuItem Header="{x:Static p:Strings.MenuKeyboardMode}">
                <MenuItem Header="{x:Static p:Strings.KeyboardModeFull}" Click="SetMode_Full"/>
                <MenuItem Header="{x:Static p:Strings.KeyboardModeGaming}" Click="SetMode_Gaming"/>
            </MenuItem>
            
            <MenuItem Header="{x:Static p:Strings.MenuLanguage}">
                <MenuItem Header="{x:Static p:Strings.MenuLanguageEnglish}" Click="SetLanguage_English"/>
                <MenuItem Header="{x:Static p:Strings.MenuLanguageFrench}" Click="SetLanguage_French"/>
            </MenuItem>
        </MenuItem>
    </Menu>
</Window>
```

---

## 💻 Utilisation dans Code C#

### Exemple: MainWindow.xaml.cs

```csharp
using System.Globalization;
using System.Threading;
using System.Windows;
using GamingKeypressOverlay.Resources;

public partial class MainWindow : Window
{
    private void SetLanguage_English(object sender, RoutedEventArgs e)
    {
        ChangeLanguage("en");
    }
    
    private void SetLanguage_French(object sender, RoutedEventArgs e)
    {
        ChangeLanguage("fr-CA");
    }
    
    private void ChangeLanguage(string cultureName)
    {
        try
        {
            // Change culture
            CultureInfo culture = new CultureInfo(cultureName);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            
            // Save preference
            var settings = SettingsManager.LoadSettings();
            settings.Language = cultureName;
            SettingsManager.SaveSettings(settings);
            
            // Notify user (requires restart for full effect)
            MessageBox.Show(
                Strings.MessageLanguageChanged,
                Strings.WindowTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information
            );
            
            // Optional: Restart app automatically
            // Application.Current.Shutdown();
            // System.Windows.Forms.Application.Restart();
        }
        catch (Exception ex)
        {
            CrashReporter.LogError($"Failed to change language: {ex.Message}");
        }
    }
    
    // Utiliser dans code
    private void ShowMessage()
    {
        string message = Strings.MessageRawInputFailed;
        MessageBox.Show(message, Strings.WindowTitle);
    }
}
```

---

## ➕ Ajouter Nouvelle Langue

### Step 1: Créer Fichier Resource

1. **Copier** `Resources/Strings.resx` → `Resources/Strings.es-ES.resx` (exemple: Espagnol)
2. **Traduire** toutes les valeurs `<value>...</value>`
3. **Garder** les mêmes `name` attributes

### Step 2: Mettre à jour .csproj

```xml
<PropertyGroup>
  <SatelliteResourceLanguages>en;fr-CA;es-ES</SatelliteResourceLanguages>
</PropertyGroup>

<ItemGroup>
  <EmbeddedResource Update="Resources\Strings.es-ES.resx">
    <DependentUpon>Strings.resx</DependentUpon>
  </EmbeddedResource>
</ItemGroup>
```

### Step 3: Ajouter Option Menu

Dans `MainWindow.xaml.cs` :

```csharp
private void SetLanguage_Spanish(object sender, RoutedEventArgs e)
{
    ChangeLanguage("es-ES");
}
```

---

## 🔑 Ajouter Nouvelle String

### Step 1: Ajouter dans Strings.resx

```xml
<data name="NewStringKey" xml:space="preserve">
  <value>English Text</value>
</data>
```

### Step 2: Traduire dans Strings.fr-CA.resx

```xml
<data name="NewStringKey" xml:space="preserve">
  <value>Texte Français</value>
</data>
```

### Step 3: Utiliser

**XAML** :
```xaml
<TextBlock Text="{x:Static p:Strings.NewStringKey}"/>
```

**C#** :
```csharp
string text = Strings.NewStringKey;
```

---

## 🎯 Bonnes Pratiques

1. **Clés descriptives** : `MenuOptions` pas `MO1`
2. **Pas de texte hardcodé** : Toujours utiliser resources
3. **Context dans nom** : `MessageRawInputFailed` pas `Message1`
4. **Traduire tout** : Même si texte identique (ex: "Matrix" reste "Matrix")
5. **Tester** : Vérifier que toutes les langues fonctionnent

---

## 🐛 Troubleshooting

### Strings non traduites

**Problème** : Texte reste en anglais même après changement langue

**Fix** :
1. Vérifier que `Strings.XX-XX.resx` existe
2. Vérifier que `name` attribute est identique dans tous les fichiers
3. Rebuild projet (`dotnet build`)
4. Redémarrer application

### Culture not found

**Problème** : `CultureInfo("fr-CA")` throw exception

**Fix** :
- Vérifier que culture code est correct (ex: `fr-CA` pas `fr_CA`)
- Vérifier que .NET supporte la culture
- Fallback vers culture parente (`fr` si `fr-CA` échoue)

---

## 📚 Ressources

- [MSDN: WPF Globalization](https://docs.microsoft.com/en-us/dotnet/desktop/wpf/advanced/wpf-globalization-and-localization-overview)
- [CultureInfo Reference](https://docs.microsoft.com/en-us/dotnet/api/system.globalization.cultureinfo)

---

**Made for gamers, by gamers** 🎮
