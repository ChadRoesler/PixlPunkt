using System;
using System.Collections.Generic;

namespace PixlPunkt.Core.Voxel.Editing
{
    /// <summary>
    /// Lightweight undo/redo stack for voxel workspace operations.
    /// </summary>
    public sealed class VoxelCommandHistory
    {
        private readonly Stack<IVoxelHistoryCommand> _undo = [];
        private readonly Stack<IVoxelHistoryCommand> _redo = [];
        private List<IVoxelHistoryCommand>? _pendingTransaction;
        private string? _pendingTransactionName;

        public event Action? HistoryChanged;

        public bool CanUndo => _undo.Count > 0 || (_pendingTransaction?.Count > 0);
        public bool CanRedo => _redo.Count > 0;
        public bool IsTransactionOpen => _pendingTransaction != null;

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
            _pendingTransaction = null;
            _pendingTransactionName = null;
            HistoryChanged?.Invoke();
        }

        public void BeginTransaction(string name)
        {
            if (_pendingTransaction != null)
                throw new InvalidOperationException("A voxel history transaction is already open.");

            _pendingTransaction = [];
            _pendingTransactionName = string.IsNullOrWhiteSpace(name) ? "Voxel Edit" : name;
        }

        public void CommitTransaction()
        {
            if (_pendingTransaction == null)
                return;

            if (_pendingTransaction.Count == 0)
            {
                _pendingTransaction = null;
                _pendingTransactionName = null;
                return;
            }

            IVoxelHistoryCommand command = _pendingTransaction.Count == 1
                ? _pendingTransaction[0]
                : new CompositeVoxelHistoryCommand(_pendingTransactionName ?? "Voxel Edit", _pendingTransaction);

            _pendingTransaction = null;
            _pendingTransactionName = null;
            Push(command);
        }

        public void CancelTransaction()
        {
            if (_pendingTransaction == null)
                return;

            for (int i = _pendingTransaction.Count - 1; i >= 0; i--)
            {
                _pendingTransaction[i].Undo();
            }

            _pendingTransaction = null;
            _pendingTransactionName = null;
            HistoryChanged?.Invoke();
        }

        public void Push(IVoxelHistoryCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));

            if (_pendingTransaction != null)
            {
                _pendingTransaction.Add(command);
                return;
            }

            _undo.Push(command);
            _redo.Clear();
            HistoryChanged?.Invoke();
        }

        public bool Undo()
        {
            if (_pendingTransaction != null)
                throw new InvalidOperationException("Cannot undo while a voxel history transaction is open.");

            if (_undo.Count == 0)
                return false;

            var cmd = _undo.Pop();
            cmd.Undo();
            _redo.Push(cmd);
            HistoryChanged?.Invoke();
            return true;
        }

        public bool Redo()
        {
            if (_pendingTransaction != null)
                throw new InvalidOperationException("Cannot redo while a voxel history transaction is open.");

            if (_redo.Count == 0)
                return false;

            var cmd = _redo.Pop();
            cmd.Redo();
            _undo.Push(cmd);
            HistoryChanged?.Invoke();
            return true;
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

        private sealed class CompositeVoxelHistoryCommand : IVoxelHistoryCommand
        {
            private readonly IVoxelHistoryCommand[] _commands;

            public CompositeVoxelHistoryCommand(string description, IList<IVoxelHistoryCommand> commands)
            {
                Description = description;
                _commands = new IVoxelHistoryCommand[commands.Count];
                for (int i = 0; i < commands.Count; i++)
                {
                    _commands[i] = commands[i];
                }
            }

            public string Description { get; }

            public void Undo()
            {
                for (int i = _commands.Length - 1; i >= 0; i--)
                    _commands[i].Undo();
            }

            public void Redo()
            {
                for (int i = 0; i < _commands.Length; i++)
                    _commands[i].Redo();
            }
        }
    }

    public interface IVoxelHistoryCommand
    {
        string Description { get; }
        void Undo();
        void Redo();
    }
}
