using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using PixlPunkt.Core.Animation;
using PixlPunkt.Core.Compositing.Helpers;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.History;
using PixlPunkt.Core.Imaging;
using PixlPunkt.Core.Reference;
using PixlPunkt.Core.Tile;
using Windows.Graphics;
using PixlPunkt.Core.Logging;

namespace PixlPunkt.Core.Document
{
    /// <summary>
    /// Represents a multi-layer pixel art canvas with tile grid support and event-driven updates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// CanvasDocument is the central document model managing layers, compositing, and document-level
    /// operations. It maintains a layer stack, tracks the active layer for editing, and provides
    /// events for UI synchronization when structure or content changes.
    /// </para>
    /// <para>
    /// The document has both pixel dimensions (actual drawable area) and tile dimensions (logical grid
    /// for tile-based editing). Layers are stored in bottom-to-top order internally, but can be
    /// converted to UI order (top-to-bottom) using <see cref="InternalToUiIndex"/>.
    /// </para>
    /// <para>
    /// All structure-modifying operations (add, remove, reorder layers) fire <see cref="BeforeStructureChanged"/>
    /// before the change and <see cref="StructureChanged"/> after, allowing observers to snapshot state
    /// or refresh UI.
    /// </para>
    /// <para><strong>Folder Support:</strong></para>
    /// <para>
    /// Layers can be organized into folders. The flattened view (for UI display) is obtained via
    /// <see cref="GetFlattenedLayers"/>. Folders cascade their visibility and lock state to children
    /// without modifying individual layer properties (Photoshop-style).
    /// </para>
    /// <para><strong>Tile Support:</strong></para>
    /// <para>
    /// The document maintains a <see cref="TileSet"/> containing all unique tiles that can be
    /// placed on the canvas. Each layer can have its own <see cref="TileMapping"/> for independent
    /// tile arrangements.
    /// </para>
    /// </remarks>
    public sealed class CanvasDocument
    {
        /// <summary>
        /// Gets the master composited surface for the document.
        /// </summary>
        public PixelSurface Surface { get; }

        /// <summary>
        /// Gets the surface that should be used as the editing target.
        /// </summary>
        public PixelSurface TargetSurface => ActiveLayer?.Surface ?? Surface;

        public int PixelWidth { get; private set; }
        public int PixelHeight { get; private set; }
        public SizeInt32 TileSize { get; private set; }
        public SizeInt32 TileCounts { get; private set; }
        public string? Name { get; set; }

        /// <summary>
        /// Gets the unified history stack for all document operations (pixel changes, resize, etc.).
        /// </summary>
        public UnifiedHistoryStack History { get; } = new();

        /// <summary>
        /// Gets a value indicating whether the document has unsaved changes.
        /// </summary>
        public bool IsDirty => History.IsDirty;

        /// <summary>
        /// Marks the document as saved, clearing the dirty flag.
        /// </summary>
        /// <remarks>
        /// Call this after a successful save operation. The <see cref="IsDirty"/> property
        /// will return false until further changes are made.
        /// </remarks>
        public void MarkSaved() => History.MarkSaved();

        /// <summary>
        /// Gets the tile set containing all unique tiles for this document.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The tile set is created lazily when first accessed. Tiles are shared across all layers,
        /// but each layer has its own <see cref="TileMapping"/> determining where tiles are placed.
        /// </para>
        /// </remarks>
        public TileSet TileSet { get; private set; }

        /// <summary>
        /// Sets the tile set for this document.
        /// </summary>
        /// <param name="tileSet">The tile set to use.</param>
        /// <remarks>
        /// Used during document loading to restore the tile set from a saved file.
        /// </remarks>
        internal void SetTileSet(TileSet tileSet)
        {
            if (TileSet != null)
            {
                // Unhook old events
                TileSet.TileAdded -= OnTileSetChanged;
                TileSet.TileRemoved -= OnTileSetChanged;
                TileSet.TileUpdated -= OnTileSetChanged;
                TileSet.TileSetCleared -= OnTileSetCleared;
            }

            TileSet = tileSet;

            if (TileSet != null)
            {
                // Hook new events
                TileSet.TileAdded += OnTileSetChanged;
                TileSet.TileRemoved += OnTileSetChanged;
                TileSet.TileUpdated += OnTileSetChanged;
                TileSet.TileSetCleared += OnTileSetCleared;
            }

            TileSetChanged?.Invoke();
        }

        private void OnTileSetChanged(TileDefinition _) => TileSetChanged?.Invoke();
        private void OnTileSetChanged(int _) => TileSetChanged?.Invoke();
        private void OnTileSetCleared() => TileSetChanged?.Invoke();

        /// <summary>
        /// Occurs when the tile set changes (tile added, removed, or updated).
        /// </summary>
        public event Action? TileSetChanged;

        // Core storage: root-level items (layers and folders)
        private readonly List<LayerBase> _rootItems = [];
        private int _active = 0;
        private int _newLayerCounter = 1;
        private int _newFolderCounter = 1;

        /// <summary>
        /// Gets the read-only list of all layers (RasterLayer instances only, excluding folders).
        /// </summary>
        /// <remarks>
        /// For backward compatibility. Use <see cref="GetFlattenedLayers"/> for UI display including folders.
        /// </remarks>
        public IReadOnlyList<RasterLayer> Layers => GetAllRasterLayers();

        /// <summary>
        /// Gets all root-level items (layers and folders) in bottom-to-top order.
        /// </summary>
        public IReadOnlyList<LayerBase> RootItems => _rootItems;

        public int ActiveLayerIndex => _active;

        public RasterLayer? ActiveLayer
        {
            get
            {
                var rasters = GetFlattenedRasterLayers();
                if (_active < 0 || _active >= rasters.Count) return null;
                return rasters[_active];
            }
        }

