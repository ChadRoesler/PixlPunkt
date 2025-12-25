using System;
using System.Collections.Generic;
using System.Linq;

namespace PixlPunkt.Core.Guides
{
    /// <summary>
    /// Represents a guide line on the canvas.
    /// </summary>
    public sealed class Guide
    {
        /// <summary>Gets or sets the position of the guide in document pixels.</summary>
        public int Position { get; set; }

        /// <summary>Gets or sets whether this is a horizontal guide (true) or vertical guide (false).</summary>
        public bool IsHorizontal { get; set; }

        /// <summary>Gets or sets whether this guide is currently selected.</summary>
        public bool IsSelected { get; set; }

        /// <summary>Gets or sets whether this guide is locked (cannot be moved/deleted).</summary>
        public bool IsLocked { get; set; }

        public Guide(int position, bool isHorizontal)
        {
            Position = position;
            IsHorizontal = isHorizontal;
        }
    }

    /// <summary>
    /// Manages guides for a document. Each document has its own guide service instance.
    /// </summary>
    public sealed class GuideService
    {
        private readonly List<Guide> _horizontalGuides = [];
        private readonly List<Guide> _verticalGuides = [];

        /// <summary>Gets all horizontal guides (sorted by position).</summary>
        public IReadOnlyList<Guide> HorizontalGuides => _horizontalGuides.OrderBy(g => g.Position).ToList();

        /// <summary>Gets all vertical guides (sorted by position).</summary>
        public IReadOnlyList<Guide> VerticalGuides => _verticalGuides.OrderBy(g => g.Position).ToList();

        /// <summary>Gets all guides.</summary>
        public IEnumerable<Guide> AllGuides => _horizontalGuides.Concat(_verticalGuides);

        /// <summary>Gets or sets whether guides are visible.</summary>
        public bool GuidesVisible { get; set; } = true;

        /// <summary>Gets or sets whether snap-to-guides is enabled.</summary>
        public bool SnapToGuides { get; set; } = true;

        /// <summary>Gets or sets the snap threshold in pixels.</summary>
        public int SnapThreshold { get; set; } = 4;

        /// <summary>Raised when guides change.</summary>
        public event Action? GuidesChanged;

        /// <summary>
        /// Adds a horizontal guide at the specified position.
        /// </summary>
        public Guide AddHorizontalGuide(int y)
        {
            var guide = new Guide(y, isHorizontal: true);
            _horizontalGuides.Add(guide);
            GuidesChanged?.Invoke();
            return guide;
        }

        /// <summary>
        /// Adds a vertical guide at the specified position.
        /// </summary>
        public Guide AddVerticalGuide(int x)
        {
            var guide = new Guide(x, isHorizontal: false);
            _verticalGuides.Add(guide);
            GuidesChanged?.Invoke();
            return guide;
        }

        /// <summary>
        /// Removes a guide.
        /// </summary>
        public bool RemoveGuide(Guide guide)
        {
            bool removed = guide.IsHorizontal
                ? _horizontalGuides.Remove(guide)
                : _verticalGuides.Remove(guide);

            if (removed)
                GuidesChanged?.Invoke();

            return removed;
        }

        /// <summary>
        /// Clears all guides.
        /// </summary>
        public void ClearAllGuides()
        {
            if (_horizontalGuides.Count == 0 && _verticalGuides.Count == 0)
                return;

            _horizontalGuides.Clear();
            _verticalGuides.Clear();
            GuidesChanged?.Invoke();
        }

        /// <summary>
        /// Clears all horizontal guides.
        /// </summary>
        public void ClearHorizontalGuides()
        {
            if (_horizontalGuides.Count == 0) return;
            _horizontalGuides.Clear();
            GuidesChanged?.Invoke();
        }

        /// <summary>
        /// Clears all vertical guides.
        /// </summary>
        public void ClearVerticalGuides()
        {
            if (_verticalGuides.Count == 0) return;
            _verticalGuides.Clear();
            GuidesChanged?.Invoke();
        }

        /// <summary>
        /// Finds the nearest guide to snap to for a given position.
        /// </summary>
        /// <param name="position">The position to check.</param>
        /// <param name="isHorizontal">True for horizontal guides (snap Y), false for vertical guides (snap X).</param>
        /// <returns>The snapped position if within threshold, or null if no snap.</returns>
        public int? GetSnapPosition(int position, bool isHorizontal)
        {
            if (!SnapToGuides || !GuidesVisible)
                return null;

            var guides = isHorizontal ? _horizontalGuides : _verticalGuides;

            int? closest = null;
            int closestDistance = int.MaxValue;

            foreach (var guide in guides)
            {
                int distance = Math.Abs(guide.Position - position);
                if (distance <= SnapThreshold && distance < closestDistance)
                {
                    closest = guide.Position;
                    closestDistance = distance;
                }
            }

            return closest;
        }

        /// <summary>
        /// Finds a guide at the given position (within threshold).
        /// </summary>
        public Guide? FindGuideAt(int position, bool isHorizontal, int threshold = 4)
        {
            var guides = isHorizontal ? _horizontalGuides : _verticalGuides;
            return guides.FirstOrDefault(g => Math.Abs(g.Position - position) <= threshold);
        }

        /// <summary>
        /// Gets snap positions for a rectangle (returns snapped edges).
        /// </summary>
        public (int? left, int? top, int? right, int? bottom) GetSnapForRect(int x, int y, int width, int height)
        {
            if (!SnapToGuides || !GuidesVisible)
                return (null, null, null, null);

            // Check left and right edges against vertical guides
            int? snapLeft = GetSnapPosition(x, isHorizontal: false);
            int? snapRight = GetSnapPosition(x + width, isHorizontal: false);

            // Check top and bottom edges against horizontal guides
            int? snapTop = GetSnapPosition(y, isHorizontal: true);
            int? snapBottom = GetSnapPosition(y + height, isHorizontal: true);

            return (snapLeft, snapTop, snapRight, snapBottom);
        }

        /// <summary>
        /// Serializes guides to a list of (position, isHorizontal) tuples.
        /// </summary>
        public List<(int position, bool isHorizontal)> Serialize()
        {
            var result = new List<(int, bool)>();
            foreach (var g in _horizontalGuides)
                result.Add((g.Position, true));
            foreach (var g in _verticalGuides)
                result.Add((g.Position, false));
            return result;
        }

        /// <summary>
        /// Deserializes guides from a list of (position, isHorizontal) tuples.
        /// </summary>
        public void Deserialize(List<(int position, bool isHorizontal)> guides)
        {
            _horizontalGuides.Clear();
            _verticalGuides.Clear();

            foreach (var (position, isHorizontal) in guides)
            {
                if (isHorizontal)
                    _horizontalGuides.Add(new Guide(position, true));
                else
                    _verticalGuides.Add(new Guide(position, false));
            }

            GuidesChanged?.Invoke();
        }
    }
}
