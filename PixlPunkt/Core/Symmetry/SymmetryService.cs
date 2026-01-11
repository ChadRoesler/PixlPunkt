using System;
using System.Collections.Generic;
using PixlPunkt.Core.Enums;

namespace PixlPunkt.Core.Symmetry
{
    /// <summary>
    /// Service for computing symmetry-mirrored points during stroke painting.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SymmetryService transforms input coordinates into multiple output coordinates
    /// based on the active symmetry mode. This enables live mirroring during painting
    /// operations.
    /// </para>
    /// <para>
    /// Supported transformations:
    /// - Horizontal: Mirror across a vertical axis (left/right)
    /// - Vertical: Mirror across a horizontal axis (top/bottom)
    /// - Both: Mirror across both axes (four quadrants)
    /// - Radial: Rotate points around a center with n-fold symmetry
    /// - Kaleidoscope: Radial with additional mirroring within each segment
    /// </para>
    /// </remarks>
    public sealed class SymmetryService
    {
        private readonly SymmetrySettings _settings;

        /// <summary>
        /// Creates a new SymmetryService instance.
        /// </summary>
        /// <param name="settings">The symmetry settings to use.</param>
        public SymmetryService(SymmetrySettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Gets whether symmetry is currently active.
        /// </summary>
        public bool IsActive => _settings.Enabled && _settings.Mode != SymmetryMode.None;

        /// <summary>
        /// Gets all symmetry points for a given input coordinate.
        /// </summary>
        /// <param name="x">Input X coordinate (document space).</param>
        /// <param name="y">Input Y coordinate (document space).</param>
        /// <param name="canvasWidth">Canvas width in pixels.</param>
        /// <param name="canvasHeight">Canvas height in pixels.</param>
        /// <returns>
        /// Enumerable of all points to paint, including the original point.
        /// If symmetry is disabled, returns only the original point.
        /// </returns>
        public IEnumerable<(int x, int y)> GetSymmetryPoints(int x, int y, int canvasWidth, int canvasHeight)
        {
            // Always include the original point
            yield return (x, y);

            if (!IsActive)
                yield break;

            // Calculate axis positions in document coordinates
            double axisX = _settings.AxisX * canvasWidth;
            double axisY = _settings.AxisY * canvasHeight;

            switch (_settings.Mode)
            {
                case SymmetryMode.Horizontal:
                    yield return MirrorHorizontal(x, y, axisX);
                    break;

                case SymmetryMode.Vertical:
                    yield return MirrorVertical(x, y, axisY);
                    break;

                case SymmetryMode.Both:
                    var hMirror = MirrorHorizontal(x, y, axisX);
                    var vMirror = MirrorVertical(x, y, axisY);
                    var hvMirror = MirrorHorizontal(vMirror.x, vMirror.y, axisX);
                    yield return hMirror;
                    yield return vMirror;
                    yield return hvMirror;
                    break;

                case SymmetryMode.Radial:
                    foreach (var pt in GetRadialPoints(x, y, axisX, axisY, _settings.RadialSegments, false))
                        yield return pt;
                    break;

                case SymmetryMode.Kaleidoscope:
                    foreach (var pt in GetRadialPoints(x, y, axisX, axisY, _settings.RadialSegments, true))
                        yield return pt;
                    break;
            }
        }

        /// <summary>
        /// Mirrors a point horizontally across a vertical axis.
        /// </summary>
        /// <param name="x">Input X.</param>
        /// <param name="y">Input Y.</param>
        /// <param name="axisX">X position of the vertical axis.</param>
        /// <returns>Mirrored point.</returns>
        private static (int x, int y) MirrorHorizontal(int x, int y, double axisX)
        {
            // Mirror x across the vertical axis line
            int mirrorX = (int)Math.Round(2.0 * axisX - x);
            return (mirrorX, y);
        }

        /// <summary>
        /// Mirrors a point vertically across a horizontal axis.
        /// </summary>
        /// <param name="x">Input X.</param>
        /// <param name="y">Input Y.</param>
        /// <param name="axisY">Y position of the horizontal axis.</param>
        /// <returns>Mirrored point.</returns>
        private static (int x, int y) MirrorVertical(int x, int y, double axisY)
        {
            // Mirror y across the horizontal axis line
            int mirrorY = (int)Math.Round(2.0 * axisY - y);
            return (x, mirrorY);
        }

        /// <summary>
        /// Gets radially symmetric points around a center.
        /// </summary>
        /// <param name="x">Input X.</param>
        /// <param name="y">Input Y.</param>
        /// <param name="centerX">Center X.</param>
        /// <param name="centerY">Center Y.</param>
        /// <param name="segments">Number of radial segments.</param>
        /// <param name="withMirror">Whether to add mirroring within each segment (kaleidoscope).</param>
        /// <returns>All radial points (excluding the original).</returns>
        private static IEnumerable<(int x, int y)> GetRadialPoints(
            int x, int y,
            double centerX, double centerY,
            int segments,
            bool withMirror)
        {
            if (segments < 2)
                yield break;

            double dx = x - centerX;
            double dy = y - centerY;

            // Calculate current angle and radius
            double radius = Math.Sqrt(dx * dx + dy * dy);
            double angle = Math.Atan2(dy, dx);

            double segmentAngle = 2.0 * Math.PI / segments;

            // Generate rotated points for each segment
            for (int i = 1; i < segments; i++)
            {
                double newAngle = angle + i * segmentAngle;
                int rotX = (int)Math.Round(centerX + radius * Math.Cos(newAngle));
                int rotY = (int)Math.Round(centerY + radius * Math.Sin(newAngle));
                yield return (rotX, rotY);
            }

            // For kaleidoscope mode, also add mirrored versions
            if (withMirror)
            {
                // Mirror the original angle across each segment center
                double mirrorAngle = -angle;

                for (int i = 0; i < segments; i++)
                {
                    double newAngle = mirrorAngle + i * segmentAngle;
                    int mirX = (int)Math.Round(centerX + radius * Math.Cos(newAngle));
                    int mirY = (int)Math.Round(centerY + radius * Math.Sin(newAngle));
                    yield return (mirX, mirY);
                }
            }
        }

        /// <summary>
        /// Gets the axis line segments for rendering the symmetry overlay.
        /// </summary>
        /// <param name="canvasWidth">Canvas width in pixels.</param>
        /// <param name="canvasHeight">Canvas height in pixels.</param>
        /// <returns>
        /// Enumerable of line segments as (x1, y1, x2, y2) tuples.
        /// </returns>
        public IEnumerable<(double x1, double y1, double x2, double y2)> GetAxisLines(int canvasWidth, int canvasHeight)
        {
            if (!_settings.ShowAxisLines || !IsActive)
                yield break;

            double axisX = _settings.AxisX * canvasWidth;
            double axisY = _settings.AxisY * canvasHeight;

            switch (_settings.Mode)
            {
                case SymmetryMode.Horizontal:
                    // Vertical line at axisX
                    yield return (axisX, 0, axisX, canvasHeight);
                    break;

                case SymmetryMode.Vertical:
                    // Horizontal line at axisY
                    yield return (0, axisY, canvasWidth, axisY);
                    break;

                case SymmetryMode.Both:
                    // Both lines
                    yield return (axisX, 0, axisX, canvasHeight);
                    yield return (0, axisY, canvasWidth, axisY);
                    break;

                case SymmetryMode.Radial:
                case SymmetryMode.Kaleidoscope:
                    // Radial lines from center
                    int segments = _settings.RadialSegments;
                    double maxRadius = Math.Sqrt(canvasWidth * canvasWidth + canvasHeight * canvasHeight);

                    for (int i = 0; i < segments; i++)
                    {
                        double angle = i * 2.0 * Math.PI / segments;
                        double x2 = axisX + maxRadius * Math.Cos(angle);
                        double y2 = axisY + maxRadius * Math.Sin(angle);
                        yield return (axisX, axisY, x2, y2);
                    }
                    break;
            }
        }

        /// <summary>
        /// Gets the center point of the symmetry axis.
        /// </summary>
        /// <param name="canvasWidth">Canvas width.</param>
        /// <param name="canvasHeight">Canvas height.</param>
        /// <returns>Center point (x, y) in document coordinates.</returns>
        public (double x, double y) GetAxisCenter(int canvasWidth, int canvasHeight)
        {
            return (_settings.AxisX * canvasWidth, _settings.AxisY * canvasHeight);
        }
    }
}
