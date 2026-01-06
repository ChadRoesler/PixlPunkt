using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Uno.Core.Canvas;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.Logging;
using PixlPunkt.Uno.UI.Helpers;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace PixlPunkt.Uno.UI
{
    /// <summary>
    /// Partial class for advanced template operations (export/import).
    /// </summary>
    public sealed partial class PixlPunktMainWindow : Window
    {
        //////////////////////////////////////////////////////////////////
        // TEMPLATE MENU INITIALIZATION
        //////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the File > New from Template submenu with available templates.
        /// </summary>
        private void BuildAdvancedTemplatesMenu()
        {
            if (File_NewFromTemplate_Submenu == null) return;

            File_NewFromTemplate_Submenu.Items.Clear();

            var templates = AdvancedTemplateIO.EnumerateTemplates();

            if (templates.Count == 0)
            {
                var emptyItem = new MenuFlyoutItem
                {
                    Text = "(No templates available)",
                    IsEnabled = false
                };
                File_NewFromTemplate_Submenu.Items.Add(emptyItem);

                // Add a hint to create templates
                var hintItem = new MenuFlyoutItem
                {
                    Text = "Export a document as template first",
                    IsEnabled = false,
                    FontStyle = Windows.UI.Text.FontStyle.Italic
                };
                File_NewFromTemplate_Submenu.Items.Add(hintItem);
                return;
            }

            // Add each template as a menu item
            foreach (var template in templates)
            {
                var item = new MenuFlyoutItem
                {
                    Text = template.Name,
                    Tag = template
                };

                // Add tooltip with template details
                var tooltip = template.Summary;
                if (!string.IsNullOrWhiteSpace(template.Description))
                    tooltip = $"{template.Description}\n{tooltip}";
                if (!string.IsNullOrWhiteSpace(template.Author))
                    tooltip += $"\nBy: {template.Author}";

                ToolTipService.SetToolTip(item, tooltip);

                item.Click += NewFromTemplate_Click;
                File_NewFromTemplate_Submenu.Items.Add(item);
            }

            // Add separator and management options
            File_NewFromTemplate_Submenu.Items.Add(new MenuFlyoutSeparator());

            var refreshItem = new MenuFlyoutItem { Text = "Refresh Templates" };
            refreshItem.Click += (s, e) => BuildAdvancedTemplatesMenu();
            File_NewFromTemplate_Submenu.Items.Add(refreshItem);

            var openFolderItem = new MenuFlyoutItem { Text = "Open Templates Folder" };
            openFolderItem.Click += (s, e) =>
            {
                var path = AdvancedTemplateIO.GetTemplatesDirectory();
                try
                {
                    AdvancedTemplateIO.EnsureDirectoryExists();
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    LoggingService.Error("Failed to open templates folder", ex);
                }
            };
            File_NewFromTemplate_Submenu.Items.Add(openFolderItem);
        }

        //////////////////////////////////////////////////////////////////
        // NEW FROM TEMPLATE
        //////////////////////////////////////////////////////////////////

        private async void NewFromTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuFlyoutItem item || item.Tag is not AdvancedTemplate template)
                return;

            if (string.IsNullOrEmpty(template.FilePath) || !File.Exists(template.FilePath))
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "Template Not Found",
                    Content = "The template file could not be found. It may have been moved or deleted.",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                });
                BuildAdvancedTemplatesMenu(); // Refresh menu
                return;
            }

            try
            {
                // Load template as new document
                var doc = AdvancedTemplateIO.LoadAsNewDocument(template.FilePath);

                // Register document & open it in a new tab
                _workspace.Add(doc);
                _documentPaths[doc] = string.Empty; // No file path - it's a new document
                _autoSave.RegisterDocument(doc);
                var tab = MakeTab(doc);
                DocsTab.TabItems.Add(tab);
                DocsTab.SelectedItem = tab;

                LoggingService.Info("Created new document from template: {TemplateName}", template.Name);
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to create document from template", ex);
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "Template Error",
                    Content = $"Could not create document from template.\n{ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                });
            }
        }

        //////////////////////////////////////////////////////////////////
        // EXPORT TO ADVANCED TEMPLATE
        //////////////////////////////////////////////////////////////////

        private async void File_Export_AdvancedTemplate_Click(object sender, RoutedEventArgs e)
        {
            var doc = CurrentHost?.Document;
            if (doc is null)
            {
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "No Document",
                    Content = "Open a document before exporting as a template.",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                });
                return;
            }

            // Show export dialog
            var nameBox = new TextBox
            {
                PlaceholderText = "Template Name",
                Text = doc.Name ?? "My Template"
            };

            var descriptionBox = new TextBox
            {
                PlaceholderText = "Description (optional)",
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                Height = 80
            };

            var authorBox = new TextBox
            {
                PlaceholderText = "Author (optional)"
            };

            // Info panel showing what will be saved
            var infoText = new TextBlock
            {
                Text = $"This template will include:\n" +
                       $"� Canvas: {doc.PixelWidth}�{doc.PixelHeight} pixels\n" +
                       $"� Layers: {doc.Layers.Count}\n" +
                       $"� Tiles: {doc.TileSet?.Count ?? 0}\n" +
                       $"� Tile mappings\n" +
                       $"� Layer effects",
                Opacity = 0.7,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var panel = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    new TextBlock { Text = "Template Name:" },
                    nameBox,
                    new TextBlock { Text = "Description:" },
                    descriptionBox,
                    new TextBlock { Text = "Author:" },
                    authorBox,
                    infoText
                }
            };

            var dialog = new ContentDialog
            {
                Title = "Export as Advanced Template",
                Content = panel,
                PrimaryButtonText = "Export",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot
            };

            var result = await ShowDialogGuardedAsync(dialog);
            if (result != ContentDialogResult.Primary)
                return;

            var templateName = string.IsNullOrWhiteSpace(nameBox.Text) ? "Untitled Template" : nameBox.Text.Trim();
            var description = string.IsNullOrWhiteSpace(descriptionBox.Text) ? null : descriptionBox.Text.Trim();
            var author = string.IsNullOrWhiteSpace(authorBox.Text) ? null : authorBox.Text.Trim();

            try
            {
                // Generate preview thumbnail (96x96 max)
                byte[]? previewBytes = await GenerateTemplateThumbnailAsync(doc, 96, 96);

                // Save template
                var templatePath = AdvancedTemplateIO.SaveAsTemplate(doc, templateName, description, author, previewBytes);

                // Refresh the templates menu
                BuildAdvancedTemplatesMenu();

                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "Template Saved",
                    Content = $"Template '{templateName}' has been saved.\n\n" +
                              $"You can create new documents from it via:\nFile ? New from Template",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                });

                LoggingService.Info("Exported advanced template: {TemplateName} -> {Path}", templateName, templatePath);
            }
            catch (Exception ex)
            {
                LoggingService.Error("Failed to export template", ex);
                await ShowDialogGuardedAsync(new ContentDialog
                {
                    Title = "Export Failed",
                    Content = $"Could not save template.\n{ex.Message}",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                });
            }
        }

        /// <summary>
        /// Generates a PNG thumbnail for the template preview.
        /// </summary>
        private static async Task<byte[]?> GenerateTemplateThumbnailAsync(CanvasDocument doc, int maxWidth, int maxHeight)
        {
            try
            {
                // Composite the document
                doc.CompositeTo(doc.Surface);

                int srcW = doc.PixelWidth;
                int srcH = doc.PixelHeight;

                // Calculate thumbnail size maintaining aspect ratio
                float scale = Math.Min((float)maxWidth / srcW, (float)maxHeight / srcH);
                scale = Math.Min(scale, 1f); // Don't upscale

                int dstW = Math.Max(1, (int)(srcW * scale));
                int dstH = Math.Max(1, (int)(srcH * scale));

                // Create scaled buffer using nearest neighbor
                var srcPixels = doc.Surface.Pixels;
                var dstPixels = new byte[dstW * dstH * 4];

                for (int y = 0; y < dstH; y++)
                {
                    int srcY = Math.Min((int)(y / scale), srcH - 1);
                    for (int x = 0; x < dstW; x++)
                    {
                        int srcX = Math.Min((int)(x / scale), srcW - 1);
                        int srcIdx = (srcY * srcW + srcX) * 4;
                        int dstIdx = (y * dstW + x) * 4;

                        dstPixels[dstIdx + 0] = srcPixels[srcIdx + 0]; // B
                        dstPixels[dstIdx + 1] = srcPixels[srcIdx + 1]; // G
                        dstPixels[dstIdx + 2] = srcPixels[srcIdx + 2]; // R
                        dstPixels[dstIdx + 3] = srcPixels[srcIdx + 3]; // A
                    }
                }

                // Encode as PNG
                using var stream = new InMemoryRandomAccessStream();
                var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.SetPixelData(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    (uint)dstW,
                    (uint)dstH,
                    96, 96,
                    dstPixels);
                await encoder.FlushAsync();

                stream.Seek(0);
                var bytes = new byte[stream.Size];
                var reader = new DataReader(stream.GetInputStreamAt(0));
                await reader.LoadAsync((uint)stream.Size);
                reader.ReadBytes(bytes);

                return bytes;
            }
            catch (Exception ex)
            {
                LoggingService.Warning("Failed to generate template thumbnail: {Error}", ex.Message);
                return null;
            }
        }
    }
}
