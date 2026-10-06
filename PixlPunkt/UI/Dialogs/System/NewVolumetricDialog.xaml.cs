using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Core.Document;
using Windows.Graphics;
using static PixlPunkt.Core.Helpers.GraphicsStructHelper;

namespace PixlPunkt.UI.Dialogs
{
    /// <summary>
    /// What the New Volumetric dialog collected.
    /// </summary>
    /// <param name="Name">The project name.</param>
    /// <param name="TileSize">Texture size per face, and for voxels the model resolution too.</param>
    /// <param name="Kind">Which sort of volumetric project this is.</param>
    /// <param name="SixFaces">True for six individual faces, false for three mirrored pairs.</param>
    /// <param name="SetCount">How many sets of face tiles to start with.</param>
    public sealed record NewVolumetricResult(
        string Name,
        SizeInt32 TileSize,
        VolumetricKind Kind,
        bool SixFaces,
        int SetCount,
        bool CrossLayout);

    /// <summary>
    /// Creates a volumetric project: a tile sheet to paint faces on, and a workspace to build in.
    /// </summary>
    /// <remarks>
    /// The dialog is in two parts on purpose. The name, the tile size and the number of sets belong
    /// to any volumetric project. The faces question belongs to voxels alone, and when a second kind
    /// arrives that section is replaced rather than added to.
    ///
    /// There is no model dimension to choose, because for voxels there is not one to choose: the
    /// volume is a cube the size of the tiles it is built from.
    /// </remarks>
    public sealed partial class NewVolumetricDialog : ContentDialog
    {
        /// <summary>The largest volume the builder will make, so the tile size is held below it.</summary>
        private const int MaxVoxelSize = 128;

        public NewVolumetricDialog()
        {
            InitializeComponent();
            UpdateSummary();
        }

        private int TileWidth => (int)Math.Clamp(TileW?.Value is double w and > 0 ? w : 16, 2, MaxVoxelSize);

        private int TileHeight => (int)Math.Clamp(TileH?.Value is double h and > 0 ? h : 16, 2, MaxVoxelSize);

        private bool SixFaces => FaceModeCombo?.SelectedIndex == 1;

        private int FacesPerSet => SixFaces ? 6 : 3;

        private int SetCountValue => (int)Math.Clamp(SetCount?.Value is double c and > 0 ? c : 1, 1, 32);

        /// <summary>The cube the builder would produce, which is the smaller tile dimension.</summary>
        private int ModelSize => Math.Min(TileWidth, TileHeight);

        private void Size_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args) => UpdateSummary();

        private void Input_Changed(object sender, TextChangedEventArgs e) => UpdateSummary();

        private void FaceMode_Changed(object sender, SelectionChangedEventArgs e) => UpdateSummary();

        private void Layout_Changed(object sender, SelectionChangedEventArgs e) => UpdateSummary();

        /// <summary>
        /// Lays six faces out as an unfolded cross, so neighbouring faces touch along the seam they
        /// share and a line can be drawn straight across it. Means nothing with three mirrored
        /// faces, which are always one row.
        /// </summary>
        private bool CrossLayout => SixFaces && LayoutCombo?.SelectedIndex == 0;

        private void UpdateSummary()
        {
            if (SummaryText is null) return;

            if (LayoutCombo is not null)
                LayoutCombo.IsEnabled = SixFaces;

            int tiles = FacesPerSet * SetCountValue;
            int columns = CrossLayout ? 4 : 3;
            int rowsPerSet = SixFaces ? (CrossLayout ? 3 : 2) : 1;
            int rows = rowsPerSet * SetCountValue;

            SummaryText.Text =
                $"{tiles} tiles · sheet {columns} × {rows} · " +
                $"{columns * TileWidth} × {rows * TileHeight} px · " +
                $"model {ModelSize}³";

            IsPrimaryButtonEnabled = true;
        }

        public NewVolumetricResult GetResult()
        {
            string name = string.IsNullOrWhiteSpace(ProjectName?.Text) ? "Volumetric" : ProjectName!.Text.Trim();

            return new NewVolumetricResult(
                Name: name,
                TileSize: CreateSize(TileWidth, TileHeight),
                Kind: VolumetricKind.Voxel,
                SixFaces: SixFaces,
                SetCount: SetCountValue,
                CrossLayout: CrossLayout);
        }
    }
}
