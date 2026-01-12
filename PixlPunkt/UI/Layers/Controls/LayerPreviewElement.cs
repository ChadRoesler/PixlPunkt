#if HAS_UNO
using System;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace PixlPunkt.UI.Layers.Controls;

/// <summary>
/// High-performance SKCanvasElement for layer preview background rendering.
/// Uses Uno's direct Skia integration for hardware-accelerated checkerboard pattern.
/// </summary>
/// <remarks>
/// <para>
/// This element is only available on Uno Skia platforms (Desktop, WebAssembly).
/// On WinAppSdk (Windows), the code-behind uses SKXamlCanvas directly.
/// </para>
/// <para>
/// This element renders the checkerboard transparency pattern behind layer previews.
/// Using SKCanvasElement instead of SKXamlCanvas provides:
/// </para>
/// <list type="bullet">
/// <item>Hardware acceleration via OpenGL (when available)</item>
/// <item>No buffer copying - draws directly to the app's Skia surface</item>
/// <item>Reduced GC pressure</item>
/// </list>
/// </remarks>
public sealed class LayerPreviewElement : SKCanvasElement
{
    private Action<SKCanvas, float, float>? _drawCallback;
    private float _lastWidth;
    private float _lastHeight;

    /// <summary>
    /// Gets or sets the callback invoked during rendering.
    /// </summary>
    /// <remarks>
    /// The callback receives the SKCanvas and the available drawing area dimensions.
    /// </remarks>
    public Action<SKCanvas, float, float>? DrawCallback
    {
        get => _drawCallback;
        set => _drawCallback = value;
    }

    /// <summary>
    /// Gets the last rendered width in pixels.
    /// </summary>
    public float LastWidth => _lastWidth;

    /// <summary>
    /// Gets the last rendered height in pixels.
    /// </summary>
    public float LastHeight => _lastHeight;

    /// <summary>
    /// Renders the layer preview background using the registered callback.
    /// </summary>
    /// <param name="canvas">The SKCanvas to draw on.</param>
    /// <param name="area">The available drawing area size.</param>
    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        _lastWidth = (float)area.Width;
        _lastHeight = (float)area.Height;

        if (_drawCallback == null)
        {
            canvas.Clear(SKColors.Transparent);
            return;
        }

        _drawCallback(canvas, (float)area.Width, (float)area.Height);
    }
}
#endif
