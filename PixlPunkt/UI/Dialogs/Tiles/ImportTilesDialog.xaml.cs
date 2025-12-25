using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Tile;
using Windows.Graphics.Imaging;
using System.Threading.Tasks;

namespace PixlPunkt.UI.Dialogs
{
    /// <summary>
    /// Represents a tile available for import with selection state.
    /// </summary>
    public sealed class ImportTileItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private int _tileId;
        private byte[] _pixels = Array.Empty<byte>();
        private int _width;
        private int _height;
        private BitmapImage? _preview;

        public int TileId
        {
            get => _tileId;
            set => _tileId = value;
        }

        public byte[] Pixels
        {
            get => _pixels;
            set => _pixels = value;
        }

        public int Width
        {
            get => _width;
            set => _width = value;
        }

        public int Height
        {
            get => _height;
            set => _height = value;
        }

        public BitmapImage? Preview
        {
            get => _preview;
            set => _preview = value;
        }

        public string IdText => $"#{TileId}";

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>
    /// Represents a document available as import source.
    /// </summary>
    public sealed class ImportDocumentItem
    {
        private CanvasDocument _document = null!;

        public CanvasDocument Document
        {
            get => _document;
            set => _document = value;
        }

        public string Name => Document?.Name ?? "Untitled";
        public string TileCountText => Document?.TileSet != null
            ? $"({Document.TileSet.Count} tiles)"
            : "(no tiles)";
    }

    /// <summary>
    /// Dialog for importing tiles from another open document.
    /// </summary>
    public sealed partial class ImportTilesDialog : ContentDialog, INotifyPropertyChanged
    {
        private readonly CanvasDocument _targetDocument;
        private ImportDocumentItem? _selectedDocument;

        // UI element references (will be populated by InitializeComponent)
        private InfoBar? _sizeWarning;
        private TextBlock? _selectionSummary;
        private CheckBox? _duplicateHandling;

        public ObservableCollection<ImportDocumentItem> Documents { get; } = new();
        public ObservableCollection<ImportTileItem> TileItems { get; } = new();

        public ImportDocumentItem? SelectedDocument
        {
            get => _selectedDocument;
            set
            {
                if (_selectedDocument != value)
                {
                    _selectedDocument = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedDocument)));
                    OnDocumentSelected();
                }
            }
        }

        public bool HasSelection => TileItems.Any(t => t.IsSelected);

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Creates a new import tiles dialog.
        /// </summary>
        /// <param name="targetDocument">The document to import tiles into.</param>
        /// <param name="allDocuments">All open documents to choose from.</param>
        public ImportTilesDialog(CanvasDocument targetDocument, IEnumerable<CanvasDocument> allDocuments)
        {
            _targetDocument = targetDocument;
            InitializeComponent();
            DataContext = this;

            // Get references to named elements
            _sizeWarning = FindName("SizeWarning") as InfoBar;
            _selectionSummary = FindName("SelectionSummary") as TextBlock;
            _duplicateHandling = FindName("DuplicateHandling") as CheckBox;

            // Populate document list (excluding target document)
            foreach (var doc in allDocuments)
            {
                if (doc != targetDocument && doc.TileSet != null && doc.TileSet.Count > 0)
                {
                    Documents.Add(new ImportDocumentItem { Document = doc });
                }
            }

            // Auto-select first document if available
            if (Documents.Count > 0)
            {
                SelectedDocument = Documents[0];
            }

            UpdateSelectionSummary();
        }

        private void DocumentCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Handled by SelectedDocument property setter
        }

        private async void OnDocumentSelected()
        {
            TileItems.Clear();

            if (_selectedDocument?.Document.TileSet == null)
            {
                UpdateSelectionSummary();
                CheckTileSizeCompatibility();
                return;
            }

            var tileSet = _selectedDocument.Document.TileSet;

            foreach (var tile in tileSet.Tiles)
            {
                var item = new ImportTileItem
                {
                    TileId = tile.Id,
                    Pixels = tile.Pixels,
                    Width = tile.Width,
                    Height = tile.Height,
                    IsSelected = true // Default to selected
                };

                // Generate preview image
                try
                {
                    item.Preview = await CreatePreviewAsync(tile.Pixels, tile.Width, tile.Height);
                }
                catch
                {
                    // Preview generation failed - continue without preview
                }

                TileItems.Add(item);
            }

            UpdateSelectionSummary();
            CheckTileSizeCompatibility();
        }

        private async Task<BitmapImage?> CreatePreviewAsync(byte[] pixels, int width, int height)
        {
            if (width <= 0 || height <= 0 || pixels.Length == 0)
                return null;

            var bitmap = new WriteableBitmap(width, height);
            using (var stream = bitmap.PixelBuffer.AsStream())
            {
                await stream.WriteAsync(pixels, 0, pixels.Length);
            }
            bitmap.Invalidate();

            // Convert to BitmapImage for display
            var bitmapImage = new BitmapImage();
            using (var memStream = new Windows.Storage.Streams.InMemoryRandomAccessStream())
            {
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, memStream);
                encoder.SetPixelData(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    (uint)width,
                    (uint)height,
                    96, 96,
                    pixels);
                await encoder.FlushAsync();
                memStream.Seek(0);
                await bitmapImage.SetSourceAsync(memStream);
            }

            return bitmapImage;
        }

        private void CheckTileSizeCompatibility()
        {
            if (_sizeWarning == null) return;

            if (_selectedDocument?.Document.TileSet == null || _targetDocument.TileSet == null)
            {
                _sizeWarning.IsOpen = false;
                return;
            }

            var source = _selectedDocument.Document.TileSet;
            var target = _targetDocument.TileSet;

            bool sizeMatch = source.TileWidth == target.TileWidth &&
                             source.TileHeight == target.TileHeight;

            _sizeWarning.IsOpen = !sizeMatch;
            if (!sizeMatch)
            {
                _sizeWarning.Message = $"Source tiles ({source.TileWidth}x{source.TileHeight}) will be scaled to target size ({target.TileWidth}x{target.TileHeight}).";
            }
        }

        private void TileCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateSelectionSummary();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasSelection)));
        }

        private void UpdateSelectionSummary()
        {
            if (_selectionSummary == null) return;

            int selected = TileItems.Count(t => t.IsSelected);
            int total = TileItems.Count;

            _selectionSummary.Text = selected == 0
                ? "No tiles selected"
                : $"{selected} of {total} tiles selected";

            // Update primary button state
            IsPrimaryButtonEnabled = selected > 0;
        }

        private void SelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in TileItems)
            {
                item.IsSelected = true;
            }
            UpdateSelectionSummary();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasSelection)));
        }

        private void SelectNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in TileItems)
            {
                item.IsSelected = false;
            }
            UpdateSelectionSummary();
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasSelection)));
        }

        /// <summary>
        /// Gets the selected tiles for import.
        /// </summary>
        public IReadOnlyList<ImportTileItem> GetSelectedTiles()
        {
            return TileItems.Where(t => t.IsSelected).ToList();
        }

        /// <summary>
        /// Gets whether to skip duplicate tiles.
        /// </summary>
        public bool SkipDuplicates => _duplicateHandling?.IsChecked == true;

        /// <summary>
        /// Gets the source tile set for size comparison during import.
        /// </summary>
        public TileSet? SourceTileSet => _selectedDocument?.Document.TileSet;
    }
}
