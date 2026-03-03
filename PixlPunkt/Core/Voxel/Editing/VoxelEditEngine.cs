using System;
using System.Collections.Generic;
using PixlPunkt.Core.Document;
using PixlPunkt.PluginSdk.Voxel;

namespace PixlPunkt.Core.Voxel.Editing
{
    /// <summary>
    /// Core voxel edit engine for canonical voxel model operations (Phase 1 scaffold).
    /// </summary>
    public sealed class VoxelEditEngine
    {
        private readonly VoxelModelDocumentState _model;

        public VoxelEditEngine(VoxelModelDocumentState model)
        {
            _model = model ?? throw new ArgumentNullException(nameof(model));
            Selection = new VoxelSelectionService();
            History = new VoxelCommandHistory();

            Selection.SelectionChanged += () => SelectionChanged?.Invoke();
            History.HistoryChanged += () => HistoryChanged?.Invoke();
        }

        public VoxelModelDocumentState Model => _model;

        public VoxelSelectionService Selection { get; }

        public VoxelCommandHistory History { get; }

        public event Action? ModelChanged;
        public event Action? SelectionChanged;
        public event Action? HistoryChanged;

        public bool HasModel => _model.HasModel && _model.IsStorageValid;

        public void InitializeModel(int width, int height, int depth, VoxelModelSourceKind sourceKind = VoxelModelSourceKind.Manual)
        {
            var before = _model.Clone();
            _model.Initialize(width, height, depth);
            _model.SourceKind = sourceKind;
            _model.DirtyFromSource = false;
            _model.LastGeneratedUtcTicks = DateTime.UtcNow.Ticks;
            PushModelSnapshotCommand("Initialize Voxel Model", before, _model.Clone());
            ModelChanged?.Invoke();
        }

        public void ReplaceModelFromVolume(VoxelVolume volume, VoxelModelSourceKind sourceKind = VoxelModelSourceKind.TileOrthoGenerated)
        {
            if (volume == null) throw new ArgumentNullException(nameof(volume));

            var before = _model.Clone();
            _model.SourceKind = sourceKind;
            _model.DirtyFromSource = false;
            _model.SetFromVoxelVolume(volume);
            PushModelSnapshotCommand("Rebuild Voxel Model", before, _model.Clone());
            ModelChanged?.Invoke();
        }

        public bool CreateVoxel(int x, int y, int z, uint faceColorBgra)
            => SetVoxel(x, y, z, occupied: true, faceColorBgra);

        public bool DeleteVoxel(int x, int y, int z)
            => SetVoxel(x, y, z, occupied: false, faceColorBgra: 0);

        public bool SetFaceColor(int x, int y, int z, Face face, uint colorBgra)
        {
            if (!HasModel || !_model.IsInBounds(x, y, z) || !_model.IsOccupied(x, y, z))
                return false;

            uint before = _model.GetFaceColorBgra(x, y, z, face);
            if (before == colorBgra)
                return false;

            _model.SetFaceColorBgra(x, y, z, face, colorBgra);
            _model.DirtyFromSource = true;

            History.Push(new VoxelCommandHistory.DelegateCommand(
                "Voxel Face Color",
                undo: () =>
                {
                    _model.SetFaceColorBgra(x, y, z, face, before);
                    _model.DirtyFromSource = true;
                    ModelChanged?.Invoke();
                },
                redo: () =>
                {
                    _model.SetFaceColorBgra(x, y, z, face, colorBgra);
                    _model.DirtyFromSource = true;
                    ModelChanged?.Invoke();
                }));

            ModelChanged?.Invoke();
            return true;
        }

        public bool SetSelection(IEnumerable<Int3> voxels, VoxelSelectionMode mode)
        {
            var before = Selection.ToArray();
            if (!Selection.SetSelection(voxels, mode))
                return false;

            var after = Selection.ToArray();
            History.Push(new VoxelCommandHistory.DelegateCommand(
                "Voxel Selection",
                undo: () => Selection.ReplaceAll(before),
                redo: () => Selection.ReplaceAll(after)));
            return true;
        }

        public bool ClearSelection()
        {
            if (Selection.Count == 0)
                return false;

            var before = Selection.ToArray();
            Selection.Clear();
            History.Push(new VoxelCommandHistory.DelegateCommand(
                "Clear Voxel Selection",
                undo: () => Selection.ReplaceAll(before),
                redo: () => Selection.ReplaceAll(Array.Empty<Int3>())));
            return true;
        }

        public bool ExpandSelectionConnected()
        {
            var before = Selection.ToArray();
            if (!Selection.ExpandConnected(_model))
                return false;

            var after = Selection.ToArray();
            History.Push(new VoxelCommandHistory.DelegateCommand(
                "Expand Voxel Selection",
                undo: () => Selection.ReplaceAll(before),
                redo: () => Selection.ReplaceAll(after)));
            return true;
        }

