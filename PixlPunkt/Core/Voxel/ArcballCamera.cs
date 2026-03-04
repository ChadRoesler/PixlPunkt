using System;
using System.Numerics;

namespace PixlPunkt.Core.Voxel
{
    /// <summary>
    /// Arcball camera for free rotation around a 3D voxel object.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implements Ken Shoemake's arcball rotation model. The user conceptually
    /// grabs a virtual sphere surrounding the object — dragging maps to a
    /// rotation quaternion with no gimbal lock or axis constraints.
    /// </para>
    /// <para>
    /// Usage: call <see cref="BeginDrag"/> on pointer down, <see cref="UpdateDrag"/>
    /// on pointer move, and <see cref="EndDrag"/> on pointer up. The resulting
    /// <see cref="ViewMatrix"/> transforms world-space geometry for rendering.
    /// </para>
    /// <para>
    /// All coordinates use the <c>System.Numerics</c> right-hand coordinate system.
    /// The default view looks from +Z toward the origin.
    /// </para>
    /// </remarks>
    public sealed class ArcballCamera
    {
        private Quaternion _rotation = Quaternion.Identity;
        private Quaternion _dragStart = Quaternion.Identity;
        private Vector3 _dragStartPoint;
        private bool _isDragging;

        /// <summary>
        /// Distance from the camera to the object center.
        /// Controls zoom level.
        /// </summary>
        public float Distance { get; set; } = 3.0f;

        /// <summary>
        /// Minimum zoom distance.
        /// </summary>
        public float MinDistance { get; set; } = 1.5f;

        /// <summary>
        /// Maximum zoom distance.
        /// </summary>
        public float MaxDistance { get; set; } = 10.0f;

        /// <summary>
        /// Gets the current rotation as a quaternion.
        /// </summary>
        public Quaternion Rotation => _rotation;

        /// <summary>
        /// Gets whether a drag operation is in progress.
        /// </summary>
        public bool IsDragging => _isDragging;

        /// <summary>
        /// Computes the view matrix for rendering.
        /// </summary>
        /// <remarks>
        /// The camera orbits around the origin at <see cref="Distance"/>,
        /// always looking at (0, 0, 0). The default (identity rotation) position
        /// is at (0, 0, <see cref="Distance"/>) looking toward −Z.
        /// </remarks>
        public Matrix4x4 ViewMatrix
        {
            get
            {
                var rotMatrix = Matrix4x4.CreateFromQuaternion(_rotation);
                var eye = Vector3.Transform(new Vector3(0, 0, Distance), rotMatrix);
                var up = Vector3.Transform(Vector3.UnitY, rotMatrix);
                return Matrix4x4.CreateLookAt(eye, Vector3.Zero, up);
            }
        }

        /// <summary>
        /// Computes a combined view-projection matrix for a perspective camera.
        /// </summary>
        /// <param name="aspectRatio">Viewport width / height.</param>
        /// <param name="fovRadians">Vertical field of view in radians. Default is 45°.</param>
        /// <param name="nearPlane">Near clipping plane distance.</param>
        /// <param name="farPlane">Far clipping plane distance.</param>
        /// <returns>Combined view × projection matrix.</returns>
        public Matrix4x4 GetViewProjection(
            float aspectRatio,
            float fovRadians = MathF.PI / 4f,
            float nearPlane = 0.1f,
            float farPlane = 100f)
        {
            var view = ViewMatrix;
            var proj = Matrix4x4.CreatePerspectiveFieldOfView(fovRadians, aspectRatio, nearPlane, farPlane);
            return view * proj;
        }

        // ════════════════════════════════════════════════════════════════════
        // DRAG ROTATION
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Begins a drag rotation from normalized viewport coordinates.
        /// </summary>
        /// <param name="viewportX">X in [−1, 1] range (left to right).</param>
        /// <param name="viewportY">Y in [−1, 1] range (bottom to top).</param>
        public void BeginDrag(float viewportX, float viewportY)
        {
            _isDragging = true;
            _dragStart = _rotation;
            _dragStartPoint = MapToSphere(viewportX, viewportY);
        }

