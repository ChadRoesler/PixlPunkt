using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PixlPunkt.Core.Document.Layer
{
    /// <summary>
    /// Represents a folder/group layer that can contain child layers for organizational hierarchy.
    /// </summary>
    /// <remarks>
    /// <para>
    /// LayerFolder provides grouping functionality for organizing layers into hierarchical structures.
    /// Unlike <see cref="RasterLayer"/>, folders don't contain pixel data but serve as containers
    /// for other layers (both raster layers and nested folders).
    /// </para>
    /// <para><strong>Cascading Properties:</strong></para>
    /// <para>
    /// When a folder is locked or hidden, all children are **effectively** locked/hidden for rendering
    /// and editing purposes, but their individual Locked/Visible properties remain unchanged. This
    /// allows toggling folder visibility without losing child visibility states.
    /// </para>
    /// <para><strong>UI Display:</strong></para>
    /// <para>
    /// Folders are displayed in a flattened ListView with indentation based on depth. The
    /// <see cref="IsExpanded"/> property controls whether children are visible in the UI list.
    /// </para>
    /// </remarks>
    /// <seealso cref="LayerBase"/>
    /// <seealso cref="RasterLayer"/>
    public sealed class LayerFolder : LayerBase
    {
        private bool _isExpanded;
        private readonly ObservableCollection<LayerBase> _children = new();

        /// <summary>
        /// Gets or sets a value indicating whether this folder is expanded in the UI layer list.
        /// </summary>
        /// <value>
        /// <c>true</c> if the folder's children should be visible in the UI list; otherwise, <c>false</c>.
        /// Default is <c>true</c> (expanded).
        /// </value>
        /// <remarks>
        /// This property controls UI presentation only and does not affect rendering.
        /// Child layers within a collapsed folder still contribute to compositing if visible.
        /// </remarks>
        public bool IsExpanded
        {
            get => _isExpanded;
            set { if (_isExpanded != value) { _isExpanded = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Gets a value indicating that folders can contain child layers.
        /// </summary>
        /// <value>
        /// Always returns <c>true</c> for <see cref="LayerFolder"/>.
        /// </value>
        public override bool CanHaveChildren => true;

        /// <summary>
        /// Gets the read-only collection of child layers in bottom-to-top order.
        /// </summary>
        /// <value>
        /// An observable collection of child layers. Index 0 is the bottom layer.
        /// </value>
        public IReadOnlyList<LayerBase> Children => _children;

        /// <summary>
        /// Initializes a new instance of the <see cref="LayerFolder"/> class.
        /// </summary>
        /// <param name="name">The folder name.</param>
        public LayerFolder(string name)
        {
            Name = name;
            Visible = true;
            Locked = false;
            _isExpanded = true; // Folders default to expanded
        }

        /// <summary>
        /// Adds a layer as a child to this folder (at the top).
        /// </summary>
        /// <param name="layer">The layer to add.</param>
        /// <remarks>
        /// Sets the layer's parent to this folder and raises property changed notifications.
        /// </remarks>
        public void AddChild(LayerBase layer)
        {
            layer.Parent = this;
            _children.Add(layer);
            OnPropertyChanged(nameof(Children));
        }

        /// <summary>
        /// Removes a layer from this folder's children.
        /// </summary>
        /// <param name="layer">The layer to remove.</param>
        /// <returns><c>true</c> if the layer was found and removed; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// Clears the layer's parent reference and raises property changed notifications.
        /// </remarks>
        public bool RemoveChild(LayerBase layer)
        {
            if (_children.Remove(layer))
            {
                layer.Parent = null;
                OnPropertyChanged(nameof(Children));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Inserts a layer at the specified index in this folder's child collection.
        /// </summary>
        /// <param name="index">The zero-based index at which to insert the layer.</param>
        /// <param name="layer">The layer to insert.</param>
        /// <remarks>
        /// Index is clamped to [0, Children.Count]. Sets the layer's parent to this folder.
        /// </remarks>
        public void InsertChild(int index, LayerBase layer)
        {
            layer.Parent = this;
            int clampedIndex = System.Math.Clamp(index, 0, _children.Count);
            _children.Insert(clampedIndex, layer);
            OnPropertyChanged(nameof(Children));
        }

        /// <summary>
        /// Gets the index of a child layer within this folder.
        /// </summary>
        /// <param name="layer">The layer to find.</param>
        /// <returns>The zero-based index of the layer, or -1 if not found.</returns>
        public int IndexOfChild(LayerBase layer)
        {
            return _children.IndexOf(layer);
        }

        /// <summary>
        /// Moves a child layer from one index to another within this folder.
        /// </summary>
        /// <param name="fromIndex">The source index.</param>
        /// <param name="toIndex">The destination index.</param>
        /// <remarks>
        /// After removal, the toIndex is clamped to the valid range [0, Count-1] since
        /// the collection shrinks by one element before insertion.
        /// </remarks>
        public void MoveChild(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= _children.Count) return;
            if (fromIndex == toIndex) return;

            var layer = _children[fromIndex];
            _children.RemoveAt(fromIndex);

            // After removal, clamp toIndex to valid range [0, _children.Count]
            // Note: Insert allows index == Count (appends to end)
            int clampedToIndex = System.Math.Clamp(toIndex, 0, _children.Count);
            _children.Insert(clampedToIndex, layer);
            OnPropertyChanged(nameof(Children));
        }

        /// <summary>
        /// Performs a depth-first traversal of this folder and all descendants.
        /// </summary>
        /// <returns>An enumerable of all layers in depth-first order (folder, then its children recursively).</returns>
        /// <remarks>
        /// The folder itself is yielded first, followed by each child in order. For child folders,
        /// their descendants are recursively yielded before moving to the next sibling.
        /// </remarks>
        public IEnumerable<LayerBase> FlattenDepthFirst()
        {
            yield return this;
            foreach (var child in _children)
            {
                if (child is LayerFolder folder)
                {
                    foreach (var descendant in folder.FlattenDepthFirst())
                        yield return descendant;
                }
                else
                {
                    yield return child;
                }
            }
        }

        /// <summary>
        /// Gets all raster layers within this folder and descendants in bottom-to-top rendering order.
        /// </summary>
        /// <returns>An enumerable of raster layers for compositing.</returns>
        /// <remarks>
        /// Recursively collects raster layers from all descendants. The folder's visibility and
        /// lock state affect whether children are included based on the calling context.
        /// </remarks>
        public IEnumerable<RasterLayer> GetRasterLayersBottomToTop()
        {
            foreach (var child in _children)
            {
                if (child is RasterLayer raster)
                {
                    yield return raster;
                }
                else if (child is LayerFolder folder)
                {
                    foreach (var descendant in folder.GetRasterLayersBottomToTop())
                        yield return descendant;
                }
            }
        }

        /// <summary>
        /// Gets all visible raster layers within this folder and descendants.
        /// </summary>
        /// <returns>An enumerable of visible raster layers for rendering.</returns>
        /// <remarks>
        /// Respects both the folder's visibility and each child's visibility. A layer is only
        /// included if both it and all ancestor folders are visible.
        /// </remarks>
        public IEnumerable<RasterLayer> GetVisibleRasterLayers()
        {
            if (!Visible) yield break;

            foreach (var child in _children)
            {
                if (!child.Visible) continue;

                if (child is RasterLayer raster)
                {
                    yield return raster;
                }
                else if (child is LayerFolder folder)
                {
                    foreach (var descendant in folder.GetVisibleRasterLayers())
                        yield return descendant;
                }
            }
        }
    }
}