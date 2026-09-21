using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.History;
using Windows.Graphics;
using static PixlPunkt.Core.Helpers.GraphicsStructHelper;
using PixlPunkt.Core.Selection;

namespace PixlPunkt.UI.Dialogs
{
    /// <summary>
    /// Window for editing canvas properties including name and size (in tiles).
    /// </summary>
    public sealed partial class EditCanvasWindow : Window
    {
        private readonly CanvasDocument _document;
        private readonly int _originalTileWidth;
        private readonly int _originalTileHeight;
        private AnchorPosition _selectedAnchor = AnchorPosition.MiddleCenter;

        /// <summary>
        /// Callback invoked when canvas changes are saved, allowing the host to refresh.
        /// Parameters are (contentOffsetX, contentOffsetY) indicating how much the content was shifted.
        /// </summary>
        public Action<int, int>? OnCanvasChanged { get; set; }

        /// <summary>
        /// Anchor position for canvas resize operations.
        /// </summary>
        public enum AnchorPosition
        {
            TopLeft, TopCenter, TopRight,
            MiddleLeft, MiddleCenter, MiddleRight,
            BottomLeft, BottomCenter, BottomRight
        }

        public EditCanvasWindow(CanvasDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _originalTileWidth = document.TileCounts.Width;
            _originalTileHeight = document.TileCounts.Height;

            InitializeComponent();
            LoadDocumentValues();
            SetUpFontSection();
        }

        /// <summary>
        /// Loads current document values into the UI controls.
        /// </summary>
        private void LoadDocumentValues()
        {
            // Document name
            DocumentNameTextBox.Text = _document.Name ?? "Untitled";

            // Document size (read-only display)
            DocumentSizeText.Text = $"{_document.PixelWidth} × {_document.PixelHeight}";

            // Tile size (read-only display)
            TileSizeText.Text = $"{_document.TileSize.Width} × {_document.TileSize.Height}";

            // Tile counts
            TileWidthBox.Value = _document.TileCounts.Width;
            TileHeightBox.Value = _document.TileCounts.Height;

            // Update preview
            UpdateNewSizePreview();
        }

        // ── font documents ──────────────────────────────────────────────
        // A font's tile is its glyph cell, made of an em box plus drawing room, and its tile count
        // follows from the character set. So for a font those two sections are replaced rather than
        // shown alongside: editing tile counts directly would put the sheet out of step with the
        // characters it holds.

        private const string PrintableAsciiName = "Printable ASCII";
        private const string UpperDigits = " ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.,!?'\"-:";
        private const string LettersDigits = " ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,!?'\"-:";

        private bool _fontReady;

        private bool IsFontDocument => _document.FontState.HasState;

        private void SetUpFontSection()
        {
            if (!IsFontDocument) return;

            TileSizeSection.Visibility = Visibility.Collapsed;
            TileCountSection.Visibility = Visibility.Collapsed;
            FontSection.Visibility = Visibility.Visible;

            var em = FontMetricsOps.EmBox(_document);
            EmWidthBox.Value = em.Width;
            EmHeightBox.Value = em.Height;

            // Drawing room is whatever margin the cell has around the em, per side.
            DrawingRoomBox.Value = Math.Max(0, (_document.TileSize.Width - em.Width) / 2);

            FontCharSetCombo.SelectedIndex = 0;
            _fontReady = true;
            UpdateFontShape();
        }

        private static string PrintableAscii()
        {
            var sb = new StringBuilder();
            for (char c = ' '; c <= '~'; c++) sb.Append(c);
            return sb.ToString();
        }

        /// <summary>The character set the dialog is currently asking for.</summary>
        private string ResolveCharacters() => (FontCharSetCombo?.SelectedIndex ?? 0) switch
        {
            1 => PrintableAscii(),
            2 => UpperDigits,
            3 => LettersDigits,
            4 => FontCustomChars?.Text ?? string.Empty,
            _ => FontReshapeOps.CurrentCharacters(_document),
        };

        /// <summary>The reshape the current settings describe, worked out but not carried out.</summary>
        private FontReshapePlan CurrentFontPlan() => FontReshapeOps.Plan(
            _document,
            (int)(EmWidthBox?.Value is double w and > 0 ? w : 1),
            (int)(EmHeightBox?.Value is double h and > 0 ? h : 1),
            (int)(DrawingRoomBox?.Value is double r and >= 0 ? r : 0),
            ResolveCharacters(),
            columns: Math.Max(1, _document.TileCounts.Width));

