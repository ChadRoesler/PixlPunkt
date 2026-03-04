using System;
using System.Numerics;

namespace PixlPunkt.Core.Voxel
{
    /// <summary>
    /// Orthographic orbit camera for voxel preview rendering.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses spherical coordinates (pitch + yaw) instead of quaternion arcball,
    /// giving predictable and intuitive orbit behavior. The camera always looks
    /// at the model center with an orthographic projection, preserving parallel
    /// lines — the standard look for pixel art voxel rendering.
    /// </para>
    /// <para>
    /// Supports:
    /// </para>
    /// <list type="bullet">
    /// <item><strong>Orbit rotation:</strong> drag to orbit via pitch/yaw.</item>
    /// <item><strong>Zoom:</strong> percentage-based frustum scaling.</item>
    /// <item><strong>View snapping:</strong> magnetizes to isometric and orthographic
    /// presets with animated transitions.</item>
    /// <item><strong>Preset views:</strong> front, back, left, right, top, bottom,
    /// and 8 isometric compass directions.</item>
    /// </list>
    /// </remarks>
    public sealed class OrbitCamera
    {
        // ════════════════════════════════════════════════════════════════════
        // PUBLIC STRUCTS (matching reference PixzelOrbitCameraController)
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Orthographic frustum bounds.</summary>
        public readonly struct Frustum
        {
            public readonly float Left;
            public readonly float Right;
            public readonly float Top;
            public readonly float Bottom;

            public Frustum(float left, float right, float top, float bottom)
            {
                Left = left; Right = right; Top = top; Bottom = bottom;
            }

            public float Width => Right - Left;
            public float Height => Top - Bottom;
        }

        /// <summary>Camera position, look-at target, and up vector.</summary>
        public readonly struct CameraPose
        {
            public readonly Vector3 Position;
            public readonly Vector3 Target;
            public readonly Vector3 Up;

            public CameraPose(Vector3 position, Vector3 target, Vector3 up)
            {
                Position = position; Target = target; Up = up;
            }
        }

        /// <summary>Camera-space basis vectors (right, up, forward).</summary>
        public readonly struct CameraBasis
        {
            public readonly Vector3 Right;
            public readonly Vector3 Up;
            public readonly Vector3 Forward;

            public CameraBasis(Vector3 right, Vector3 up, Vector3 forward)
            {
                Right = right; Up = up; Forward = forward;
            }
        }

        // ════════════════════════════════════════════════════════════════════
        // SNAP DEFINITIONS
        // ════════════════════════════════════════════════════════════════════

        private readonly struct SnapPoint
        {
            public readonly string Name;
            public readonly float Pitch;
            public readonly float Yaw;

            public SnapPoint(string name, float pitch, float yaw)
            {
                Name = name;
                Pitch = pitch;
                Yaw = yaw;
            }
        }

        private sealed class ViewAnimation
        {
            public float FromPitch;
            public float FromYaw;
            public float ToPitch;
            public float ToYaw;
            public double StartMs;
            public double DurationMs;
        }

        private static readonly SnapPoint[] SnapPoints =
        {
            // Isometric compass (30° elevation)
            new("south",     Deg(30), MathF.PI),
            new("southwest", Deg(30), Deg(225)),
            new("west",      Deg(30), Deg(270)),
            new("northwest", Deg(30), Deg(315)),
            new("north",     Deg(30), Deg(0)),
            new("northeast", Deg(30), Deg(45)),
            new("east",      Deg(30), Deg(90)),
            new("southeast", Deg(30), Deg(135)),
            // Orthographic
            new("front",     0f, 0f),
            new("back",      0f, MathF.PI),
            new("left",      0f, Deg(90)),
            new("right",     0f, Deg(270)),
            new("top",       Deg(90), MathF.PI),
            new("bottom",   -Deg(90), MathF.PI),
        };

        private const float SnapThreshold = 0.07f; // ~4°

        // ════════════════════════════════════════════════════════════════════
        // STATE
        // ════════════════════════════════════════════════════════════════════

        private float _pitch = Deg(30);
        private float _yaw = Deg(225);
        private float _zoomPercent = 100f;
        private float _viewportWidth = 512f;
        private float _viewportHeight = 512f;
        private float _cameraDistance = 150f;
        private ViewAnimation? _animation;

