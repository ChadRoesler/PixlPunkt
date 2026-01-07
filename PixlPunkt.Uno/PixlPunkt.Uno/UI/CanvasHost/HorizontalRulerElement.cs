#if HAS_UNO
using System;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace PixlPunkt.Uno.UI.CanvasHost;

/// <summary>
/// High-performance SKCanvasElement for the horizontal ruler.
/// Uses Uno's direct Skia integration for hardware-accelerated rendering.
/// </summary>
/// <remarks>
/// <para>
/// This element is only available on Uno Skia platforms (Desktop, WebAssembly).
/// On WinAppSdk (Windows), the code-behind uses SKXamlCanvas directly.
/// </para>
/// </remarks>
public sealed class HorizontalRulerElement : SKCanvasElement
{
    private Action<SKCanvas, float, float>? _drawCallback;

    /// <summary>
    /// Gets or sets the callback invoked during rendering.
    /// The callback receives the SKCanvas and the width/height of the drawing area.
    /// </summary>
    public Action<SKCanvas, float, float>? DrawCallback
    {
        get => _drawCallback;
        set => _drawCallback = value;
    }

    /// <summary>
    /// Renders the ruler content using the registered callback.
    /// </summary>
    /// <param name="canvas">The SKCanvas to draw on.</param>
    /// <param name="area">The available drawing area size.</param>
    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        if (_drawCallback == null)
        {
            canvas.Clear(SKColors.Transparent);
            return;
        }

        _drawCallback(canvas, (float)area.Width, (float)area.Height);
    }
}
#endif
