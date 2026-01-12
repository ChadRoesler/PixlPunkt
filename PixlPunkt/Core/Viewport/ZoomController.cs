using System;
using Windows.Foundation;

namespace PixlPunkt.Core.Viewport
{
    /// <summary>
    /// Manages zoom level and pan offset for viewport transformations, converting between
    /// screen coordinates and document coordinates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ZoomController maintains the transformation matrix for displaying a document
    /// within a viewport. It handles zoom-in/out, panning, fitting to view, and coordinate
    /// conversion between screen space and document space.
    /// </para>
    /// <para>
    /// Typical usage:
    /// 1. Call <see cref="SetDocSize"/> with document pixel dimensions
    /// 2. Call <see cref="SetViewportSize"/> with viewport pixel dimensions
    /// 3. Use <see cref="Fit"/> or <see cref="Actual"/> to set initial view
    /// 4. Use <see cref="ZoomAt"/> and <see cref="PanBy"/> for user interaction
    /// 5. Use coordinate conversion methods for hit-testing and rendering
    /// </para>
    /// </remarks>
    public sealed class ZoomController
    {
        /// <summary>
        /// Gets the current zoom scale factor.
        /// </summary>
        /// <value>
        /// A value where 1.0 represents 100% (actual size), 0.5 is 50%, 2.0 is 200%, etc.
        /// Typically clamped between 0.05 and 64.0.
        /// </value>
        public double Scale { get; private set; } = 1.0;

        /// <summary>
        /// Gets the current pan offset in screen coordinates.
        /// </summary>
        /// <value>
        /// The offset from the screen origin (0,0) to the document origin after scaling.
        /// Positive values move the document right/down in screen space.
        /// </value>
        public Point Offset { get; private set; } = new(0, 0);

        private Size _doc;
        private Size _viewport;

        /// <summary>
        /// Sets the document dimensions in pixels.
        /// </summary>
        /// <param name="w">The width of the document in pixels.</param>
        /// <param name="h">The height of the document in pixels.</param>
        public void SetDocSize(double w, double h) => _doc = new Size(w, h);

        /// <summary>
        /// Sets the viewport dimensions in pixels.
        /// </summary>
        /// <param name="w">The width of the viewport in pixels.</param>
        /// <param name="h">The height of the viewport in pixels.</param>
        public void SetViewportSize(double w, double h) => _viewport = new Size(w, h);

        /// <summary>
        /// Calculates the destination rectangle for rendering the document in screen coordinates.
        /// </summary>
        /// <returns>
        /// A rectangle defining where the document should be drawn in the viewport,
        /// accounting for current scale and offset.
        /// </returns>
        public Rect GetDestRect()
        {
            var w = _doc.Width * Scale;
            var h = _doc.Height * Scale;
            return new Rect(Offset.X, Offset.Y, w, h);
        }

        /// <summary>
        /// Adjusts the scale and offset to fit the entire document in the viewport with padding.
        /// </summary>
        /// <param name="padding">The padding in pixels to leave around the document. Default is 16.</param>
        /// <remarks>
        /// Calculates the scale that will fit the document entirely within the viewport,
        /// maintaining aspect ratio, then centers the document. No action is taken if
        /// viewport or document dimensions are invalid.
        /// </remarks>
        public void Fit(double padding = 16)
        {
            if (_viewport.Width <= 0 || _viewport.Height <= 0 || _doc.Width <= 0 || _doc.Height <= 0)
                return;

            var sx = (_viewport.Width - padding * 2) / _doc.Width;
            var sy = (_viewport.Height - padding * 2) / _doc.Height;
            Scale = Math.Max(0.05, Math.Min(sx, sy));
            Center();
        }

        /// <summary>
        /// Sets the scale to 1.0 (100%, actual size) and centers the document in the viewport.
        /// </summary>
        public void Actual()
        {
            Scale = 1.0;
            Center();
        }

