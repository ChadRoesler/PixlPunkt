using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.History;
using Windows.Graphics;
using static PixlPunkt.Core.Helpers.GraphicsStructHelper;

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

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
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

                // Create history item BEFORE resize (captures before state)
                var historyItem = new CanvasResizeItem(_document);

                // Perform canvas resize
                ResizeCanvas(newTileW, newTileH, _selectedAnchor, contentOffsetX, contentOffsetY);

                // Capture after state and push to unified history
                historyItem.CaptureAfterState();
                _document.History.Push(historyItem);
            }

            // Notify document changed
            _document.RaiseStructureChanged();

            // Invoke callback to refresh the canvas view with offset info
            OnCanvasChanged?.Invoke(contentOffsetX, contentOffsetY);

            Close();
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