        /// <summary>
        /// Updates the rotation during a drag operation.
        /// </summary>
        /// <param name="viewportX">Current X in [−1, 1].</param>
        /// <param name="viewportY">Current Y in [−1, 1].</param>
        public void UpdateDrag(float viewportX, float viewportY)
        {
            if (!_isDragging) return;

            var currentPoint = MapToSphere(viewportX, viewportY);
            var dragRotation = RotationBetween(_dragStartPoint, currentPoint);
            _rotation = Quaternion.Normalize(Quaternion.Concatenate(_dragStart, dragRotation));
        }

        /// <summary>
        /// Ends the drag operation, locking in the current rotation.
        /// </summary>
        public void EndDrag()
        {
            _isDragging = false;
        }

        // ════════════════════════════════════════════════════════════════════
        // ZOOM
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Adjusts zoom distance by a scroll delta.
        /// </summary>
        /// <param name="delta">Positive = zoom in, negative = zoom out.</param>
        /// <param name="sensitivity">Zoom speed multiplier.</param>
        public void Zoom(float delta, float sensitivity = 0.1f)
        {
            Distance = Math.Clamp(Distance - delta * sensitivity, MinDistance, MaxDistance);
        }

        // ════════════════════════════════════════════════════════════════════
        // PRESETS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Resets rotation to the default front-facing view and default zoom.
        /// </summary>
        public void Reset()
        {
            _rotation = Quaternion.Identity;
            Distance = 3.0f;
            _isDragging = false;
        }

        /// <summary>
        /// Snaps to a preset orthographic view angle.
        /// </summary>
        /// <param name="view">The preset view direction.</param>
        public void SetView(PresetView view)
        {
            _isDragging = false;
            _rotation = view switch
            {
                PresetView.Front  => Quaternion.Identity,
                PresetView.Back   => Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI),
                PresetView.Left   => Quaternion.CreateFromAxisAngle(Vector3.UnitY, -MathF.PI / 2),
                PresetView.Right  => Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2),
                PresetView.Top    => Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2),
                PresetView.Bottom => Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathF.PI / 2),
                _ => Quaternion.Identity
            };
        }

        // ════════════════════════════════════════════════════════════════════
        // ARCBALL MATH
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Maps 2D viewport coordinates to a point on the virtual arcball sphere.
        /// </summary>
        /// <remarks>
        /// Points inside the sphere project directly onto its surface.
        /// Points outside the sphere (near viewport edges) are projected onto the
        /// equator, providing smooth rotation even when dragging beyond the boundary.
        /// </remarks>
        private static Vector3 MapToSphere(float x, float y)
        {
            float lengthSq = x * x + y * y;

            if (lengthSq <= 1.0f)
            {
                // Inside the sphere — project up onto the surface
                return new Vector3(x, y, MathF.Sqrt(1.0f - lengthSq));
            }

            // Outside the sphere — normalize onto the equator
            float invLen = 1.0f / MathF.Sqrt(lengthSq);
            return new Vector3(x * invLen, y * invLen, 0);
        }

        /// <summary>
        /// Computes the shortest rotation quaternion that maps vector
        /// <paramref name="from"/> to vector <paramref name="to"/> on the unit sphere.
        /// </summary>
        private static Quaternion RotationBetween(Vector3 from, Vector3 to)
        {
            var axis = Vector3.Cross(from, to);
            float dot = Vector3.Dot(from, to);

            // Near-parallel vectors — no rotation needed
            if (axis.LengthSquared() < 1e-10f)
                return Quaternion.Identity;

            // Quaternion shortcut: (axis, 1 + dot) then normalize
            // This avoids the explicit angle calculation (cos(θ/2), sin(θ/2)).
            return Quaternion.Normalize(new Quaternion(axis.X, axis.Y, axis.Z, 1.0f + dot));
        }
    }
}
