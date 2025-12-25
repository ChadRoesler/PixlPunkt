using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using PixlPunkt.Core.Ascii;
using PixlPunkt.Core.Serialization;
using PixlPunkt.Core.Settings;
using Windows.Storage.Pickers;

namespace PixlPunkt.UI.Ascii
{
    /// <summary>
    /// Window for creating and editing custom ASCII glyph sets.
    /// Organizes glyph sets using actual file system folders.
    /// </summary>
    public sealed partial class GlyphSetEditorWindow : Window
    {
        // ====================================================================
        // STATE
        // ====================================================================

        // Root folders for organization
        private readonly GlyphSetFolder _builtInFolder = new("Built-in", isBuiltInFolder: true);
        private readonly GlyphSetFolder _customRootFolder = new("Custom", isBuiltInFolder: false);
        
        // Flattened list for display
        private readonly ObservableCollection<GlyphSetItemBase> _displayItems = [];
        private readonly ObservableCollection<GlyphCharItem> _glyphItems = [];
        
        private GlyphSetItemBase? _selectedItem;
        private GlyphSetItem? _selectedSet;
        private int _selectedGlyphIndex = -1;
        private int _glyphSize = 4;
        private bool _isUpdating;
        private bool _isDirty;

        // Bitmap editor state
        private readonly List<Button> _bitmapCells = [];
        private readonly List<Border> _previewCells = [];
        private ulong _currentBitmap;

        // Colors for bitmap editor
        private static readonly SolidColorBrush OnBrush = new(Colors.White);
        private static readonly SolidColorBrush OffBrush = new(Colors.Black);

        // ====================================================================
        // VIEW MODEL - GlyphCharItem for glyph selector
        // ====================================================================

        public class GlyphCharItem : System.ComponentModel.INotifyPropertyChanged
        {
            private bool _isSelected;
            
            public int Index { get; set; }
            public string Character { get; set; } = string.Empty;
            
            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (_isSelected != value)
                    {
                        _isSelected = value;
                        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
                    }
                }
            }