        public event Action? BeforeStructureChanged;
        public event Action? StructureChanged;
        public event Action? LayersChanged;
        public event Action? ActiveLayerChanged;
        public event Action? DocumentModified;

        public CanvasDocument(string name, int pxW, int pxH, SizeInt32 tileSize, SizeInt32 tileCounts)
        {
            Name = name;
            PixelWidth = pxW;
            PixelHeight = pxH;
            TileSize = tileSize;
            TileCounts = tileCounts;

            Surface = new PixelSurface(pxW, pxH);
            Surface.Clear(0x00000000);

            // Enable memory management for history with 256MB limit
            // This allows automatic offloading of old history items to disk
            // while preserving full timeline for timelapse export
            History.EnableMemoryManagement(documentId: name);

            // Initialize tile set with document's tile dimensions
            TileSet = new TileSet(tileSize.Width, tileSize.Height);
            TileSet.TileAdded += _ => TileSetChanged?.Invoke();
            TileSet.TileRemoved += _ => TileSetChanged?.Invoke();
            TileSet.TileUpdated += _ => TileSetChanged?.Invoke();
            TileSet.TileSetCleared += () => TileSetChanged?.Invoke();

            _rootItems.Add(new RasterLayer(PixelWidth, PixelHeight, $"Layer {_newLayerCounter++}"));
            HookLayer(_rootItems[^1]);
            _active = 0;
        }

        /// <summary>
        /// Gets a flattened list of all layers and folders in bottom-to-top display order.
        /// </summary>
        /// <returns>
        /// A list containing all layers and folders, with folder children appearing after their parent.
        /// Used for UI display in ListView with indentation.
        /// </returns>
        public List<LayerBase> GetFlattenedLayers()
        {
            var result = new List<LayerBase>();
            foreach (var item in _rootItems)
            {
                if (item is LayerFolder folder)
                {
                    result.AddRange(folder.FlattenDepthFirst());
                }
                else
                {
                    result.Add(item);
                }
            }
            return result;
        }

        /// <summary>
        /// Gets all raster layers (excluding folders) in bottom-to-top rendering order.
        /// </summary>
        public List<RasterLayer> GetAllRasterLayers()
        {
            var result = new List<RasterLayer>();
            foreach (var item in _rootItems)
            {
                if (item is RasterLayer raster)
                {
                    result.Add(raster);
                }
                else if (item is LayerFolder folder)
                {
                    result.AddRange(folder.GetRasterLayersBottomToTop());
                }
            }
            return result;
        }

        /// <summary>
        /// Gets only raster layers (no folders) in flattened order.
        /// </summary>
        private List<RasterLayer> GetFlattenedRasterLayers()
        {
            return GetAllRasterLayers();
        }

        public void CompositeTo(PixelSurface destination)
        {
            var visibleRasters = new List<RasterLayer>();

            foreach (var item in _rootItems)
            {
                if (item is RasterLayer raster && raster.IsEffectivelyVisible())
                {
                    visibleRasters.Add(raster);
                }
                else if (item is LayerFolder folder && folder.IsEffectivelyVisible())
                {
                    visibleRasters.AddRange(folder.GetVisibleRasterLayers());
                }
            }

            Compositor.CompositeLinear(visibleRasters, destination);
        }

        public static bool IsEffectivelyLocked(RasterLayer layer) => layer?.IsEffectivelyLocked() == true;

        public bool IsEffectivelyLocked(int layerIndex)
        {
            var rasters = GetFlattenedRasterLayers();
            if (layerIndex < 0 || layerIndex >= rasters.Count) return false;
            return rasters[layerIndex].IsEffectivelyLocked();
        }

        public LayerBase DuplicateLayerTree(LayerBase src)
        {
            if (src == null) return null!;

            var clone = LayerCloneUtil.CloneLayerTree(src, this);
            var parent = src.Parent;

            int insertIndex = parent == null
                ? _rootItems.IndexOf(src) + 1
                : parent.IndexOfChild(src) + 1;

            RaiseBeforeStructureChanged();
            InsertLayerTreeWithoutHistory(clone, parent, insertIndex);
            LayersChanged?.Invoke();
            RaiseStructureChanged();

            History.Push(new LayerTreeAddItem(this, clone, parent, insertIndex, "Duplicate Layer"));

            // If you duplicated a raster, make it active
            if (clone is RasterLayer)
                SetActiveLayer(insertIndex);

            return clone;
        }
        internal void InsertLayerTreeWithoutHistory(LayerBase item, LayerFolder? parent, int index)
        {
            HookLayerTree(item);

            if (parent == null)
            {
                item.Parent = null;
                index = Math.Clamp(index, 0, _rootItems.Count);
                _rootItems.Insert(index, item);
            }
            else
            {
                index = Math.Clamp(index, 0, parent.Children.Count);
                parent.InsertChild(index, item);
            }
            LayersChanged?.Invoke();
        }

        internal void RemoveLayerTreeWithoutHistory(LayerBase item)
        {
            if (item.Parent == null)
                _rootItems.Remove(item);
            else
                item.Parent.RemoveChild(item);
            LayersChanged?.Invoke();
            UnhookLayerTree(item);
        }

        private void HookLayerTree(LayerBase item)
        {
            HookLayer(item);
            if (item is LayerFolder f)
                foreach (var c in f.Children)
                    HookLayerTree(c);
        }

        private void UnhookLayerTree(LayerBase item)
        {
            UnhookLayer(item);
            if (item is LayerFolder f)
                foreach (var c in f.Children)
                    UnhookLayerTree(c);
        }


