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

GitHub Wikis are actually separate git repositories. To publish:

### Option 1: Manual Copy
1. Go to your repo's Wiki tab on GitHub
2. Create pages manually
3. Copy content from these files

### Option 2: Git Clone (Recommended)

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

### Option 3: GitHub Action (Automated)

Add this workflow to `.github/workflows/sync-wiki.yml`:

```yaml
name: Sync Wiki

on:
  push:
    branches: [main]
    paths:
      - 'wiki/**'

jobs:
  sync:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      
      - name: Sync Wiki
        uses: Andrew-Chen-Wang/github-wiki-action@v4
        with:
          path: wiki
          token: ${{ secrets.GITHUB_TOKEN }}
```

## Page Naming Convention

- Use `Title-Case-With-Dashes.md` for filenames
- This becomes the page URL: `wiki/Title-Case-With-Dashes`
- Internal links use `[[Display Text|Page-Name]]` syntax

## Link Syntax

```markdown
# Internal wiki links
[[Tools]]                    # Links to Tools.md
[[Gradient Fill|Gradient-Fill]]  # Custom display text

# External links
[GitHub Repo](https://github.com/ChadRoesler/PixlPunkt)

# Links to docs/ folder
[Cheat Sheet](../docs/CHEAT_SHEET.md)
```

## Adding New Pages

1. Create `New-Page.md` in this folder
2. Add to `_Sidebar.md` for navigation
3. Link from relevant existing pages
4. Sync to wiki using method above

## Updating Existing Pages

1. Edit the `.md` file in this folder
2. Commit to main branch
3. Sync to wiki (manual or automated)

This keeps wiki source in version control with the code!
