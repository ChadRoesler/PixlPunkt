using System;
using System.Collections.Generic;
using System.Numerics;
using Windows.Foundation;

namespace PixlPunkt.Uno.Core.Selection
{
    /// <summary>
    /// Handles 2D transform operations (move, rotate, scale) on selection polygons.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SelectionTransform provides interactive transformation of <see cref="PixelSelection"/> polygons
    /// without modifying the underlying pixel mask until commit. All transforms operate in document
    /// pixel space with snap-to-grid behavior for precise positioning.
    /// </para>
    /// <para><strong>Transform Operations:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Move</strong>: Translates selection by dragging. Snaps to pixel grid for alignment.</item>
    /// <item><strong>Rotate</strong>: Rotates around polygon center. Supports angle snapping (e.g., 15° increments).</item>
    /// <item><strong>Scale</strong>: Resizes via 8 corner/edge handles. Supports uniform and non-uniform scaling.</item>
    /// </list>
    /// <para><strong>Interaction Pattern:</strong></para>
    /// <code>
    /// 1. BeginMove/Rotate/Scale(pointerDoc)   → Captures initial state
    /// 2. UpdateMove/Rotate/Scale(pointerDoc)  → Applies delta from start
    /// 3. Commit transform                     → Selection updates its state
    /// </code>
    /// <para><strong>Transform Order:</strong></para>
    /// <para>
    /// Transforms are applied in SRT order: Scale → Rotate → Translate. This ensures scaling happens
    /// around the origin before rotation, and translation moves the fully-transformed shape.
    /// </para>
    /// <para><strong>Snapping Behavior:</strong></para>
    /// <para>
    /// - Move: Snaps delta to nearest pixel (Math.Round)
    /// <br/>- Rotate: Optional angle snap (default 1°, can use 15° for coarse snapping)
    /// <br/>- Scale: Snaps to 0.01 increments (1% precision), clamped to minimum 0.01
    /// </para>
    /// </remarks>
    /// <seealso cref="PixelSelection"/>
    /// <seealso cref="SelectionEngine"/>
    public class SelectionTransform
    {
        private readonly PixelSelection _sel;

        /// <summary>
        /// Initializes a new instance of the <see cref="SelectionTransform"/> class.
        /// </summary>
        /// <param name="sel">The pixel selection to transform.</param>
        public SelectionTransform(PixelSelection sel)
        {
            _sel = sel;
        }

        // ─────────────────────────────────────────────────────────────
        // MOVE
        // ─────────────────────────────────────────────────────────────

        private Vector2 _moveStartTranslation;
        private Vector2 _moveStartPointerDoc;

        /// <summary>
        /// Begins a move operation by capturing the current translation and pointer position.
        /// </summary>
        /// <param name="pointerDoc">Initial pointer position in document pixel coordinates.</param>
        /// <remarks>
        /// Call this when user presses mouse button to start dragging the selection.
        /// Stores current translation state for delta calculation.
        /// </remarks>
        public void BeginMove(Vector2 pointerDoc)
        {
            _moveStartTranslation = _sel.Translation;
            _moveStartPointerDoc = pointerDoc;
        }

        /// <summary>
        /// Updates the move operation by calculating and applying pixel-snapped translation delta.
        /// </summary>
        /// <param name="pointerDoc">Current pointer position in document pixel coordinates.</param>
        /// <remarks>
        /// Calculates delta from <see cref="BeginMove"/> position, snaps to pixel grid, and updates
        /// selection's <see cref="PixelSelection.Translation"/>. Called repeatedly during drag.
        /// </remarks>
        public void UpdateMove(Vector2 pointerDoc)
        {
            var delta = pointerDoc - _moveStartPointerDoc;
            // snap to pixel grid
            delta.X = (float)Math.Round(delta.X);
            delta.Y = (float)Math.Round(delta.Y);
            _sel.Translation = _moveStartTranslation + delta;
        }

        // ─────────────────────────────────────────────────────────────
        // ROTATION
        // ─────────────────────────────────────────────────────────────

        private float _rotStartAngleDeg;
        private float _rotPointerAnchorDeg;

        /// <summary>
        /// Begins a rotation operation by capturing the initial angle and pointer's angular position.
        /// </summary>
        /// <param name="pointerDoc">Initial pointer position in document pixel coordinates.</param>
        /// <remarks>
        /// <para>
        /// Computes pointer's angle relative to polygon center using <see cref="Math.Atan2"/>.
        /// This anchor angle is used to calculate rotation delta during <see cref="UpdateRotate"/>.
        /// </para>
        /// <para>
        /// Rotation always occurs around the polygon's centroid, computed via <see cref="ComputePolyCenter"/>.
        /// </para>
        /// </remarks>
        public void BeginRotate(Vector2 pointerDoc)
        {
            _rotStartAngleDeg = _sel.RotationDeg;

            var center = ComputePolyCenter(_sel.Poly);
            var dir = pointerDoc - center;

            _rotPointerAnchorDeg = (float)(Math.Atan2(dir.Y, dir.X) * 180.0 / Math.PI);
        }

