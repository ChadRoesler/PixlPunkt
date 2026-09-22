using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Core.Document;

namespace PixlPunkt.UI.Dialogs
{
    /// <summary>
    /// Collects what a TrueType export needs: the names to record, and which sizes to embed as
    /// pictures alongside the outlines.
    /// </summary>
    /// <remarks>
    /// There is no size to choose for the font itself. The outlines are the pixels and the units per
    /// em is an exact multiple of the pixel grid, so it is crisp at every whole multiple of the em
    /// and nowhere else. The embedded sizes are a belt-and-braces measure on top of that, for
    /// readers that do not grid-fit the way they were asked to.
    /// </remarks>
    public sealed partial class FontTtfExportDialog : ContentDialog
    {
        private readonly CanvasDocument _document;
        private readonly List<CheckBox> _strikeBoxes = new();

        public FontTtfExportDialog(CanvasDocument document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));

            InitializeComponent();

            FamilyBox.Text = string.IsNullOrWhiteSpace(document.FontState.FamilyName)
                ? document.Name ?? "Font"
                : document.FontState.FamilyName;

            StyleBox.Text = string.IsNullOrWhiteSpace(document.FontState.StyleName)
                ? "Regular"
                : document.FontState.StyleName;

            PopulateStrikes();
            UpdateSummary();
        }

        /// <summary>The family name to record.</summary>
        public string FamilyName =>
            string.IsNullOrWhiteSpace(FamilyBox?.Text) ? "Font" : FamilyBox!.Text.Trim();

        /// <summary>The style to record.</summary>
        public string StyleName =>
            string.IsNullOrWhiteSpace(StyleBox?.Text) ? "Regular" : StyleBox!.Text.Trim();

        /// <summary>The multiples of the em that were ticked.</summary>
        public IReadOnlyList<int> StrikeScales =>
            _strikeBoxes.Where(b => b.IsChecked == true)
                        .Select(b => (int)b.Tag)
                        .ToList();

        private void PopulateStrikes()
        {
            int em = FontMetricsOps.EmHeightOf(_document);

            foreach (int scale in FontStrikeOps.AvailableScales(_document))
            {
                var box = new CheckBox
                {
                    Content = $"{em * scale} px",
                    Tag = scale,
                    Margin = new Thickness(0, 0, 12, 0),

                    // The drawn size and double it cover most uses, and every extra size makes the
                    // file bigger for a gain that only shows on a reader that misbehaves.
                    IsChecked = scale <= 2,
                };

                box.Checked += (_, __) => UpdateSummary();
                box.Unchecked += (_, __) => UpdateSummary();

                _strikeBoxes.Add(box);
                StrikeList.Items.Add(box);
            }
        }

        private void UpdateSummary()
        {
            if (SummaryText is null) return;

            int em = FontMetricsOps.EmHeightOf(_document);
            int glyphs = _document.FontState.Glyphs.Count;
            var chosen = StrikeScales;

            string sizes = chosen.Count == 0
                ? "no embedded sizes"
                : "embedded at " + string.Join(", ", chosen.Select(s => $"{em * s} px"));

            SummaryText.Text =
                $"{glyphs} glyphs at {FontOutlineOps.UnitsPerEm(em)} units per em, " +
                $"which is {em} pixels exactly. {char.ToUpperInvariant(sizes[0])}{sizes[1..]}.";

            IsPrimaryButtonEnabled = glyphs > 0;
        }
    }
}
