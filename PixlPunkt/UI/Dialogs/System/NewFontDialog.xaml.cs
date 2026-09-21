using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using static PixlPunkt.Core.Helpers.GraphicsStructHelper;

namespace PixlPunkt.UI.Dialogs
{
    /// <summary>
    /// What the New Font dialog collected. <see cref="Characters"/> is already de-duplicated and
    /// in the order the glyph cells will be laid out.
    /// </summary>
    public sealed record NewFontResult(
        string Name,
        SizeInt32 EmSize,
        SizeInt32 CellSize,
        string Characters,
        bool Monospace,
        int Columns);

    /// <summary>
    /// Creates a pixel font document: a glyph sheet whose tile size is the em box.
    /// </summary>
    /// <remarks>
    /// The em box decides which pixel sizes the finished font renders evenly at, namely its own
    /// multiples, so the dialog says so out loud rather than letting it be discovered later.
    /// </remarks>
    public sealed partial class NewFontDialog : ContentDialog
    {
        private const int MaxColumns = 16;
        private const int DefaultOverhang = 2;
        private const string UpperDigits = " ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.,!?'\"-:";
        private const string LettersDigits = " ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789.,!?'\"-:";

        public NewFontDialog()
        {
            InitializeComponent();
            UpdateInfo();
        }

        private static string PrintableAscii()
        {
            var sb = new StringBuilder();
            for (char c = ' '; c <= '~'; c++) sb.Append(c);
            return sb.ToString();
        }

        /// <summary>The chosen set, with duplicates and line breaks removed, order preserved.</summary>
        public string ResolveCharacters()
        {
            string source = (CharSetCombo?.SelectedIndex ?? 0) switch
            {
                1 => UpperDigits,
                2 => LettersDigits,
                3 => CustomChars?.Text ?? string.Empty,
                _ => PrintableAscii(),
            };

            var seen = new HashSet<char>();
            var sb = new StringBuilder();
            foreach (char c in source)
            {
                if (c is '\r' or '\n' or '\t') continue;
                if (seen.Add(c)) sb.Append(c);
            }
            return sb.ToString();
        }

        private int EmWidth => (int)Math.Max(3, EmW?.Value is double w and > 0 ? w : 8);
        private int EmHeight => (int)Math.Max(3, EmH?.Value is double h and > 0 ? h : 8);

        /// <summary>
        /// Blank columns and rows around the em box, where overhanging ink can live. Two by default:
        /// a font with none cannot draw a swash or a heavy accent at all, and that limit is not
        /// something anyone should have to discover halfway through drawing a set.
        /// </summary>
        private int OverhangRoom => (int)Math.Clamp(Overhang?.Value is double o and >= 0 ? o : DefaultOverhang, 0, 16);

        private int CellWidth => EmWidth + OverhangRoom * 2;
        private int CellHeight => EmHeight + OverhangRoom * 2;

        private static int ColumnsFor(int glyphCount) => Math.Max(1, Math.Min(MaxColumns, glyphCount));

        private static int RowsFor(int glyphCount, int columns) =>
            Math.Max(1, (glyphCount + columns - 1) / columns);

        private void UpdateInfo()
        {
            if (SheetInfo is null || CleanSizesText is null) return;

            string chars = ResolveCharacters();
            int n = chars.Length;
            int emH = EmHeight;
            int cellW = CellWidth, cellH = CellHeight;
            int cols = ColumnsFor(n);
            int rows = RowsFor(n, cols);

            SheetInfo.Text = n == 0
                ? "No characters selected."
                : $"{n} glyphs · cell {cellW} × {cellH} · sheet {cols} × {rows} · {cols * cellW} × {rows * cellH} px";

            var sizes = new List<int>();
            for (int k = 1; k * emH <= 48; k++)
                if (k * emH >= 8) sizes.Add(k * emH);
            if (sizes.Count == 0) sizes.Add(emH);

            CleanSizesText.Text =
                $"Crisp at {string.Join(", ", sizes)} px. Other sizes render with uneven stems, " +
                "whatever the font is built with.";

            IsPrimaryButtonEnabled = n > 0;
        }

        private void Size_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args) => UpdateInfo();

        private void Input_Changed(object sender, TextChangedEventArgs e) => UpdateInfo();

        private void CharSet_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (CustomChars is not null)
                CustomChars.Visibility = (CharSetCombo?.SelectedIndex == 3) ? Visibility.Visible : Visibility.Collapsed;
            UpdateInfo();
        }

        public NewFontResult GetResult()
        {
            string name = string.IsNullOrWhiteSpace(FontName?.Text) ? "NewFont" : FontName!.Text.Trim();
            string chars = ResolveCharacters();
            return new NewFontResult(
                Name: name,
                EmSize: CreateSize(EmWidth, EmHeight),
                CellSize: CreateSize(CellWidth, CellHeight),
                Characters: chars,
                Monospace: MonospaceCheck?.IsChecked == true,
                Columns: ColumnsFor(chars.Length));
        }
    }
}
