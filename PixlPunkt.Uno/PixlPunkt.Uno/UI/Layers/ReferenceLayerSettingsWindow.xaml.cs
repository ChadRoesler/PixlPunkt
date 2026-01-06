using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.Document.Layer;

namespace PixlPunkt.Uno.UI.Layers;

/// <summary>
/// Settings window for editing reference layer properties.
/// </summary>
public sealed partial class ReferenceLayerSettingsWindow : Window
{
    private readonly CanvasDocument _doc;
    private readonly ReferenceLayer _layer;

    /// <summary>
    /// Gets the reference layer being edited.
    /// </summary>
    public ReferenceLayer Layer => _layer;

    /// <summary>
    /// Initializes a new instance of the <see cref="ReferenceLayerSettingsWindow"/> class.
    /// </summary>
    /// <param name="doc">The document containing the layer.</param>
    /// <param name="layer">The reference layer to edit.</param>
    public ReferenceLayerSettingsWindow(CanvasDocument doc, ReferenceLayer layer)
    {
        _doc = doc;
        _layer = layer;
        InitializeComponent();
    }

    private void Opacity_ValueChanged(object sender, object e)
    {
        _doc.RaiseStructureChanged();
    }

    private void Transform_ValueChanged(object sender, object e)
    {
        _doc.RaiseStructureChanged();
    }

    private void VisBtn_Click(object sender, RoutedEventArgs e)
    {
        _doc.RaiseStructureChanged();
    }

    private void LockBtn_Click(object sender, RoutedEventArgs e)
    {
        _doc.RaiseStructureChanged();
    }

    private void FitToCanvas_Click(object sender, RoutedEventArgs e)
    {
        _layer.FitToCanvas(_doc.PixelWidth, _doc.PixelHeight, 0.05f);
        _doc.RaiseStructureChanged();
    }

    private void ResetTransform_Click(object sender, RoutedEventArgs e)
    {
        _layer.ResetTransform();
        _doc.RaiseStructureChanged();
    }

    private void CloseBtn_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
