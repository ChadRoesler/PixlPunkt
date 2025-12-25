# PixlPunkt Plugin SDK Build Tools

This folder contains MSBuild targets and tasks for packaging PixlPunkt plugins.

## Files

- `PixlPunkt.PluginSdk.targets` - MSBuild targets for .punk packaging
- `GenerateManifest.targets` - Inline task for generating manifest.json

## How It Works

When a plugin project references the SDK and sets `<PackAsPlugin>true</PackAsPlugin>`,
the build process will:

1. Generate a `manifest.json` from project properties or assembly attributes
2. Copy output DLL and dependencies to a staging folder
3. Create a `.punk` file (ZIP archive) containing everything
4. Output the .punk to the build output directory

## Configuration

See the SDK documentation for configurable properties.