        public bool MoveSelection(Int3 delta, VoxelMoveMode mode = VoxelMoveMode.CutPaste)
        {
            if (!HasModel || Selection.Count == 0)
                return false;

            if (delta.X == 0 && delta.Y == 0 && delta.Z == 0)
                return false;

            var selected = Selection.ToArray();
            if (selected.Length == 0)
                return false;

            var selectedSet = new HashSet<Int3>(selected);
            var moving = new List<Int3>(selected.Length);
            for (int i = 0; i < selected.Length; i++)
            {
                var p = selected[i];
                if (_model.IsInBounds(p.X, p.Y, p.Z) && _model.IsOccupied(p.X, p.Y, p.Z))
                {
                    moving.Add(p);
                }
            }

            if (moving.Count == 0)
                return false;

            var dest = new Int3[moving.Count];
            for (int i = 0; i < moving.Count; i++)
            {
                var p = moving[i];
                var d = new Int3(p.X + delta.X, p.Y + delta.Y, p.Z + delta.Z);
                if (!_model.IsInBounds(d.X, d.Y, d.Z))
                    return false;

                if (_model.IsOccupied(d.X, d.Y, d.Z))
                {
                    bool occupiedBySelection = selectedSet.Contains(d);
                    if (mode == VoxelMoveMode.Copy || !occupiedBySelection)
                        return false;
                }

                dest[i] = d;
            }

            var beforeModel = _model.Clone();
            var beforeSelection = Selection.ToArray();

            // Capture source cells before mutation.
            var sourceCells = new VoxelCellSnapshot[moving.Count];
            for (int i = 0; i < moving.Count; i++)
            {
                sourceCells[i] = ReadCell(moving[i].X, moving[i].Y, moving[i].Z);
            }

            if (mode == VoxelMoveMode.CutPaste)
            {
                for (int i = 0; i < moving.Count; i++)
                {
                    _model.SetOccupied(moving[i].X, moving[i].Y, moving[i].Z, false);
                }
            }

            for (int i = 0; i < moving.Count; i++)
            {
                WriteCell(dest[i].X, dest[i].Y, dest[i].Z, sourceCells[i]);
            }

            _model.DirtyFromSource = true;
            Selection.ReplaceAll(dest);

            var afterModel = _model.Clone();
            var afterSelection = Selection.ToArray();

            History.Push(new VoxelCommandHistory.DelegateCommand(
                "Move Voxels",
                undo: () =>
                {
                    _model.CopyFrom(beforeModel);
                    Selection.ReplaceAll(beforeSelection);
                    ModelChanged?.Invoke();
                },
                redo: () =>
                {
                    _model.CopyFrom(afterModel);
                    Selection.ReplaceAll(afterSelection);
                    ModelChanged?.Invoke();
                }));

            ModelChanged?.Invoke();
            return true;
        }

        public bool Undo()
        {
            if (!History.Undo())
                return false;
            return true;
        }

        public bool Redo()
        {
            if (!History.Redo())
                return false;
            return true;
        }

        public void BeginHistoryTransaction(string name) => History.BeginTransaction(name);

        public void CommitHistoryTransaction()
        {
            History.CommitTransaction();
        }

        public void CancelHistoryTransaction()
        {
            History.CancelTransaction();
        }

        private bool SetVoxel(int x, int y, int z, bool occupied, uint faceColorBgra)
        {
            if (!HasModel || !_model.IsInBounds(x, y, z))
                return false;

            var before = ReadCell(x, y, z);
            var after = before;

            if (occupied)
            {
                after.Occupied = true;
                after.Front = faceColorBgra;
                after.Back = faceColorBgra;
                after.Left = faceColorBgra;
                after.Right = faceColorBgra;
                after.Top = faceColorBgra;
                after.Bottom = faceColorBgra;
            }
            else
            {
                after.Occupied = false;
            }

            if (before.Equals(after))
                return false;

            WriteCell(x, y, z, after);
            _model.DirtyFromSource = true;

            History.Push(new VoxelCommandHistory.DelegateCommand(
                occupied ? "Create Voxel" : "Delete Voxel",
                undo: () =>
                {
                    WriteCell(x, y, z, before);
                    _model.DirtyFromSource = true;
                    ModelChanged?.Invoke();
                },
                redo: () =>
                {
                    WriteCell(x, y, z, after);
                    _model.DirtyFromSource = true;
                    ModelChanged?.Invoke();
                }));

            ModelChanged?.Invoke();
            return true;
        }

        private VoxelCellSnapshot ReadCell(int x, int y, int z)
        {
            return new VoxelCellSnapshot
            {
                Occupied = _model.IsOccupied(x, y, z),
                Front = _model.GetFaceColorBgra(x, y, z, Face.Front),
                Back = _model.GetFaceColorBgra(x, y, z, Face.Back),
                Left = _model.GetFaceColorBgra(x, y, z, Face.Left),
                Right = _model.GetFaceColorBgra(x, y, z, Face.Right),
                Top = _model.GetFaceColorBgra(x, y, z, Face.Top),
                Bottom = _model.GetFaceColorBgra(x, y, z, Face.Bottom),
            };
        }

        private void WriteCell(int x, int y, int z, VoxelCellSnapshot cell)
        {
            _model.SetOccupied(x, y, z, cell.Occupied);
            _model.SetFaceColorBgra(x, y, z, Face.Front, cell.Front);
            _model.SetFaceColorBgra(x, y, z, Face.Back, cell.Back);
            _model.SetFaceColorBgra(x, y, z, Face.Left, cell.Left);
            _model.SetFaceColorBgra(x, y, z, Face.Right, cell.Right);
            _model.SetFaceColorBgra(x, y, z, Face.Top, cell.Top);
            _model.SetFaceColorBgra(x, y, z, Face.Bottom, cell.Bottom);
        }

        private void PushModelSnapshotCommand(string description, VoxelModelDocumentState before, VoxelModelDocumentState after)
        {
            History.Push(new VoxelCommandHistory.DelegateCommand(
                description,
                undo: () =>
                {
                    _model.CopyFrom(before);
                    ModelChanged?.Invoke();
                },
                redo: () =>
                {
                    _model.CopyFrom(after);
                    ModelChanged?.Invoke();
                }));
        }

        private struct VoxelCellSnapshot
        {
            public bool Occupied;
            public uint Front;
            public uint Back;
            public uint Left;
            public uint Right;
            public uint Top;
            public uint Bottom;
        }
    }
}
