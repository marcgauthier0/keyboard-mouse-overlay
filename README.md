<img width="665" height="344" alt="image" src="https://github.com/user-attachments/assets/ac8c5d4c-26c6-47b0-9790-c6620f41fde8" />


# Keyboard & Mouse Overlay

A low-latency keyboard and mouse overlay for Windows. It is useful for streaming, tutorials, presentations, accessibility demonstrations, software training, and games.

The complete application is free and open source under the MIT License.

## Download

Download the ready-to-install Windows version from the [latest GitHub Release](https://github.com/marcgauthier0/keyboard-mouse-overlay/releases/latest).

In the release assets, choose `KeyboardMouseOverlay_Setup_v1.0.0.exe`. The installer is self-contained, so users do not need to install .NET separately.

## Features

- Real-time keyboard and mouse input visualization
- Low-latency Raw Input capture on a dedicated thread
- GPU-accelerated Direct2D rendering
- QWERTY, AZERTY, and QWERTZ layouts
- General, FPS, MMO, MOBA, racing, and survival key configurations
- Fully customizable colors with `#RRGGBB` input and the Windows color picker
- Nine matching color presets that can be fine-tuned freely
- Automatic English or French interface based on the Windows display language
- Gaming and Minimal mouse designs, with no flashing background halos
- Opaque or transparent background for OBS and screen capture
- Persistent settings stored in `%LocalAppData%\GamingKeypressOverlay\settings.json`
- No accounts, activation keys, locked features, or telemetry requirements

## Customize the Colors

Right-click the overlay and open **Personalization → Colors and presets (HEX)...**. Choose from nine matching palettes—including Cyan Night, Heroic Orange, Tactical Ops, Midnight Gold, and Neon Storm—then customize the background, surfaces, keys, text, borders, and accents. Selecting a preset updates the colors immediately; **Apply** saves them.

## Controls

- Hold down the left mouse button anywhere on the overlay and drag to move it.
- Right-click the overlay to open the contextual menu and access all settings.

## Language

The application and installer automatically use French when the Windows display language is French. English is used for all other Windows languages.

## Build from Source

Requirements: Windows 10 or 11 and the .NET 8 SDK.

```powershell
dotnet restore
dotnet build GamingKeypressOverlay.csproj -c Release
dotnet run --project GamingKeypressOverlay.csproj
```

## Run the Tests

```powershell
dotnet test GamingKeypressOverlay.Tests/GamingKeypressOverlay.Tests.csproj
```

## Build the Installer

Install [NSIS](https://nsis.sourceforge.io/Download), then run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-release.ps1
```

## Contributing

Bug fixes, improvements, and translations are welcome. Read the [contribution guide](docs/CONTRIBUTING.md) before opening a pull request.

## License

Distributed under the [MIT License](LICENSE). You may use, modify, and redistribute the code as long as the license notice is preserved.