        // Frustum state (recomputed on resize/zoom/pixel-perfect toggle)
        private Frustum _baseFrustum;
        private Frustum _frustum;

        // Pixel-perfect frustum state
        private bool _pixelPerfectEnabled;
        private int _pixelPerfectRtWidth;
        private int _pixelPerfectRtHeight;

        /// <summary>Grid size in world units (matches tile size).</summary>
        public float GridSize { get; set; } = 64f;

        /// <summary>Current pitch angle in radians (elevation).</summary>
        public float Pitch => _pitch;

        /// <summary>Current yaw angle in radians (azimuth).</summary>
        public float Yaw => _yaw;

        /// <summary>Current zoom percentage (100 = 1:1).</summary>
        public float ZoomPercent => _zoomPercent;

        /// <summary>Minimum zoom percentage.</summary>
        public float ZoomMin { get; set; } = 20f;

        /// <summary>Maximum zoom percentage.</summary>
        public float ZoomMax { get; set; } = 600f;

        /// <summary>Rotation sensitivity (radians per pixel of mouse delta).</summary>
        public float RotationSensitivity { get; set; } = 0.01f;

        /// <summary>Whether a drag is in progress.</summary>
        public bool IsDragging { get; private set; }

        /// <summary>Name of the current snap point, or null if free-rotating.</summary>
        public string? CurrentSnapName { get; private set; }

        // ════════════════════════════════════════════════════════════════════
        // INITIALIZATION
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates an orbit camera configured for the given volume size.
        /// </summary>
        /// <param name="volumeSize">
        /// Voxel volume side length. Used to set grid size and camera distance.
        /// </param>
        public OrbitCamera(int volumeSize = 16)
        {
            ConfigureForVolume(volumeSize);
        }

        /// <summary>
        /// Reconfigures camera framing for a new voxel volume size.
        /// Keeps the current angles/zoom, but updates grid/frustum scale and
        /// camera distance so larger mapped volumes do not clip near the camera.
        /// </summary>
        public void ConfigureForVolume(int volumeSize)
        {
            volumeSize = Math.Max(1, volumeSize);
            GridSize = volumeSize;

            // Orthographic framing is controlled by the frustum, not distance.
            // Distance only needs to be far enough that the whole model remains
            // in front of the camera at oblique angles.
            _cameraDistance = volumeSize * 4f;
            RecomputeFrustum();
        }

        /// <summary>
        /// Updates the viewport dimensions (call when window resizes).
        /// </summary>
        public void ResizeViewport(float width, float height)
        {
            _viewportWidth = MathF.Max(1f, width);
            _viewportHeight = MathF.Max(1f, height);
            RecomputeFrustum();
        }

        /// <summary>
        /// Enables pixel-perfect frustum mode. The frustum height equals the
        /// render target height so that 1 world unit = 1 render pixel, preventing
        /// any sub-pixel distortion.
        /// </summary>
        /// <param name="renderTargetWidth">Low-res render target width.</param>
        /// <param name="renderTargetHeight">Low-res render target height.</param>
        public void EnablePixelPerfectFrustum(int renderTargetWidth, int renderTargetHeight)
        {
            _pixelPerfectEnabled = true;
            _pixelPerfectRtWidth = Math.Max(1, renderTargetWidth);
            _pixelPerfectRtHeight = Math.Max(1, renderTargetHeight);
            RecomputeFrustum();
        }

        /// <summary>
        /// Disables pixel-perfect frustum mode, returning to normal grid-based frustum.
        /// </summary>
        public void DisablePixelPerfectFrustum()
        {
            _pixelPerfectEnabled = false;
            _pixelPerfectRtWidth = 0;
            _pixelPerfectRtHeight = 0;
            RecomputeFrustum();
        }

        // ════════════════════════════════════════════════════════════════════
        // ORBIT ROTATION
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Begins a drag rotation.
        /// </summary>
        public void BeginDrag()
        {
            IsDragging = true;
            _animation = null; // cancel any running animation
        }

