using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PixlPunkt.Uno.Constants;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.Enums;
using PixlPunkt.Uno.Core.Imaging;
using PixlPunkt.Uno.UI.Rendering;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace PixlPunkt.Uno.UI.Dialogs.Export
{
    /// <summary>
    /// Dialog for exporting cursor (.cur) files with hotspot selection and multi-resolution options.
    /// </summary>
    public sealed partial class CursorExportDialog : ContentDialog
    {
        private readonly CanvasDocument _document;
        private byte[] _currentCursorPixels = Array.Empty<byte>();
        private int _hotspotX = CursorExportConstants.DefaultHotspotX;
        private int _hotspotY = CursorExportConstants.DefaultHotspotY;
        private int _hoverX = -1;
        private int _hoverY = -1;

        public int HotspotX => _hotspotX;
        public int HotspotY => _hotspotY;
        public ScaleMode SelectedScaleMode { get; private set; }
        public bool SeparateLayers => SeparateLayersCheck.IsChecked == true;
        public bool MultiResolution => MultiResolutionCheck.IsChecked == true;

        public CursorExportDialog(CanvasDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            InitializeComponent();
            InitializeScaleModeCombo();
            SetupPreviewSizes();
        }

        private void InitializeScaleModeCombo()
        {
            foreach (var mode in Enum.GetNames(typeof(ScaleMode)))
                ScaleModeCombo.Items.Add(mode);

            ScaleModeCombo.SelectedItem = ScaleMode.Bilinear.ToString();
            SelectedScaleMode = ScaleMode.Bilinear;
        }

        private void SetupPreviewSizes()
        {
            ActualSizeLabel.Text = $"Actual {CursorExportConstants.PreviewActualSize}�{CursorExportConstants.PreviewActualSize}";
            EnlargedSizeLabel.Text = $"Hotspot Preview (�{CursorExportConstants.PreviewEnlargementFactor} = {CursorExportConstants.PreviewEnlargedSize}�{CursorExportConstants.PreviewEnlargedSize})";

            // Set sizes
            ActualImageStriped.Width = ActualImageStriped.Height = CursorExportConstants.PreviewActualSize;
            ActualImageGrey.Width = ActualImageGrey.Height = CursorExportConstants.PreviewActualSize;
            ActualImageWhite.Width = ActualImageWhite.Height = CursorExportConstants.PreviewActualSize;
            ActualImageBlack.Width = ActualImageBlack.Height = CursorExportConstants.PreviewActualSize;

            ActualStripedBorder.Width = ActualStripedBorder.Height = CursorExportConstants.PreviewActualSize;
            ActualGreyBorder.Width = ActualGreyBorder.Height = CursorExportConstants.PreviewActualSize;
            ActualWhiteBorder.Width = ActualWhiteBorder.Height = CursorExportConstants.PreviewActualSize;
            ActualBlackBorder.Width = ActualBlackBorder.Height = CursorExportConstants.PreviewActualSize;

            EnlargedHost.Width = EnlargedHost.Height = CursorExportConstants.PreviewEnlargedSize;
            EnlargedImage.Width = EnlargedImage.Height = CursorExportConstants.PreviewEnlargedSize;
            EnlargedBorder.Width = EnlargedBorder.Height = CursorExportConstants.PreviewEnlargedSize;
            HitCanvas.Width = HitCanvas.Height = CursorExportConstants.PreviewEnlargedSize;
            GridOverlay.Width = GridOverlay.Height = CursorExportConstants.PreviewEnlargedSize;
            HighlightLayer.Width = HighlightLayer.Height = CursorExportConstants.PreviewEnlargedSize;
            HoverLayer.Width = HoverLayer.Height = CursorExportConstants.PreviewEnlargedSize;

            // Background colors
            ActualStripedBorder.Background = new SolidColorBrush(Colors.Transparent);
            ActualGreyBorder.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 170, 170, 170));
            ActualWhiteBorder.Background = new SolidColorBrush(Colors.White);
            ActualBlackBorder.Background = new SolidColorBrush(Colors.Black);

            // Hotspot indicator sizing
            HighlightBorder.Width = HighlightBorder.Height = CursorExportConstants.GridCellSize;
            HoverBorder.Width = HoverBorder.Height = CursorExportConstants.GridCellSize;
            HoverContainer.Width = HoverContainer.Height = CursorExportConstants.GridCellSize;

            DrawGridOverlay();
        }

        private void DrawGridOverlay()
        {
            GridOverlay.Children.Clear();

            for (int i = 1; i < CursorExportConstants.PreviewActualSize; i++)
            {
                double offset = i * CursorExportConstants.GridCellSize;

                // Vertical line
                var vLine = new Border
                {
                    Width = 1,
                    Height = CursorExportConstants.PreviewEnlargedSize,
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(
                        (byte)(255 * CursorExportConstants.GridLineOpacity), 128, 128, 128))
                };
                Canvas.SetLeft(vLine, offset);
                GridOverlay.Children.Add(vLine);

                // Horizontal line
                var hLine = new Border
                {
                    Width = CursorExportConstants.PreviewEnlargedSize,
                    Height = 1,
                    Background = new SolidColorBrush(Windows.UI.Color.FromArgb(
                        (byte)(255 * CursorExportConstants.GridLineOpacity), 128, 128, 128))
                };
                Canvas.SetTop(hLine, offset);
                GridOverlay.Children.Add(hLine);
            }
        }

        public async Task LoadPreviewAsync()
        {
            // Compose document
            var composed = new PixelSurface(_document.PixelWidth, _document.PixelHeight);
            _document.CompositeTo(composed);

            // Resize to cursor size
            _currentCursorPixels = ResizeUsingMode(
                SelectedScaleMode,
                composed.Pixels,
                _document.PixelWidth,
                _document.PixelHeight,
                CursorExportConstants.PreviewActualSize,
                CursorExportConstants.PreviewActualSize);

            await RefreshPreviews();
        }

        private async Task RefreshPreviews()
        {
            // Actual size previews
            var actualPng = await EncodePngAsync(_currentCursorPixels,
                CursorExportConstants.PreviewActualSize,
                CursorExportConstants.PreviewActualSize);

            var stripedPixels = (byte[])_currentCursorPixels.Clone();
            CompositeOverStripes(stripedPixels,
                CursorExportConstants.PreviewActualSize,
                CursorExportConstants.PreviewActualSize);
            var stripedPng = await EncodePngAsync(stripedPixels,
                CursorExportConstants.PreviewActualSize,
                CursorExportConstants.PreviewActualSize);

            await SetImageSource(ActualImageStriped, stripedPng);
            await SetImageSource(ActualImageGrey, actualPng);
            await SetImageSource(ActualImageWhite, actualPng);
            await SetImageSource(ActualImageBlack, actualPng);

            // Enlarged preview
            var enlarged = PixelOps.ResizeNearest(_currentCursorPixels,
                CursorExportConstants.PreviewActualSize,
                CursorExportConstants.PreviewActualSize,
                CursorExportConstants.PreviewEnlargedSize,
                CursorExportConstants.PreviewEnlargedSize);

            CompositeOverStripes(enlarged,
                CursorExportConstants.PreviewEnlargedSize,
                CursorExportConstants.PreviewEnlargedSize);

            var enlargedPng = await EncodePngAsync(enlarged,
                CursorExportConstants.PreviewEnlargedSize,
                CursorExportConstants.PreviewEnlargedSize);

            await SetImageSource(EnlargedImage, enlargedPng);

            UpdateHotspotIndicator();
        }

        private void UpdateHotspotIndicator()
        {
            Canvas.SetLeft(HighlightBorder, _hotspotX * CursorExportConstants.GridCellSize);
            Canvas.SetTop(HighlightBorder, _hotspotY * CursorExportConstants.GridCellSize);

            // Update color contrast
            var rgb = SamplePixelColor(_hotspotX, _hotspotY);
            var brush = (SolidColorBrush)HighlightBorder.BorderBrush;
            brush.Color = GetContrastColor(rgb);
        }

        private void UpdateHoverIndicator()
        {
            if (_hoverX < 0 || _hoverY < 0)
            {
                HoverContainer.Visibility = Visibility.Collapsed;
                return;
            }

            HoverContainer.Visibility = Visibility.Visible;
            Canvas.SetLeft(HoverContainer, _hoverX * CursorExportConstants.GridCellSize);
            Canvas.SetTop(HoverContainer, _hoverY * CursorExportConstants.GridCellSize);

            // Update X lines
            double cellSize = CursorExportConstants.GridCellSize;
            HoverLineX1.X1 = 1; HoverLineX1.Y1 = 1;
            HoverLineX1.X2 = cellSize - 2; HoverLineX1.Y2 = cellSize - 2;
            HoverLineX2.X1 = cellSize - 2; HoverLineX2.Y1 = 1;
            HoverLineX2.X2 = 1; HoverLineX2.Y2 = cellSize - 2;

            var rgb = SamplePixelColor(_hoverX, _hoverY);
            var color = GetContrastColor(rgb);
            ((SolidColorBrush)HoverBorder.BorderBrush).Color = color;
            ((SolidColorBrush)HoverLineX1.Stroke).Color = color;
            ((SolidColorBrush)HoverLineX2.Stroke).Color = color;
        }

        private (byte r, byte g, byte b) SamplePixelColor(int x, int y)
        {
            if (_currentCursorPixels.Length < CursorExportConstants.PreviewActualSize * CursorExportConstants.PreviewActualSize * 4)
                return (255, 255, 255);

            if (x < 0 || y < 0 || x >= CursorExportConstants.PreviewActualSize || y >= CursorExportConstants.PreviewActualSize)
                return (255, 255, 255);

            int i = (y * CursorExportConstants.PreviewActualSize + x) * 4;
            byte b = _currentCursorPixels[i + 0];
            byte g = _currentCursorPixels[i + 1];
            byte r = _currentCursorPixels[i + 2];
            byte a = _currentCursorPixels[i + 3];

            // Apply stripe background
            const int stripeBand = 8;
            bool stripe = (((x + y) / stripeBand) & 1) == 0;
            byte baseR = stripe ? (byte)255 : (byte)230;
            byte baseG = stripe ? (byte)255 : (byte)230;
            byte baseB = stripe ? (byte)255 : (byte)230;

            if (a == 255) return (r, g, b);
            if (a == 0) return (baseR, baseG, baseB);

            int invA = 255 - a;
            byte outR = (byte)((r * a + baseR * invA) / 255);
            byte outG = (byte)((g * a + baseG * invA) / 255);
            byte outB = (byte)((b * a + baseB * invA) / 255);
            return (outR, outG, outB);
        }

        private Windows.UI.Color GetContrastColor((byte r, byte g, byte b) rgb)
        {
            double lum = 0.299 * rgb.r + 0.587 * rgb.g + 0.114 * rgb.b;
            return lum < 80 ? Colors.White : Colors.Black;
        }

        private void HitCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(HitCanvas).Position;
            var (cx, cy, inside) = MapPointerToCell(point);

            if (inside)
            {
                _hotspotX = cx;
                _hotspotY = cy;
                UpdateHotspotIndicator();
            }
        }

        private void HitCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var point = e.GetCurrentPoint(HitCanvas).Position;
            var (cx, cy, inside) = MapPointerToCell(point);

            if (!inside)
            {
                _hoverX = _hoverY = -1;
            }
            else if (_hoverX != cx || _hoverY != cy)
            {
                _hoverX = cx;
                _hoverY = cy;
            }

            UpdateHoverIndicator();
        }

        private void HitCanvas_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            _hoverX = _hoverY = -1;
            UpdateHoverIndicator();
        }

        private (int x, int y, bool inside) MapPointerToCell(Point point)
        {
            if (point.X < 0 || point.Y < 0 ||
                point.X >= CursorExportConstants.PreviewEnlargedSize ||
                point.Y >= CursorExportConstants.PreviewEnlargedSize)
                return (-1, -1, false);

            int cx = (int)(point.X / CursorExportConstants.GridCellSize);
            int cy = (int)(point.Y / CursorExportConstants.GridCellSize);

            if (cx < 0 || cy < 0 ||
                cx >= CursorExportConstants.PreviewActualSize ||
                cy >= CursorExportConstants.PreviewActualSize)
                return (-1, -1, false);

            return (cx, cy, true);
        }

        private async void ScaleModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScaleModeCombo.SelectedItem is string modeName)
            {
                SelectedScaleMode = Enum.Parse<ScaleMode>(modeName);
                await LoadPreviewAsync();
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
    }
}
