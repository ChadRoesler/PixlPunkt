using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PixlPunkt.Constants;
using PixlPunkt.Core.Coloring.Helpers;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Imaging;
using PixlPunkt.UI.ColorPick;
using PixlPunkt.UI.Helpers;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using System.Diagnostics;

namespace PixlPunkt.UI.Dialogs.Export
{
    /// <summary>
    /// Dialog for exporting single images in various formats (PNG, GIF, BMP, JPEG, TIFF).
    /// Supports scaling, background color selection for non-alpha formats, and layer separation.
    /// </summary>
    public sealed partial class ImageExportDialog : ContentDialog
    {
        private readonly CanvasDocument _document;
        private readonly byte[] _sourcePixels;
        private readonly int _sourceWidth;
        private readonly int _sourceHeight;
        private Windows.UI.Color _backgroundColor;

        // UI Controls
        private readonly NumberBox _scaleNumberBox;
        private readonly ComboBox _formatComboBox;
        private readonly CheckBox _separateLayersCheckBox;
        private readonly StackPanel _backgroundColorPanel;
        private readonly Border _backgroundColorSwatch;
        private readonly Image _previewImage;
        private readonly TextBlock _previewInfoText;
        private readonly Border _previewBorder;

        public new int Scale { get; private set; }
        public string SelectedFormat { get; private set; }
        public bool SeparateLayers => _separateLayersCheckBox.IsChecked == true;
        public uint BackgroundColor => ColorUtil.ToBGRA(_backgroundColor);

        public ImageExportDialog(CanvasDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));

            // Compose document immediately
            var composed = new PixelSurface(_document.PixelWidth, _document.PixelHeight);
            _document.CompositeTo(composed);
            _sourcePixels = composed.Pixels;
            _sourceWidth = _document.PixelWidth;
            _sourceHeight = _document.PixelHeight;

            // Default background color (white)
            _backgroundColor = Windows.UI.Color.FromArgb(
                ImageExportConstants.DefaultBgA,
                ImageExportConstants.DefaultBgR,
                ImageExportConstants.DefaultBgG,
                ImageExportConstants.DefaultBgB);

            // Setup dialog properties
            Title = "Export Image";
            PrimaryButtonText = "Export";
            CloseButtonText = "Cancel";
            DefaultButton = ContentDialogButton.Primary;

            // Create controls
            _scaleNumberBox = new NumberBox
            {
                Width = 100,
                Minimum = ImageExportConstants.MinScale,
                Maximum = ImageExportConstants.MaxScale,
                Value = ImageExportConstants.DefaultScale,
                SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Hidden  // Hide chevrons
            };

            _formatComboBox = new ComboBox { Width = 160 };
            _separateLayersCheckBox = new CheckBox { Content = DialogMessages.LayersAsSeparateFiles };

