using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PixlPunkt.Uno.Constants;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.Enums;
using PixlPunkt.Uno.Core.Imaging;
using PixlPunkt.Uno.UI.Rendering;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace PixlPunkt.Uno.UI.Dialogs.Export
{
    /// <summary>
    /// Dialog for exporting multi-resolution icon (.ico) files with preview.
    /// Shows each icon size with 4 different backgrounds: striped transparency, white, grey, and black.
    /// </summary>
    public sealed partial class IconExportDialog : ContentDialog
    {
        private readonly CanvasDocument _document;
        private readonly byte[] _sourcePixels;
        private readonly int _sourceWidth;
        private readonly int _sourceHeight;

        // Store 4 images per size: striped, white, grey, black
        private readonly List<(Image striped, Image white, Image grey, Image black)> _previewImageSets = new();

        public ScaleMode SelectedScaleMode { get; private set; }

        public IconExportDialog(CanvasDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));

            // Compose document immediately
            var composed = new PixelSurface(_document.PixelWidth, _document.PixelHeight);
            _document.CompositeTo(composed);
            _sourcePixels = composed.Pixels;
            _sourceWidth = _document.PixelWidth;
            _sourceHeight = _document.PixelHeight;

            // Initialize XAML components
            InitializeComponent();

            // Build preview UI FIRST (before setting up combo, to avoid SelectionChanged firing early)
            BuildPreviewUI();

            // Setup scale mode combo AFTER preview UI is ready
            InitializeScaleModeCombo();
        }

        private void InitializeScaleModeCombo()
        {
            // Temporarily detach event handler to prevent firing during initialization
            ScaleModeCombo.SelectionChanged -= ScaleModeCombo_SelectionChanged;

            foreach (var mode in Enum.GetNames(typeof(ScaleMode)))
                ScaleModeCombo.Items.Add(mode);

            ScaleModeCombo.SelectedItem = IconExportConstants.DefaultScaleMode;
            SelectedScaleMode = ScaleMode.Bilinear;

            // Reattach event handler
            ScaleModeCombo.SelectionChanged += ScaleModeCombo_SelectionChanged;
        }

        private void BuildPreviewUI()
        {
            PreviewStack.Children.Clear();
            _previewImageSets.Clear();

            // Only show sizes in PreviewSizes (excludes 256×256 and 128×128)
            foreach (var size in IconExportConstants.PreviewSizes)
            {
                // Show all preview sizes at their actual size (don't cap 64×64)
                int displaySize = size;

                // Create vertical stack for this size
                var sizeColumn = new StackPanel
                {
                    Spacing = 4,
                    Width = IconExportConstants.PreviewBoxWidth,
                    HorizontalAlignment = HorizontalAlignment.Center
                };

                // Size label
                var sizeLabel = new TextBlock
                {
                    Text = $"{size}×{size}",
                    FontSize = IconExportConstants.PreviewLabelFontSize,
                    FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                sizeColumn.Children.Add(sizeLabel);

                // Create 4 background variants
                var stripedImg = CreatePreviewImage(displaySize);
                var whiteImg = CreatePreviewImage(displaySize);
                var greyImg = CreatePreviewImage(displaySize);
                var blackImg = CreatePreviewImage(displaySize);

                // Striped background (transparency pattern)
                var stripedBorder = new Border
                {
                    Width = displaySize,
                    Height = displaySize,
                    Background = new SolidColorBrush(Colors.Transparent),
                    BorderBrush = new SolidColorBrush(Colors.Gray),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 2, 0, 0),
                    Child = stripedImg
                };
                sizeColumn.Children.Add(stripedBorder);

                // White background
                var whiteBorder = new Border
                {
                    Width = displaySize,
                    Height = displaySize,
                    Background = new SolidColorBrush(Colors.White),
                    BorderBrush = new SolidColorBrush(Colors.Gray),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 2, 0, 0),
                    Child = whiteImg
                };
                sizeColumn.Children.Add(whiteBorder);

                // Grey background
                var greyBorder = new Border
                {
                    Width = displaySize,
                    Height = displaySize,
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 170, 170, 170)),
                    BorderBrush = new SolidColorBrush(Colors.Gray),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 2, 0, 0),
                    Child = greyImg
                };
                sizeColumn.Children.Add(greyBorder);

                // Black background
                var blackBorder = new Border
                {
                    Width = displaySize,
                    Height = displaySize,
                    Background = new SolidColorBrush(Colors.Black),
                    BorderBrush = new SolidColorBrush(Colors.Gray),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 2, 0, 0),
                    Child = blackImg
                };
                sizeColumn.Children.Add(blackBorder);

                PreviewStack.Children.Add(sizeColumn);
                _previewImageSets.Add((stripedImg, whiteImg, greyImg, blackImg));
            }
        }

        private static Image CreatePreviewImage(int displaySize)
        {
            return new Image
            {
                Width = displaySize,
                Height = displaySize,
                Stretch = Stretch.Uniform
            };
        }

        public async Task LoadPreviewsAsync()
        {
            await RegeneratePreviews();
        }

        private async void ScaleModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Don't regenerate if we're still initializing (preview UI not built yet)
            if (_previewImageSets.Count == 0)
            {
                return;
            }

            if (ScaleModeCombo.SelectedItem is string modeName)
            {
                SelectedScaleMode = Enum.Parse<ScaleMode>(modeName);
                await RegeneratePreviews();
            }
        }

        private async Task RegeneratePreviews()
        {
            // Safety check: ensure preview UI is built
            if (_previewImageSets.Count == 0)
            {
                return;
            }

            // Only regenerate previews for sizes we're actually showing
            for (int i = 0; i < IconExportConstants.PreviewSizes.Length; i++)
            {
                // Safety check: ensure we have a corresponding image set
                if (i >= _previewImageSets.Count)
                {
                    break;
                }

                int size = IconExportConstants.PreviewSizes[i];
                byte[] resized = ResizeUsingMode(SelectedScaleMode, _sourcePixels, _sourceWidth, _sourceHeight, size, size);

                // Generate 4 variants:
                // 1. With transparency stripes
                var stripedPixels = (byte[])resized.Clone();
                CompositeOverStripes(stripedPixels, size, size);
                var stripedPng = await EncodePngAsync(stripedPixels, size, size);

                // 2-4. Original with alpha preserved (backgrounds set in XAML borders)
                var originalPng = await EncodePngAsync(resized, size, size);

                var (stripedImg, whiteImg, greyImg, blackImg) = _previewImageSets[i];

                await SetImageSource(stripedImg, stripedPng);
                await SetImageSource(whiteImg, originalPng);
                await SetImageSource(greyImg, originalPng);
                await SetImageSource(blackImg, originalPng);
            }
        }

        private static byte[] ResizeUsingMode(ScaleMode mode, byte[] src, int sw, int sh, int dw, int dh)
        {
            return mode switch
            {
                ScaleMode.NearestNeighbor => PixelOps.ResizeNearest(src, sw, sh, dw, dh),
                ScaleMode.Bilinear => PixelOps.ResizeBilinear(src, sw, sh, dw, dh),
                ScaleMode.EPX => PixelOps.ScaleBy2xStepsThenNearest(src, sw, sh, dw, dh, true).buf,
                ScaleMode.Scale2x => PixelOps.ScaleBy2xStepsThenNearest(src, sw, sh, dw, dh, false).buf,
                _ => PixelOps.ResizeNearest(src, sw, sh, dw, dh)
            };
        }

        private static void CompositeOverStripes(byte[] buf, int w, int h)
        {
            const int stripeBand = 8;
            byte wR = TransparencyStripeMixer.LightR;
            byte wG = TransparencyStripeMixer.LightG;
            byte wB = TransparencyStripeMixer.LightB;
            byte gR = TransparencyStripeMixer.DarkR;
            byte gG = TransparencyStripeMixer.DarkG;
            byte gB = TransparencyStripeMixer.DarkB;

            for (int y = 0; y < h; y++)
            {
                int row = y * w * 4;
                for (int x = 0; x < w; x++)
                {
                    int i = row + x * 4;
                    byte b = buf[i + 0];
                    byte g = buf[i + 1];
                    byte r = buf[i + 2];
                    byte a = buf[i + 3];

                    bool stripe = (((x + y) / stripeBand) & 1) == 0;
                    byte baseR = stripe ? wR : gR;
                    byte baseG = stripe ? wG : gG;
                    byte baseB = stripe ? wB : gB;

                    if (a == 0)
                    {
                        buf[i + 0] = baseB;
                        buf[i + 1] = baseG;
                        buf[i + 2] = baseR;
                        buf[i + 3] = 255;
                    }
                    else if (a < 255)
                    {
                        int invA = 255 - a;
                        buf[i + 0] = (byte)((b * a + baseB * invA) / 255);
                        buf[i + 1] = (byte)((g * a + baseG * invA) / 255);
                        buf[i + 2] = (byte)((r * a + baseR * invA) / 255);
                        buf[i + 3] = 255;
                    }
                }
            }
        }

        private static async Task<byte[]> EncodePngAsync(byte[] bgra, int w, int h)
        {
            using var ras = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, ras);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                (uint)w, (uint)h, 96, 96, bgra);
            await encoder.FlushAsync();
            ras.Seek(0);
            var reader = new Windows.Storage.Streams.DataReader(ras.GetInputStreamAt(0));
            var bytes = new byte[ras.Size];
            await reader.LoadAsync((uint)ras.Size);
            reader.ReadBytes(bytes);
            return bytes;
        }

        private static async Task SetImageSource(Image image, byte[] pngBytes)
        {
            using var ras = new InMemoryRandomAccessStream();
            await ras.WriteAsync(pngBytes.AsBuffer());
            ras.Seek(0);
            var bmp = new BitmapImage();
            await bmp.SetSourceAsync(ras);
            image.Source = bmp;
        }

        /// <summary>
        /// Generates final PNG byte arrays for all icon sizes using the selected scale mode.
        /// </summary>
        public async Task<List<byte[]>> GenerateFinalIconPngsAsync()
        {
            var pngList = new List<byte[]>();
            foreach (var size in IconExportConstants.StandardSizes)
            {
                var resized = ResizeUsingMode(SelectedScaleMode, _sourcePixels, _sourceWidth, _sourceHeight, size, size);
                var pngData = await EncodePngAsync(resized, size, size);
                pngList.Add(pngData);
            }
            return pngList;
        }
    }
}