        /// <summary>
        /// Zooms in or out at a specific screen point, maintaining that point's position.
        /// </summary>
        /// <param name="screenPt">The screen coordinate to zoom towards (typically mouse position).</param>
        /// <param name="factor">
        /// The zoom multiplier. Values greater than 1.0 zoom in, less than 1.0 zoom out.
        /// Clamped to range [0.05, 20.0].
        /// </param>
        /// <param name="min">The minimum allowed scale. Default is 0.05 (5%).</param>
        /// <param name="max">The maximum allowed scale. Default is 64.0 (6400%).</param>
        /// <remarks>
        /// This method adjusts the scale and offset such that the document point under
        /// <paramref name="screenPt"/> remains at the same screen position after zooming.
        /// This creates an intuitive "zoom to cursor" behavior.
        /// </remarks>
        public void ZoomAt(Point screenPt, double factor, double min = 0.05, double max = 64.0)
        {
            factor = Math.Clamp(factor, 0.05, 20);
            var beforeDoc = ScreenToDoc(screenPt);
            Scale = Math.Clamp(Scale * factor, min, max);
            var afterScreen = DocToScreen(beforeDoc);
            PanBy(screenPt.X - afterScreen.X, screenPt.Y - afterScreen.Y);
        }

        /// <summary>
        /// Centers the document in the viewport at the current scale.
        /// </summary>
        /// <remarks>
        /// Calculates the offset that places the document's center at the viewport's center.
        /// </remarks>
        public void Center()
        {
            var w = _doc.Width * Scale;
            var h = _doc.Height * Scale;
            Offset = new Point((_viewport.Width - w) * 0.5, (_viewport.Height - h) * 0.5);
        }

        /// <summary>
        /// Adjusts the pan offset by the specified delta.
        /// </summary>
        /// <param name="dx">The horizontal delta in screen pixels (positive = right).</param>
        /// <param name="dy">The vertical delta in screen pixels (positive = down).</param>
        public void PanBy(double dx, double dy) =>
            Offset = new Point(Offset.X + dx, Offset.Y + dy);

        /// <summary>
        /// Converts a screen coordinate to document coordinate space.
        /// </summary>
        /// <param name="screen">The screen coordinate to convert.</param>
        /// <returns>The corresponding document coordinate (may be outside document bounds).</returns>
        /// <remarks>
        /// Formula: docCoord = (screenCoord - offset) / scale
        /// </remarks>
        public Point ScreenToDoc(Point screen)
        {
            return new Point(
                (screen.X - Offset.X) / Scale,
                (screen.Y - Offset.Y) / Scale);
        }

        /// <summary>
        /// Converts a document coordinate to screen coordinate space.
        /// </summary>
        /// <param name="doc">The document coordinate to convert.</param>
        /// <returns>The corresponding screen coordinate.</returns>
        /// <remarks>
        /// Formula: screenCoord = offset + (docCoord * scale)
        /// </remarks>
        public Point DocToScreen(Point doc)
        {
            return new Point(
                Offset.X + doc.X * Scale,
                Offset.Y + doc.Y * Scale);
        }

        /// <summary>
        /// Converts a screen coordinate to document coordinate space and clamps to document bounds.
        /// </summary>
        /// <param name="screen">The screen coordinate to convert.</param>
        /// <param name="x">
        /// When this method returns, contains the X coordinate in document space, floored to an integer.
        /// </param>
        /// <param name="y">
        /// When this method returns, contains the Y coordinate in document space, floored to an integer.
        /// </param>
        /// <returns>
        /// <c>true</c> if the converted coordinate is within document bounds; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This is useful for determining if a mouse click is within the document bounds and
        /// getting the corresponding pixel coordinate for editing operations.
        /// </remarks>
        public bool ScreenToDocClamped(Point screen, out int x, out int y)
        {
            var p = ScreenToDoc(screen);
            x = (int)Math.Floor(p.X);
            y = (int)Math.Floor(p.Y);
            return x >= 0 && y >= 0 && x < (int)_doc.Width && y < (int)_doc.Height;
        }
    }
}