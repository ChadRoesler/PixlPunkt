# Troubleshooting

Common issues and solutions for PixlPunkt.

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/warning_16.png" width="16"> Installation Issues

### Windows: "Windows protected your PC"

**Solution:**
1. Click **More info**
2. Click **Run anyway**

This appears because PixlPunkt isn't code-signed with an EV certificate (yet).

### Windows: App won't start

**Possible causes:**
- Missing .NET runtime
- Antivirus blocking

**Solutions:**
1. Install [.NET 10 Runtime](https://dotnet.microsoft.com/download)
2. Add PixlPunkt to antivirus exceptions
3. Try portable version instead

### macOS: "App is damaged"

**Solution:**
```bash
xattr -cr /Applications/PixlPunkt.app
```

Then try opening again.

### macOS: "Cannot be opened because the developer cannot be verified"

**Solution:**
1. Right-click the app
2. Select **Open**
3. Click **Open** in the dialog

### Linux: Missing libraries

**Debian/Ubuntu:**
```bash
sudo apt-get install libx11-6 libgtk-3-0 libskia*
```

**Fedora:**
```bash
sudo dnf install gtk3 libX11
```

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/warning_16.png" width="16"> Performance Issues

### Slow drawing / lag

**Causes:**
- Very large canvas
- Many layers
- Hardware acceleration disabled

**Solutions:**
1. Reduce canvas size if possible
2. Merge unnecessary layers
3. Enable **Settings ? Performance ? Hardware acceleration**
4. Close other applications

### High memory usage

**Causes:**
- Large undo history
- Many open documents
- Large canvas sizes

**Solutions:**
1. **Edit ? Purge ? Undo History**
2. Close unused documents
3. Reduce **Settings ? Performance ? Undo memory limit**

### Animation preview stutters

**Causes:**
- High frame rate
- Large canvas
- Many layers

**Solutions:**
1. Lower FPS temporarily for preview
2. Preview at smaller zoom level
3. Close other panels during preview

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/warning_16.png" width="16"> Display Issues

### Blurry canvas / UI

**Cause:** Display scaling (HiDPI)

**Solutions:**
1. Run at 100% display scaling
2. Or adjust in **Settings ? General ? UI Scale**

### Black canvas

**Causes:**
- GPU driver issue
- Hardware acceleration problem

**Solutions:**
1. Update graphics drivers
2. Disable **Settings ? Performance ? Hardware acceleration**
3. Try different renderer (if available)

### Transparency shows wrong color

**Solution:**
Adjust in **Settings ? Canvas ? Transparency stripe colors**

### Grid not showing

**Solutions:**
1. Zoom in (grid shows at 8× by default)
2. Check **View ? Show Pixel Grid** is enabled
3. Adjust **Settings ? Grid ? Show at zoom**

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/warning_16.png" width="16"> Tool Issues

### Brush not drawing

**Possible causes:**
- Wrong layer selected
- Layer is locked
- Layer is hidden
- Drawing outside canvas
- Color matches background

**Solutions:**
1. Check selected layer in Layers panel
2. Unlock layer (click lock icon)
3. Show layer (click eye icon)
4. Zoom out to see canvas bounds
5. Change foreground color

### Fill tool not working

**Possible causes:**
- Tolerance too low
- Selecting same color
- Locked layer

**Solutions:**
1. Increase **Tolerance** in tool options
2. Verify colors are different
3. Unlock the layer

### Selection not visible

**Possible causes:**
- Selection exists but empty
- Marching ants disabled
- Selection on wrong layer

**Solutions:**
1. **Select ? Select All** to verify
2. Check **View ? Selection Edges**
3. Selection affects all layers - check active layer

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/warning_16.png" width="16"> File Issues

### Can't save file

**Possible causes:**
- No write permission
- Disk full
- File in use

**Solutions:**
1. Save to different location
2. Check disk space
3. Close file in other applications

### Project won't open

**Possible causes:**
- Corrupted file
- Wrong version
- Missing dependencies

**Solutions:**
1. Try **File ? Open Recent** for backup
2. Check auto-save folder for backups
3. Try opening in text editor to verify it's valid JSON

### Export fails

**Possible causes:**
- Invalid path
- Unsupported options
- FFmpeg missing (for video)

**Solutions:**
1. Export to simple path (no special characters)
2. Use default export settings
3. For video: install FFmpeg or let PixlPunkt download it

### Images import as wrong colors

**Cause:** Color profile mismatch

**Solution:**
- Convert to sRGB before importing
- Or use PNG format (no embedded profile)

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/warning_16.png" width="16"> Animation Issues

### Animation won't play

**Possible causes:**
- Only one frame
- Timeline panel closed
- FPS set to 0

**Solutions:**
1. Add more frames
2. Open Timeline panel (`T`)
3. Set FPS > 0

### Onion skin not showing

**Solutions:**
1. Enable with `Ctrl+Shift+O`
2. Ensure frames before/after > 0 in settings
3. Check opacity isn't 0

### Audio not playing

**Possible causes:**
- Audio muted
- System audio issue
- Unsupported format

**Solutions:**
1. Click speaker icon to unmute
2. Check system volume
3. Convert to WAV format

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/warning_16.png" width="16"> Plugin Issues

### Plugin not loading

**Possible causes:**
- Wrong folder
- Incompatible version
- Missing dependencies

**Solutions:**
1. Verify plugin is in Plugins folder:
   - Windows: `%LocalAppData%\PixlPunkt\Plugins\`
2. Check plugin SDK version matches
3. Check for missing DLLs

### Plugin causes crash

**Solutions:**
1. Remove plugin from Plugins folder
2. Restart PixlPunkt
3. Report issue to plugin author

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/wrench_16.png" width="16"> Reset & Recovery

### Reset all settings

1. Close PixlPunkt
2. Delete settings file:
   - Windows: `%LocalAppData%\PixlPunkt\settings.json`
3. Restart PixlPunkt

### Reset keyboard shortcuts

**Settings ? Keyboard ? Reset All**

### Recover auto-saved files

Auto-saves are in:
- Windows: `%LocalAppData%\PixlPunkt\AutoSave\`
- macOS: `~/Library/Application Support/PixlPunkt/AutoSave/`
- Linux: `~/.local/share/PixlPunkt/AutoSave/`

### Clear all data

**Warning:** This removes all settings, presets, and plugins!

1. Close PixlPunkt
2. Delete the PixlPunkt folder:
   - Windows: `%LocalAppData%\PixlPunkt\`
3. Reinstall if needed

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/code_16.png" width="16"> Getting Debug Info

### Enable verbose logging

1. **Settings ? Experimental ? Verbose logging**
2. Reproduce the issue
3. Check logs in:
   - Windows: `%LocalAppData%\PixlPunkt\Logs\`

### Report a bug

1. Check [existing issues](https://github.com/ChadRoesler/PixlPunkt/issues)
2. If new, [create an issue](https://github.com/ChadRoesler/PixlPunkt/issues/new)
3. Include:
   - PixlPunkt version
   - Operating system
   - Steps to reproduce
   - Error message / log file
   - Screenshot if applicable

---

## <img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/question_16.png" width="16"> Still Need Help?

- [GitHub Issues](https://github.com/ChadRoesler/PixlPunkt/issues) - Report bugs
- [GitHub Discussions](https://github.com/ChadRoesler/PixlPunkt/discussions) - Ask questions
- Check other wiki pages for feature-specific help

---

## See Also

- [[Installation]] - Installation guide
- [[Settings]] - Configuration options
- [[Quick Start|Quick-Start]] - Getting started
