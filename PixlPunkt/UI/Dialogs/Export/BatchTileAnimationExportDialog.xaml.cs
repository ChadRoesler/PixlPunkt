using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Core.Animation;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Export;

namespace PixlPunkt.UI.Dialogs.Export
{
    /// <summary>
    /// Dialog for batch exporting multiple tile animation reels at once.
    /// </summary>
    public sealed partial class BatchTileAnimationExportDialog : ContentDialog
    {
        //////////////////////////////////////////////////////////////////
        // VIEW MODEL
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// View model for a reel in the selection list.
        /// </summary>
        public class ReelViewModel
        {
            public TileAnimationReel Reel { get; }
            public string Name => Reel.Name;
            public int FrameCount => Reel.FrameCount;
            public string FrameCountText => $"{FrameCount} frames";

            public ReelViewModel(TileAnimationReel reel)
            {
                Reel = reel;
            }
        }

        //////////////////////////////////////////////////////////////////
        // FIELDS
        //////////////////////////////////////////////////////////////////

        private readonly CanvasDocument _document;
        private readonly ObservableCollection<ReelViewModel> _reelViewModels = new();
        private bool _isInitialized;

        //////////////////////////////////////////////////////////////////
        // PROPERTIES
        //////////////////////////////////////////////////////////////////

        /// <summary>Gets the selected reels for export.</summary>
        public IReadOnlyList<TileAnimationReel> SelectedReels
        {
            get
            {
                var selected = new List<TileAnimationReel>();
                foreach (var item in ReelsListView.SelectedItems)
                {
                    if (item is ReelViewModel vm)
                        selected.Add(vm.Reel);
                }
                return selected;
            }
        }

        /// <summary>Gets the selected export format tag.</summary>
        public string SelectedFormat => (FormatComboBox?.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "gif";

        /// <summary>Gets the pixel scale factor.</summary>
        public int Scale => (int)(ScaleNumberBox?.Value ?? 1);

        /// <summary>Gets whether animations should loop (GIF).</summary>
        public bool Loop => LoopCheckBox?.IsChecked == true;

        /// <summary>Gets video quality (0-100).</summary>
        public int VideoQuality => (int)(QualitySlider?.Value ?? 80);

        /// <summary>Gets whether FPS is overridden.</summary>
        public bool OverrideFps => OverrideFpsCheckBox?.IsChecked == true;

        /// <summary>Gets the FPS override value.</summary>
        public int Fps => OverrideFps ? (int)(FpsNumberBox?.Value ?? 12) : 0;

        /// <summary>Gets whether there are any reels to export.</summary>
        public bool HasReels => _document.TileAnimationState.Reels.Count > 0;

        //////////////////////////////////////////////////////////////////
        // CONSTRUCTOR
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a new BatchTileAnimationExportDialog.
        /// </summary>
        /// <param name="document">The document containing tile animations.</param>
        public BatchTileAnimationExportDialog(CanvasDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));

            InitializeComponent();

            // Populate reels list
            foreach (var reel in _document.TileAnimationState.Reels)
            {
                if (reel.FrameCount > 0) // Only show reels with frames
                {
                    _reelViewModels.Add(new ReelViewModel(reel));
                }
            }

            ReelsListView.ItemsSource = _reelViewModels;

            // Select all by default
            foreach (var vm in _reelViewModels)
            {
                ReelsListView.SelectedItems.Add(vm);
            }

            _isInitialized = true;

            UpdateSelectionInfo();
            UpdateFormatOptions();
            UpdateOutputSize();
        }

        //////////////////////////////////////////////////////////////////
        // PUBLIC API
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets export options based on dialog settings.
        /// </summary>
        public AnimationExportService.ExportOptions GetExportOptions()
        {
            return new AnimationExportService.ExportOptions
            {
                Scale = Scale,
                UseStage = false, // Tile animations don't use stage
                SeparateLayers = false,
                FrameDelayMs = OverrideFps ? (int)(1000.0 / Math.Max(1, Fps)) : 0
            };
        }

