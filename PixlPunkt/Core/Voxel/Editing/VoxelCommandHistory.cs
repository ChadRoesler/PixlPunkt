using System;
using FluentIcons.Common;
using PixlPunkt.Core.History;

namespace PixlPunkt.Core.Voxel.Editing
{
    /// <summary>
    /// The voxel editor's view of a <see cref="UnifiedHistoryStack"/>. Voxel commands are pushed
    /// as <see cref="VoxelHistoryItem"/>s and transactions are history groups, so when the stack
    /// is the document's the canvas and the voxel workspace share one undo timeline, one dirty
    /// flag and one memory budget. Undo/redo here only step items this class pushed; anything
    /// else on top is left for the canvas host, which knows how to refresh after it.
    /// </summary>
    public sealed class VoxelCommandHistory
    {
        /// <summary>The stack this history writes to.</summary>
        public UnifiedHistoryStack Stack { get; }

        public event Action? HistoryChanged;

        public VoxelCommandHistory(UnifiedHistoryStack? stack = null)
        {
            Stack = stack ?? new UnifiedHistoryStack();
            Stack.HistoryChanged += () => HistoryChanged?.Invoke();
        }

        /// <summary>True when the stack can undo anything at all.</summary>
        public bool CanUndo => Stack.CanUndo || Stack.IsGroupOpen;

        public bool CanRedo => Stack.CanRedo;

        /// <summary>True when the next undo step is a voxel edit.</summary>
        public bool CanUndoVoxel => Stack.PeekUndo() is VoxelHistoryItem;

        /// <summary>True when the next redo step is a voxel edit.</summary>
        public bool CanRedoVoxel => Stack.PeekRedo() is VoxelHistoryItem;

        public bool IsTransactionOpen => Stack.IsGroupOpen;

        public void BeginTransaction(string name)
        {
            if (Stack.IsGroupOpen)
                throw new InvalidOperationException("A voxel history transaction is already open.");
            Stack.BeginGroup(string.IsNullOrWhiteSpace(name) ? "Voxel Edit" : name);
        }

        public void CommitTransaction() => Stack.EndGroup();

        public void CancelTransaction()
        {
            if (!Stack.IsGroupOpen) return;
            Stack.CancelGroup();
            HistoryChanged?.Invoke();
        }

        public void Push(IVoxelHistoryCommand command)
        {
            ArgumentNullException.ThrowIfNull(command);
            Stack.Push(new VoxelHistoryItem(command));
        }

        /// <summary>Undoes the top item if it is a voxel edit; false otherwise.</summary>
        public bool Undo()
        {
            if (Stack.IsGroupOpen)
                throw new InvalidOperationException("Cannot undo while a voxel history transaction is open.");
            return CanUndoVoxel && Stack.Undo();
        }

        /// <summary>Redoes the top item if it is a voxel edit; false otherwise.</summary>
        public bool Redo()
        {
            if (Stack.IsGroupOpen)
                throw new InvalidOperationException("Cannot redo while a voxel history transaction is open.");
            return CanRedoVoxel && Stack.Redo();
        }

        public sealed class DelegateCommand : IVoxelHistoryCommand
        {
            private readonly Action _undo;
            private readonly Action _redo;

            public DelegateCommand(string description, Action undo, Action redo)
            {
                Description = string.IsNullOrWhiteSpace(description) ? "Voxel Edit" : description;
                _undo = undo ?? throw new ArgumentNullException(nameof(undo));
                _redo = redo ?? throw new ArgumentNullException(nameof(redo));
            }

            public string Description { get; }
            public void Undo() => _undo();
            public void Redo() => _redo();
        }
    }

    /// <summary>A voxel command on the unified history stack.</summary>
    public sealed class VoxelHistoryItem : IHistoryItem
    {
        public IVoxelHistoryCommand Command { get; }
        public Icon HistoryIcon { get; set; } = Icon.Cube;
        public string Description => Command.Description;

        public VoxelHistoryItem(IVoxelHistoryCommand command)
        {
            Command = command ?? throw new ArgumentNullException(nameof(command));
        }

        public void Undo() => Command.Undo();
        public void Redo() => Command.Redo();
    }

    public interface IVoxelHistoryCommand
    {
        string Description { get; }
        void Undo();
        void Redo();
    }
}
