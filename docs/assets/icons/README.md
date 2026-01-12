# Documentation Icons

This folder contains Fluent UI System Icons exported as PNG files for use in documentation.

## Source

Icons are from [Microsoft Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons) (MIT License).

## Regenerating Icons

Run the PowerShell script to download and convert icons:

```powershell
.\scripts\export-doc-icons.ps1
```

This will:
1. Download SVGs from the Fluent UI System Icons repo
2. Convert them to 16x16 and 20x20 PNGs (white color for dark theme compatibility)
3. Place them in this folder

## Icon Naming Convention

- `{icon_name}_16.png` - 16x16 version (for inline text)
- `{icon_name}_20.png` - 20x20 version (for tables/headers)

## Usage in Markdown

```markdown
<img src="assets/icons/play_16.png" width="16" height="16"> Play
```

Or in tables:
```markdown
| <img src="assets/icons/play_20.png" width="20"> | Play | Start playback |
```