        public void MoveRootItemWithHistory(LayerBase item, int newIndex)
        {
            if (item == null || item.Parent != null) return;

            int oldIndex = _rootItems.IndexOf(item);
            if (oldIndex < 0) return;

            newIndex = Math.Clamp(newIndex, 0, _rootItems.Count - 1);
            if (oldIndex == newIndex) return;

            RaiseBeforeStructureChanged();

            _rootItems.RemoveAt(oldIndex);

            // If moving toward top (higher index) after removal, index shifts down by 1
            if (newIndex > oldIndex) newIndex--;

            _rootItems.Insert(newIndex, item);

            // Push appropriate history item based on item type
            if (item is RasterLayer rl)
            {
                History.Push(new LayerReorderItem(this, rl, oldIndex, newIndex));
            }
            else if (item is LayerFolder folder)
            {
                History.Push(new FolderReorderItem(this, folder, oldIndex, newIndex));
            }

            LayersChanged?.Invoke();
            RaiseStructureChanged();
        }


        public void ShowAllLayers()
        {
            RaiseBeforeStructureChanged();
            var allItems = GetFlattenedLayers();
            foreach (var item in allItems)
                item.Visible = true;
            LayersChanged?.Invoke();
            RaiseStructureChanged();
        }

        public void Solo(RasterLayer target)
        {
            if (target == null) return;
            RaiseBeforeStructureChanged();
            var allRasters = GetAllRasterLayers();
            foreach (var l in allRasters)
                l.Visible = ReferenceEquals(l, target);
            LayersChanged?.Invoke();
            RaiseStructureChanged();
        }

        public int AddLayer(string? name = null, int? insertAt = null)
        {
            RaiseBeforeStructureChanged();

            string nm = name ?? $"Layer {_newLayerCounter++}";
            var layer = new RasterLayer(PixelWidth, PixelHeight, nm);
            HookLayer(layer);

            // Determine target folder based on active layer's parent
            var activeLayer = ActiveLayer;
            LayerFolder? targetFolder = activeLayer?.Parent;

            if (targetFolder != null)
            {
                // Insert into the same folder as the active layer, after the active layer
                int activeIdx = targetFolder.IndexOfChild(activeLayer!);
                int insertIdx = insertAt ?? (activeIdx + 1);
                insertIdx = Math.Clamp(insertIdx, 0, targetFolder.Children.Count);
                targetFolder.InsertChild(insertIdx, layer);

                // Push to unified history with folder context
                History.Push(new LayerTreeAddItem(this, layer, targetFolder, insertIdx, "Add Layer"));
            }
            else
            {
                // Insert at root level
                int at = insertAt ?? (_active + 1);
                at = Math.Clamp(at, 0, _rootItems.Count);
                _rootItems.Insert(at, layer);

                // Push to unified history
                History.Push(new LayerAddItem(this, layer, at));
            }

            var rasters = GetFlattenedRasterLayers();
            _active = rasters.IndexOf(layer);
            if (_active < 0) _active = 0;

            LayersChanged?.Invoke();
            ActiveLayerChanged?.Invoke();
            RaiseStructureChanged();
            return _active;
        }

        /// <summary>
        /// Adds a layer without pushing to history (used by undo/redo).
        /// </summary>
        internal void AddLayerWithoutHistory(RasterLayer layer, int insertAt)
        {
            RaiseBeforeStructureChanged();

            int at = Math.Clamp(insertAt, 0, _rootItems.Count);
            HookLayer(layer);
            _rootItems.Insert(at, layer);

            var rasters = GetFlattenedRasterLayers();
            _active = rasters.IndexOf(layer);
            if (_active < 0) _active = 0;

            // Recomposite after structural change
            CompositeTo(Surface);

            LayersChanged?.Invoke();
            ActiveLayerChanged?.Invoke();
            RaiseStructureChanged();
        }

        /// <summary>
        /// Creates a new folder. If the active layer is inside a folder, the new folder
        /// is created inside that same parent folder. Otherwise, creates at root level.
        /// </summary>
        public LayerFolder AddFolder(string? name = null, int? insertAt = null)
        {
            RaiseBeforeStructureChanged();

            string nm = name ?? $"Folder {_newFolderCounter++}";
            var folder = new LayerFolder(nm);
            HookLayer(folder);

            // Determine target folder based on active layer's parent
            var activeLayer = ActiveLayer;
            LayerFolder? targetFolder = activeLayer?.Parent;

            if (targetFolder != null)
            {
                // Insert into the same folder as the active layer, after the active layer
                int activeIdx = targetFolder.IndexOfChild(activeLayer!);
                int insertIdx = insertAt ?? (activeIdx + 1);
                insertIdx = Math.Clamp(insertIdx, 0, targetFolder.Children.Count);
                targetFolder.InsertChild(insertIdx, folder);

                // Push to unified history with folder context
                History.Push(new FolderAddItem(this, folder, targetFolder, insertIdx));
            }
            else
            {
                // Insert at root level
                int at = insertAt ?? _rootItems.Count;
                at = Math.Clamp(at, 0, _rootItems.Count);
                _rootItems.Insert(at, folder);

                // Push to unified history
                History.Push(new FolderAddItem(this, folder, null, at));
            }

            LayersChanged?.Invoke();
            RaiseStructureChanged();
            return folder;
        }

        /// <summary>
        /// Adds a folder directly without history and without considering the active layer's parent.
        /// Used during document loading to ensure folders are placed exactly where specified.
        /// </summary>
        /// <param name="name">The folder name.</param>
        /// <param name="insertAt">The index at which to insert in root items.</param>
        /// <returns>The created folder.</returns>
        internal LayerFolder AddFolderAtRootWithoutHistory(string name, int insertAt)
        {
            RaiseBeforeStructureChanged();

            var folder = new LayerFolder(name);
            HookLayer(folder);

            // Always insert at root level, ignoring active layer's parent
            insertAt = Math.Clamp(insertAt, 0, _rootItems.Count);
            _rootItems.Insert(insertAt, folder);

            LayersChanged?.Invoke();
            RaiseStructureChanged();
            return folder;
        }