        /// <summary>
        /// Updates the rotation by calculating angular delta and applying snap-to-grid.
        /// </summary>
        /// <param name="pointerDoc">Current pointer position in document pixel coordinates.</param>
        /// <param name="snapStep">Angle snapping granularity in degrees. Default is 1° (fine).
        /// Use 15° for coarse snapping.</param>
        /// <remarks>
        /// <para><strong>Algorithm:</strong></para>
        /// <list type="number">
        /// <item>Compute current pointer angle from polygon center</item>
        /// <item>Calculate delta from anchor angle stored in <see cref="BeginRotate"/></item>
        /// <item>Apply delta to starting rotation angle</item>
        /// <item>Normalize to -180°..180° range</item>
        /// <item>Snap to <paramref name="snapStep"/> increments</item>
        /// </list>
        /// <para>
        /// Negative angles represent clockwise rotation. Snapping helps users achieve precise
        /// alignments (0°, 45°, 90°, etc.) without manual entry.
        /// </para>
        /// </remarks>
        public void UpdateRotate(Vector2 pointerDoc, float snapStep = 1.0f)
        {
            var center = ComputePolyCenter(_sel.Poly);
            var dir = pointerDoc - center;
            var cur = (float)(Math.Atan2(dir.Y, dir.X) * 180.0 / Math.PI);

            var delta = cur - _rotPointerAnchorDeg;

            // apply delta to starting angle
            float raw = _rotStartAngleDeg + delta;

            // normalize -180..180
            while (raw <= -180) raw += 360;
            while (raw > 180) raw -= 360;

            // gentle snapping
            raw = (float)(Math.Round(raw / snapStep) * snapStep);

            _sel.RotationDeg = raw;
        }

        // ─────────────────────────────────────────────────────────────
        // SCALING
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Defines the 8 scale handle positions plus None.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Corner handles (NW, NE, SE, SW) typically scale both axes uniformly.
        /// Edge handles (N, E, S, W) scale single axis or both depending on <c>uniformLink</c> flag.
        /// </para>
        /// </remarks>
        public enum ScaleHandle
        {
            /// <summary>No handle selected.</summary>
            None,
            /// <summary>Northwest corner handle.</summary>
            NW,
            /// <summary>North edge handle.</summary>
            N,
            /// <summary>Northeast corner handle.</summary>
            NE,
            /// <summary>East edge handle.</summary>
            E,
            /// <summary>Southeast corner handle.</summary>
            SE,
            /// <summary>South edge handle.</summary>
            S,
            /// <summary>Southwest corner handle.</summary>
            SW,
            /// <summary>West edge handle.</summary>
            W
        }

        private Vector2 _scaleStartPointerDoc;
        private Vector2 _scaleStartTranslation;
        private float _scaleStartX, _scaleStartY;

        /// <summary>
        /// Begins a scale operation by capturing initial state and active handle.
        /// </summary>
        /// <param name="h">Which handle is being dragged.</param>
        /// <param name="pointerDoc">Initial pointer position in document pixel coordinates.</param>
        /// <remarks>
        /// Stores starting scale factors, translation, and pointer position for delta calculation.
        /// Sets <see cref="ScaleHandleActive"/> to track which handle is driving the transform.
        /// </remarks>
        public void BeginScale(ScaleHandle h, Vector2 pointerDoc)
        {
            ScaleHandleActive = h;
            _scaleStartPointerDoc = pointerDoc;
            _scaleStartTranslation = _sel.Translation;
            _scaleStartX = _sel.ScaleX;
            _scaleStartY = _sel.ScaleY;
        }

        /// <summary>
        /// Gets the currently active scale handle.
        /// </summary>
        /// <value>
        /// The <see cref="ScaleHandle"/> being dragged, or <see cref="ScaleHandle.None"/> if not scaling.
        /// </value>
        public ScaleHandle ScaleHandleActive { get; private set; } = ScaleHandle.None;