        private void FontShape_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args) =>
            UpdateFontShape();

        private void FontCustom_Changed(object sender, TextChangedEventArgs e) => UpdateFontShape();

        private void FontCharSet_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (FontCustomChars is not null)
                FontCustomChars.Visibility =
                    FontCharSetCombo?.SelectedIndex == 4 ? Visibility.Visible : Visibility.Collapsed;

            UpdateFontShape();
        }

        /// <summary>
        /// Restates the plan and raises the warning. Everything destructive is said here, before
        /// Save is pressed, as well as in the confirmation afterwards.
        /// </summary>
        private void UpdateFontShape()
        {
            if (!_fontReady || FontShapeSummary is null) return;

            var plan = CurrentFontPlan();

            FontShapeSummary.Text =
                $"Cell {plan.CellSize.Width} × {plan.CellSize.Height} · " +
                $"{plan.Characters.Length} glyphs · sheet {plan.Columns} × {plan.Rows} · " +
                $"{plan.CanvasSize.Width} × {plan.CanvasSize.Height} px";

            string? warning = DescribeCost(plan);
            FontShapeWarning.Text = warning ?? string.Empty;
            FontShapeWarning.Visibility = warning is null ? Visibility.Collapsed : Visibility.Visible;
        }

        /// <summary>What going ahead would cost, or null when nothing drawn is at risk.</summary>
        private string? DescribeCost(in FontReshapePlan plan)
        {
            var parts = new List<string>();

            if (plan.LosesCharacters)
            {
                parts.Add($"⚠ {plan.Removed.Count} character{(plan.Removed.Count == 1 ? "" : "s")} " +
                          "would be removed, along with anything drawn in them.");
            }

            if (plan.CropsGlyphs)
            {
                parts.Add("⚠ The cell is getting smaller, so any ink outside the new cell " +
                          "will be cut off every glyph.");
            }

            return parts.Count == 0 ? null : string.Join("\n", parts);
        }

        /// <summary>
        /// Updates the new size preview text based on current tile count values.
        /// </summary>
        private void UpdateNewSizePreview()
        {
            int newTileW = (int)TileWidthBox.Value;
            int newTileH = (int)TileHeightBox.Value;
            int newPixelW = newTileW * _document.TileSize.Width;
            int newPixelH = newTileH * _document.TileSize.Height;

            int deltaW = newTileW - _originalTileWidth;
            int deltaH = newTileH - _originalTileHeight;

            string deltaWStr = deltaW == 0 ? "" : (deltaW > 0 ? $" (+{deltaW})" : $" ({deltaW})");
            string deltaHStr = deltaH == 0 ? "" : (deltaH > 0 ? $" (+{deltaH})" : $" ({deltaH})");

            NewSizePreviewText.Text = $"New size: {newPixelW} × {newPixelH} px ({newTileW}{deltaWStr} × {newTileH}{deltaHStr} tiles)";
        }

        private void TileCount_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            // Handle NaN when user clears the box
            if (double.IsNaN(args.NewValue))
            {
                sender.Value = args.OldValue;
                return;
            }

            UpdateNewSizePreview();
        }

        private void Anchor_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string tagStr)
            {
                _selectedAnchor = Enum.TryParse<AnchorPosition>(tagStr, out var pos)
                    ? pos
                    : AnchorPosition.MiddleCenter;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsFontDocument)
            {
                await SaveFontAsync();
                return;
            }

            // Validate inputs
            int newTileW = (int)TileWidthBox.Value;
            int newTileH = (int)TileHeightBox.Value;

            if (newTileW < 1 || newTileH < 1)
            {
                // Show error - shouldn't happen with NumberBox constraints
                return;
            }

            // Update document name
            var newName = DocumentNameTextBox.Text?.Trim();
            if (!string.IsNullOrEmpty(newName))
            {
                _document.Name = newName;
            }

            // Check if canvas resize is needed
            int deltaTileW = newTileW - _originalTileWidth;
            int deltaTileH = newTileH - _originalTileHeight;

            int contentOffsetX = 0;
            int contentOffsetY = 0;

            if (deltaTileW != 0 || deltaTileH != 0)
            {
                // Calculate offset in TILE units first (so odd tiles round down correctly)
                // then convert to pixels
                var (tileOffsetX, tileOffsetY) = CalculateTileOffset(_selectedAnchor, deltaTileW, deltaTileH);
                contentOffsetX = tileOffsetX * _document.TileSize.Width;
                contentOffsetY = tileOffsetY * _document.TileSize.Height;

                // A floating selection has no place in a resize; commit it first. The commit and
                // the resize undo as one step.
                _document.History.BeginGroup("Resize Canvas");
                try
                {
                    if (_document.Floating != null)
                    {
                        var commit = FloatingSelectionOps.Commit(_document);
                        if (commit != null) _document.History.Push(commit);
                    }

                    // Create history item BEFORE resize (captures before state)
                    var historyItem = new CanvasResizeItem(_document);

                    // Perform canvas resize
                    ResizeCanvas(newTileW, newTileH, _selectedAnchor, contentOffsetX, contentOffsetY);

                    // Capture after state and push to unified history
                    historyItem.CaptureAfterState();
                    _document.History.Push(historyItem);
                }
                finally
                {
                    _document.History.EndGroup();
                }
            }

            // Notify document changed
            _document.RaiseStructureChanged();

            // Invoke callback to refresh the canvas view with offset info
            OnCanvasChanged?.Invoke(contentOffsetX, contentOffsetY);

            Close();
        }

        /// <summary>
        /// Applies a font reshape, asking first when it would destroy work. The question is asked
        /// here rather than left to undo, because losing a set of hand-drawn glyphs is the kind of
        /// thing someone should agree to in advance.
        /// </summary>
        private async Task SaveFontAsync()
        {
            var newName = DocumentNameTextBox.Text?.Trim();
            if (!string.IsNullOrEmpty(newName)) _document.Name = newName;

            var plan = CurrentFontPlan();

            if (plan.Characters.Length == 0)
            {
                await ShowMessageAsync("Nothing to keep",
                    "A font needs at least one character. Choose a character set, or type some in.");
                return;
            }

            if (plan.IsDestructive && !await ConfirmDestructiveAsync(plan))
                return;

            var item = new FontReshapeItem(_document, "Reshape Font");
            FontReshapeOps.Apply(_document, plan, AnchorFor(_selectedAnchor));
            item.CaptureAfter();
            _document.History.Push(item);

            OnCanvasChanged?.Invoke(_document.PixelWidth, _document.PixelHeight);
            _document.RaiseStructureChanged();
            Close();
        }

        /// <summary>Maps the dialog's anchor to the one the reshape understands. Same nine positions.</summary>
        private static GlyphAnchor AnchorFor(AnchorPosition anchor) => anchor switch
        {
            AnchorPosition.TopLeft => GlyphAnchor.TopLeft,
            AnchorPosition.TopCenter => GlyphAnchor.TopCenter,
            AnchorPosition.TopRight => GlyphAnchor.TopRight,
            AnchorPosition.MiddleLeft => GlyphAnchor.MiddleLeft,
            AnchorPosition.MiddleRight => GlyphAnchor.MiddleRight,
            AnchorPosition.BottomLeft => GlyphAnchor.BottomLeft,
            AnchorPosition.BottomCenter => GlyphAnchor.BottomCenter,
            AnchorPosition.BottomRight => GlyphAnchor.BottomRight,
            _ => GlyphAnchor.MiddleCenter,
        };

        private async Task<bool> ConfirmDestructiveAsync(FontReshapePlan plan)
        {
            var body = new StringBuilder();

            if (plan.LosesCharacters)
            {
                body.Append(plan.Removed.Count)
                    .Append(plan.Removed.Count == 1 ? " character will be removed" : " characters will be removed")
                    .Append(" from this font, along with anything drawn in them");

                body.Append(": ").Append(DescribeRemoved(plan)).Append('.');
                body.AppendLine().AppendLine();
            }

            if (plan.CropsGlyphs)
            {
                body.Append("The cell is going from ")
                    .Append(_document.TileSize.Width).Append(" × ").Append(_document.TileSize.Height)
                    .Append(" to ")
                    .Append(plan.CellSize.Width).Append(" × ").Append(plan.CellSize.Height)
                    .Append(". Any ink outside the smaller cell will be cut off every glyph.")
                    .AppendLine().AppendLine();
            }

            body.Append("This can be undone.");

            var dialog = new ContentDialog
            {
                Title = "This will remove work",
                Content = body.ToString(),
                PrimaryButtonText = "Go ahead",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot,
            };

            return await dialog.ShowAsync() == ContentDialogResult.Primary;
        }

        /// <summary>Lists the characters being dropped, trimmed so the dialog stays readable.</summary>
        private static string DescribeRemoved(in FontReshapePlan plan)
        {
            var shown = new List<string>();
            foreach (int codepoint in plan.Removed)
            {
                if (shown.Count == 12)
                {
                    shown.Add($"and {plan.Removed.Count - 12} more");
                    break;
                }
                shown.Add(FontGlyphOps.LabelFor(codepoint));
            }
            return string.Join(" ", shown);
        }

        private async Task ShowMessageAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot,
            };
            await dialog.ShowAsync();
        }

        /// <summary>
        /// Resizes the canvas by adjusting tile counts with anchor-based pixel shifting.
        /// </summary>
        /// <param name="newTileW">New width in tiles.</param>
        /// <param name="newTileH">New height in tiles.</param>
        /// <param name="anchor">Anchor position determining where existing content is placed.</param>
        /// <param name="offsetX">Pre-calculated X offset in pixels.</param>
        /// <param name="offsetY">Pre-calculated Y offset in pixels.</param>
        private void ResizeCanvas(int newTileW, int newTileH, AnchorPosition anchor, int offsetX, int offsetY)
        {
            int tileW = _document.TileSize.Width;
            int tileH = _document.TileSize.Height;

            int newPixelW = newTileW * tileW;
            int newPixelH = newTileH * tileH;

            // Resize the document with the calculated offset
            _document.ResizeCanvas(newPixelW, newPixelH, offsetX, offsetY);

            // Update tile counts
            _document.SetTileCounts(CreateSize(newTileW, newTileH));
        }

        /// <summary>
        /// Calculates the TILE offset for existing content based on anchor position and tile delta.
        /// This ensures that with odd tile deltas, the extra tile goes to right/bottom
        /// (content stays at whole-tile boundaries).
        /// </summary>
        /// <param name="anchor">The anchor position where existing content is pinned.</param>
        /// <param name="deltaTileW">Width change in tiles (positive = expand, negative = shrink).</param>
        /// <param name="deltaTileH">Height change in tiles (positive = expand, negative = shrink).</param>
        /// <returns>Offset (X, Y) in tile units where existing content should be placed.</returns>
        private static (int tileOffsetX, int tileOffsetY) CalculateTileOffset(AnchorPosition anchor, int deltaTileW, int deltaTileH)
        {
            int offsetX = 0;
            int offsetY = 0;

            // The anchor determines where content is PINNED (stays in place).
            // New space is added on the OPPOSITE side of the anchor.
            // 
            // For center anchors: space is split evenly, but odd tiles go to right/bottom
            // (floor division keeps content at whole-tile boundary closer to top-left)

            // Vertical offset (based on anchor vertical position)
            switch (anchor)
            {
                case AnchorPosition.TopLeft:
                case AnchorPosition.TopCenter:
                case AnchorPosition.TopRight:
                    // Anchor at top - content pinned at top, new space added to bottom
                    offsetY = 0;
                    break;

                case AnchorPosition.MiddleLeft:
                case AnchorPosition.MiddleCenter:
                case AnchorPosition.MiddleRight:
                    // Anchor at middle - new space split between top and bottom
                    // Floor division: odd tiles go to bottom (content stays higher)
                    // +1 tile -> offset 0, +2 tiles -> offset 1, +3 tiles -> offset 1
                    offsetY = deltaTileH / 2;
                    break;

                case AnchorPosition.BottomLeft:
                case AnchorPosition.BottomCenter:
                case AnchorPosition.BottomRight:
                    // Anchor at bottom - content pinned at bottom, new space added to top
                    // Content shifts down by the full delta
                    offsetY = deltaTileH;
                    break;
            }

            // Horizontal offset (based on anchor horizontal position)
            switch (anchor)
            {
                case AnchorPosition.TopLeft:
                case AnchorPosition.MiddleLeft:
                case AnchorPosition.BottomLeft:
                    // Anchor at left - content pinned at left, new space added to right
                    offsetX = 0;
                    break;

                case AnchorPosition.TopCenter:
                case AnchorPosition.MiddleCenter:
                case AnchorPosition.BottomCenter:
                    // Anchor at center - new space split between left and right
                    // Floor division: odd tiles go to right (content stays more left)
                    // +1 tile -> offset 0, +2 tiles -> offset 1, +3 tiles -> offset 1
                    offsetX = deltaTileW / 2;
                    break;

                case AnchorPosition.TopRight:
                case AnchorPosition.MiddleRight:
                case AnchorPosition.BottomRight:
                    // Anchor at right - content pinned at right, new space added to left
                    // Content shifts right by the full delta
                    offsetX = deltaTileW;
                    break;
            }

            return (offsetX, offsetY);
        }
    }
}