        /// <summary>
        /// Moves a layer into a folder or back to root, with history support.
        /// </summary>
        /// <param name="layer">The layer to move.</param>
        /// <param name="targetFolder">The target folder, or null to move to root.</param>
        /// <param name="targetIndex">Optional target index within the folder or root. If null, appends to the end.</param>
        public void MoveLayerToFolder(LayerBase layer, LayerFolder? targetFolder, int? targetIndex = null)
        {
            if (layer == null) return;

            // Capture original state for history
            var originalParent = layer.Parent;
            int originalIndex;
            if (originalParent != null)
            {
                originalIndex = originalParent.IndexOfChild(layer);
            }
            else
            {
                originalIndex = _rootItems.IndexOf(layer);
            }

            LoggingService.Info("MoveLayerToFolder: Moving '{LayerName}' from parent='{OriginalParent}' (index {OriginalIndex}) to targetFolder='{TargetFolder}'",
                layer.Name ?? "unnamed",
                originalParent?.Name ?? "root",
                originalIndex,
                targetFolder?.Name ?? "root");

            RaiseBeforeStructureChanged();

            // Remove from current parent
            if (layer.Parent != null)
            {
                var parentBeforeRemove = layer.Parent;
                bool removed = layer.Parent.RemoveChild(layer);
                LoggingService.Info("MoveLayerToFolder: RemoveChild returned {Result}, folder '{Name}' now has {Count} children", 
                    removed, parentBeforeRemove.Name, parentBeforeRemove.Children.Count);
            }
            else
            {
                _rootItems.Remove(layer);
                LoggingService.Info("MoveLayerToFolder: Removed from root items");
            }

            // Add to new parent
            int newIndex;
            if (targetFolder != null)
            {
                newIndex = targetIndex ?? targetFolder.Children.Count;
                newIndex = Math.Clamp(newIndex, 0, targetFolder.Children.Count);
                targetFolder.InsertChild(newIndex, layer);
                LoggingService.Info("MoveLayerToFolder: Inserted into folder '{FolderName}' at index {Index}", targetFolder.Name ?? "unnamed", newIndex);
            }
            else
            {
                // Moving to root - explicitly clear Parent
                layer.Parent = null;
                newIndex = targetIndex ?? _rootItems.Count;
                newIndex = Math.Clamp(newIndex, 0, _rootItems.Count);
                _rootItems.Insert(newIndex, layer);
                LoggingService.Info("MoveLayerToFolder: Inserted into root at index {Index}, _rootItems.Count now = {Count}", newIndex, _rootItems.Count);
            }

            // Push to history
            History.Push(new LayerMoveToFolderItem(this, layer, originalParent, originalIndex, targetFolder, newIndex));

            LayersChanged?.Invoke();
            RaiseStructureChanged();
        }

        /// <summary>
        /// Moves a layer into a folder or back to root without pushing to history (used by undo/redo).
        /// </summary>
        /// <param name="layer">The layer to move.</param>
        /// <param name="targetFolder">The target folder, or null to move to root.</param>
        /// <param name="targetIndex">The target index within the folder or root.</param>
        internal void MoveLayerToFolderWithoutHistory(LayerBase layer, LayerFolder? targetFolder, int targetIndex)
        {
            if (layer == null) return;

            RaiseBeforeStructureChanged();

            // Remove from current parent
            if (layer.Parent != null)
            {
                layer.Parent.RemoveChild(layer);
            }
            else
            {
                _rootItems.Remove(layer);
            }

            // Add to new parent at specific index
            if (targetFolder != null)
            {
                targetIndex = Math.Clamp(targetIndex, 0, targetFolder.Children.Count);
                targetFolder.InsertChild(targetIndex, layer);
            }
            else
            {
                // Moving to root - explicitly clear Parent
                layer.Parent = null;
                targetIndex = Math.Clamp(targetIndex, 0, _rootItems.Count);
                _rootItems.Insert(targetIndex, layer);
            }

            // Recomposite after structural change
            CompositeTo(Surface);

            LayersChanged?.Invoke();
            RaiseStructureChanged();
        }

        /// <summary>
        /// Moves a root-level item to a new position in the root collection.
        /// </summary>
        public void MoveRootItem(LayerBase item, int newIndex)
        {
            if (item == null || item.Parent != null) return;

            int oldIndex = _rootItems.IndexOf(item);
            if (oldIndex < 0) return;

            newIndex = Math.Clamp(newIndex, 0, _rootItems.Count - 1);
            if (oldIndex == newIndex) return;

            RaiseBeforeStructureChanged();

            _rootItems.RemoveAt(oldIndex);
            _rootItems.Insert(newIndex, item);

            LayersChanged?.Invoke();
            RaiseStructureChanged();
        }

        public void RemoveLayer(int index)
        {
            var rasters = GetFlattenedRasterLayers();
            if (rasters.Count <= 1) return;

            RaiseBeforeStructureChanged();

            index = Math.Clamp(index, 0, rasters.Count - 1);
            var removed = rasters[index];

            // Find the index in root items for history
            int rootIndex = _rootItems.IndexOf(removed);

            UnhookLayer(removed);

            // Remove from parent or root
            if (removed.Parent != null)
            {
                removed.Parent.RemoveChild(removed);
            }
            else
            {
                _rootItems.Remove(removed);
            }

            // Adjust active index
            var newRasters = GetFlattenedRasterLayers();
            _active = Math.Clamp(_active, 0, Math.Max(0, newRasters.Count - 1));

            // Push to unified history
            History.Push(new LayerRemoveItem(this, removed, rootIndex >= 0 ? rootIndex : index));

            LayersChanged?.Invoke();
            ActiveLayerChanged?.Invoke();
            RaiseStructureChanged();
        }

