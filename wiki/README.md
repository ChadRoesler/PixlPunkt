# PixlPunkt Wiki

This folder contains the source files for the [PixlPunkt GitHub Wiki](https://github.com/ChadRoesler/PixlPunkt/wiki).

## Structure

```
wiki/
??? Home.md              # Wiki landing page
??? _Sidebar.md          # Navigation sidebar
??? Tools.md             # All drawing tools
??? Gradient-Fill.md     # Gradient tool deep dive
??? Layers.md            # Layer management
??? Effects.md           # Layer effects
??? Canvas-Animation.md  # Frame-by-frame animation
??? Tile-Animation.md    # Sprite sheet animation
??? Stage.md             # Camera system
??? Shortcuts.md         # Keyboard reference
??? ... more pages
```

## Publishing to GitHub Wiki

This wiki is automatically synced using the GitHub Action in `.github/workflows/sync-wiki.yml`.

When you push changes to the `wiki/` folder on the `main` branch, they will be automatically published to the GitHub Wiki.

### Manual Sync (if needed)

```bash
# Clone the wiki repo (it's separate from main repo!)
git clone https://github.com/ChadRoesler/PixlPunkt.wiki.git

# Copy files from this folder
cp -r wiki/* PixlPunkt.wiki/

# Push to wiki
cd PixlPunkt.wiki
git add .
git commit -m "Update wiki from main repo"
git push
```

## Page Naming Convention

- Use `Title-Case-With-Dashes.md` for filenames
- This becomes the page URL: `wiki/Title-Case-With-Dashes`
- Internal links use `[[Display Text|Page-Name]]` syntax

## Link Syntax

```markdown
# Internal wiki links
[[Tools]]                        # Links to Tools.md
[[Gradient Fill|Gradient-Fill]]  # Custom display text

# External links
[GitHub Repo](https://github.com/ChadRoesler/PixlPunkt)

# Links to main repo files
[Cheat Sheet](https://github.com/ChadRoesler/PixlPunkt/blob/main/docs/CHEAT_SHEET.md)
```

## Images

Images should reference the main repo using raw GitHub URLs:

```markdown
<img src="https://raw.githubusercontent.com/ChadRoesler/PixlPunkt/main/docs/assets/icons/edit_16.png" width="16">
```

## Adding New Pages

1. Create `New-Page.md` in this folder
2. Add to `_Sidebar.md` for navigation
3. Link from relevant existing pages
4. Push to main - wiki syncs automatically!

## Updating Existing Pages

1. Edit the `.md` file in this folder
2. Commit and push to main branch
3. Wiki syncs automatically via GitHub Action

This keeps wiki source in version control with the code!