            _backgroundColorSwatch = new Border
            {
                Width = 28,
                Height = 18,
                Background = new SolidColorBrush(_backgroundColor),
                BorderBrush = new SolidColorBrush(Colors.Gray),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3)
            };

            var backgroundColorButton = new Button
            {
                Content = _backgroundColorSwatch,
                Padding = new Thickness(4)
            };
            backgroundColorButton.Click += BackgroundColorButton_Click;

            _backgroundColorPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Visibility = Visibility.Collapsed
            };
            _backgroundColorPanel.Children.Add(new TextBlock
            {
                Text = DialogMessages.BackgroundColorLabel,
                VerticalAlignment = VerticalAlignment.Center
            });
            _backgroundColorPanel.Children.Add(backgroundColorButton);

            _previewImage = new Image { Stretch = Stretch.Uniform };
            _previewInfoText = new TextBlock
            {
                FontSize = 11,
                Opacity = 0.7
            };

            _previewBorder = new Border
            {
                BorderBrush = new SolidColorBrush(Colors.Gray),
                BorderThickness = new Thickness(1),
                MaxWidth = ImageExportConstants.PreviewMaxWidth,
                MaxHeight = ImageExportConstants.PreviewMaxHeight,
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = _previewImage
            };

            // Build UI
            Content = BuildDialogContent();

            // Initialize after UI is built
            Scale = ImageExportConstants.DefaultScale;
            SelectedFormat = ImageExportConstants.DefaultFormat;
            InitializeFormatComboBox();

            // Attach event handlers after initialization
            _scaleNumberBox.ValueChanged += ScaleNumberBox_ValueChanged;
        }

        private UIElement BuildDialogContent()
        {
            var mainPanel = new StackPanel { Spacing = 12, Padding = new Thickness(4) };

            // Row 1: Scale and Format
            var row1 = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            row1.Children.Add(new TextBlock
            {
                Text = DialogMessages.PixelScaleLabel,
                VerticalAlignment = VerticalAlignment.Center
            });
            row1.Children.Add(_scaleNumberBox);
            row1.Children.Add(new TextBlock
            {
                Text = DialogMessages.FormatLabel,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0)
            });
            row1.Children.Add(_formatComboBox);
            mainPanel.Children.Add(row1);

            // Row 2: Options
            mainPanel.Children.Add(_separateLayersCheckBox);
            mainPanel.Children.Add(_backgroundColorPanel);

            // Preview section
            var previewSection = new StackPanel { Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
            previewSection.Children.Add(new TextBlock
            {
                Text = "Preview",
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
            });
            previewSection.Children.Add(_previewBorder);
            previewSection.Children.Add(_previewInfoText);
            mainPanel.Children.Add(previewSection);

            return mainPanel;
        }

        private void InitializeFormatComboBox()
        {
            for (int i = 0; i < ImageExportConstants.SupportedFormats.Length; i++)
            {
                _formatComboBox.Items.Add(ImageExportConstants.FormatDisplayNames[i]);
            }

            int defaultIndex = Array.IndexOf(
                ImageExportConstants.SupportedFormats,
                ImageExportConstants.DefaultFormat);
            _formatComboBox.SelectedIndex = defaultIndex;

            _formatComboBox.SelectionChanged += FormatComboBox_SelectionChanged;
        }

        private void UpdateBackgroundColorVisibility()
        {
            // Show background color picker only for formats that don't support alpha
            bool needsBackground = Array.IndexOf(
                ImageExportConstants.FormatsRequiringBackground,
                SelectedFormat) >= 0;

            _backgroundColorPanel.Visibility = needsBackground
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public async Task LoadPreviewAsync()
        {
            await RegeneratePreview();
        }

        private async void ScaleNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            // Coerce to positive integer (minimum 1)
            int newScale = Math.Max(
                ImageExportConstants.MinScale,
                (int)Math.Round(Math.Max(1, args.NewValue)));  // Ensure positive

            if (_scaleNumberBox.Value != newScale)
            {
                _scaleNumberBox.Value = newScale;
                return;
            }

            Scale = newScale;
            await RegeneratePreview();
        }

        private async void FormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_formatComboBox.SelectedIndex >= 0)
            {
                SelectedFormat = ImageExportConstants.SupportedFormats[_formatComboBox.SelectedIndex];
                UpdateBackgroundColorVisibility();
                await RegeneratePreview();
            }
        }

        private void BackgroundColorButton_Click(object sender, RoutedEventArgs e)
        {
            // Open color picker window
            var colorPicker = new ColorPickerWindow
            {
                GetCurrent = () => ColorUtil.MakeOpaque(_backgroundColor),
                SetLive = c =>
                {
                    _backgroundColor = ColorUtil.MakeOpaque(c);
                    _backgroundColorSwatch.Background = new SolidColorBrush(_backgroundColor);
                    _ = RegeneratePreview();
                },
                Commit = c =>
                {
                    _backgroundColor = ColorUtil.MakeOpaque(c);
                    _backgroundColorSwatch.Background = new SolidColorBrush(_backgroundColor);
                    _ = RegeneratePreview();
                }
            };

            colorPicker.Load(ColorUtil.MakeOpaque(_backgroundColor), ColorUtil.MakeOpaque(_backgroundColor));
            colorPicker.Activate();

            var appWindow = WindowHost.ApplyChrome(
                colorPicker,
                resizable: false,
                alwaysOnTop: true,
                minimizable: false,
                title: "Background Color",
                owner: App.PixlPunktMainWindow);

            WindowHost.FitToContentAfterLayout(
                colorPicker,
                (FrameworkElement)colorPicker.Content,
                maxScreenFraction: 0.90,
                minLogicalWidth: 420,
                minLogicalHeight: 380);

            WindowHost.Place(appWindow, WindowPlacement.CenterOnScreen);
        }

        private async Task RegeneratePreview()
        {
            try
            {
                // Calculate preview size
                int previewWidth = _sourceWidth * Scale;
                int previewHeight = _sourceHeight * Scale;

                // Create preview pixel buffer
                byte[] previewPixels = new byte[previewWidth * previewHeight * 4];

                // Scale using nearest neighbor
                for (int y = 0; y < previewHeight; y++)
                {
                    int sy = Math.Min(_sourceHeight - 1, y / Math.Max(1, Scale));
                    for (int x = 0; x < previewWidth; x++)
                    {
                        int sx = Math.Min(_sourceWidth - 1, x / Math.Max(1, Scale));
                        int srcIdx = (sy * _sourceWidth + sx) * 4;
                        int dstIdx = (y * previewWidth + x) * 4;

                        previewPixels[dstIdx + 0] = _sourcePixels[srcIdx + 0];
                        previewPixels[dstIdx + 1] = _sourcePixels[srcIdx + 1];
                        previewPixels[dstIdx + 2] = _sourcePixels[srcIdx + 2];
                        previewPixels[dstIdx + 3] = _sourcePixels[srcIdx + 3];
                    }
                }

                // Apply background if format doesn't support alpha
                bool needsBackground = Array.IndexOf(
                    ImageExportConstants.FormatsRequiringBackground,
                    SelectedFormat) >= 0;

                if (needsBackground)
                {
                    ApplyBackgroundColor(previewPixels, previewWidth, previewHeight);
                }

                // Encode to PNG for preview
                var pngBytes = await EncodePngAsync(previewPixels, previewWidth, previewHeight);

                // Set preview image
                using var stream = new InMemoryRandomAccessStream();
                await stream.WriteAsync(pngBytes.AsBuffer());
                stream.Seek(0);

                var bitmap = new BitmapImage();
                await bitmap.SetSourceAsync(stream);
                _previewImage.Source = bitmap;

                // Update info text
                _previewInfoText.Text = $"{previewWidth}×{previewHeight} pixels ({SelectedFormat.ToUpperInvariant()})";

                // Update preview border size
                _previewBorder.Width = Math.Min(previewWidth, ImageExportConstants.PreviewMaxWidth);
                _previewBorder.Height = Math.Min(previewHeight, ImageExportConstants.PreviewMaxHeight);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Preview regeneration failed: {ex}");
            }
        }

        private void ApplyBackgroundColor(byte[] pixels, int width, int height)
        {
            byte bgB = _backgroundColor.B;
            byte bgG = _backgroundColor.G;
            byte bgR = _backgroundColor.R;

            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte a = pixels[i + 3];

                if (a == 0)
                {
                    // Fully transparent → use background
                    pixels[i + 0] = bgB;
                    pixels[i + 1] = bgG;
                    pixels[i + 2] = bgR;
                    pixels[i + 3] = 255;
                }
                else if (a < 255)
                {
                    // Semi-transparent → blend with background
                    int invA = 255 - a;
                    pixels[i + 0] = (byte)((pixels[i + 0] * a + bgB * invA) / 255);
                    pixels[i + 1] = (byte)((pixels[i + 1] * a + bgG * invA) / 255);
                    pixels[i + 2] = (byte)((pixels[i + 2] * a + bgR * invA) / 255);
                    pixels[i + 3] = 255;
                }
            }
        }

        private static async Task<byte[]> EncodePngAsync(byte[] bgra, int w, int h)
        {
            using var stream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                (uint)w,
                (uint)h,
                96,
                96,
                bgra);
            await encoder.FlushAsync();

            stream.Seek(0);
            var reader = new Windows.Storage.Streams.DataReader(stream.GetInputStreamAt(0));
            var bytes = new byte[stream.Size];
            await reader.LoadAsync((uint)stream.Size);
            reader.ReadBytes(bytes);
            return bytes;
        }

        /// <summary>
        /// Gets the final export pixels with the selected scale and background applied.
        /// </summary>
        public byte[] GetExportPixels()
        {
            int exportWidth = _sourceWidth * Scale;
            int exportHeight = _sourceHeight * Scale;
            byte[] exportPixels = new byte[exportWidth * exportHeight * 4];

            // Scale pixels
            for (int y = 0; y < exportHeight; y++)
            {
                int sy = Math.Min(_sourceHeight - 1, y / Math.Max(1, Scale));
                for (int x = 0; x < exportWidth; x++)
                {
                    int sx = Math.Min(_sourceWidth - 1, x / Math.Max(1, Scale));
                    int srcIdx = (sy * _sourceWidth + sx) * 4;
                    int dstIdx = (y * exportWidth + x) * 4;

                    exportPixels[dstIdx + 0] = _sourcePixels[srcIdx + 0];
                    exportPixels[dstIdx + 1] = _sourcePixels[srcIdx + 1];
                    exportPixels[dstIdx + 2] = _sourcePixels[srcIdx + 2];
                    exportPixels[dstIdx + 3] = _sourcePixels[srcIdx + 3];
                }
            }

            // Apply background if needed
            bool needsBackground = Array.IndexOf(
                ImageExportConstants.FormatsRequiringBackground,
                SelectedFormat) >= 0;

            if (needsBackground)
            {
                ApplyBackgroundColor(exportPixels, exportWidth, exportHeight);
            }

            return exportPixels;
        }

        /// <summary>
        /// Gets the export dimensions (width and height after scaling).
        /// </summary>
        public (int width, int height) GetExportDimensions()
        {
            return (_sourceWidth * Scale, _sourceHeight * Scale);
        }
    }
}