        /// <summary>
        /// Removes a layer without pushing to history (used by undo/redo).
        /// </summary>
        /// <param name="layer">The layer to remove.</param>
        /// <param name="allowRemoveLast">If true, allows removing even if it's the last layer (for redo operations).</param>
        internal void RemoveLayerWithoutHistory(RasterLayer layer, bool allowRemoveLast = false)
        {
            if (layer == null) return;
            if (!allowRemoveLast && GetAllRasterLayers().Count <= 1) return;

            RaiseBeforeStructureChanged();

            UnhookLayer(layer);

            if (layer.Parent != null)
            {
                layer.Parent.RemoveChild(layer);
            }
            else
            {
                _rootItems.Remove(layer);
            }

            var newRasters = GetFlattenedRasterLayers();
            _active = Math.Clamp(_active, 0, Math.Max(0, newRasters.Count - 1));

            // Recomposite after structural change
            CompositeTo(Surface);

            LayersChanged?.Invoke();
            ActiveLayerChanged?.Invoke();
            RaiseStructureChanged();
        }

        /// <summary>
        /// Removes a folder or layer from the document.
        /// </summary>
        public void RemoveItem(LayerBase item)
        {
            if (item == null) return;

            // Don't allow removing last raster layer
            if (item is RasterLayer && GetAllRasterLayers().Count <= 1) return;

            RaiseBeforeStructureChanged();
            UnhookLayer(item);

            if (item.Parent != null)
            {
                item.Parent.RemoveChild(item);
            }
            else
            {
                _rootItems.Remove(item);
            }

            // If we removed a folder, orphan its children to root
            if (item is LayerFolder folder)
            {
                foreach (var child in folder.Children.ToList())
                {
                    folder.RemoveChild(child);
                    _rootItems.Add(child);
                }
            }

            var newRasters = GetFlattenedRasterLayers();
            _active = Math.Clamp(_active, 0, Math.Max(0, newRasters.Count - 1));

            LayersChanged?.Invoke();
            ActiveLayerChanged?.Invoke();
            RaiseStructureChanged();
        }

        public void MoveLayer(int from, int to)
        {
            var rasters = GetFlattenedRasterLayers();
            if (rasters.Count <= 1) return;

            from = Math.Clamp(from, 0, rasters.Count - 1);
            to = Math.Clamp(to, 0, rasters.Count - 1);
            if (from == to) return;

            var activeLayer = ActiveLayer;
            var layer = rasters[from];

            // Only support root-level moves for now
            if (layer.Parent != null || !_rootItems.Contains(layer)) return;

            int fromRootIdx = _rootItems.IndexOf(layer);
            int toRootIdx = Math.Clamp(to, 0, _rootItems.Count - 1);
            if (fromRootIdx == toRootIdx) return;

            RaiseBeforeStructureChanged();

            _rootItems.RemoveAt(fromRootIdx);
            _rootItems.Insert(toRootIdx, layer);

            // Push to unified history with root indices
            if (layer is RasterLayer rl)
            {
                History.Push(new LayerReorderItem(this, rl, fromRootIdx, toRootIdx));
            }

            _active = GetFlattenedRasterLayers().IndexOf(activeLayer);
            if (_active < 0) _active = 0;

            LayersChanged?.Invoke();
            ActiveLayerChanged?.Invoke();
            RaiseStructureChanged();
        }

        /// <summary>
        /// Moves a layer by reference to a specific root index without pushing to history (used by undo/redo).
        /// </summary>
        /// <param name="layer">The layer to move.</param>
        /// <param name="targetRootIndex">The target index in the root items list where the layer should end up.</param>
        internal void MoveLayerByReferenceWithoutHistory(RasterLayer layer, int targetRootIndex)
        {
            if (layer == null) return;
            if (layer.Parent != null) return; // Only support root-level layers

            int currentIndex = _rootItems.IndexOf(layer);
            if (currentIndex < 0) return; // Layer not found

            // Clamp target to valid range
            int maxIndex = _rootItems.Count - 1;
            targetRootIndex = Math.Clamp(targetRootIndex, 0, maxIndex);

            if (currentIndex == targetRootIndex) return;

            RaiseBeforeStructureChanged();

            var activeLayer = ActiveLayer;

            // Remove from current position
            _rootItems.RemoveAt(currentIndex);

            // Insert at target position (see detailed comments above for why this is correct)
            _rootItems.Insert(targetRootIndex, layer);

            _active = GetFlattenedRasterLayers().IndexOf(activeLayer);
            if (_active < 0) _active = 0;

            // Recomposite after structural change
            CompositeTo(Surface);

            LayersChanged?.Invoke();
            ActiveLayerChanged?.Invoke();
            RaiseStructureChanged();
        }

        /// <summary>
        /// Moves a layer without pushing to history (used by undo/redo).
        /// </summary>
        /// <param name="from">The current index of the layer.</param>
        /// <param name="to">The target index for the layer.</param>
        internal void MoveLayerWithoutHistory(int from, int to)
        {
            var rasters = GetFlattenedRasterLayers();
            if (rasters.Count <= 1) return;

            from = Math.Clamp(from, 0, rasters.Count - 1);
            to = Math.Clamp(to, 0, rasters.Count - 1);
            if (from == to) return;

            RaiseBeforeStructureChanged();

            var activeLayer = ActiveLayer;
            var layer = rasters[from];

            if (layer.Parent == null && _rootItems.Contains(layer))
            {
                int fromIdx = _rootItems.IndexOf(layer);
                int toIdx = Math.Clamp(to, 0, _rootItems.Count - 1);

                _rootItems.RemoveAt(fromIdx);
                _rootItems.Insert(toIdx, layer);
            }

            _active = GetFlattenedRasterLayers().IndexOf(activeLayer);
            if (_active < 0) _active = 0;

            LayersChanged?.Invoke();
            ActiveLayerChanged?.Invoke();
            RaiseStructureChanged();
        }

