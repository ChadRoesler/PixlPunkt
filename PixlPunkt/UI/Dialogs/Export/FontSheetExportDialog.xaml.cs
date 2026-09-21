using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Core.Document;

namespace PixlPunkt.UI.Dialogs
{
    /// <summary>
    /// Collects what a sprite sheet export needs: the face name to record, how far to magnify the
    /// sheet, and whether to write the metrics file beside it.
    /// </summary>
    /// <remarks>
    /// Only whole multiples of the em are offered, as everywhere else in the font editor. A sheet
    /// exported at a fractional size is the lumpy result this editor exists to avoid, and a game
    /// engine given one has no way to recover the even stems.
    /// </remarks>
    public sealed partial class FontSheetExportDialog : ContentDialog
    {
        private readonly CanvasDocument _document;
        private bool _ready;

        public FontSheetExportDialog(CanvasDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));

            InitializeComponent();

            FaceNameBox.Text = string.IsNullOrWhiteSpace(document.FontState.FamilyName)
                ? document.Name ?? "Font"
                : document.FontState.FamilyName;

            PopulateScales();
            _ready = true;
            UpdateSummary();
        }

        /// <summary>Output pixels per font pixel.</summary>
        public int Scale => ScaleCombo?.SelectedItem is ComboBoxItem { Tag: int s } ? s : 1;

        /// <summary>The family name to record in the metrics.</summary>
        public string FaceName =>
            string.IsNullOrWhiteSpace(FaceNameBox?.Text) ? "Font" : FaceNameBox!.Text.Trim();

        /// <summary>Whether a BMFont metrics file should be written next to the image.</summary>
        public bool ShouldWriteMetrics => WriteMetrics?.IsChecked == true;

        private int EmHeight => FontMetricsOps.EmHeightOf(_document);

        private void PopulateScales()
        {
            int em = EmHeight;

            for (int multiple = 1; multiple <= 8; multiple++)
            {
                ScaleCombo.Items.Add(new ComboBoxItem
                {
                    Content = multiple == 1
                        ? $"{em} px  (as drawn)"
                        : $"{em * multiple} px  ({multiple}×)",
                    Tag = multiple,
                });
            }

            ScaleCombo.SelectedIndex = 0;
        }

        private void Scale_Changed(object sender, SelectionChangedEventArgs e) => UpdateSummary();

        private void Input_Changed(object sender, TextChangedEventArgs e) => UpdateSummary();

        private void Toggle_Changed(object sender, RoutedEventArgs e) => UpdateSummary();

        private void UpdateSummary()
        {
            if (!_ready || SummaryText is null) return;

            var sheet = FontExportOps.SheetSize(_document, Scale);
            int glyphs = _document.FontState.Glyphs.Count;

            SummaryText.Text =
                $"{glyphs} glyphs · sheet {sheet.Width} × {sheet.Height} px" +
                (ShouldWriteMetrics ? " · image and .fnt" : " · image only");

            IsPrimaryButtonEnabled = glyphs > 0;
        }
    }
}
