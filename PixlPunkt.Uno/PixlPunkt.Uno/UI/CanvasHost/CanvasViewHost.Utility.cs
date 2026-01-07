using System;
using System.Collections.Generic;
using PixlPunkt.Uno.Core.Tools;
using PixlPunkt.Uno.Core.Tools.Utility;
using Windows.Foundation;

namespace PixlPunkt.Uno.UI.CanvasHost
{
    /// <summary>
    /// Utility tool subsystem for CanvasViewHost:
    /// - IUtilityContext adapter implementation
    /// - Utility handler lifecycle management
    /// </summary>
    public sealed partial class CanvasViewHost
    {
        // ═══════════════════════════════════════════════════════════════════════
        // UTILITY HANDLER STATE
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Dictionary of all utility handlers keyed by tool ID.
        /// Includes both built-in and plugin utility tools.
        /// </summary>
        private readonly Dictionary<string, IUtilityHandler> _utilityHandlers = new();

        private CanvasUtilityContext? _utilityContext;

        // ═══════════════════════════════════════════════════════════════════════
        // UTILITY CONTEXT ADAPTER
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Adapter class that bridges IUtilityContext to CanvasViewHost methods.
        /// </summary>
        private sealed class CanvasUtilityContext : IUtilityContext
        {
            private readonly CanvasViewHost _host;

            public CanvasUtilityContext(CanvasViewHost host) => _host = host;

            public (int Width, int Height) DocumentSize
                => (_host.Document.PixelWidth, _host.Document.PixelHeight);

            public double ZoomScale => _host._zoom.Scale;

            public void PanBy(double deltaX, double deltaY)
            {
                _host._zoom.PanBy(deltaX, deltaY);
                _host.UpdateViewport();
                // Always invalidate rulers during pan, even if viewport rect didn't change
                // (the document position changes even when fully visible)
                _host.InvalidateRulers();
                _host.InvalidateMainCanvas();
            }

            public void ZoomAt(Point screenPos, double factor)
            {
                _host._zoom.ZoomAt(screenPos, factor, MinZoomScale, MaxZoomScale);
                _host.UpdateViewport();
                _host.ZoomLevel.Text = _host.ZoomLevelText;
                // Rulers are already invalidated by UpdateViewport when zoom changes
            }

            public uint SampleColorAt(int docX, int docY)
                => _host.ReadCompositeBGRA(docX, docY);

            public void SetForegroundColor(uint bgra)
            {
                _host.SetForeground(bgra);
                ForegroundSampled?.Invoke(bgra);
                _host.ForegroundSampledLive?.Invoke(bgra);
            }

            public void SetBackgroundColor(uint bgra)
            {
                BackgroundSampled?.Invoke(bgra);
                _host.BackgroundSampledLive?.Invoke(bgra);
            }

            public void RequestRedraw() => _host.InvalidateMainCanvas();

            public void CapturePointer()
            {
                // Pointer capture is handled at the event level
                // This is a no-op as capture is managed by the input handlers
            }

            public void ReleasePointer()
            {
                _host._mainCanvas.ReleasePointerCaptures();
            }

            public event Action<uint>? ForegroundSampled;
            public event Action<uint>? BackgroundSampled;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // UTILITY HANDLER INITIALIZATION
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Initializes utility handlers from the tool registry.
        /// Called during BindToolState.
        /// </summary>
        private void InitUtilityHandlers()
        {
            if (_toolState == null) return;

            _utilityContext = new CanvasUtilityContext(this);
            _utilityHandlers.Clear();

            // Create handlers for ALL registered utility tools (built-in and plugins)
            foreach (var toolId in _toolState.Registry.RegisteredIds)
            {
                var reg = _toolState.GetUtilityRegistration(toolId);
                if (reg?.HasHandler == true)
                {
                    var handler = reg.CreateHandler(_utilityContext);
                    if (handler != null)
                    {
                        _utilityHandlers[toolId] = handler;
                    }
                }
            }

            // Configure dropper with current opacity
            if (_utilityHandlers.TryGetValue(ToolIds.Dropper, out var dropperHandler) &&
                dropperHandler is DropperHandler dropper)
            {
                dropper.CurrentOpacity = _brushOpacity;
            }
        }

        /// <summary>
        /// Updates dropper handler's opacity when brush opacity changes.
        /// </summary>
        private void SyncDropperOpacity()
        {
            if (_utilityHandlers.TryGetValue(ToolIds.Dropper, out var dropperHandler) &&
                dropperHandler is DropperHandler dropper)
            {
                dropper.CurrentOpacity = _brushOpacity;
            }
        }

        /// <summary>
        /// Gets the appropriate handler for the current tool.
        /// </summary>
        private IUtilityHandler? GetActiveUtilityHandler()
        {
            if (_toolState == null) return null;

            var activeId = _toolState.ActiveToolId;
            return _utilityHandlers.TryGetValue(activeId, out var handler) ? handler : null;
        }

        /// <summary>
        /// Converts screen position to document position.
        /// </summary>
        private Point ScreenToDocPoint(Point screenPos)
        {
            var docPt = _zoom.ScreenToDoc(screenPos);
            return new Point(Math.Floor(docPt.X), Math.Floor(docPt.Y));
        }
    }
}