        public void SetActiveLayer(int index)
        {
            var rasters = GetFlattenedRasterLayers();
            int clamped = Math.Clamp(index, 0, Math.Max(0, rasters.Count - 1));
            if (clamped == _active) return;
            _active = clamped;
            ActiveLayerChanged?.Invoke();
        }

        public IEnumerable<RasterLayer> VisibleBottomToTop()
        {
            return GetAllRasterLayers().Where(l => l.IsEffectivelyVisible());
        }

        public int UiToInternalIndex(int topListIndex)
        {
            var rasters = GetFlattenedRasterLayers();
            return (rasters.Count - 1) - topListIndex;
        }

        public int InternalToUiIndex(int internalIndex)
        {
            var rasters = GetFlattenedRasterLayers();
            return (rasters.Count - 1) - internalIndex;
        }

        public void RaiseBeforeStructureChanged() => BeforeStructureChanged?.Invoke();
        public void RaiseStructureChanged() => StructureChanged?.Invoke();

        public void RaiseDocumentModified() => DocumentModified?.Invoke();

        /// <summary>
        /// Updates the tile counts metadata without resizing the canvas.
        /// </summary>
        /// <param name="newTileCounts">The new tile counts.</param>
        public void SetTileCounts(SizeInt32 newTileCounts)
        {
            TileCounts = newTileCounts;
        }

        /// <summary>
        /// Restores document dimensions directly (used by undo/redo).
        /// Does NOT modify layer content - that must be done separately.
        /// </summary>
        /// <param name="width">The pixel width to restore.</param>
        /// <param name="height">The pixel height to restore.</param>
        /// <param name="tileCounts">The tile counts to restore.</param>
        internal void RestoreDimensions(int width, int height, SizeInt32 tileCounts)
        {
            PixelWidth = width;
            PixelHeight = height;
            TileCounts = tileCounts;
        }

        /// <summary>
        /// Resizes the canvas to new dimensions, repositioning existing content based on offset.
        /// </summary>
        /// <param name="newWidth">New width in pixels.</param>
        /// <param name="newHeight">New height in pixels.</param>
        /// <param name="offsetX">Horizontal offset for existing content (where to place old content in new canvas).</param>
        /// <param name="offsetY">Vertical offset for existing content (where to place old content in new canvas).</param>
        /// <remarks>
        /// <para>
        /// This method resizes all layer surfaces and the composite surface, copying existing pixel
        /// data to the new surfaces at the specified offset. Pixels outside the old bounds are
        /// initialized to transparent (0x00000000).
        /// </para>
        /// <para>
        /// The offset determines where the top-left corner of the old content appears in the new canvas:
        /// - Positive offset: old content shifted right/down (space added at left/top)
        /// - Negative offset: old content shifted left/up (content cropped from left/top)
        /// - Zero offset: old content stays at top-left corner
        /// </para>
        /// </remarks>
        public void ResizeCanvas(int newWidth, int newHeight, int offsetX, int offsetY)
        {
            if (newWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(newWidth), "Canvas width must be positive.");
            if (newHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(newHeight), "Canvas height must be positive.");

            // No change needed
            if (newWidth == PixelWidth && newHeight == PixelHeight && offsetX == 0 && offsetY == 0)
                return;

            RaiseBeforeStructureChanged();

            int oldWidth = PixelWidth;
            int oldHeight = PixelHeight;

            // Resize all raster layers
            foreach (var layer in GetAllRasterLayers())
            {
                ResizeLayerSurface(layer, oldWidth, oldHeight, newWidth, newHeight, offsetX, offsetY);
            }

            // Resize the composite surface
            ResizeSurface(Surface, oldWidth, oldHeight, newWidth, newHeight, offsetX, offsetY);

            // Update document dimensions
            PixelWidth = newWidth;
            PixelHeight = newHeight;

            // Recomposite
            CompositeTo(Surface);

            LayersChanged?.Invoke();
            RaiseStructureChanged();
        }

        /// <summary>
        /// Resizes a layer's surface, copying existing pixels to the new location.
        /// </summary>
        private static void ResizeLayerSurface(RasterLayer layer, int oldW, int oldH, int newW, int newH, int offsetX, int offsetY)
        {
            var oldSurface = layer.Surface;
            var newSurface = new PixelSurface(newW, newH);
            newSurface.Clear(0x00000000);

            CopyPixelsWithOffset(oldSurface.Pixels, oldW, oldH, newSurface.Pixels, newW, newH, offsetX, offsetY);

            // Replace the layer's surface
            layer.ReplaceSurface(newSurface);
            layer.UpdatePreview();
        }

        /// <summary>
        /// Resizes a PixelSurface in place by creating a new buffer and copying pixels.
        /// </summary>
        private static void ResizeSurface(PixelSurface surface, int oldW, int oldH, int newW, int newH, int offsetX, int offsetY)
        {
            var newPixels = new byte[newW * newH * 4];
            Array.Clear(newPixels, 0, newPixels.Length);

            CopyPixelsWithOffset(surface.Pixels, oldW, oldH, newPixels, newW, newH, offsetX, offsetY);

            // PixelSurface needs to be resized - we'll need to use reflection or add a method
            surface.Resize(newW, newH, newPixels);
        }

