using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Imaging;
using PixlPunkt.UI.CanvasHost;
using PixlPunkt.UI.Dialogs;
using PixlPunkt.UI.Font;
using PixlPunkt.UI.Helpers;

namespace PixlPunkt.UI
{
    /// <summary>
    /// Font-specific pieces of the main window. A font document wants a different bottom pane from
    /// an image one: an animation timeline says nothing about a typeface, while seeing a letter
    /// beside its neighbours is how spacing is judged.
    /// </summary>
    public sealed partial class PixlPunktMainWindow
    {
        /// <summary>
        /// Swaps the bottom pane to suit the document, and points the glyph strip at it. Passing
        /// null puts the animation panel back, which is the right resting state with nothing open.
        /// </summary>
        private void UpdateBottomPaneForDocument(CanvasViewHost? host)
        {
            bool isFont = host?.Document.FontState.HasState == true;

            if (AnimationPanel is not null)
                AnimationPanel.Visibility = isFont ? Visibility.Collapsed : Visibility.Visible;

            if (GlyphStrip is null) return;

            GlyphStrip.Visibility = isFont ? Visibility.Visible : Visibility.Collapsed;
            GlyphStrip.Bind(isFont ? host!.Document : null, isFont ? host : null);

            if (FontMenu is not null)
                FontMenu.Visibility = isFont ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Font_Preview_Click(object sender, RoutedEventArgs e) => OpenFontPreview();

        /// <summary>
        /// Opens the running-text preview for the font in the active tab. Reached from the Font
        /// menu and from the button on the glyph strip, which is where you already are when the
        /// question "how does this read?" comes up.
        /// </summary>
        private void OpenFontPreview()
        {
            var doc = CurrentHost?.Document;
            if (doc is null || !doc.FontState.HasState) return;

            var window = new FontPreviewWindow();
            window.Bind(doc);
            window.Activate();

            string name = doc.FontState.FamilyName;
            if (string.IsNullOrWhiteSpace(name)) name = doc.Name ?? "Untitled";

            var appWindow = WindowHost.ApplyChrome(
                window,
                resizable: true,
                alwaysOnTop: false,
                minimizable: true,
                maximizable: true,
                title: $"Font Preview - {name}",
                owner: App.PixlPunktMainWindow);

            WindowHost.Place(appWindow, Core.Enums.WindowPlacement.CenterOnScreen, App.PixlPunktMainWindow);
        }

        private async void Font_ExportSheet_Click(object sender, RoutedEventArgs e) =>
            await ExportFontSheetAsync();

        /// <summary>
        /// Writes the font out as a sprite sheet, and as BMFont metrics beside it when asked. The
        /// metrics take the image's own file name, so the pair stays linked wherever it is moved.
        /// </summary>
        private async Task ExportFontSheetAsync()
        {
            var doc = CurrentHost?.Document;
            if (doc is null || !doc.FontState.HasState) return;

            var dialog = new FontSheetExportDialog(doc) { XamlRoot = MainXamlRoot };
            if (await ShowDialogGuardedAsync(dialog) != ContentDialogResult.Primary) return;

            int scale = dialog.Scale;
            string faceName = dialog.FaceName;
            bool writeMetrics = dialog.ShouldWriteMetrics;

            var picker = WindowHost.CreateFileSavePicker(this, SafeFileName(faceName), ".png");
            var file = await picker.PickSaveFileAsync();
            if (file is null) return;

            try
            {
                var sheet = FontExportOps.SheetSize(doc, scale);
                var pixels = FontExportOps.RenderSheet(doc, scale);
                SkiaImageEncoder.Encode(pixels, sheet.Width, sheet.Height, file.Path);

                if (writeMetrics)
                {
                    var options = new FontExportOptions(scale, faceName, Path.GetFileName(file.Path));
                    string metricsPath = Path.ChangeExtension(file.Path, ".fnt");
                    File.WriteAllText(metricsPath, FontExportOps.BuildBMFont(doc, options));
                }
            }
            catch (Exception ex)
            {
                await ShowFontMessageAsync("Export failed", ex.Message);
            }
        }

        /// <summary>Strips whatever a file name cannot hold, so a face name can be used as one.</summary>
        private static string SafeFileName(string name)
        {
            foreach (char bad in Path.GetInvalidFileNameChars())
                name = name.Replace(bad, '_');

            return string.IsNullOrWhiteSpace(name) ? "font" : name.Trim();
        }

        private async Task ShowFontMessageAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "OK",
                XamlRoot = MainXamlRoot,
            };
            await ShowDialogGuardedAsync(dialog);
        }
    }
}