        /// <summary>
        /// Gets the file extension for the selected format.
        /// </summary>
        public string GetFileExtension()
        {
            return SelectedFormat switch
            {
                "gif" => ".gif",
                "mp4" => ".mp4",
                "png" => "", // Folder-based
                "strip" => ".png", // Single file sprite strip
                _ => ".gif"
            };
        }

        /// <summary>
        /// Returns true if the format exports to a folder (image sequence).
        /// </summary>
        public bool IsImageSequence => SelectedFormat == "png";

        /// <summary>
        /// Returns true if the format exports as a sprite strip (single horizontal image).
        /// </summary>
        public bool IsSpriteStrip => SelectedFormat == "strip";

        //////////////////////////////////////////////////////////////////
        // EVENT HANDLERS
        //////////////////////////////////////////////////////////////////

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            ReelsListView.SelectAll();
        }

        private void SelectNoneButton_Click(object sender, RoutedEventArgs e)
        {
            ReelsListView.SelectedItems.Clear();
        }

        private void ReelsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            UpdateSelectionInfo();
        }

        private void FormatComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            UpdateFormatOptions();
            UpdateOutputInfo();
        }

        private void ScaleNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (!_isInitialized) return;
            UpdateOutputSize();
        }

        private void QualitySlider_ValueChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
        {
            if (!_isInitialized || QualityText == null) return;
            QualityText.Text = $"{(int)QualitySlider.Value}%";
        }

        private void OverrideFpsCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || FpsNumberBox == null) return;
            FpsNumberBox.IsEnabled = OverrideFpsCheckBox?.IsChecked == true;
        }

        //////////////////////////////////////////////////////////////////
        // PRIVATE METHODS
        //////////////////////////////////////////////////////////////////

        private void UpdateSelectionInfo()
        {
            int selectedCount = ReelsListView.SelectedItems.Count;
            int totalFrames = 0;

            foreach (var item in ReelsListView.SelectedItems)
            {
                if (item is ReelViewModel vm)
                    totalFrames += vm.FrameCount;
            }

            SelectionInfoText.Text = selectedCount == 1
                ? "1 animation selected"
                : $"{selectedCount} animations selected";

            TotalFramesText.Text = $"Total: {totalFrames} frames across {selectedCount} animations";

            // Enable/disable primary button based on selection
            IsPrimaryButtonEnabled = selectedCount > 0;
        }

        private void UpdateFormatOptions()
        {
            string format = SelectedFormat;
            bool isVideo = format == "mp4";
            bool isGif = format == "gif";

            // Show/hide format-specific options
            LoopCheckBox.Visibility = isGif ? Visibility.Visible : Visibility.Collapsed;
            VideoQualityPanel.Visibility = isVideo ? Visibility.Visible : Visibility.Collapsed;

            UpdateOutputInfo();
        }

        private void UpdateOutputSize()
        {
            if (OutputSizeText == null) return;

            int tileW = _document.TileSize.Width;
            int tileH = _document.TileSize.Height;
            int scale = Scale;

            OutputSizeText.Text = $"{tileW * scale}×{tileH * scale} pixels";
        }

        private void UpdateOutputInfo()
        {
            if (OutputInfoText == null) return;

            string ext = GetFileExtension();
            if (IsImageSequence)
            {
                OutputInfoText.Text = "Each animation will be saved to a subfolder:\nReelName/frame_00000.png, frame_00001.png, ...";
            }
            else if (IsSpriteStrip)
            {
                OutputInfoText.Text = "Each animation will be saved as a horizontal sprite strip:\nReelName.png (all frames side-by-side)";
            }
            else
            {
                OutputInfoText.Text = $"Each animation will be saved as:\nReelName{ext}";
            }
        }
    }
}