        /// <summary>
        /// Copies pixels from source to destination with an offset.
        /// </summary>
        /// <param name="src">Source pixel buffer (BGRA).</param>
        /// <param name="srcW">Source width.</param>
        /// <param name="srcH">Source height.</param>
        /// <param name="dst">Destination pixel buffer (BGRA).</param>
        /// <param name="dstW">Destination width.</param>
        /// <param name="dstH">Destination height.</param>
        /// <param name="offsetX">X offset in destination for source origin.</param>
        /// <param name="offsetY">Y offset in destination for source origin.</param>
        private static void CopyPixelsWithOffset(byte[] src, int srcW, int srcH, byte[] dst, int dstW, int dstH, int offsetX, int offsetY)
        {
            // Calculate the overlapping region
            int srcStartX = Math.Max(0, -offsetX);
            int srcStartY = Math.Max(0, -offsetY);
            int dstStartX = Math.Max(0, offsetX);
            int dstStartY = Math.Max(0, offsetY);

            int copyWidth = Math.Min(srcW - srcStartX, dstW - dstStartX);
            int copyHeight = Math.Min(srcH - srcStartY, dstH - dstStartY);

            if (copyWidth <= 0 || copyHeight <= 0)
                return; // No overlap

            int srcStride = srcW * 4;
            int dstStride = dstW * 4;
            int rowBytes = copyWidth * 4;

            for (int y = 0; y < copyHeight; y++)
            {
                int srcOffset = (srcStartY + y) * srcStride + srcStartX * 4;
                int dstOffset = (dstStartY + y) * dstStride + dstStartX * 4;
                Buffer.BlockCopy(src, srcOffset, dst, dstOffset, rowBytes);
            }
        }

        public bool CanMergeDown(RasterLayer top)
        {
            if (top.Parent is LayerFolder pf)
            {
                int idx = pf.IndexOfChild(top);
                return idx > 0 && pf.Children[idx - 1] is RasterLayer;
            }
            else
            {
                int idx = -1;
                for (int i = 0; i < RootItems.Count; i++)
                {
                    if (ReferenceEquals(RootItems[i], top))
                    {
                        idx = i;
                        break;
                    }
                }
                return idx > 0 && RootItems[idx - 1] is RasterLayer;
            }
        }

        public bool MergeDown(RasterLayer top)
        {
            if (!CanMergeDown(top))
                return false;

            LayerFolder? parent = top.Parent;
            IList<LayerBase> siblings = (IList<LayerBase>)(parent != null ? parent.Children : RootItems);

            int topIdx = siblings.IndexOf(top);
            if (siblings[topIdx - 1] is not RasterLayer below) return false;

            RaiseBeforeStructureChanged();

            // Composite the two layers onto a fresh surface (bakes effects + blend between them)
            var merged = new RasterLayer(PixelWidth, PixelHeight)
            {
                Name = below.Name,
                Visible = true,
                Locked = false,
                Blend = BlendMode.Normal,
                Opacity = 255
            };

            // IMPORTANT: order is bottom->top
            Compositor.CompositeLinear([below, top], merged.Surface);

            // Remove top first (higher index), then below
            RemoveLayerTreeWithoutHistory(top);
            RemoveLayerTreeWithoutHistory(below);

            // Insert merged at below’s index
            InsertLayerTreeWithoutHistory(merged, parent, topIdx - 1);

            // Make merged active
            var flat = GetAllRasterLayers();
            int mergedIdx = flat.IndexOf(merged);
            if (mergedIdx >= 0)
                SetActiveLayer(mergedIdx);

            RaiseStructureChanged();
            History.Push(new LayerMergeDownItem(this, top, below, merged, topIdx - 1, $"Merged Layer: {top.Name} onto {below.Name} creating {merged.Name}"));
            return true;
        }

        public static bool CanFlattenFolderVisible(LayerFolder folder)
        {
            // visible-only flatten: if folder has any visible rasters inside
            return folder.GetVisibleRasterLayers().Any();
        }

        public bool FlattenFolderVisible(LayerFolder folder)
        {
            var visibleRasters = folder.GetVisibleRasterLayers().ToList();
            if (visibleRasters.Count == 0)
                return false;

            LayerFolder? parent = folder.Parent;
            IList<LayerBase> siblings = (IList<LayerBase>)(parent != null ? parent.Children : RootItems);
            int folderIdx = siblings.IndexOf(folder);

            RaiseBeforeStructureChanged();

            var flattened = new RasterLayer(PixelWidth, PixelHeight)
            {
                Name = $"{folder.Name} (Flattened)",
                Visible = folder.Visible,
                Locked = folder.Locked,
                Blend = BlendMode.Normal,
                Opacity = 255
            };

            // Composite *visible only* layers in correct order bottom->top
            Compositor.CompositeLinear(visibleRasters, flattened.Surface);

            // Replace folder node with flattened layer
            RemoveLayerTreeWithoutHistory(folder);
            InsertLayerTreeWithoutHistory(flattened, parent, folderIdx);

            var flat = GetAllRasterLayers();
            int idx = flat.IndexOf(flattened);
            if (idx >= 0)
                SetActiveLayer(idx);

            RaiseStructureChanged();
            History.Push(new FlattenFolderItem(this, folder, flattened, folderIdx, $"Flattened Folder {folder.Name} to {flattened.Name}"));
            return true;
        }

        private void HookLayer(LayerBase l) => l.PropertyChanged += Layer_PropertyChanged;
        private void UnhookLayer(LayerBase l) => l.PropertyChanged -= Layer_PropertyChanged;