        /// <summary>
        /// Updates rotation from pixel-space mouse deltas.
        /// </summary>
        /// <param name="deltaX">Horizontal pixel delta (positive = right).</param>
        /// <param name="deltaY">Vertical pixel delta (positive = down).</param>
        public void UpdateDrag(float deltaX, float deltaY)
        {
            if (!IsDragging) return;

            _yaw -= deltaX * RotationSensitivity;
            _pitch += deltaY * RotationSensitivity;

            _yaw = Wrap0To2Pi(_yaw);
            _pitch = Math.Clamp(_pitch, -MathF.PI * 0.5f, MathF.PI * 0.5f);

            // Try snapping
            TrySnap();
        }

        /// <summary>
        /// Ends the drag operation.
        /// </summary>
        public void EndDrag()
        {
            IsDragging = false;
        }

        // ════════════════════════════════════════════════════════════════════
        // ZOOM
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Zooms by mouse wheel steps. Positive = zoom in.
        /// </summary>
        /// <param name="wheelSteps">Number of scroll steps (positive = in).</param>
        /// <param name="perStepScale">Multiplicative scale per step.</param>
        public void Zoom(float wheelSteps, float perStepScale = 1.10f)
        {
            if (wheelSteps == 0f) return;
            _zoomPercent = Math.Clamp(
                _zoomPercent * MathF.Pow(perStepScale, wheelSteps),
                ZoomMin, ZoomMax);
            RecomputeFrustum();
        }

        /// <summary>
        /// Sets camera orbit orientation directly (radians), applying the same
        /// clamp/wrap rules as interactive drag rotation.
        /// </summary>
        public void SetOrientation(float pitch, float yaw, bool allowSnap = false)
        {
            _animation = null;
            _pitch = Math.Clamp(pitch, -MathF.PI * 0.5f, MathF.PI * 0.5f);
            _yaw = Wrap0To2Pi(yaw);

            if (allowSnap)
                TrySnap();
            else
                CurrentSnapName = null;
        }

        /// <summary>
        /// Sets zoom directly in percent (100 = 1:1), clamped to zoom limits.
        /// </summary>
        public void SetZoomPercent(float zoomPercent)
        {
            float clamped = Math.Clamp(zoomPercent, ZoomMin, ZoomMax);
            if (MathF.Abs(clamped - _zoomPercent) < 1e-4f) return;
            _zoomPercent = clamped;
            RecomputeFrustum();
        }

        // ════════════════════════════════════════════════════════════════════
        // PRESET VIEWS
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Snaps to a named preset view with animated transition.
        /// </summary>
        /// <param name="viewName">
        /// Case-insensitive view name: front, back, left, right, top, bottom,
        /// south, southwest, west, northwest, north, northeast, east, southeast.
        /// </param>
        /// <param name="animated">Whether to animate the transition.</param>
        public void SetView(string viewName, bool animated = true)
        {
            foreach (var snap in SnapPoints)
            {
                if (!string.Equals(snap.Name, viewName, StringComparison.OrdinalIgnoreCase))
                    continue;

                CurrentSnapName = snap.Name;
                bool isTopBottom = IsTopBottomName(snap.Name);
                float targetYaw = isTopBottom ? _yaw : snap.Yaw;

                if (animated)
                {
                    // Top/Bottom lock should not force a yaw snap; keep current spin.
                    StartAnimation(snap.Pitch, targetYaw, 350);
                }
                else
                {
                    _pitch = snap.Pitch;
                    if (!isTopBottom)
                        _yaw = targetYaw;
                    _animation = null;
                }
                return;
            }

            // Default: reset to south isometric
            CurrentSnapName = null;
            if (animated)
                StartAnimation(Deg(30), Deg(225), 350);
            else
            {
                _pitch = Deg(30);
                _yaw = Deg(225);
            }
        }

        /// <summary>
        /// Resets to the default south-west isometric view and 100% zoom.
        /// </summary>
        public void Reset()
        {
            SetView("southwest", animated: false);
            _zoomPercent = 100f;
            RecomputeFrustum();
        }

