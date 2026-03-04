using System;
using System.Collections.Generic;
using PixlPunkt.Core.Document;
using PixlPunkt.PluginSdk.Voxel;

namespace PixlPunkt.Core.Voxel.Editing
{
    /// <summary>
    /// Maintains voxel selection state for the voxel workspace.
    /// </summary>
    public sealed class VoxelSelectionService
    {
        private readonly HashSet<Int3> _selected = [];

        public event Action? SelectionChanged;

        public int Count => _selected.Count;

        public bool Contains(Int3 position) => _selected.Contains(position);

        public IEnumerable<Int3> Enumerate() => _selected;

        public Int3[] ToArray()
        {
            var result = new Int3[_selected.Count];
            _selected.CopyTo(result);
            return result;
        }

        public VoxelSelectionSnapshot Snapshot() => new(_selected);

        public bool Clear() => ReplaceAll(Array.Empty<Int3>());

        public bool ReplaceAll(IEnumerable<Int3> voxels)
        {
            _selected.Clear();
            if (voxels != null)
            {
                foreach (var voxel in voxels)
                {
                    _selected.Add(voxel);
                }
            }

            SelectionChanged?.Invoke();
            return true;
        }

        public bool SetSelection(IEnumerable<Int3> voxels, VoxelSelectionMode mode)
        {
            if (voxels == null)
                return false;

            bool changed = false;

            switch (mode)
            {
                case VoxelSelectionMode.Replace:
                {
                    var next = new HashSet<Int3>(voxels);
                    if (_selected.SetEquals(next))
                        return false;

                    _selected.Clear();
                    foreach (var v in next)
                        _selected.Add(v);
                    changed = true;
                    break;
                }

                case VoxelSelectionMode.Add:
                    foreach (var v in voxels)
                        changed |= _selected.Add(v);
                    break;

                case VoxelSelectionMode.Remove:
                    foreach (var v in voxels)
                        changed |= _selected.Remove(v);
                    break;

                case VoxelSelectionMode.Toggle:
                    foreach (var v in voxels)
                    {
                        if (!_selected.Remove(v))
                        {
                            _selected.Add(v);
                        }
                        changed = true;
                    }
                    break;

                default:
                    return false;
            }

            if (changed)
                SelectionChanged?.Invoke();

            return changed;
        }

        public bool ExpandConnected(VoxelModelDocumentState model)
        {
            if (model == null || !model.HasModel || !model.IsStorageValid || _selected.Count == 0)
                return false;

            var seeds = ToArray();
            var queue = new Queue<Int3>(seeds.Length);
            var visited = new HashSet<Int3>(seeds.Length);

            for (int i = 0; i < seeds.Length; i++)
            {
                var seed = seeds[i];
                if (!model.IsInBounds(seed.X, seed.Y, seed.Z) || !model.IsOccupied(seed.X, seed.Y, seed.Z))
                    continue;

                if (visited.Add(seed))
                    queue.Enqueue(seed);
            }

            if (queue.Count == 0)
                return false;

            while (queue.Count > 0)
            {
                var cur = queue.Dequeue();
                TryEnqueue(cur.X + 1, cur.Y, cur.Z);
                TryEnqueue(cur.X - 1, cur.Y, cur.Z);
                TryEnqueue(cur.X, cur.Y + 1, cur.Z);
                TryEnqueue(cur.X, cur.Y - 1, cur.Z);
                TryEnqueue(cur.X, cur.Y, cur.Z + 1);
                TryEnqueue(cur.X, cur.Y, cur.Z - 1);
            }

            if (_selected.SetEquals(visited))
                return false;

            _selected.Clear();
            foreach (var v in visited)
                _selected.Add(v);

            SelectionChanged?.Invoke();
            return true;

            void TryEnqueue(int x, int y, int z)
            {
                var p = new Int3(x, y, z);
                if (!model.IsInBounds(x, y, z) || !model.IsOccupied(x, y, z))
                    return;

                if (visited.Add(p))
                    queue.Enqueue(p);
            }
        }
    }
}
