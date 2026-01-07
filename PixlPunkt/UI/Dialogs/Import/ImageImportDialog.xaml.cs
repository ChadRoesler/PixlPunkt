using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace PixlPunkt.UI.Dialogs.Import
{
    /// <summary>
    /// Dialog for importing images with tile-based canvas configuration.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Allows users to choose between:
    /// - Single tile mode: Creates a 1x1 tile canvas where tile size equals image size
    /// - Custom tile mode: Specify tile dimensions, canvas expands to fit entire image
    /// </para>
    /// <para>
    /// For custom tiles, the canvas grid is calculated to ensure the entire image fits:
    /// - TilesX = ceil(imageWidth / tileWidth)
    /// - TilesY = ceil(imageHeight / tileHeight)
    /// </para>
    /// </remarks>
    public sealed partial class ImageImportDialog : ContentDialog
    {
        private readonly int _imageWidth;
        private readonly int _imageHeight;
        private readonly byte[] _imagePixels;
        private readonly string _fileName;

        /// <summary>
        /// Gets the resulting tile size for the document.
        /// </summary>
        public SizeInt32 TileSize { get; private set; }

        /// <summary>
        /// Gets the resulting tile counts (grid dimensions) for the document.
        /// </summary>
        public SizeInt32 TileCounts { get; private set; }

        /// <summary>
        /// Gets the resulting canvas pixel width.
        /// </summary>
        public int CanvasWidth => TileSize.Width * TileCounts.Width;

        /// <summary>
        /// Gets the resulting canvas pixel height.
        /// </summary>
        public int CanvasHeight => TileSize.Height * TileCounts.Height;

        /// <summary>
        /// Gets the original image width.
        /// </summary>
        public int ImageWidth => _imageWidth;

        /// <summary>
        /// Gets the original image height.
        /// </summary>
        public int ImageHeight => _imageHeight;

        /// <summary>
        /// Gets the image pixel data in BGRA format.
        /// </summary>
        public byte[] ImagePixels => _imagePixels;

        /// <summary>
        /// Creates a new image import dialog.
        /// </summary>
        /// <param name="fileName">The source file name for display.</param>
        /// <param name="width">Image width in pixels.</param>
        /// <param name="height">Image height in pixels.</param>
        /// <param name="pixels">Image pixel data in BGRA format.</param>
        public ImageImportDialog(string fileName, int width, int height, byte[] pixels)
        {
            _fileName = fileName;
            _imageWidth = width;
            _imageHeight = height;
            _imagePixels = pixels;

            InitializeComponent();

            // Set initial info
            ImageInfoText.Text = $"{fileName} - {width} x {height} pixels";

            // Initialize with custom tile mode selected
            CustomTileMode.IsChecked = true;
            UpdateCalculations();
        }

        /// <summary>
        /// Loads and displays the image preview.
        /// </summary>
        public async Task LoadPreviewAsync()
        {
            // Preview loading is optional - we removed the preview for simplicity
            await Task.CompletedTask;
        }

        private void TileMode_Changed(object sender, RoutedEventArgs e)
        {
            if (CustomTilePanel != null)
            {
                CustomTilePanel.Visibility = CustomTileMode.IsChecked == true
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            UpdateCalculations();
        }

        private void TileSize_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            UpdateCalculations();
        }

        private void Preset8x8_Click(object sender, RoutedEventArgs e)
        {
            TileWidthBox.Value = 8;
            TileHeightBox.Value = 8;
        }

        private void Preset16x16_Click(object sender, RoutedEventArgs e)
        {
            TileWidthBox.Value = 16;
            TileHeightBox.Value = 16;
        }

        private void Preset32x32_Click(object sender, RoutedEventArgs e)
        {
            TileWidthBox.Value = 32;
            TileHeightBox.Value = 32;
        }

        private void Preset64x64_Click(object sender, RoutedEventArgs e)
        {
            TileWidthBox.Value = 64;
            TileHeightBox.Value = 64;
        }

        /// <summary>
        /// Updates calculated canvas dimensions based on current settings.
        /// </summary>
        private void UpdateCalculations()
        {
            if (ResultTileSizeText == null) return; // Not yet initialized

            int tileW, tileH, tilesX, tilesY;

            if (SingleTileMode.IsChecked == true)
            {
                // Single tile mode: tile size = image size, 1x1 grid
                tileW = _imageWidth;
                tileH = _imageHeight;
                tilesX = 1;
                tilesY = 1;
            }
            else
            {
                // Custom tile mode: calculate grid to fit image
                tileW = Math.Max(1, (int)TileWidthBox.Value);
                tileH = Math.Max(1, (int)TileHeightBox.Value);

                // Calculate tiles needed to fit the entire image (round up)
                tilesX = (_imageWidth + tileW - 1) / tileW;
                tilesY = (_imageHeight + tileH - 1) / tileH;
            }

            // Store results
            TileSize = new SizeInt32(tileW, tileH);
            TileCounts = new SizeInt32(tilesX, tilesY);

            // Update UI
            int canvasW = tileW * tilesX;
            int canvasH = tileH * tilesY;

            ResultTileSizeText.Text = $"{tileW} x {tileH} pixels";
            ResultTileGridText.Text = $"{tilesX} x {tilesY} tiles";
            ResultCanvasSizeText.Text = $"{canvasW} x {canvasH} pixels";

            // Show if canvas is larger than image
            if (canvasW > _imageWidth || canvasH > _imageHeight)
            {
                int extraW = canvasW - _imageWidth;
                int extraH = canvasH - _imageHeight;
                ResultCanvasSizeText.Text += $" (+{extraW}px, +{extraH}px padding)";
            }
        }
    }
}