        // ════════════════════════════════════════════════════════════════════
        // ANIMATION
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Updates any running view transition animation.
        /// </summary>
        /// <returns>True if the animation is still running and a re-render is needed.</returns>
        public bool UpdateAnimation()
        {
            if (_animation == null) return false;

            var anim = _animation;
            double nowMs = (DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds;
            float t = Math.Clamp(
                (float)((nowMs - anim.StartMs) / Math.Max(1, anim.DurationMs)), 0f, 1f);

            // Ease in-out quadratic
            float ease = t < 0.5f
                ? 2f * t * t
                : 1f - MathF.Pow(-2f * t + 2f, 2f) * 0.5f;

            _pitch = anim.FromPitch + (anim.ToPitch - anim.FromPitch) * ease;
            _yaw = LerpAngle(anim.FromYaw, anim.ToYaw, ease);

            if (t >= 1f)
            {
                _pitch = anim.ToPitch;
                _yaw = anim.ToYaw;
                _animation = null;
            }

            return true;
        }

        /// <summary>
        /// Gets whether an animation is currently running.
        /// </summary>
        public bool IsAnimating => _animation != null;

        // ════════════════════════════════════════════════════════════════════
        // VIEW / PROJECTION
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets the camera pose (position, target, up).
        /// </summary>
        public CameraPose GetCameraPose()
        {
            float x = _cameraDistance * MathF.Sin(_yaw) * MathF.Cos(_pitch);
            float y = _cameraDistance * MathF.Sin(_pitch);
            float z = _cameraDistance * MathF.Cos(_yaw) * MathF.Cos(_pitch);

            // Keep position numerically stable exactly at the poles.
            float poleEps = Deg(1.5f);
            if (MathF.Abs(MathF.Abs(_pitch) - MathF.PI * 0.5f) < poleEps)
            {
                x = 0f;
                z = 0f;
                y = _pitch >= 0f ? _cameraDistance : -_cameraDistance;
            }

            var position = new Vector3(x, y, z);
            var forward = SafeNormalize(Vector3.Zero - position, new Vector3(0f, -1f, 0f));

            // Build a continuous no-roll-up vector with a pole-safe fallback.
            // This avoids the old 90° quantization jump when entering top/bottom.
            var right = Vector3.Cross(forward, Vector3.UnitY);
            if (!IsFinite(right) || right.LengthSquared() < 1e-8f)
            {
                right = new Vector3(MathF.Cos(_yaw), 0f, -MathF.Sin(_yaw));
            }
            right = SafeNormalize(right, Vector3.UnitX);

            var up = SafeNormalize(Vector3.Cross(right, forward), Vector3.UnitY);
            return new CameraPose(position, Vector3.Zero, up);
        }

        /// <summary>
        /// Gets the camera-space basis vectors from a pose.
        /// </summary>
        public CameraBasis GetCameraBasis(CameraPose pose)
        {
            var forward = SafeNormalize(pose.Target - pose.Position, new Vector3(0f, 0f, -1f));
            var right = Vector3.Cross(forward, pose.Up);
            if (!IsFinite(right) || right.LengthSquared() < 1e-8f)
            {
                right = Vector3.Cross(forward, Vector3.UnitY);
                if (!IsFinite(right) || right.LengthSquared() < 1e-8f)
                {
                    right = Vector3.Cross(forward, Vector3.UnitZ);
                }
            }
            right = SafeNormalize(right, Vector3.UnitX);
            var up = SafeNormalize(Vector3.Cross(right, forward), Vector3.UnitY);
            return new CameraBasis(right, up, forward);
        }

        /// <summary>
        /// Gets the camera-space basis vectors for the current pose.
        /// </summary>
        public CameraBasis GetCameraBasis() => GetCameraBasis(GetCameraPose());

        /// <summary>Gets the current orthographic frustum.</summary>
        public Frustum GetFrustum() => _frustum;

        /// <summary>Gets the current viewport width.</summary>
        public float ViewportWidth => _viewportWidth;

        /// <summary>Gets the current viewport height.</summary>
        public float ViewportHeight => _viewportHeight;

        /// <summary>
        /// Computes a combined orthographic view × projection matrix.
        /// </summary>
        /// <returns>Matrix suitable for <see cref="SoftwareRasterizer"/>.</returns>
        public Matrix4x4 GetViewProjection()
        {
            var pose = GetCameraPose();
            var view = Matrix4x4.CreateLookAt(pose.Position, pose.Target, pose.Up);

            var proj = Matrix4x4.CreateOrthographic(
                _frustum.Width,
                _frustum.Height,
                0.1f,
                _cameraDistance * 3f);

            return view * proj;
        }

        /// <summary>
        /// Recomputes the orthographic frustum from current viewport, zoom, and pixel-perfect settings.
        /// </summary>
        private void RecomputeFrustum()
        {
            if (_pixelPerfectEnabled && _pixelPerfectRtWidth > 0 && _pixelPerfectRtHeight > 0)
            {
                // Pixel-perfect: frustum height = render target height
                // so 1 world unit = 1 render pixel
                float targetW = _pixelPerfectRtWidth;
                float targetH = _pixelPerfectRtHeight;
                float aspect = targetW / targetH;
                float frustumH = targetH;

                _baseFrustum = new Frustum(
                    -frustumH * aspect * 0.5f,
                     frustumH * aspect * 0.5f,
                     frustumH * 0.5f,
                    -frustumH * 0.5f);
            }
            else
            {
                // Normal: frustum sized to grid, scaled by zoom
                float aspect = _viewportWidth / MathF.Max(1f, _viewportHeight);
                float zoom = MathF.Max(0.0001f, _zoomPercent / 100f);
                float frustumSize = GridSize / zoom;

                _baseFrustum = new Frustum(
                    -frustumSize * aspect * 0.5f,
                     frustumSize * aspect * 0.5f,
                     frustumSize * 0.5f,
                    -frustumSize * 0.5f);
            }

            _frustum = _baseFrustum;
        }

        // ════════════════════════════════════════════════════════════════════
        // SNAP LOGIC
        // ════════════════════════════════════════════════════════════════════

        private void TrySnap()
        {
            float bestDist = float.MaxValue;
            SnapPoint? best = null;

            for (int i = 0; i < SnapPoints.Length; i++)
            {
                var snap = SnapPoints[i];

                float diffPitch = MathF.Abs(_pitch - snap.Pitch);
                float diffYaw = MathF.Abs(ShortestAngleDelta(_yaw, snap.Yaw));

                // Top/bottom only check pitch
                bool isTopBottom = IsTopBottomName(snap.Name);

                if (isTopBottom)
                    diffYaw = 0f;

                if (diffPitch < SnapThreshold && diffYaw < SnapThreshold)
                {
                    float dist = MathF.Sqrt(diffPitch * diffPitch + diffYaw * diffYaw);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = snap;
                    }
                }
            }

            if (best.HasValue)
            {
                _pitch = best.Value.Pitch;
                if (!IsTopBottomName(best.Value.Name))
                    _yaw = best.Value.Yaw;
                CurrentSnapName = best.Value.Name;
            }
            else
            {
                CurrentSnapName = null;
            }
        }

