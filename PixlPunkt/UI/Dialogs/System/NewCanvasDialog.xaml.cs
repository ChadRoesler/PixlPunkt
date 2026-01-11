using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Core.Canvas;
using Windows.Graphics;
using System.Threading.Tasks;
using static PixlPunkt.Core.Helpers.GraphicsStructHelper;

namespace PixlPunkt.UI.Dialogs
{
    /// <summary>
    /// Result from the New Canvas dialog containing all information needed to create a document.
    /// </summary>
    public sealed record NewCanvasResult(
        string Name,
        SizeInt32 TileSize,
        SizeInt32 TileCounts,
        CanvasTemplate? Template
    );

    public sealed partial class NewCanvasDialog : ContentDialog
    {
        public ObservableCollection<CanvasTemplate> Templates { get; } = new();
        public CanvasTemplate? SelectedTemplate { get; set; }

        public NewCanvasDialog()
        {
            this.InitializeComponent();

            // Set DataContext for simple XAML bindings
            DataContext = this;

            // Load all templates (built-in + custom)
            LoadAllTemplates();

            // Initialize selection to first item (if any)
            if (Templates.Count > 0)
            {
                SelectedTemplate = Templates[0];
                ApplyTemplate(SelectedTemplate);
            }
        }

        /// <summary>
        /// Loads all templates (built-in and custom) into the combined list.
        /// </summary>
        private void LoadAllTemplates()
        {
            Templates.Clear();

            // Add built-in templates first
            foreach (var template in DefaultCanvasTemplates.All)
            {
                Templates.Add(template);
            }

            // Add custom templates
            CustomTemplateService.Instance.Initialize();
            foreach (var customTemplate in CustomTemplateService.Instance.Templates)
            {
                var template = customTemplate.ToCanvasTemplate();
                Templates.Add(template);
            }
        }

        private void TemplateType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TemplateType.SelectedItem is CanvasTemplate selected)
            {
                SelectedTemplate = selected;
                ApplyTemplate(selected);
            }
        }

        private void ApplyTemplate(CanvasTemplate t)
        {
            if (t is null) return;
            TileW.Value = t.TileWidth;
            TileH.Value = t.TileHeight;
            TilesX.Value = t.TileCountX;
            TilesY.Value = t.TileCountY;
        }

        /// <summary>
        /// Saves the current settings as a custom template.
        /// Uses inline UI instead of nested dialog.
        /// </summary>
        private void SaveAsTemplate_Click(object sender, RoutedEventArgs e)
        {
            // Get current values
            int tw = (int)Math.Max(1, TileW.Value);
            int th = (int)Math.Max(1, TileH.Value);
            int cx = (int)Math.Max(1, TilesX.Value);
            int cy = (int)Math.Max(1, TilesY.Value);

            // Use canvas name or default
            string templateName = string.IsNullOrWhiteSpace(CanvasName.Text)
                ? $"Template_{tw}x{th}_{cx}x{cy}"
                : CanvasName.Text.Trim();

            try
            {
                CustomTemplateService.Instance.SaveTemplate(templateName, tw, th, cx, cy);
                LoadAllTemplates();

                // Show inline feedback by temporarily changing button text
                if (sender is Button btn)
                {
                    var originalContent = btn.Content;
                    btn.Content = $"? Saved: {templateName}";
                    btn.IsEnabled = false;

                    // Reset after delay using fire-and-forget pattern with proper exception handling
                    _ = ResetButtonAfterDelayAsync(btn, originalContent, 2000);
                }
            }
            catch (Exception ex)
            {
                // Show error inline
                if (sender is Button btn)
                {
                    var originalContent = btn.Content;
                    btn.Content = $"? Error: {ex.Message}";

                    // Reset after delay using fire-and-forget pattern with proper exception handling
                    _ = ResetButtonAfterDelayAsync(btn, originalContent, 3000);
                }
            }
        }

        /// <summary>
        /// Resets a button's content after a delay. Handles exceptions properly unlike async void lambdas.
        /// </summary>
        private async Task ResetButtonAfterDelayAsync(Button btn, object? originalContent, int delayMs)
        {
            try
            {
                await Task.Delay(delayMs);
                // Ensure we're on the UI thread
                DispatcherQueue.TryEnqueue(() =>
                {
                    btn.Content = originalContent;
                    btn.IsEnabled = true;
                });
            }
            catch
            {
                // Ignore exceptions during cleanup (window may have closed)
            }
        }

        /// <summary>
        /// Gets the dialog result with all values needed to create a canvas.
        /// </summary>
        public NewCanvasResult GetResult()
        {
            string name = string.IsNullOrWhiteSpace(CanvasName.Text) ? "NewCanvas" : CanvasName.Text;
            int tw = (int)Math.Max(1, TileW.Value);
            int th = (int)Math.Max(1, TileH.Value);
            int cx = (int)Math.Max(1, TilesX.Value);
            int cy = (int)Math.Max(1, TilesY.Value);

            return new NewCanvasResult(
                Name: name,
                TileSize: CreateSize(tw, th),
                TileCounts: CreateSize(cx, cy),
                Template: SelectedTemplate
            );
        }

        /// <summary>
        /// Legacy method for backward compatibility.
        /// </summary>
        public (string name, int pxW, int pxH, SizeInt32 tileSize, SizeInt32 tileCounts) GetValues()
        {
            var result = GetResult();
            int pxW = result.TileSize.Width * result.TileCounts.Width;
            int pxH = result.TileSize.Height * result.TileCounts.Height;
            return (result.Name, pxW, pxH, result.TileSize, result.TileCounts);
        }
    }
}
