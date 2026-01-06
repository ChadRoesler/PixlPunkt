using System;
using Microsoft.UI.Xaml;

namespace PixlPunkt.Uno.UI
{
    /// <summary>
    /// Partial class for reference image menu commands and operations.
    /// </summary>
    public sealed partial class PixlPunktMainWindow : Window
    {
        ///////////////////////////////////////////////////////////////////////
        // REFERENCE IMAGE COMMANDS
        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Opens a file picker to add a reference image to the active document.
        /// </summary>
        private async void View_AddReferenceImage_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentHost == null) return;

            await CurrentHost.AddReferenceImageAsync();
        }

        /// <summary>
        /// Toggles visibility of all reference images.
        /// </summary>
        private void View_ToggleReferenceImages_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentHost?.Document?.ReferenceImages == null) return;

            var refService = CurrentHost.Document.ReferenceImages;
            refService.OverlaysVisible = !refService.OverlaysVisible;
            CurrentHost.InvalidateCanvas();

            Core.Logging.LoggingService.Debug($"Toggled reference images: {refService.OverlaysVisible}");
        }

        /// <summary>
        /// Removes the currently selected reference image.
        /// </summary>
        private void View_RemoveSelectedReference_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentHost?.Document?.ReferenceImages == null) return;

            var refService = CurrentHost.Document.ReferenceImages;
            if (refService.SelectedImage != null)
            {
                var name = refService.SelectedImage.Name;
                refService.Remove(refService.SelectedImage);
                CurrentHost.InvalidateCanvas();

                Core.Logging.LoggingService.Debug($"Removed reference image: {name}");
            }
        }

        /// <summary>
        /// Clears all reference images from the active document.
        /// </summary>
        private async void View_ClearAllReferences_Click(object sender, RoutedEventArgs e)
        {
            if (CurrentHost?.Document?.ReferenceImages == null) return;

            var refService = CurrentHost.Document.ReferenceImages;
            if (refService.Count == 0) return;

            // Confirm with user
            var dlg = new Microsoft.UI.Xaml.Controls.ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = "Clear Reference Images",
                Content = $"Are you sure you want to remove all {refService.Count} reference image(s)?",
                PrimaryButtonText = "Clear All",
                CloseButtonText = "Cancel",
                DefaultButton = Microsoft.UI.Xaml.Controls.ContentDialogButton.Close
            };

            var result = await ShowDialogGuardedAsync(dlg);
            if (result != Microsoft.UI.Xaml.Controls.ContentDialogResult.Primary)
                return;

            int count = refService.Count;
            refService.Clear();
            CurrentHost.InvalidateCanvas();

            Core.Logging.LoggingService.Debug($"Cleared {count} reference images");
        }
    }
}
