using System;
using System.Collections.Generic;
using System.Numerics;

namespace PixlPunkt.Core.Structs
{
    /// <summary>
    /// Immutable snapshot of brush overlay state for rendering the brush cursor preview.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This struct captures the complete state needed to render a brush preview overlay,
    /// including cursor position, brush settings, shape tool preview, and shift-line preview.
    /// </para>
    /// <para>
    /// The snapshot pattern ensures thread-safe rendering - the render thread receives
    /// an immutable copy of the brush state at a specific moment in time.
    /// </para>
    /// </remarks>
    public readonly struct BrushOverlaySnapshot
    {
        // ════════════════════════════════════════════════════════════════════
        // CURSOR POSITION
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Integer X coordinate of cursor hover position (document space).</summary>
        public readonly int HoverX;

        /// <summary>Integer Y coordinate of cursor hover position (document space).</summary>
        public readonly int HoverY;

        /// <summary>Legacy float-based center position. Use HoverX/HoverY for precise coordinates.</summary>
        [Obsolete("Use HoverX/HoverY instead for precise integer coordinates")]
        public readonly Vector2 Center;

        /// <summary>Brush radius for circular preview.</summary>
        public readonly float Radius;

        /// <summary>Pre-computed pixel offsets relative to brush center.</summary>
        public readonly IReadOnlyList<(int dx, int dy)> Mask;

        /// <summary>Whether to render filled preview pixels.</summary>
        public readonly bool FillGhost;

        /// <summary>Whether the overlay should be visible at all.</summary>
        public readonly bool Visible;

        // ════════════════════════════════════════════════════════════════════
        // BRUSH SETTINGS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Current brush size in pixels.</summary>
        public readonly int BrushSize;

        /// <summary>Current brush shape (square, round, diamond).</summary>
        public readonly BrushShape BrushShape;

        /// <summary>Brush density/flow (0-255).</summary>
        public readonly byte BrushDensity;

        /// <summary>Brush opacity (0-255).</summary>
        public readonly byte BrushOpacity;

        // ════════════════════════════════════════════════════════════════════
        // CUSTOM BRUSH
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The full name of the selected custom brush (author.brushname), or null if using built-in shape.
        /// </summary>
        public readonly string? CustomBrushFullName;

        /// <summary>
        /// Whether a custom brush is currently selected.
        /// </summary>
        public bool IsCustomBrush => !string.IsNullOrEmpty(CustomBrushFullName);

        // ════════════════════════════════════════════════════════════════════
        // SHAPE PREVIEW STATE
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Whether a shape drag operation is in progress.</summary>
        public readonly bool IsShapeDrag;

        /// <summary>True for ellipse shapes, false for rectangles.</summary>
        public readonly bool IsEllipse;

        /// <summary>Whether the shape should be filled.</summary>
        public readonly bool IsFilled;

        /// <summary>Shape bounding box start X coordinate.</summary>
        public readonly int ShapeX0;

        /// <summary>Shape bounding box start Y coordinate.</summary>
        public readonly int ShapeY0;

        /// <summary>Shape bounding box end X coordinate.</summary>
        public readonly int ShapeX1;

        /// <summary>Shape bounding box end Y coordinate.</summary>
        public readonly int ShapeY1;

        /// <summary>Shape stroke width in pixels.</summary>
        public readonly int ShapeStrokeWidth;

        /// <summary>Brush shape used for shape stroke.</summary>
        public readonly BrushShape ShapeBrushShape;

        /// <summary>Brush density for shape stroke.</summary>
        public readonly byte ShapeBrushDensity;

        /// <summary>Brush opacity for shape stroke.</summary>
        public readonly byte ShapeBrushOpacity;

        // ════════════════════════════════════════════════════════════════════
        // SHAPE START POINT HOVER
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Whether to show shape start point indicator.</summary>
        public readonly bool ShowShapeStartPoint;

        /// <summary>Shape start point X coordinate.</summary>
        public readonly int ShapeStartX;

        /// <summary>Shape start point Y coordinate.</summary>
        public readonly int ShapeStartY;

        // ════════════════════════════════════════════════════════════════════
        // SHIFT-LINE PREVIEW STATE
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Whether a shift-line drag operation is in progress.</summary>
        public readonly bool IsShiftLineDrag;

        /// <summary>Shift-line start X coordinate.</summary>
        public readonly int ShiftLineX0;

        /// <summary>Shift-line start Y coordinate.</summary>
        public readonly int ShiftLineY0;

        /// <summary>Shift-line end X coordinate.</summary>
        public readonly int ShiftLineX1;

        /// <summary>Shift-line end Y coordinate.</summary>
        public readonly int ShiftLineY1;

        /// <summary>
        /// Creates a new brush overlay snapshot with the specified state.
        /// </summary>
        public BrushOverlaySnapshot(
            Vector2 center,
            float radius,
            IReadOnlyList<(int dx, int dy)> mask,
            bool fillGhost,
            bool visible,
            int brushSize = 1,
            BrushShape brushShape = BrushShape.Square,
            byte brushDensity = 255,
            byte brushOpacity = 255,
            string? customBrushFullName = null,
            bool isShapeDrag = false,
            bool isEllipse = false,
            bool isFilled = false,
            int shapeX0 = 0,
            int shapeY0 = 0,
            int shapeX1 = 0,
            int shapeY1 = 0,
            int shapeStrokeWidth = 1,
            BrushShape shapeBrushShape = BrushShape.Square,
            byte shapeBrushDensity = 255,
            byte shapeBrushOpacity = 255,
            bool showShapeStartPoint = false,
            int shapeStartX = 0,
            int shapeStartY = 0,
            bool isShiftLineDrag = false,
            int shiftLineX0 = 0,
            int shiftLineY0 = 0,
            int shiftLineX1 = 0,
            int shiftLineY1 = 0)
        {
            // CRITICAL: Use the integer shape start coordinates as the hover position
            // (the caller passes _hoverX/_hoverY here for precise integer alignment)
            HoverX = shapeStartX;
            HoverY = shapeStartY;
            Center = center;
            Radius = radius;
            Mask = mask;
            FillGhost = fillGhost;
            Visible = visible;
            BrushSize = brushSize;
            BrushShape = brushShape;
            BrushDensity = brushDensity;
            BrushOpacity = brushOpacity;
            CustomBrushFullName = customBrushFullName;
            IsShapeDrag = isShapeDrag;
            IsEllipse = isEllipse;
            IsFilled = isFilled;
            ShapeX0 = shapeX0;
            ShapeY0 = shapeY0;
            ShapeX1 = shapeX1;
            ShapeY1 = shapeY1;
            ShapeStrokeWidth = shapeStrokeWidth;
            ShapeBrushShape = shapeBrushShape;
            ShapeBrushDensity = shapeBrushDensity;
            ShapeBrushOpacity = shapeBrushOpacity;
            ShowShapeStartPoint = showShapeStartPoint;
            ShapeStartX = shapeStartX;
            ShapeStartY = shapeStartY;
            IsShiftLineDrag = isShiftLineDrag;
            ShiftLineX0 = shiftLineX0;
            ShiftLineY0 = shiftLineY0;
            ShiftLineX1 = shiftLineX1;
            ShiftLineY1 = shiftLineY1;
        }

        /// <summary>
        /// Gets an empty/invisible brush overlay snapshot.
        /// </summary>
        public static BrushOverlaySnapshot Empty { get; } =
            new BrushOverlaySnapshot(Vector2.Zero, 0, Array.Empty<(int, int)>(), false, false);
    }
}