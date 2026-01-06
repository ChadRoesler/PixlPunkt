using System;
using Windows.Foundation;

namespace PixlPunkt.Uno.Core.Tools.Utility
{
    /// <summary>
    /// Context interface providing utility handlers access to canvas operations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="IUtilityContext"/> abstracts the canvas host operations that utility
    /// handlers need, allowing handlers to be tested independently and keeping them
    /// decoupled from the UI layer.
    /// </para>
    /// </remarks>
    public interface IUtilityContext
    {
        /// <summary>
        /// Gets the document dimensions in pixels.
        /// </summary>
        (int Width, int Height) DocumentSize { get; }

        /// <summary>
        /// Gets the current zoom scale factor.
        /// </summary>
        double ZoomScale { get; }

        /// <summary>
        /// Pans the viewport by the specified screen delta.
        /// </summary>
        /// <param name="deltaX">Horizontal pan amount in screen pixels.</param>
        /// <param name="deltaY">Vertical pan amount in screen pixels.</param>
        void PanBy(double deltaX, double deltaY);

        /// <summary>
        /// Zooms the viewport at the specified screen position.
        /// </summary>
        /// <param name="screenPos">Screen position to zoom toward/from.</param>
        /// <param name="factor">Zoom factor (&gt;1 = zoom in, &lt;1 = zoom out).</param>
        void ZoomAt(Point screenPos, double factor);

        /// <summary>
        /// Reads a pixel color from the composite at document coordinates.
        /// </summary>
        /// <param name="docX">Document X coordinate.</param>
        /// <param name="docY">Document Y coordinate.</param>
        /// <returns>BGRA color value at the specified position.</returns>
        uint SampleColorAt(int docX, int docY);

        /// <summary>
        /// Sets the foreground color (typically from dropper sampling).
        /// </summary>
        /// <param name="bgra">BGRA color value to set as foreground.</param>
        void SetForegroundColor(uint bgra);

        /// <summary>
        /// Sets the background color (typically from dropper sampling).
        /// </summary>
        /// <param name="bgra">BGRA color value to set as background.</param>
        void SetBackgroundColor(uint bgra);

        /// <summary>
        /// Requests a canvas redraw/invalidate.
        /// </summary>
        void RequestRedraw();

        /// <summary>
        /// Captures pointer for continued tracking during drag operations.
        /// </summary>
        void CapturePointer();

        /// <summary>
        /// Releases captured pointer.
        /// </summary>
        void ReleasePointer();

        /// <summary>
        /// Raised when foreground color is sampled (for live preview updates).
        /// </summary>
        event Action<uint>? ForegroundSampled;

        /// <summary>
        /// Raised when background color is sampled (for live preview updates).
        /// </summary>
        event Action<uint>? BackgroundSampled;
    }
}
