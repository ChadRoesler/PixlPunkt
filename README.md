<p align="center">
  <img src="PixlPunkt/Assets/Icons/PixlPunkt.ico" alt="PixlPunkt Logo" width="128" height="128">
</p>

<h1 align="center">PixlPunkt</h1>

<p align="center">
  <strong>A modern, cross-platform pixel art editor</strong>
</p>

<p align="center">
  <a href="#features">Features</a> •
  <a href="#documentation">Documentation</a> •
  <a href="#installation">Installation</a> •
  <a href="#plugin-development">Plugin Development</a> •
  <a href="#contributing">Contributing</a> •
  <a href="#license">License</a>
</p>

---

## Documentation

### Quick Reference
*For when you're in the zone and just need a reminder*

| Document | Description |
|----------|-------------|
| **[Quick Start Guide](docs/QUICK_START.md)** | Get up and running in 5 minutes |
| **[Cheat Sheet](docs/CHEAT_SHEET.md)** | One-page keyboard shortcuts & tool reference |

### The Wiki (Deep Dive)
*For when you actually want to RTFM*

**[→ PixlPunkt Wiki](https://github.com/ChadRoesler/PixlPunkt/wiki)**

| Topic | What You'll Learn |
|-------|-------------------|
| [Tools](https://github.com/ChadRoesler/PixlPunkt/wiki/Tools) | Every brush, eraser, and shape tool |
| [Gradient Fill](https://github.com/ChadRoesler/PixlPunkt/wiki/Gradient-Fill) | Dithering algorithms explained (yes, even Riemersma) |
| [Layers & Effects](https://github.com/ChadRoesler/PixlPunkt/wiki/Layers) | Blend modes, masks, effects |
| [Canvas Animation](https://github.com/ChadRoesler/PixlPunkt/wiki/Canvas-Animation) | Frame-by-frame, keyframes, onion skinning |
| [Tile Animation](https://github.com/ChadRoesler/PixlPunkt/wiki/Tile-Animation) | Sprite sheets, reels, game exports |
| [Stage & Camera](https://github.com/ChadRoesler/PixlPunkt/wiki/Stage) | Pan, zoom, rotate - cinematic camera |
| [Shortcuts](https://github.com/ChadRoesler/PixlPunkt/wiki/Shortcuts) | Complete keyboard reference |

### Plugin Development

| Document | Description |
|----------|-------------|
| **[Plugin SDK Guide](PixlPunkt.PluginSdk/DEVELOPER_README.md)** | Create your own tools and effects |
| **[Example Plugin](PixlPunkt.ExamplePlugin/)** | Working code to steal from |

---

## Features

### Drawing Tools
- **Brush Tool** - Configurable size, shape, opacity, density, and pixel-perfect mode
- **Eraser** - Clean up your artwork with precision, supports opacity and custom brushes
- **Shape Tools** - Rectangle, ellipse tools with fill options, opacity, and density
- **Fill Tool** - Flood fill with tolerance and contiguous mode
- **Selection Tools** - Rectangle, lasso, magic wand, paint selection with transform support

### Gradient Fill Tool
Create stunning gradients with pixel-art-friendly dithering:
- **Gradient Types** - Linear, Radial, Angular, Diamond
- **Dithering Styles**:
  - Ordered: Bayer 2×2, 4×4, 8×8, Checker, Diagonal, Crosshatch
  - Error Diffusion: Floyd-Steinberg, Atkinson, **Riemersma** (Hilbert curve!)
  - Stochastic: Blue Noise
- **Multi-Color Gradients** - Custom gradients use your entire palette
- **Controls** - Adjustable strength and pattern scale

### Layer System
- Multiple layers with opacity and blend modes
- Layer folders for organization
- **Animatable layer effects** (drop shadow, outline, glow, scanlines, and more)
- Non-destructive editing

### Layer Effects
Apply non-destructive effects to any layer:
- **Stylize**: Drop Shadow, Outline, Glow/Bloom, Chromatic Aberration
- **Filter**: Scan Lines, Grain, Vignette, CRT, Pixelate
- **Color**: Color Adjust, Palette Quantize, ASCII Art
- **All effects are animatable!**

### Animation System
Full-featured animation with two modes:

**Canvas Animation** (Aseprite-style):
- Layer-based keyframes with pixel data
- Animated layer effects
- Onion skinning
- **Stage (Camera) System** with interpolated transforms
- **Audio reference tracks** with waveform display

**Tile Animation**:
- Frame sequences from tile coordinates
- Reels with custom timing
- Export to sprite sheets

### Tile System
- Create and manage tile sets
- Per-layer tile mappings
- Tile tessellation tools
- Import Tiled (.tmx/.tsx) files

### Color Management
- HSL color picker with ladder controls
- Palette presets (NES, GameBoy, CGA, PICO-8, and more)
- Custom palette import/export
- Color extraction from images
- Gradient generator

### Plugin System
- Extensible architecture via Plugin SDK
- Create custom brushes, shapes, effects, and tools
- Import/export handler support for custom file formats

### File Support
- Native `.pxp` format with full feature preservation
- **Import**: PNG, Aseprite, PyxelEdit, ICO, CUR, Tiled
- **Export**: PNG, GIF, MP4, AVI, WMV, BMP, JPEG, TIFF
- Custom brush export (`.pbx`)

---

## Installation

### Supported Platforms

| Platform | Architecture | Download |
|----------|--------------|----------|
| **Windows** | x64, ARM64 | Setup.exe (auto-updates) or Portable ZIP |
| **macOS** | Intel (x64), Apple Silicon (ARM64) | DMG or ZIP |
| **Linux** | x64 | DEB, RPM, or tarball |

### Requirements
- **Windows**: Windows 10 version 1809 (build 17763) or later
- **macOS**: macOS 10.15 (Catalina) or later
- **Linux**: X11-based desktop environment

### Download
Download the latest release from the [Releases](https://github.com/ChadRoesler/PixlPunkt/releases) page.

#### Windows
- **Recommended**: `PixlPunkt-X.X.X-Windows-Setup.exe` - Installer with auto-updates
- **Portable**: `PixlPunkt-X.X.X-Desktop-Windows-x64-Portable.zip` - No installation required

#### macOS
- **Apple Silicon**: `PixlPunkt-X.X.X-macOS-arm64.dmg`
- **Intel**: `PixlPunkt-X.X.X-macOS-x64.dmg`

#### Linux
- **Debian/Ubuntu**: `pixlpunkt_X.X.X_amd64.deb`
- **Fedora/RHEL**: `pixlpunkt-X.X.X-1.x86_64.rpm`
- **Portable**: `pixlpunkt-X.X.X-desktop-linux-x64.tar.gz`

### Build from Source
```bash
git clone https://github.com/ChadRoesler/PixlPunkt.git
cd PixlPunkt
dotnet build
```

#### Build for Specific Platform
```bash
# Windows (Skia Desktop)
dotnet publish PixlPunkt/PixlPunkt.csproj -c Release -f net10.0-desktop -r win-x64 -p:SkiaOnly=true

# Linux
dotnet publish PixlPunkt/PixlPunkt.csproj -c Release -f net10.0-desktop -r linux-x64 -p:SkiaOnly=true

# macOS (Apple Silicon)
dotnet publish PixlPunkt/PixlPunkt.csproj -c Release -f net10.0-desktop -r osx-arm64 -p:SkiaOnly=true

# macOS (Intel)
dotnet publish PixlPunkt/PixlPunkt.csproj -c Release -f net10.0-desktop -r osx-x64 -p:SkiaOnly=true
```

---

## Plugin Development

PixlPunkt supports plugins through the **PixlPunkt.PluginSdk** NuGet package.

### Quick Start

1. Create a new .NET class library project
2. Add the SDK reference:
   ```xml
   <PackageReference Include="PixlPunkt.PluginSdk" Version="1.0.0" />
   ```
3. Implement the `IPlugin` interface:
   ```csharp
   public class MyPlugin : IPlugin
   {
       public string Id => "com.example.myplugin";
       public string DisplayName => "My Plugin";
       public string Version => "1.0.0";
       public string Author => "Your Name";
       public string Description => "A custom plugin";

       public void Initialize(IPluginContext context) { }
       
       public IEnumerable<IToolRegistration> GetToolRegistrations()
       {
           yield return ToolBuilders.BrushTool("com.example.brush.custom")
               .WithDisplayName("Custom Brush")
               .WithSettings(new MyBrushSettings())
               .WithPainter(() => new MyBrushPainter())
               .Build();
       }
       
       // ... other registration methods
   }
   ```

4. Build and place the `.dll` in `%AppData%\PixlPunkt\Plugins\`

### Documentation
- [Plugin SDK Developer Guide](PixlPunkt.PluginSdk/DEVELOPER_README.md)
- [Example Plugin](PixlPunkt.ExamplePlugin/)

---

## Project Structure

```
PixlPunkt/
├── PixlPunkt/                 # Main application (Uno Platform)
│   ├── Core/                  # Core logic (document, imaging, tools, animation)
│   │   ├── Animation/         # Canvas & tile animation systems
│   │   ├── Painting/          # Brush painters, dithering algorithms
│   │   └── Tools/             # Tool implementations and settings
│   ├── UI/                    # User interface components
│   │   ├── Animation/         # Timeline and keyframe UI
│   │   ├── CanvasHost/        # Main canvas rendering
│   │   └── ColorPick/         # Color picker and gradient editor
│   ├── Platforms/             # Platform-specific code
│   │   ├── Desktop/           # Skia Desktop (Windows, Linux, macOS)
│   │   ├── Windows/           # WinAppSdk specific
│   │   ├── Android/           # Android specific
│   │   └── iOS/               # iOS specific
│   └── Constants/             # Application constants
├── PixlPunkt.Tests/           # Unit tests for main application
├── PixlPunkt.PluginSdk/       # Plugin SDK (NuGet package)
│   ├── Plugins/               # Plugin interfaces
│   ├── Tools/                 # Tool abstractions
│   ├── Effects/               # Effect system
│   └── IO/                    # Import/export handlers
├── PixlPunkt.PluginSdk.Tests/ # Plugin SDK unit tests
├── PixlPunkt.ExamplePlugin/   # Example plugin implementation
└── scripts/                   # Build and installer scripts
    ├── create-installers.ps1  # Windows installer creation
    └── create-installers.sh   # Linux/macOS installer creation
```

---

## Contributing

We welcome contributions! Please see our [Contributing Guidelines](CONTRIBUTING.md) for details.

### Development Setup
1. Clone the repository
2. Open `PixlPunkt.sln` in Visual Studio 2022
3. Restore NuGet packages
4. Build and run

### Code Style
- Follow existing code conventions
- Use XML documentation for public APIs
- Write meaningful commit messages

---

## Roadmap

- [x] SkiaSharp rendering backend
- [x] Uno Platform migration (cross-platform support!)
- [x] Symmetry tools
- [ ] Sprite sheet export improvements

---

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

## Acknowledgments

- Built with [WinUI 3](https://docs.microsoft.com/en-us/windows/apps/winui/winui3/)
- Icons from [Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons)
- Community Toolkit for WinUI

---

<p align="center">
  The PixlPunkt Team © 2025
</p>
