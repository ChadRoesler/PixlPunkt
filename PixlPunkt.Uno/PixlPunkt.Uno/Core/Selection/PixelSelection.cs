using System;
using System.Collections.Generic;
using System.Numerics;

namespace PixlPunkt.Uno.Core.Selection
{
    /// <summary>
    /// Represents a geometric selection with pixel mask, polygon outline, and transform state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// PixelSelection combines a boolean pixel mask (<see cref="SelectionMask"/>) with a vector polygon
    /// outline and 2D transform state (translation, rotation, scale). This hybrid approach enables both
    /// pixel-accurate operations (magic wand, lasso) and smooth geometric transformations.
    /// </para>
    /// <para><strong>Dual Representation:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Mask</strong>: Authoritative for hit-testing, compositing, and tool operations.
    /// Defines which pixels are "inside" the selection.</item>
    /// <item><strong>Poly</strong>: Geometric outline for rendering handles, marching ants, and transform UI.
    /// Derived from mask via <see cref="SelectionOutlineBuilder"/> or from rect operations.</item>
    /// </list>
    /// <para><strong>State Machine:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Inactive</strong>: <see cref="Active"/> = false, no selection exists.</item>
    /// <item><strong>Active</strong>: Selection defined but not floating. Mask and poly populated.</item>
    /// <item><strong>Armed</strong>: Selection ready for transform operations (handles visible).</item>
    /// <item><strong>Floating</strong>: Selection lifted to buffer, can be moved/transformed independently.</item>
    /// </list>
    /// <para><strong>Transform Workflow:</strong></para>
    /// <code>
    /// 1. Create selection (mask + poly)
    /// 2. User drags handles → Transform state updated
    /// 3. GetTransformMatrix() → Preview transformed poly
    /// 4. Commit → Rasterize transformed poly back to mask
    /// </code>
    /// <para><strong>Floating Buffer:</strong></para>
    /// <para>
    /// When <see cref="Floating"/> is true, <see cref="Buffer"/> contains a snapshot of selected pixels
    /// that can be moved and transformed independently of the document. Buffer uses BGRA byte format
    /// (4 bytes per pixel) with dimensions (<see cref="BufW"/>, <see cref="BufH"/>).
    /// </para>
    /// </remarks>
    /// <seealso cref="SelectionMask"/>
    /// <seealso cref="SelectionTransform"/>
    /// <seealso cref="SelectionEngine"/>
    public class PixelSelection
    {
        // ─────────────────────────────────────────────────────────────
        // BASIC STATE
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Gets or sets a value indicating whether a selection exists.
        /// </summary>
        /// <value>
        /// <c>true</c> if selection is defined (mask non-empty); otherwise, <c>false</c>.
        /// </value>
        public bool Active { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the selection is floating (lifted from document).
        /// </summary>
        /// <value>
        /// <c>true</c> if selection is in <see cref="Buffer"/>; <c>false</c> if pixels remain in place.
        /// </value>
        public bool Floating { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the selection is armed for transform operations.
        /// </summary>
        /// <value>
        /// <c>true</c> if transform handles should be visible/interactive; otherwise, <c>false</c>.
        /// </value>
        public bool Armed { get; set; }

        // ─────────────────────────────────────────────────────────────
        // PIXEL MASK (true hit area)
        // This is authoritative for wand/lasso/composition
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Gets the pixel-accurate selection mask.
        /// </summary>
        /// <value>
        /// Boolean mask defining which pixels are selected. Used for hit-testing, magic wand flood fill,
        /// and compositing operations.
        /// </value>
        public SelectionMask Mask { get; private set; }

        // ─────────────────────────────────────────────────────────────
        // POLYGON OUTLINE (geometric shell)
        // Always pixel-snapped. Derived from Mask or rect.
        // Used for handles + transform UI.
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Gets the polygon outline vertices.
        /// </summary>
        /// <value>
        /// List of vertices in document pixel space, always pixel-snapped. Used for rendering
        /// marching ants, transform handles, and geometric operations.
        /// </value>
        public List<Vector2> Poly { get; private set; }

        // ─────────────────────────────────────────────────────────────
        // FLOATING BUFFER
        // When selection is lifted from doc.
        // This is the CURRENT editable pixel chunk.
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Gets or sets the floating pixel buffer (BGRA byte format).
        /// </summary>
        /// <value>
        /// Byte array containing BGRA pixels (4 bytes per pixel), or null if not floating.
        /// Length = <see cref="BufW"/> × <see cref="BufH"/> × 4.
        /// </value>
        public byte[]? Buffer { get; set; }

        /// <summary>
        /// Gets or sets the width of the floating buffer in pixels.
        /// </summary>
        public int BufW { get; set; }

        /// <summary>
        /// Gets or sets the height of the floating buffer in pixels.
        /// </summary>
        public int BufH { get; set; }

        // ─────────────────────────────────────────────────────────────
        // TRANSFORM STATE (applies to polygon + buffer)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Gets or sets the rotation angle in degrees (clockwise positive).
        /// </summary>
        /// <value>
        /// Rotation angle, typically normalized to -180°..180° range. Default is 0 (no rotation).
        /// </value>
        public float RotationDeg { get; set; } = 0;

        /// <summary>
        /// Gets or sets the horizontal scale factor.
        /// </summary>
        /// <value>
        /// Scale multiplier along X axis. 1.0 = 100% (original size), 0.5 = 50%, 2.0 = 200%.
        /// Default is 1.0.
        /// </value>
        public float ScaleX { get; set; } = 1;

        /// <summary>
        /// Gets or sets the vertical scale factor.
        /// </summary>
        /// <value>
        /// Scale multiplier along Y axis. 1.0 = 100% (original size), 0.5 = 50%, 2.0 = 200%.
        /// Default is 1.0.
        /// </value>
        public float ScaleY { get; set; } = 1;

        /// <summary>
        /// Gets or sets the translation offset in pixel space.
        /// </summary>
        /// <value>
        /// X and Y offset from original position. Default is <see cref="Vector2.Zero"/>.
        /// </value>
        public Vector2 Translation { get; set; } = Vector2.Zero;

        // ─────────────────────────────────────────────────────────────
        // CONSTRUCTOR
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Initializes a new instance of the <see cref="PixelSelection"/> class.
        /// </summary>
        /// <param name="docWidth">Document width in pixels (mask dimensions).</param>
        /// <param name="docHeight">Document height in pixels (mask dimensions).</param>
        public PixelSelection(int docWidth, int docHeight)
        {
            Mask = new SelectionMask(docWidth, docHeight);
            Poly = new List<Vector2>();
        }

        // ─────────────────────────────────────────────────────────────
        // RESET EVERYTHING
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Clears the selection and resets all state to defaults.
        /// </summary>
        /// <remarks>
        /// Clears mask, polygon, buffer, and transform state. Sets all flags to false.
        /// Does not deallocate the mask buffer, only clears its contents.
        /// </remarks>
        public void Clear()
        {
            Active = false;
            Floating = false;
            Armed = false;

            Mask.Clear();
            Poly.Clear();

            Buffer = null;
            BufW = BufH = 0;

            RotationDeg = 0;
            ScaleX = ScaleY = 1;
            Translation = Vector2.Zero;
        }

        // ─────────────────────────────────────────────────────────────
        // Set selection from an axis-aligned rectangle
        // (Used for marquee selection or Select All)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Sets the selection to an axis-aligned rectangle.
        /// </summary>
        /// <param name="x">Left edge in document pixels.</param>
        /// <param name="y">Top edge in document pixels.</param>
        /// <param name="w">Width in pixels.</param>
        /// <param name="h">Height in pixels.</param>
        /// <remarks>
        /// <para>
        /// Clears existing mask and creates new rectangular selection. Polygon is set to a 4-vertex
        /// rectangle with corners at pixel edges. Transform state is reset to identity.
        /// </para>
        /// <para>
        /// Used for marquee tool, Select All command, and rectangular mask operations.
        /// Sets <see cref="Active"/> and <see cref="Armed"/> to true.
        /// </para>
        /// </remarks>
        public void SetRect(int x, int y, int w, int h)
        {
            Active = true;
            Floating = false;
            Armed = true;

            Mask.Clear();
            Mask.AddRect(x, y, w, h);

            Poly.Clear();
            Poly.Add(new Vector2(x, y));
            Poly.Add(new Vector2(x + w, y));
            Poly.Add(new Vector2(x + w, y + h));
            Poly.Add(new Vector2(x, y + h));

            RotationDeg = 0;
            ScaleX = ScaleY = 1;
            Translation = Vector2.Zero;
        }

        // ─────────────────────────────────────────────────────────────
        // Apply transform matrix to polygon (preview only)
        // The final mask/outline is rebuilt AFTER commit (Phase 3)
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Computes the transform matrix from current transform state.
        /// </summary>
        /// <returns>3×2 affine transform matrix in SRT order (Scale → Rotate → Translate).</returns>
        /// <remarks>
        /// <para><strong>Transform Order:</strong></para>
        /// <list type="number">
        /// <item>Scale around origin: (<see cref="ScaleX"/>, <see cref="ScaleY"/>)</item>
        /// <item>Rotate around origin: <see cref="RotationDeg"/> (degrees → radians)</item>
        /// <item>Translate: <see cref="Translation"/></item>
        /// </list>
        /// <para>
        /// This order ensures scaling happens before rotation, and translation moves the entire
        /// transformed shape without affecting rotation/scale pivots.
        /// </para>
        /// </remarks>
        public Matrix3x2 GetTransformMatrix()
        {
            return
                Matrix3x2.CreateScale(ScaleX, ScaleY) *
                Matrix3x2.CreateRotation(RotationDeg * (float)Math.PI / 180f) *
                Matrix3x2.CreateTranslation(Translation);
        }

        /// <summary>
        /// Transforms a point using the current transform matrix.
        /// </summary>
        /// <param name="p">Point in original (pre-transform) coordinates.</param>
        /// <returns>Transformed point.</returns>
        /// <remarks>
        /// Convenience wrapper for <see cref="Vector2.Transform(Vector2, Matrix3x2)"/> using
        /// <see cref="GetTransformMatrix"/>. Useful for transforming individual polygon vertices
        /// or hit-test points.
        /// </remarks>
        public Vector2 TransformPoint(Vector2 p)
        {
            return Vector2.Transform(p, GetTransformMatrix());
        }
    }
}
