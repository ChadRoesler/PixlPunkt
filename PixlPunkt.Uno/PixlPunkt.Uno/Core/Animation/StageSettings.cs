using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PixlPunkt.Uno.Core.Enums;

namespace PixlPunkt.Uno.Core.Animation
{
    /// <summary>
    /// Defines the scaling algorithm used when the stage is zoomed.
    /// </summary>
    public enum StageScalingAlgorithm
    {
        /// <summary>Nearest neighbor - preserves pixel art sharpness.</summary>
        NearestNeighbor,

        /// <summary>Bilinear interpolation - smooth scaling.</summary>
        Bilinear,

        /// <summary>Bicubic interpolation - high quality smooth scaling.</summary>
        Bicubic
    }

    /// <summary>
    /// Defines how the stage is constrained within the canvas bounds.
    /// </summary>
    public enum StageBoundsMode
    {
        /// <summary>Stage can move freely, may show content outside canvas (transparent).</summary>
        Free,

        /// <summary>Stage corners are constrained to stay within canvas bounds.</summary>
        Constrained,

        /// <summary>Stage is locked to canvas center, only zoom/rotation allowed.</summary>
        CenterLocked
    }

    /// <summary>
    /// Defines the interpolation method for rotation keyframes.
    /// </summary>
    public enum StageRotationInterpolation
    {
        /// <summary>Linear interpolation - constant rotation speed.</summary>
        Linear,

        /// <summary>Ease in/out - smooth acceleration and deceleration.</summary>
        EaseInOut,

        /// <summary>No interpolation - snap to keyframe values.</summary>
        Step
    }

    /// <summary>
    /// Settings for the animation stage (virtual camera/viewport).
    /// The stage defines the visible area of the canvas for final render output.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Stage acts as a virtual camera that can pan, zoom, and rotate within the canvas.
    /// This allows for dynamic camera movements in animations, where layers can
    /// "enter from off-stage" or the camera can follow action.
    /// </para>
    /// <para>
    /// Stage dimensions define the final output resolution. The stage area within the canvas
    /// is rendered to this output size using the specified scaling algorithm.
    /// </para>
    /// </remarks>
    public sealed class StageSettings : INotifyPropertyChanged
    {
        // ====================================================================
        // DIMENSIONS
        // ====================================================================

        private int _outputWidth = 64;
        private int _outputHeight = 64;

        /// <summary>
        /// Gets or sets the output width in pixels for the final render.
        /// </summary>
        public int OutputWidth
        {
            get => _outputWidth;
            set
            {
                if (_outputWidth != value && value > 0)
                {
                    _outputWidth = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the output height in pixels for the final render.
        /// </summary>
        public int OutputHeight
        {
            get => _outputHeight;
            set
            {
                if (_outputHeight != value && value > 0)
                {
                    _outputHeight = value;
                    OnPropertyChanged();
                }
            }
        }

        // ====================================================================
        // STAGE AREA (within canvas)
        // ====================================================================

        private int _stageX = 0;
        private int _stageY = 0;
        private int _stageWidth = 64;
        private int _stageHeight = 64;

        /// <summary>
        /// Gets or sets the X position of the stage area within the canvas.
        /// </summary>
        public int StageX
        {
            get => _stageX;
            set
            {
                if (_stageX != value)
                {
                    _stageX = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the Y position of the stage area within the canvas.
        /// </summary>
        public int StageY
        {
            get => _stageY;
            set
            {
                if (_stageY != value)
                {
                    _stageY = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the width of the stage area within the canvas.
        /// </summary>
        public int StageWidth
        {
            get => _stageWidth;
            set
            {
                if (_stageWidth != value && value > 0)
                {
                    _stageWidth = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the height of the stage area within the canvas.
        /// </summary>
        public int StageHeight
        {
            get => _stageHeight;
            set
            {
                if (_stageHeight != value && value > 0)
                {
                    _stageHeight = value;
                    OnPropertyChanged();
                }
            }
        }

        // ====================================================================
        // SCALING & INTERPOLATION
        // ====================================================================

        private StageScalingAlgorithm _scalingAlgorithm = StageScalingAlgorithm.NearestNeighbor;
        private StageRotationInterpolation _rotationInterpolation = StageRotationInterpolation.Linear;
        private StageBoundsMode _boundsMode = StageBoundsMode.Constrained;

        /// <summary>
        /// Gets or sets the scaling algorithm used when rendering the stage.
        /// </summary>
        public StageScalingAlgorithm ScalingAlgorithm
        {
            get => _scalingAlgorithm;
            set
            {
                if (_scalingAlgorithm != value)
                {
                    _scalingAlgorithm = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets the interpolation method for rotation between keyframes.
        /// </summary>
        public StageRotationInterpolation RotationInterpolation
        {
            get => _rotationInterpolation;
            set
            {
                if (_rotationInterpolation != value)
                {
                    _rotationInterpolation = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Gets or sets how the stage is constrained within canvas bounds.
        /// </summary>
        public StageBoundsMode BoundsMode
        {
            get => _boundsMode;
            set
            {
                if (_boundsMode != value)
                {
                    _boundsMode = value;
                    OnPropertyChanged();
                }
            }
        }

        // ====================================================================
        // ENABLED STATE
        // ====================================================================

        private bool _enabled = false;

        /// <summary>
        /// Gets or sets whether the stage is enabled.
        /// When disabled, the full canvas is rendered without stage transforms.
        /// </summary>
        public bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled != value)
                {
                    _enabled = value;
                    OnPropertyChanged();
                }
            }
        }

        // ====================================================================
        // EVENTS
        // ====================================================================

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // ====================================================================
        // METHODS
        // ====================================================================

        /// <summary>
        /// Sets the stage to match the full canvas dimensions.
        /// </summary>
        /// <param name="canvasWidth">Canvas width in pixels.</param>
        /// <param name="canvasHeight">Canvas height in pixels.</param>
        public void MatchCanvas(int canvasWidth, int canvasHeight)
        {
            StageX = 0;
            StageY = 0;
            StageWidth = canvasWidth;
            StageHeight = canvasHeight;
            OutputWidth = canvasWidth;
            OutputHeight = canvasHeight;
        }

        /// <summary>
        /// Creates a clone of these settings.
        /// </summary>
        public StageSettings Clone()
        {
            return new StageSettings
            {
                OutputWidth = OutputWidth,
                OutputHeight = OutputHeight,
                StageX = StageX,
                StageY = StageY,
                StageWidth = StageWidth,
                StageHeight = StageHeight,
                ScalingAlgorithm = ScalingAlgorithm,
                RotationInterpolation = RotationInterpolation,
                BoundsMode = BoundsMode,
                Enabled = Enabled
            };
        }
    }
}
