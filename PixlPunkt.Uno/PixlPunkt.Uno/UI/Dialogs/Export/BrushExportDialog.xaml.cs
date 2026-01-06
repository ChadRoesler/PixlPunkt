using System;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using PixlPunkt.Uno.Constants;
using PixlPunkt.Uno.Core.Brush;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.Document.Layer;
using PixlPunkt.Uno.Core.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace PixlPunkt.Uno.UI.Dialogs.Export
{
    /// <summary>
    /// Dialog for exporting a document as a custom brush (.mrk file).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Custom brushes use a single 16x16 mask. The dialog validates that
    /// the document is 16x16 pixels and has content.
    /// </para>
    /// </remarks>
    public sealed partial class BrushExportDialog : ContentDialog
    {
        private readonly CanvasDocument _doc;
        private bool _hasContent;
        private (int x, int y, int w, int h)? _contentBounds;

        /// <summary>
        /// Gets the user-entered author name.
        /// </summary>
        public string Author => string.IsNullOrWhiteSpace(AuthorBox.Text)
            ? BrushExportConstants.DefaultAuthor
            : AuthorBox.Text.Trim();

        /// <summary>
        /// Gets the user-entered brush name.
        /// </summary>
        public string BrushName => string.IsNullOrWhiteSpace(BrushNameBox.Text)
            ? BrushExportConstants.DefaultBrushName
            : BrushNameBox.Text.Trim();

        /// <summary>
        /// Gets the full brush identifier (author.brushname).
        /// </summary>
        public string FullName => $"{Author}.{BrushName}";

        /// <summary>
        /// Gets the selected pivot X coordinate (0.0, 0.5, or 1.0).
        /// </summary>
        public float PivotX { get; private set; } = 0.5f;

        /// <summary>
        /// Gets the selected pivot Y coordinate (0.0, 0.5, or 1.0).
        /// </summary>
        public float PivotY { get; private set; } = 0.5f;

        /// <summary>
        /// Gets whether the document is valid for brush export.
        /// </summary>
        public bool IsValidForExport { get; private set; }

        /// <summary>
        /// Creates a new brush export dialog for the given document.
        /// </summary>
        public BrushExportDialog(CanvasDocument doc)
        {
            _doc = doc;
            InitializeComponent();

            // Set default name from document
            BrushNameBox.Text = SanitizeBrushName(doc.Name ?? BrushExportConstants.DefaultBrushName);
            AuthorBox.Text = BrushExportConstants.DefaultAuthor;

            // Wire text changed to update full name preview
            AuthorBox.TextChanged += (s, e) => UpdateFullNamePreview();
            BrushNameBox.TextChanged += (s, e) => UpdateFullNamePreview();
            UpdateFullNamePreview();

            // Wire pivot radio buttons
            PivotTL.Checked += (s, e) => { PivotX = 0.0f; PivotY = 0.0f; };
            PivotTC.Checked += (s, e) => { PivotX = 0.5f; PivotY = 0.0f; };
            PivotTR.Checked += (s, e) => { PivotX = 1.0f; PivotY = 0.0f; };
            PivotML.Checked += (s, e) => { PivotX = 0.0f; PivotY = 0.5f; };
            PivotMC.Checked += (s, e) => { PivotX = 0.5f; PivotY = 0.5f; };
            PivotMR.Checked += (s, e) => { PivotX = 1.0f; PivotY = 0.5f; };
            PivotBL.Checked += (s, e) => { PivotX = 0.0f; PivotY = 1.0f; };
            PivotBC.Checked += (s, e) => { PivotX = 0.5f; PivotY = 1.0f; };
            PivotBR.Checked += (s, e) => { PivotX = 1.0f; PivotY = 1.0f; };
        }

        /// <summary>
        /// Sanitizes a brush name by removing spaces and special characters.
        /// </summary>
        private static string SanitizeBrushName(string name)
        {
            return name.Replace(" ", "").Replace(".", "_").Replace("-", "_");
        }

        /// <summary>
        /// Updates the full name preview text.
        /// </summary>
        private void UpdateFullNamePreview()
        {
            FullNamePreview.Text = FullName;
        }

        /// <summary>
        /// Analyzes the document and validates it for brush export.
        /// </summary>
        public async Task AnalyzeLayersAsync()
        {
            _hasContent = false;
            _contentBounds = null;
            IsValidForExport = false;

            const int expectedSize = BrushExportConstants.MaskSize; // 16

            // Check document size
            if (_doc.PixelWidth != expectedSize || _doc.PixelHeight != expectedSize)
            {
                ShowValidationError($"Document must be {expectedSize}x{expectedSize} pixels. Current size: {_doc.PixelWidth}x{_doc.PixelHeight}");
                return;
            }

            // Check for content in any visible layer
            var compositeSurface = new PixelSurface(expectedSize, expectedSize);
            foreach (var layer in _doc.Layers)
            {
                if (layer is RasterLayer rl && rl.Visible)
                {
                    CompositeLayer(compositeSurface, rl.Surface);
                }
            }

            // Find content bounds
            _contentBounds = GetContentBounds(compositeSurface);
            _hasContent = _contentBounds.HasValue;

            if (!_hasContent)
            {
                ShowValidationError("No content found in the document. Draw your brush shape in the 16x16 canvas.");
                return;
            }

            // Document is valid
            IsValidForExport = true;
            ValidationMessage.Visibility = Visibility.Collapsed;
            IsPrimaryButtonEnabled = true;

            // Update tier status UI to show "16px" check
            UpdateTierStatusUI();

            // Generate outline icon preview
            await GenerateOutlineIconPreviewAsync(compositeSurface);
        }

        /// <summary>
        /// Composites a layer onto the target surface.
        /// </summary>
        private static void CompositeLayer(PixelSurface target, PixelSurface source)
        {
            if (target.Width != source.Width || target.Height != source.Height)
                return;

            for (int i = 0; i < target.Pixels.Length; i += 4)
            {
                byte srcA = source.Pixels[i + 3];
                if (srcA > 0)
                {
                    // Simple over blend
                    target.Pixels[i + 0] = source.Pixels[i + 0];
                    target.Pixels[i + 1] = source.Pixels[i + 1];
                    target.Pixels[i + 2] = source.Pixels[i + 2];
                    target.Pixels[i + 3] = srcA;
                }
            }
        }

        /// <summary>
        /// Shows a validation error message.
        /// </summary>
        private void ShowValidationError(string message)
        {
            ValidationMessage.Text = message;
            ValidationMessage.Visibility = Visibility.Visible;
            IsPrimaryButtonEnabled = false;
        }

        /// <summary>
        /// Updates the tier status UI for single 16x16 mask.
        /// </summary>
        private void UpdateTierStatusUI()
        {
            const string checkGlyph = "\uE73E";  // Checkmark
            const string crossGlyph = "\uE711";  // X

            // Only show 16px tier (hide the others)
            Tier16Icon.Visibility = Visibility.Collapsed;
            Tier16Text.Visibility = Visibility.Collapsed;

            // Update 16px status (reusing Tier8 controls)
            Tier16Icon.Visibility = Visibility.Visible;
            Tier16Text.Visibility = Visibility.Visible;
            Tier16Icon.Glyph = _hasContent ? checkGlyph : crossGlyph;
            Tier16Icon.Foreground = _hasContent
                ? new SolidColorBrush(Colors.LimeGreen)
                : new SolidColorBrush(Colors.OrangeRed);
            Tier16Text.Text = "16px";
            Tier16Text.Opacity = _hasContent ? 1.0 : 0.5;
        }

        /// <summary>
        /// Generates the outline icon preview from the composite surface.
        /// </summary>
        private async Task GenerateOutlineIconPreviewAsync(PixelSurface surface)
        {
            if (!_hasContent)
            {
                IconPreview.Source = null;
                return;
            }

            // Generate 32x32 outline icon from 16x16 surface (2x scale)
            var outlineIcon = GenerateOutlineIconFromSurface(surface);

            // Convert to BitmapImage for display
            var bitmap = await CreateBitmapFromBgraAsync(outlineIcon, BrushExportConstants.IconSize, BrushExportConstants.IconSize);
            IconPreview.Source = bitmap;
        }

        /// <summary>
        /// Generates a 32x32 outline icon from a 16x16 surface.
        /// </summary>
        private static byte[] GenerateOutlineIconFromSurface(PixelSurface surface)
        {
            const int iconSize = 32;
            const int maskSize = 16;
            var icon = new byte[iconSize * iconSize * 4];

            // Scale 16x16 to 32x32 (2x)
            var filled = new bool[iconSize, iconSize];
            for (int y = 0; y < iconSize; y++)
            {
                int srcY = y / 2;
                for (int x = 0; x < iconSize; x++)
                {
                    int srcX = x / 2;
                    int srcIdx = (srcY * maskSize + srcX) * 4;
                    if (surface.Pixels[srcIdx + 3] > 0)
                        filled[x, y] = true;
                }
            }

            // Find outline pixels (edges) - WHITE color
            for (int y = 0; y < iconSize; y++)
            {
                for (int x = 0; x < iconSize; x++)
                {
                    if (!filled[x, y])
                        continue;

                    bool isEdge = false;
                    for (int dy = -1; dy <= 1 && !isEdge; dy++)
                    {
                        for (int dx = -1; dx <= 1 && !isEdge; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx < 0 || nx >= iconSize || ny < 0 || ny >= iconSize || !filled[nx, ny])
                                isEdge = true;
                        }
                    }

                    if (isEdge)
                    {
                        int idx = (y * iconSize + x) * 4;
                        icon[idx + 0] = 255; // B - WHITE
                        icon[idx + 1] = 255; // G - WHITE
                        icon[idx + 2] = 255; // R - WHITE
                        icon[idx + 3] = 255; // A
                    }
                }
            }

            return icon;
        }

        /// <summary>
        /// Generates the final BrushTemplate from the document.
        /// </summary>
        public BrushTemplate GenerateBrushTemplate()
        {
            var brush = new BrushTemplate(Author, BrushName)
            {
                PivotX = PivotX,
                PivotY = PivotY
            };

            const int maskSize = BrushExportConstants.MaskSize; // 16

            // Composite all visible layers
            var compositeSurface = new PixelSurface(maskSize, maskSize);
            foreach (var layer in _doc.Layers)
            {
                if (layer is RasterLayer rl && rl.Visible)
                {
                    CompositeLayer(compositeSurface, rl.Surface);
                }
            }

            // Create 16x16 mask (32 bytes)
            brush.Mask = BrushTemplate.CreateMaskFromBgra(compositeSurface.Pixels);

            // Generate 32x32 outline icon
            brush.IconData = GenerateOutlineIconFromSurface(compositeSurface);
            brush.IconWidth = BrushExportConstants.IconSize;
            brush.IconHeight = BrushExportConstants.IconSize;

            return brush;
        }

        /// <summary>
        /// Gets the bounding box of non-transparent pixels in a surface.
        /// </summary>
        private static (int x, int y, int w, int h)? GetContentBounds(PixelSurface surface)
        {
            int minX = int.MaxValue, minY = int.MaxValue;
            int maxX = int.MinValue, maxY = int.MinValue;

            for (int y = 0; y < surface.Height; y++)
            {
                for (int x = 0; x < surface.Width; x++)
                {
                    int idx = (y * surface.Width + x) * 4;
                    if (surface.Pixels[idx + 3] > 0)
                    {
                        minX = Math.Min(minX, x);
                        minY = Math.Min(minY, y);
                        maxX = Math.Max(maxX, x);
                        maxY = Math.Max(maxY, y);
                    }
                }
            }

            if (minX > maxX || minY > maxY)
                return null;

            return (minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        /// <summary>
        /// Creates a BitmapImage from BGRA byte array.
        /// </summary>
        private static async Task<BitmapImage> CreateBitmapFromBgraAsync(byte[] bgra, int width, int height)
        {
            using var stream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
                (uint)width, (uint)height, 96, 96, bgra);
            await encoder.FlushAsync();

            stream.Seek(0);
            var bitmap = new BitmapImage();
            await bitmap.SetSourceAsync(stream);
            return bitmap;
        }
    }
}