            public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        }

        // ====================================================================
        // CONSTRUCTION
        // ====================================================================

        public GlyphSetEditorWindow()
        {
            InitializeComponent();

            GlyphSetList.ItemsSource = _displayItems;
            GlyphSelector.ItemsSource = _glyphItems;
            GlyphSizeCombo.SelectedIndex = 0;

            if (Content is FrameworkElement root)
            {
                root.Loaded += Root_Loaded;
            }

            Closed += GlyphSetEditorWindow_Closed;
        }

        private void Root_Loaded(object sender, RoutedEventArgs e)
        {
            LoadGlyphSets();
            BuildBitmapGrid();
            BuildPreviewGrid();
            RebuildDisplayList();
            UpdateUI();
        }

        private void GlyphSetEditorWindow_Closed(object sender, WindowEventArgs args)
        {
            if (_isDirty)
            {
                SaveCurrentSet();
            }
        }

        // ====================================================================
        // LOAD GLYPH SETS FROM FILE SYSTEM
        // ====================================================================

        private void LoadGlyphSets()
        {
            // Clear existing
            ClearFolder(_builtInFolder);
            ClearFolder(_customRootFolder);

            // Load built-in sets
            foreach (var set in AsciiGlyphSets.All.Where(s => AsciiGlyphSets.IsBuiltIn(s.Name)))
            {
                var item = new GlyphSetItem(set.Name, isBuiltIn: true)
                {
                    RegisteredName = set.Name,
                    Ramp = set.Ramp,
                    GlyphWidth = set.GlyphWidth,
                    GlyphHeight = set.GlyphHeight,
                    Bitmaps = set.GlyphBitmaps?.ToList() ?? []
                };
                _builtInFolder.AddChild(item);
            }

            // Load custom sets from file system (including folder structure)
            AppPaths.EnsureDirectoryExists(AppPaths.GlyphSetsDirectory);
            LoadFolderContents(_customRootFolder, AppPaths.GlyphSetsDirectory);
        }

        /// <summary>
        /// Recursively loads folder contents from the file system.
        /// </summary>
        private void LoadFolderContents(GlyphSetFolder parentFolder, string directoryPath)
        {
            if (!Directory.Exists(directoryPath)) return;

            // Load subdirectories as folders
            foreach (var subDir in Directory.GetDirectories(directoryPath).OrderBy(d => d))
            {
                var folderName = Path.GetFileName(subDir);
                var folder = new GlyphSetFolder(folderName, isBuiltInFolder: false)
                {
                    FolderPath = subDir
                };
                parentFolder.AddChild(folder);

                // Recursively load contents
                LoadFolderContents(folder, subDir);
            }

            // Load glyph set files
            foreach (var file in Directory.GetFiles(directoryPath, "*.asciifont.json").OrderBy(f => f))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var model = JsonSerializer.Deserialize(json, AsciiGlyphSetJsonContext.Default.AsciiGlyphSetJson);
                    
                    if (model == null || string.IsNullOrWhiteSpace(model.Ramp)) continue;

                    var bitmaps = new List<ulong>();
                    if (model.Bitmaps != null)
                    {
                        foreach (var hex in model.Bitmaps)
                        {
                            if (ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bits))
                                bitmaps.Add(bits);
                        }
                    }

                    var item = new GlyphSetItem(model.Name ?? Path.GetFileNameWithoutExtension(file), isBuiltIn: false)
                    {
                        RegisteredName = model.Name,
                        Ramp = model.Ramp,
                        GlyphWidth = model.GlyphWidth > 0 ? model.GlyphWidth : 4,
                        GlyphHeight = model.GlyphHeight > 0 ? model.GlyphHeight : 4,
                        Bitmaps = bitmaps,
                        FilePath = file
                    };

                    parentFolder.AddChild(item);

                    // Register with the glyph set system
                    AsciiGlyphSets.Register(new AsciiGlyphSet
                    {
                        Name = item.Name,
                        Ramp = item.Ramp,
                        GlyphWidth = item.GlyphWidth,
                        GlyphHeight = item.GlyphHeight,
                        GlyphBitmaps = item.Bitmaps
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[GlyphSetEditor] Failed to load {file}: {ex.Message}");
                }
            }
        }

        private void ClearFolder(GlyphSetFolder folder)
        {
            while (folder.Children.Count > 0)
                folder.RemoveChild(folder.Children[0]);
        }

        /// <summary>
        /// Rebuilds the flattened display list from the folder hierarchy.
        /// </summary>
        private void RebuildDisplayList()
        {
            _displayItems.Clear();

            // Add built-in folder and its visible contents
            foreach (var item in _builtInFolder.FlattenVisible())
                _displayItems.Add(item);

            // Add custom folder and its visible contents
            foreach (var item in _customRootFolder.FlattenVisible())
                _displayItems.Add(item);

            // Select first glyph set if nothing selected
            if (_selectedSet == null && _displayItems.Count > 0)
            {
                var firstSet = _displayItems.OfType<GlyphSetItem>().FirstOrDefault();
                if (firstSet != null)
                {
                    _isUpdating = true;
                    GlyphSetList.SelectedItem = firstSet;
                    _isUpdating = false;
                    _selectedItem = firstSet;
                    _selectedSet = firstSet;
                }
            }
        }

        // ====================================================================
        // BUILD BITMAP EDITOR
        // ====================================================================

        private void BuildBitmapGrid()
        {
            BitmapGrid.Children.Clear();
            BitmapGrid.RowDefinitions.Clear();
            BitmapGrid.ColumnDefinitions.Clear();
            _bitmapCells.Clear();

            int cellSize = 40;

            for (int i = 0; i < _glyphSize; i++)
            {
                BitmapGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(cellSize) });
                BitmapGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cellSize) });
            }

            for (int y = 0; y < _glyphSize; y++)
            {
                for (int x = 0; x < _glyphSize; x++)
                {
                    var btn = new Button
                    {
                        Width = cellSize - 2,
                        Height = cellSize - 2,
                        Margin = new Thickness(1),
                        Padding = new Thickness(0),
                        Background = OffBrush,
                        Tag = y * _glyphSize + x,
                        CornerRadius = new CornerRadius(2)
                    };

                    btn.Click += BitmapCell_Click;

                    Grid.SetRow(btn, y);
                    Grid.SetColumn(btn, x);
                    BitmapGrid.Children.Add(btn);
                    _bitmapCells.Add(btn);
                }
            }
        }

        private void BuildPreviewGrid()
        {
            PreviewGrid.Children.Clear();
            PreviewGrid.RowDefinitions.Clear();
            PreviewGrid.ColumnDefinitions.Clear();
            _previewCells.Clear();

            int cellSize = 100 / _glyphSize;

            for (int i = 0; i < _glyphSize; i++)
            {
                PreviewGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(cellSize) });
                PreviewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(cellSize) });
            }

            for (int y = 0; y < _glyphSize; y++)
            {
                for (int x = 0; x < _glyphSize; x++)
                {
                    var border = new Border
                    {
                        Background = OffBrush,
                        Margin = new Thickness(0)
                    };

                    Grid.SetRow(border, y);
                    Grid.SetColumn(border, x);
                    PreviewGrid.Children.Add(border);
                    _previewCells.Add(border);
                }
            }
        }

        // ====================================================================
        // BITMAP CELL INTERACTION
        // ====================================================================

        private void BitmapCell_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not int bitIndex) return;
            if (_selectedSet == null || _selectedGlyphIndex < 0) return;

            _currentBitmap ^= (1UL << bitIndex);
            bool isOn = ((_currentBitmap >> bitIndex) & 1UL) != 0;
            btn.Background = isOn ? OnBrush : OffBrush;

            UpdatePreview();
            SaveBitmapToSet();
            _isDirty = true;
        }

        private void UpdatePreview()
        {
            for (int i = 0; i < _previewCells.Count && i < _glyphSize * _glyphSize; i++)
            {
                bool isOn = ((_currentBitmap >> i) & 1UL) != 0;
                _previewCells[i].Background = isOn ? OnBrush : OffBrush;
            }
        }

        private void UpdateBitmapGrid()
        {
            for (int i = 0; i < _bitmapCells.Count && i < _glyphSize * _glyphSize; i++)
            {
                bool isOn = ((_currentBitmap >> i) & 1UL) != 0;
                _bitmapCells[i].Background = isOn ? OnBrush : OffBrush;
            }
            UpdatePreview();
        }

        // ====================================================================
        // SELECTION
        // ====================================================================

        private void GlyphSetList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdating) return;

            if (_isDirty && _selectedSet != null && !_selectedSet.IsBuiltIn)
            {
                SaveCurrentSet();
            }

            _selectedItem = GlyphSetList.SelectedItem as GlyphSetItemBase;
            
            if (_selectedItem is GlyphSetFolder folder)
            {
                folder.IsExpanded = !folder.IsExpanded;
                RebuildDisplayList();
                
                _isUpdating = true;
                GlyphSetList.SelectedItem = folder;
                _isUpdating = false;
                return;
            }
            
            _selectedSet = _selectedItem as GlyphSetItem;
            _selectedGlyphIndex = -1;
            _isDirty = false;

            UpdateUI();
            UpdateGlyphSelector();
        }

        private void FolderChevron_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton btn) return;
            if (btn.DataContext is not GlyphSetFolder folder) return;

            RebuildDisplayList();
            
            if (_selectedItem != null && _displayItems.Contains(_selectedItem))
            {
                _isUpdating = true;
                GlyphSetList.SelectedItem = _selectedItem;
                _isUpdating = false;
            }
        }

        private void UpdateUI()
        {
            bool hasSelection = _selectedSet != null;
            bool isEditable = hasSelection && !_selectedSet!.IsBuiltIn;
            bool isFolder = _selectedItem is GlyphSetFolder;

            // Enable/disable controls
            SetNameBox.IsEnabled = isEditable && !isFolder;
            GlyphSizeCombo.IsEnabled = isEditable && !isFolder;
            RampBox.IsEnabled = isEditable && !isFolder;
            DeleteSetBtn.IsEnabled = isEditable || (_selectedItem is GlyphSetFolder f && !f.IsBuiltInFolder);
            SaveBtn.IsEnabled = isEditable && !isFolder;

            _isUpdating = true;

            if (_selectedSet != null)
            {
                SetNameBox.Text = _selectedSet.Name;
                RampBox.Text = _selectedSet.Ramp;
                RampCountLabel.Text = $"({_selectedSet.Ramp.Length} glyphs)";

                _glyphSize = _selectedSet.GlyphWidth;
                GlyphSizeCombo.SelectedIndex = _glyphSize switch
                {
                    4 => 0,
                    6 => 1,
                    8 => 2,
                    _ => 0
                };

                if (_bitmapCells.Count != _glyphSize * _glyphSize)
                {
                    BuildBitmapGrid();
                    BuildPreviewGrid();
                }
            }
            else
            {
                SetNameBox.Text = string.Empty;
                RampBox.Text = string.Empty;
                RampCountLabel.Text = "(0 glyphs)";
            }

            _isUpdating = false;
        }

        private void UpdateGlyphSelector()
        {
            _glyphItems.Clear();

            if (_selectedSet != null && !string.IsNullOrEmpty(_selectedSet.Ramp))
            {
                for (int i = 0; i < _selectedSet.Ramp.Length; i++)
                {
                    _glyphItems.Add(new GlyphCharItem
                    {
                        Index = i,
                        Character = _selectedSet.Ramp[i].ToString(),
                        IsSelected = false
                    });
                }
            }

            if (_selectedSet != null && !string.IsNullOrEmpty(_selectedSet.Ramp))
            {
                SelectGlyph(0);
            }
            else
            {
                _selectedGlyphIndex = -1;
                _currentBitmap = 0;
                CurrentGlyphLabel.Text = "(none)";
                CurrentGlyphIndexLabel.Text = "Index: -";
                UpdateBitmapGrid();
            }
        }

        private void SelectGlyph(int index)
        {
            foreach (var item in _glyphItems)
                item.IsSelected = false;

            if (_selectedSet == null || index < 0 || index >= _selectedSet.Ramp.Length)
            {
                _selectedGlyphIndex = -1;
                _currentBitmap = 0;
                CurrentGlyphLabel.Text = "(none)";
                CurrentGlyphIndexLabel.Text = "Index: -";
                UpdateBitmapGrid();
                return;
            }

            _selectedGlyphIndex = index;

            if (index < _glyphItems.Count)
                _glyphItems[index].IsSelected = true;

            if (index < _selectedSet.Bitmaps.Count)
                _currentBitmap = _selectedSet.Bitmaps[index];
            else
                _currentBitmap = 0;

            CurrentGlyphLabel.Text = _selectedSet.Ramp[index].ToString();
            CurrentGlyphIndexLabel.Text = $"Index: {index}";

            UpdateBitmapGrid();
        }

        private void GlyphToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton btn || btn.Tag is not int index) return;
            if (_selectedSet == null) return;
            SelectGlyph(index);
        }

        // ====================================================================
        // PROPERTY CHANGES
        // ====================================================================

        private void SetNameBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating || _selectedSet == null || _selectedSet.IsBuiltIn) return;
            _selectedSet.Name = SetNameBox.Text;
            _isDirty = true;
        }

        private void GlyphSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isUpdating || _selectedSet == null) return;

            var item = GlyphSizeCombo.SelectedItem as ComboBoxItem;
            if (item?.Tag is string sizeStr && int.TryParse(sizeStr, out int size))
            {
                _glyphSize = size;

                if (!_selectedSet.IsBuiltIn)
                {
                    _selectedSet.GlyphWidth = size;
                    _selectedSet.GlyphHeight = size;
                    _isDirty = true;
                }

                BuildBitmapGrid();
                BuildPreviewGrid();
                UpdateBitmapGrid();
            }
        }

        private void RampBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isUpdating || _selectedSet == null || _selectedSet.IsBuiltIn) return;

            var newRamp = RampBox.Text ?? string.Empty;
            _selectedSet.Ramp = newRamp;
            RampCountLabel.Text = $"({newRamp.Length} glyphs)";

            while (_selectedSet.Bitmaps.Count < newRamp.Length)
                _selectedSet.Bitmaps.Add(0);

            _isDirty = true;
            UpdateGlyphSelector();
        }

        // ====================================================================
        // BITMAP TOOLS
        // ====================================================================

        private void ClearBitmapBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSet == null || _selectedGlyphIndex < 0) return;
            _currentBitmap = 0;
            UpdateBitmapGrid();
            SaveBitmapToSet();
            _isDirty = true;
        }

        private void FillBitmapBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSet == null || _selectedGlyphIndex < 0) return;
            int bits = _glyphSize * _glyphSize;
            // Handle 64-bit case specially to avoid overflow
            _currentBitmap = bits >= 64 ? ulong.MaxValue : (1UL << bits) - 1;
            UpdateBitmapGrid();
            SaveBitmapToSet();
            _isDirty = true;
        }

        private void InvertBitmapBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSet == null || _selectedGlyphIndex < 0) return;
            int bits = _glyphSize * _glyphSize;
            // Handle 64-bit case specially to avoid overflow
            ulong mask = bits >= 64 ? ulong.MaxValue : (1UL << bits) - 1;
            _currentBitmap = (~_currentBitmap) & mask;
            UpdateBitmapGrid();
            SaveBitmapToSet();
            _isDirty = true;
        }

        private void SaveBitmapToSet()
        {
            if (_selectedSet == null || _selectedGlyphIndex < 0) return;
            while (_selectedSet.Bitmaps.Count <= _selectedGlyphIndex)
                _selectedSet.Bitmaps.Add(0);
            _selectedSet.Bitmaps[_selectedGlyphIndex] = _currentBitmap;
        }

        // ====================================================================
        // SET MANAGEMENT
        // ====================================================================

        private void NewSetBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_isDirty && _selectedSet != null && !_selectedSet.IsBuiltIn)
                SaveCurrentSet();

            var newSet = new GlyphSetItem("New Glyph Set", false)
            {
                Ramp = " .:-=+*#%@",
                GlyphWidth = 4,
                GlyphHeight = 4,
                Bitmaps = new List<ulong>(new ulong[10])
            };

            // Add to appropriate folder
            var targetFolder = GetTargetFolderForNewItem();
            targetFolder.AddChild(newSet);

            // Determine file path based on folder
            var folderPath = GetFolderPath(targetFolder);
            newSet.FilePath = Path.Combine(folderPath, $"{SanitizeFileName(newSet.Name)}.asciifont.json");
            
            RebuildDisplayList();

            _selectedItem = newSet;
            _selectedSet = newSet;
            _selectedGlyphIndex = -1;
            _isDirty = true;
            
            _isUpdating = true;
            GlyphSetList.SelectedItem = newSet;
            _isUpdating = false;
            
            UpdateUI();
            UpdateGlyphSelector();
        }

        private void NewFolderBtn_Click(object sender, RoutedEventArgs e)
        {
            var targetFolder = GetTargetFolderForNewItem();
            var parentPath = GetFolderPath(targetFolder);
            
            // Find a unique folder name
            var baseName = "New Folder";
            var folderName = baseName;
            var folderPath = Path.Combine(parentPath, folderName);
            int counter = 1;
            while (Directory.Exists(folderPath))
            {
                folderName = $"{baseName} ({counter++})";
                folderPath = Path.Combine(parentPath, folderName);
            }
            
            // Create the actual folder on disk
            try
            {
                Directory.CreateDirectory(folderPath);
                System.Diagnostics.Debug.WriteLine($"[GlyphSetEditor] Created folder: {folderPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GlyphSetEditor] Failed to create folder: {ex.Message}");
                return;
            }

            var newFolder = new GlyphSetFolder(folderName, false)
            {
                FolderPath = folderPath
            };
            
            targetFolder.AddChild(newFolder);
            
            RebuildDisplayList();
            
            _isUpdating = true;
            GlyphSetList.SelectedItem = newFolder;
            _isUpdating = false;
            _selectedItem = newFolder;
        }

        /// <summary>
        /// Gets the file system path for a folder.
        /// </summary>
        private string GetFolderPath(GlyphSetFolder folder)
        {
            if (folder == _customRootFolder)
                return AppPaths.GlyphSetsDirectory;
            
            return folder.FolderPath ?? AppPaths.GlyphSetsDirectory;
        }

        /// <summary>
        /// Gets the appropriate folder to add new items to.
        /// </summary>
        private GlyphSetFolder GetTargetFolderForNewItem()
        {
            if (_selectedItem is GlyphSetFolder selectedFolder && !selectedFolder.IsBuiltInFolder)
                return selectedFolder;
            
            if (_selectedItem is GlyphSetItem selectedItem && !selectedItem.IsBuiltIn && selectedItem.Parent != null)
                return selectedItem.Parent;
            
            return _customRootFolder;
        }

        private void DuplicateBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSet == null) return;

            if (_isDirty && !_selectedSet.IsBuiltIn)
                SaveCurrentSet();

            var duplicate = new GlyphSetItem(_selectedSet.Name + " (Copy)", false)
            {
                Ramp = _selectedSet.Ramp,
                GlyphWidth = _selectedSet.GlyphWidth,
                GlyphHeight = _selectedSet.GlyphHeight,
                Bitmaps = new List<ulong>(_selectedSet.Bitmaps)
            };

            // Add to custom folder
            var targetFolder = _customRootFolder;
            targetFolder.AddChild(duplicate);
            
            // Set file path
            var folderPath = GetFolderPath(targetFolder);
            duplicate.FilePath = Path.Combine(folderPath, $"{SanitizeFileName(duplicate.Name)}.asciifont.json");
            
            RebuildDisplayList();

            _selectedItem = duplicate;
            _selectedSet = duplicate;
            _selectedGlyphIndex = -1;
            _isDirty = true;
            
            _isUpdating = true;
            GlyphSetList.SelectedItem = duplicate;
            _isUpdating = false;
            
            UpdateUI();
            UpdateGlyphSelector();
        }

        private async void DeleteSetBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedItem == null) return;
            
            // Handle folder deletion
            if (_selectedItem is GlyphSetFolder folder)
            {
                if (folder.IsBuiltInFolder) return;
                
                var dialog = new ContentDialog
                {
                    Title = "Delete Folder",
                    Content = $"Are you sure you want to delete the folder '{folder.Name}' and all its contents?\n\nThis will delete the folder from disk.",
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = Content.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result != ContentDialogResult.Primary) return;
                
                // Remove all glyph sets in folder from registry
                foreach (var set in folder.GetAllGlyphSets())
                {
                    if (!string.IsNullOrEmpty(set.RegisteredName))
                        AsciiGlyphSets.Remove(set.RegisteredName);
                }
                
                // Delete folder from disk
                if (!string.IsNullOrEmpty(folder.FolderPath) && Directory.Exists(folder.FolderPath))
                {
                    try
                    {
                        Directory.Delete(folder.FolderPath, recursive: true);
                        System.Diagnostics.Debug.WriteLine($"[GlyphSetEditor] Deleted folder: {folder.FolderPath}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[GlyphSetEditor] Failed to delete folder: {ex.Message}");
                    }
                }
                
                folder.Parent?.RemoveChild(folder);
                _selectedItem = null;
                _selectedSet = null;
                RebuildDisplayList();
                UpdateUI();
                UpdateGlyphSelector();
                return;
            }
            
            // Handle glyph set deletion
            if (_selectedItem is not GlyphSetItem setToDelete || setToDelete.IsBuiltIn) return;

            var displayName = setToDelete.Name;
            var registeredName = setToDelete.RegisteredName ?? setToDelete.Name;
            var fileToDelete = setToDelete.FilePath;

            var dlg = new ContentDialog
            {
                Title = "Delete Glyph Set",
                Content = $"Are you sure you want to delete '{displayName}'?\n\nThis will delete the file from disk.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };

            var res = await dlg.ShowAsync();
            if (res != ContentDialogResult.Primary) return;

            try
            {
                _isUpdating = true;
                _selectedItem = null;
                _selectedSet = null;
                _selectedGlyphIndex = -1;
                _isDirty = false;

                setToDelete.Parent?.RemoveChild(setToDelete);

                if (!string.IsNullOrEmpty(registeredName))
                    AsciiGlyphSets.Remove(registeredName);

                if (!string.IsNullOrEmpty(fileToDelete) && File.Exists(fileToDelete))
                {
                    try
                    {
                        File.Delete(fileToDelete);
                        System.Diagnostics.Debug.WriteLine($"[GlyphSetEditor] Deleted file: {fileToDelete}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[GlyphSetEditor] Failed to delete file: {ex.Message}");
                    }
                }

                _isUpdating = false;

                RebuildDisplayList();
                UpdateUI();
                UpdateGlyphSelector();
            }
            catch (Exception ex)
            {
                _isUpdating = false;
                System.Diagnostics.Debug.WriteLine($"[GlyphSetEditor] Delete failed: {ex.Message}");
            }
        }

        // ====================================================================
        // SAVE / IMPORT / EXPORT
        // ====================================================================

        private void SaveBtn_Click(object sender, RoutedEventArgs e) => SaveCurrentSet();

        private void SaveCurrentSet()
        {
            if (_selectedSet == null || _selectedSet.IsBuiltIn) return;

            try
            {
                // Get the folder path for this item
                var folderPath = _selectedSet.Parent != null 
                    ? GetFolderPath(_selectedSet.Parent) 
                    : AppPaths.GlyphSetsDirectory;
                
                AppPaths.EnsureDirectoryExists(folderPath);

                // If the name changed and we had a previous file, delete it
                var oldFilePath = _selectedSet.FilePath;
                var newFilePath = Path.Combine(folderPath, $"{SanitizeFileName(_selectedSet.Name)}.asciifont.json");
                
                if (!string.IsNullOrEmpty(oldFilePath) && oldFilePath != newFilePath && File.Exists(oldFilePath))
                {
                    try
                    {
                        File.Delete(oldFilePath);
                        System.Diagnostics.Debug.WriteLine($"[GlyphSetEditor] Deleted old file: {oldFilePath}");
                    }
                    catch { }
                }

                // Update file path
                _selectedSet.FilePath = newFilePath;

                // Remove old registration if name changed
                if (!string.IsNullOrEmpty(_selectedSet.RegisteredName) && _selectedSet.RegisteredName != _selectedSet.Name)
                {
                    AsciiGlyphSets.Remove(_selectedSet.RegisteredName);
                }

                // Build JSON model
                var model = new AsciiGlyphSetJson
                {
                    Name = _selectedSet.Name,
                    Ramp = _selectedSet.Ramp,
                    GlyphWidth = _selectedSet.GlyphWidth,
                    GlyphHeight = _selectedSet.GlyphHeight,
                    Bitmaps = _selectedSet.Bitmaps.Select(b => b.ToString("X")).ToList()
                };

                var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_selectedSet.FilePath, json);

                // Register with the glyph set system
                AsciiGlyphSets.Register(new AsciiGlyphSet
                {
                    Name = _selectedSet.Name,
                    Ramp = _selectedSet.Ramp,
                    GlyphWidth = _selectedSet.GlyphWidth,
                    GlyphHeight = _selectedSet.GlyphHeight,
                    GlyphBitmaps = _selectedSet.Bitmaps
                });

                _selectedSet.RegisteredName = _selectedSet.Name;
                _isDirty = false;
                
                System.Diagnostics.Debug.WriteLine($"[GlyphSetEditor] Saved: {_selectedSet.FilePath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GlyphSetEditor] Save failed: {ex.Message}");
            }
        }

        private async void ExportBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSet == null) return;

            var picker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                SuggestedFileName = $"{_selectedSet.Name}.asciifont"
            };
            picker.FileTypeChoices.Add("ASCII Font JSON", [".json"]);

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                try
                {
                    var model = new AsciiGlyphSetJson
                    {
                        Name = _selectedSet.Name,
                        Ramp = _selectedSet.Ramp,
                        GlyphWidth = _selectedSet.GlyphWidth,
                        GlyphHeight = _selectedSet.GlyphHeight,
                        Bitmaps = _selectedSet.Bitmaps.Select(b => b.ToString("X")).ToList()
                    };

                    var json = JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(file.Path, json);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[GlyphSetEditor] Export failed: {ex.Message}");
                }
            }
        }

        private async void ImportBtn_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker
            {
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };
            picker.FileTypeFilter.Add(".json");

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file.Path);
                    var model = JsonSerializer.Deserialize(json, AsciiGlyphSetJsonContext.Default.AsciiGlyphSetJson);
                    
                    if (model == null || string.IsNullOrWhiteSpace(model.Ramp)) return;

                    var bitmaps = new List<ulong>();
                    if (model.Bitmaps != null)
                    {
                        foreach (var hex in model.Bitmaps)
                        {
                            if (ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bits))
                                bitmaps.Add(bits);
                        }
                    }

                    var imported = new GlyphSetItem(model.Name ?? Path.GetFileNameWithoutExtension(file.Name), false)
                    {
                        Ramp = model.Ramp,
                        GlyphWidth = model.GlyphWidth > 0 ? model.GlyphWidth : 4,
                        GlyphHeight = model.GlyphHeight > 0 ? model.GlyphHeight : 4,
                        Bitmaps = bitmaps
                    };

                    var targetFolder = GetTargetFolderForNewItem();
                    targetFolder.AddChild(imported);
                    
                    // Set file path for saving
                    var folderPath = GetFolderPath(targetFolder);
                    imported.FilePath = Path.Combine(folderPath, $"{SanitizeFileName(imported.Name)}.asciifont.json");
                    
                    RebuildDisplayList();

                    _selectedItem = imported;
                    _selectedSet = imported;
                    _selectedGlyphIndex = -1;
                    _isDirty = true; // Mark dirty so it gets saved
                    
                    _isUpdating = true;
                    GlyphSetList.SelectedItem = imported;
                    _isUpdating = false;
                    
                    UpdateUI();
                    UpdateGlyphSelector();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[GlyphSetEditor] Import failed: {ex.Message}");
                }
            }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e) => Close();

        // ====================================================================
        // HELPERS
        // ====================================================================

        private static string SanitizeFileName(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "glyphset";
            var invalid = Path.GetInvalidFileNameChars();
            var result = input;
            foreach (var c in invalid)
                result = result.Replace(c, '_');
            return result.Trim();
        }
    }
}