        /// <summary>
        /// Updates the scale by calculating distance ratio from polygon center.
        /// </summary>
        /// <param name="pointerDoc">Current pointer position in document pixel coordinates.</param>
        /// <param name="uniformLink">If true, scales both axes equally. If false, applies axis-specific
        /// scaling based on handle type.</param>
        /// <remarks>
        /// <para><strong>Algorithm:</strong></para>
        /// <list type="number">
        /// <item>Compute distance from polygon center to start pointer</item>
        /// <item>Compute distance from polygon center to current pointer</item>
        /// <item>Calculate scale factor = currentDist / startDist</item>
        /// <item>Snap scale to 0.01 increments (1% precision)</item>
        /// <item>Apply to X, Y, or both axes based on handle and uniform flag</item>
        /// <item>Snap translation to pixel grid</item>
        /// </list>
        /// <para><strong>Handle Behavior:</strong></para>
        /// <para>
        /// - <strong>Corners</strong> (NW, NE, SE, SW): Scale both axes
        /// <br/>- <strong>Vertical edges</strong> (N, S): Scale Y only (unless uniform)
        /// <br/>- <strong>Horizontal edges</strong> (E, W): Scale X only (unless uniform)
        /// </para>
        /// <para>
        /// Minimum scale is clamped to 0.01 (1%) to prevent division by zero and invisibility.
        /// </para>
        /// </remarks>
        public void UpdateScale(Vector2 pointerDoc, bool uniformLink)
        {
            if (ScaleHandleActive == ScaleHandle.None)
                return;

            // scale relative to the polygon’s center
            var center = ComputePolyCenter(_sel.Poly);

            Vector2 dirStart = _scaleStartPointerDoc - center;
            Vector2 dirNow = pointerDoc - center;

            // avoid division by zero
            float lenStart = dirStart.Length();
            float lenNow = dirNow.Length();

            if (lenStart < 0.001f)
                return;

            float scale = lenNow / lenStart;

            // pixel-grid snap
            float snap = (float)Math.Round(scale * 100) / 100f;
            if (snap < 0.01f) snap = 0.01f;

            if (uniformLink)
            {
                _sel.ScaleX = snap;
                _sel.ScaleY = snap;
            }
            else
            {
                // axis-specific scaling based on handle
                switch (ScaleHandleActive)
                {
                    case ScaleHandle.N:
                    case ScaleHandle.S:
                        _sel.ScaleY = snap;
                        break;

                    case ScaleHandle.E:
                    case ScaleHandle.W:
                        _sel.ScaleX = snap;
                        break;

                    default:
                        // corners scale both axes
                        _sel.ScaleX = snap;
                        _sel.ScaleY = snap;
                        break;
                }
            }

            // translation stays pixel-snapped
            _sel.Translation = new Vector2(
                (float)Math.Round(_scaleStartTranslation.X),
                (float)Math.Round(_scaleStartTranslation.Y)
            );
        }

        // ─────────────────────────────────────────────────────────────
        // POLYGON MATH
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Computes the centroid (average position) of a polygon.
        /// </summary>
        /// <param name="poly">List of polygon vertices.</param>
        /// <returns>Centroid as a <see cref="Vector2"/>. Returns <see cref="Vector2.Zero"/> if polygon is empty.</returns>
        /// <remarks>
        /// Simple arithmetic mean of all vertices. Used as the pivot point for rotation and scaling.
        /// </remarks>
        public static Vector2 ComputePolyCenter(List<Vector2> poly)
        {
            if (poly.Count == 0) return Vector2.Zero;

            float x = 0, y = 0;
            foreach (var p in poly)
            {
                x += p.X;
                y += p.Y;
            }
            return new Vector2(x / poly.Count, y / poly.Count);
        }

        /// <summary>
        /// Transforms the selection's polygon using current transform state.
        /// </summary>
        /// <returns>A new list of transformed vertices.</returns>
        /// <remarks>
        /// Applies the selection's transform matrix (from <see cref="PixelSelection.GetTransformMatrix"/>)
        /// to each vertex. Used for preview rendering and hit-testing against transformed bounds.
        /// </remarks>
        public List<Vector2> GetTransformedPoly()
        {
            var T = _sel.GetTransformMatrix();
            var outList = new List<Vector2>(_sel.Poly.Count);
            foreach (var p in _sel.Poly)
                outList.Add(Vector2.Transform(p, T));
            return outList;
        }

        /// <summary>
        /// Tests whether a point falls within a square handle region.
        /// </summary>
        /// <param name="v">Point to test in view space.</param>
        /// <param name="hx">Handle center X coordinate.</param>
        /// <param name="hy">Handle center Y coordinate.</param>
        /// <param name="half">Half-size of the handle square (e.g., 5 pixels for 10×10 handle).</param>
        /// <returns><c>true</c> if point is inside handle bounds; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// Axis-aligned bounding box test. Used by UI layer (CanvasViewHost) to determine which
        /// handle the pointer is hovering over or clicked on.
        /// </remarks>
        public static bool HitTestPoint(Point v, float hx, float hy, float half)
        {
            return v.X >= hx - half && v.X <= hx + half &&
                   v.Y >= hy - half && v.Y <= hy + half;
        }
    }
}