        private void StartAnimation(float toPitch, float toYaw, double durationMs)
        {
            _animation = new ViewAnimation
            {
                FromPitch = _pitch,
                FromYaw = _yaw,
                ToPitch = toPitch,
                ToYaw = toYaw,
                StartMs = (DateTime.UtcNow - DateTime.UnixEpoch).TotalMilliseconds,
                DurationMs = durationMs
            };
        }

        // ════════════════════════════════════════════════════════════════════
        // MATH HELPERS
        // ════════════════════════════════════════════════════════════════════

        private static float Deg(float d) => d * MathF.PI / 180f;

        private static float Wrap0To2Pi(float a)
        {
            float twoPi = 2f * MathF.PI;
            float v = a % twoPi;
            if (v < 0f) v += twoPi;
            return v;
        }

        private static float ShortestAngleDelta(float from, float to)
        {
            float d = Wrap0To2Pi(to) - Wrap0To2Pi(from);
            if (d > MathF.PI) d -= 2f * MathF.PI;
            if (d < -MathF.PI) d += 2f * MathF.PI;
            return d;
        }

        private static float LerpAngle(float a, float b, float t)
        {
            float na = Wrap0To2Pi(a);
            float d = ShortestAngleDelta(na, b);
            return Wrap0To2Pi(na + d * t);
        }

        private static bool IsTopBottomName(string? name)
        {
            return string.Equals(name, "top", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "bottom", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFinite(Vector3 v)
        {
            return float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
        }

        private static Vector3 SafeNormalize(Vector3 v, Vector3 fallback)
        {
            if (!IsFinite(v)) return fallback;
            float lenSq = v.LengthSquared();
            if (lenSq < 1e-12f) return fallback;
            return v / MathF.Sqrt(lenSq);
        }
    }
}