        private void Layer_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LayerBase.Visible) ||
                e.PropertyName == nameof(LayerBase.Locked))
            {
                RaiseStructureChanged();
            }
            else if (e.PropertyName == nameof(LayerBase.Name))
            {
                LayersChanged?.Invoke();
            }
            // RasterLayer-specific
            else if (sender is RasterLayer &&
                     (e.PropertyName == nameof(RasterLayer.Opacity) ||
                      e.PropertyName == nameof(RasterLayer.Blend)))
            {
                RaiseStructureChanged();
            }
            // ReferenceLayer-specific
            else if (sender is ReferenceLayer &&
                     (e.PropertyName == nameof(ReferenceLayer.Opacity) ||
                      e.PropertyName == nameof(ReferenceLayer.PositionX) ||
                      e.PropertyName == nameof(ReferenceLayer.PositionY) ||
                      e.PropertyName == nameof(ReferenceLayer.Scale) ||
                      e.PropertyName == nameof(ReferenceLayer.Rotation)))
            {
                RaiseStructureChanged();
            }
            // Folder expand/collapse
            else if (sender is LayerFolder && e.PropertyName == nameof(LayerFolder.IsExpanded))
            {
                LayersChanged?.Invoke();
            }
        }

        /// <summary>
        /// Gets the tile animation state for this document.
        /// </summary>
        /// <remarks>
        /// Manages animation reels, playback, and frame selection for tile-based animations.
        /// </remarks>
        public TileAnimationState TileAnimationState { get; } = new();

        /// <summary>
        /// Gets the canvas animation state for this document.
        /// </summary>
        /// <remarks>
        /// Manages layer-based animation (Aseprite-style) with keyframes controlling
        /// layer visibility, opacity, blend mode, and pixel data per frame.
        /// </remarks>
        public CanvasAnimationState CanvasAnimationState { get; } = new();

        /// <summary>
        /// Gets the reference image service for this document.
        /// </summary>
        /// <remarks>
        /// Manages reference image overlays that can be displayed on the canvas
        /// for artistic reference while drawing. Reference images are not part of
        /// the layer stack and are not exported with the final image.
        /// </remarks>
        public ReferenceImageService ReferenceImages { get; } = new();

        /// <summary>
        /// Gets or sets the currently selected tile ID for animation editing.
        /// </summary>
        public int SelectedTileId { get; set; } = -1;

        // ════════════════════════════════════════════════════════════════════
        // REFERENCE LAYER SUPPORT
        // ════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Adds a reference layer to the document.
        /// </summary>
        /// <param name="name">The layer name.</param>
        /// <param name="pixels">Pixel data in BGRA format.</param>
        /// <param name="width">Image width.</param>
        /// <param name="height">Image height.</param>
        /// <param name="filePath">Optional source file path.</param>
        /// <returns>The created reference layer.</returns>
        public ReferenceLayer AddReferenceLayer(string name, byte[] pixels, int width, int height, string? filePath = null)
        {
            RaiseBeforeStructureChanged();

            var layer = new ReferenceLayer(name, pixels, width, height, filePath);
            HookLayer(layer);

            // Insert above the active layer or at the top
            int insertAt = _active + 1;
            insertAt = Math.Clamp(insertAt, 0, _rootItems.Count);
            _rootItems.Insert(insertAt, layer);

            // Center the reference on the canvas by default
            layer.FitToCanvas(PixelWidth, PixelHeight, 0.05f);

            History.Push(new ReferenceLayerAddItem(this, layer, insertAt));

            LayersChanged?.Invoke();
            RaiseStructureChanged();

            return layer;
        }

        /// <summary>
        /// Removes a reference layer from the document.
        /// </summary>
        /// <param name="layer">The reference layer to remove.</param>
        public void RemoveReferenceLayer(ReferenceLayer layer)
        {
            if (layer == null) return;

            int index = _rootItems.IndexOf(layer);
            if (index < 0) return;

            RaiseBeforeStructureChanged();

            UnhookLayer(layer);
            _rootItems.Remove(layer);

            History.Push(new ReferenceLayerRemoveItem(this, layer, index));

            LayersChanged?.Invoke();
            RaiseStructureChanged();
        }

        /// <summary>
        /// Removes a reference layer without pushing to history (used by undo/redo).
        /// </summary>
        internal void RemoveReferenceLayerWithoutHistory(ReferenceLayer layer)
        {
            if (layer == null) return;

            RaiseBeforeStructureChanged();

            UnhookLayer(layer);

            if (layer.Parent != null)
                layer.Parent.RemoveChild(layer);
            else
                _rootItems.Remove(layer);

            LayersChanged?.Invoke();
            RaiseStructureChanged();
        }

        /// <summary>
        /// Adds a reference layer without pushing to history (used by undo/redo).
        /// </summary>
        internal void AddReferenceLayerWithoutHistory(ReferenceLayer layer, int insertAt)
        {
            RaiseBeforeStructureChanged();

            HookLayer(layer);
            insertAt = Math.Clamp(insertAt, 0, _rootItems.Count);
            _rootItems.Insert(insertAt, layer);

            LayersChanged?.Invoke();
            RaiseStructureChanged();
        }

        /// <summary>
        /// Gets all reference layers in the document.
        /// </summary>
        /// <returns>List of reference layers in bottom-to-top order.</returns>
        public List<ReferenceLayer> GetAllReferenceLayers()
        {
            var result = new List<ReferenceLayer>();
            foreach (var item in _rootItems)
            {
                if (item is ReferenceLayer refLayer)
                {
                    result.Add(refLayer);
                }
                else if (item is LayerFolder folder)
                {
                    CollectReferenceLayers(folder, result);
                }
            }
            return result;
        }

        private static void CollectReferenceLayers(LayerFolder folder, List<ReferenceLayer> result)
        {
            foreach (var child in folder.Children)
            {
                if (child is ReferenceLayer refLayer)
                    result.Add(refLayer);
                else if (child is LayerFolder subFolder)
                    CollectReferenceLayers(subFolder, result);
            }
        }
    }
}
