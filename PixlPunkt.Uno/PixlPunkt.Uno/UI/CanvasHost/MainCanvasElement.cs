#if HAS_UNO
using System;
using PixlPunkt.Uno.Core.Rendering;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace PixlPunkt.Uno.UI.CanvasHost;

/// <summary>
/// High-performance SKCanvasElement for the main document canvas.
/// Uses Uno's direct Skia integration for hardware-accelerated rendering
/// without the buffer copying overhead of SKXamlCanvas.
/// </summary>
/// <remarks>
/// <para>
/// This element is only available on Uno Skia platforms (Desktop, WebAssembly).
/// On WinAppSdk (Windows), the code-behind uses SKXamlCanvas directly.
/// </para>
/// <para>
/// SKCanvasElement provides significant performance improvements over SKXamlCanvas:
/// </para>
/// <list type="bullet">
/// <item>Hardware acceleration via OpenGL (when available)</item>
/// <item>No buffer copying - draws directly to the app's Skia surface</item>
/// <item>Reduced GC pressure</item>
/// </list>
/// <para>
/// This element requires Skia rendering to be enabled (desktop targets or SkiaRenderer feature).
/// Check <see cref="SKCanvasElement.IsSupportedOnCurrentPlatform()"/> before instantiation.
/// </para>
/// </remarks>
public sealed class MainCanvasElement : SKCanvasElement
{
    private Action<ICanvasRenderer>? _drawCallback;
    private float _lastWidth;
    private float _lastHeight;

    /// <summary>
    /// Gets or sets the callback invoked during rendering.
    /// </summary>
    /// <remarks>
    /// The callback receives an <see cref="ICanvasRenderer"/> that wraps the SKCanvas,
    /// providing a consistent API whether using SKXamlCanvas or SKCanvasElement.
    /// </remarks>
    public Action<ICanvasRenderer>? DrawCallback
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
    /// Renders the canvas content using the registered callback.
    /// </summary>
    /// <param name="canvas">The SKCanvas to draw on. Origin is already translated to element position.</param>
    /// <param name="area">The available drawing area size.</param>
    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        _lastWidth = (float)area.Width;
        _lastHeight = (float)area.Height;

        if (_drawCallback == null)
        {
            // Clear to transparent if no callback registered
            canvas.Clear(SKColors.Transparent);
            return;
        }

        // Create renderer wrapper and invoke callback
        using var renderer = new SkiaCanvasRenderer(canvas, (float)area.Width, (float)area.Height);
        _drawCallback(renderer);
    }
}
#endif
