using System;
using PixlPunkt.Core.Enums;

namespace PixlPunkt.Core.Symmetry
{
    /// <summary>
    /// Manages symmetry drawing state for the canvas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SymmetrySettings provides the central configuration for live symmetry drawing,
    /// supporting horizontal/vertical mirroring and radial (mandala) modes.
    /// </para>
    /// </remarks>
    public sealed class SymmetrySettings
    {
        // ====================================================================
        // STATE
        // ====================================================================

        private bool _enabled;
        private SymmetryMode _mode = SymmetryMode.Horizontal;
        private double _axisX = 0.5; // Normalized 0-1 relative to canvas
        private double _axisY = 0.5;
        private int _radialSegments = 6; // For Radial/Kaleidoscope modes (2-16)
        private bool _showAxisLines = true;

        // ====================================================================
        // PROPERTIES
        // ====================================================================

        /// <summary>
        /// Gets or sets whether symmetry drawing is enabled.
        /// </summary>
        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled != value)
                {
                    _enabled = value;
                    RaiseChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the symmetry mode.
        /// </summary>
        public SymmetryMode Mode
        {
            get => _mode;
            set
            {
                if (_mode != value)
                {
                    _mode = value;
                    RaiseChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the X position of the symmetry axis (0-1 normalized).
        /// </summary>
        /// <remarks>
        /// For horizontal symmetry, this is the vertical line position.
        /// For radial symmetry, this is the center X coordinate.
        /// </remarks>
        public double AxisX
        {
            get => _axisX;
            set
            {
                var clamped = Math.Clamp(value, 0.0, 1.0);
                if (Math.Abs(_axisX - clamped) > 0.0001)
                {
                    _axisX = clamped;
                    RaiseChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the Y position of the symmetry axis (0-1 normalized).
        /// </summary>
        /// <remarks>
        /// For vertical symmetry, this is the horizontal line position.
        /// For radial symmetry, this is the center Y coordinate.
        /// </remarks>
        public double AxisY
        {
            get => _axisY;
            set
            {
                var clamped = Math.Clamp(value, 0.0, 1.0);
                if (Math.Abs(_axisY - clamped) > 0.0001)
                {
                    _axisY = clamped;
                    RaiseChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the number of radial segments for Radial/Kaleidoscope modes.
        /// </summary>
        /// <remarks>
        /// Valid range is 2-16 segments.
        /// </remarks>
        public int RadialSegments
        {
            get => _radialSegments;
            set
            {
                var clamped = Math.Clamp(value, 2, 16);
                if (_radialSegments != clamped)
                {
                    _radialSegments = clamped;
                    RaiseChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets whether to show the symmetry axis lines on the canvas.
        /// </summary>
        public bool ShowAxisLines
        {
            get => _showAxisLines;
            set
            {
                if (_showAxisLines != value)
                {
                    _showAxisLines = value;
                    RaiseChanged();
                }
            }
        }

        /// <summary>
        /// Gets the effective number of radial segments based on the current mode.
        /// </summary>
        public int EffectiveRadialSegments => _mode.IsRadial() ? _radialSegments : 1;

        /// <summary>
        /// Gets whether the current mode uses radial symmetry.
        /// </summary>
        public bool IsRadialMode => _mode.IsRadial();

        /// <summary>
        /// Gets whether the current mode includes mirroring within radial segments.
        /// </summary>
        public bool HasRadialMirror => _mode == SymmetryMode.Kaleidoscope;

        // ====================================================================
        // METHODS
        // ====================================================================

        /// <summary>
        /// Sets the symmetry mode without triggering individual change events.
        /// </summary>
        /// <param name="mode">The symmetry mode.</param>
        public void SetMode(SymmetryMode mode)
        {
            _mode = mode;
            RaiseChanged();
        }

        /// <summary>
        /// Sets the axis position without triggering individual change events.
        /// </summary>
        /// <param name="x">X position (0-1).</param>
        /// <param name="y">Y position (0-1).</param>
        public void SetAxisPosition(double x, double y)
        {
            _axisX = Math.Clamp(x, 0.0, 1.0);
            _axisY = Math.Clamp(y, 0.0, 1.0);
            RaiseChanged();
        }

        /// <summary>
        /// Centers the symmetry axis on the canvas.
        /// </summary>
        public void CenterAxis()
        {
            _axisX = 0.5;
            _axisY = 0.5;
            RaiseChanged();
        }

        /// <summary>
        /// Toggles symmetry on/off.
        /// </summary>
        public void Toggle()
        {
            Enabled = !Enabled;
        }

        /// <summary>
        /// Cycles to the next symmetry mode.
        /// </summary>
        public void CycleMode()
        {
            if (!_enabled)
            {
                _enabled = true;
                _mode = SymmetryMode.Horizontal;
            }
            else
            {
                _mode = _mode switch
                {
                    SymmetryMode.None => SymmetryMode.Horizontal,
                    SymmetryMode.Horizontal => SymmetryMode.Vertical,
                    SymmetryMode.Vertical => SymmetryMode.Both,
                    SymmetryMode.Both => SymmetryMode.Radial,
                    SymmetryMode.Radial => SymmetryMode.Kaleidoscope,
                    SymmetryMode.Kaleidoscope => SymmetryMode.None,
                    _ => SymmetryMode.Horizontal
                };

                if (_mode == SymmetryMode.None)
                {
                    _enabled = false;
                }
            }
            RaiseChanged();
        }

        // ====================================================================
        // EVENTS
        // ====================================================================

        /// <summary>
        /// Occurs when any symmetry setting changes.
        /// </summary>
        public event Action? Changed;

        private void RaiseChanged() => Changed?.Invoke();
    }
}
