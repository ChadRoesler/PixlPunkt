using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PixlPunkt.Uno.UI.Ascii
{
    /// <summary>
    /// Abstract base class for glyph set items (both individual sets and folders).
    /// Provides hierarchy support for organizing glyph sets.
    /// </summary>
    public abstract class GlyphSetItemBase : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private bool _isExpanded = true;
        private GlyphSetFolder? _parent;

        /// <summary>
        /// Gets or sets the display name of this item.
        /// </summary>
        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Gets or sets whether this item is expanded in the UI (for folders).
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set { if (_isExpanded != value) { _isExpanded = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Gets or sets the parent folder containing this item, or null if at root.
        /// </summary>
        public GlyphSetFolder? Parent
        {
            get => _parent;
            internal set { if (_parent != value) { _parent = value; OnPropertyChanged(); OnPropertyChanged(nameof(Depth)); } }
        }

        /// <summary>
        /// Gets the nesting depth of this item (0 = root level).
        /// </summary>
        public int Depth
        {
            get
            {
                int depth = 0;
                var current = Parent;
                while (current != null)
                {
                    depth++;
                    current = current.Parent;
                }
                return depth;
            }
        }

        /// <summary>
        /// Gets whether this item can contain children (true for folders).
        /// </summary>
        public abstract bool CanHaveChildren { get; }

        /// <summary>
        /// Gets whether this item is built-in (read-only).
        /// </summary>
        public abstract bool IsBuiltIn { get; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Represents a folder for organizing glyph sets.
    /// </summary>
    public sealed class GlyphSetFolder : GlyphSetItemBase
    {
        private readonly ObservableCollection<GlyphSetItemBase> _children = new();
        private bool _isBuiltInFolder;
        private string? _folderPath;

        /// <summary>
        /// Gets the children of this folder.
        /// </summary>
        public IReadOnlyList<GlyphSetItemBase> Children => _children;

        /// <summary>
        /// Gets or sets whether this is a built-in folder (e.g., "Built-in" section).
        /// Built-in folders cannot be renamed, deleted, or have items added/removed.
        /// </summary>
        public bool IsBuiltInFolder
        {
            get => _isBuiltInFolder;
            set { if (_isBuiltInFolder != value) { _isBuiltInFolder = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsBuiltIn)); } }
        }

        /// <summary>
        /// Gets or sets the file system path for this folder.
        /// </summary>
        public string? FolderPath
        {
            get => _folderPath;
            set { if (_folderPath != value) { _folderPath = value; OnPropertyChanged(); } }
        }

        /// <inheritdoc/>
        public override bool CanHaveChildren => true;

        /// <inheritdoc/>
        public override bool IsBuiltIn => IsBuiltInFolder;

        /// <summary>
        /// Creates a new glyph set folder.
        /// </summary>
        public GlyphSetFolder(string name, bool isBuiltInFolder = false)
        {
            Name = name;
            IsBuiltInFolder = isBuiltInFolder;
            IsExpanded = true;
        }

        /// <summary>
        /// Adds a child item to this folder.
        /// </summary>
        public void AddChild(GlyphSetItemBase item)
        {
            item.Parent = this;
            _children.Add(item);
            OnPropertyChanged(nameof(Children));
        }

        /// <summary>
        /// Inserts a child item at the specified index.
        /// </summary>
        public void InsertChild(int index, GlyphSetItemBase item)
        {
            item.Parent = this;
            int clampedIndex = System.Math.Clamp(index, 0, _children.Count);
            _children.Insert(clampedIndex, item);
            OnPropertyChanged(nameof(Children));
        }

        /// <summary>
        /// Removes a child item from this folder.
        /// </summary>
        public bool RemoveChild(GlyphSetItemBase item)
        {
            if (_children.Remove(item))
            {
                item.Parent = null;
                OnPropertyChanged(nameof(Children));
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the index of a child item.
        /// </summary>
        public int IndexOfChild(GlyphSetItemBase item) => _children.IndexOf(item);

        /// <summary>
        /// Moves a child from one index to another.
        /// </summary>
        public void MoveChild(int fromIndex, int toIndex)
        {
            if (fromIndex < 0 || fromIndex >= _children.Count) return;
            if (toIndex < 0 || toIndex >= _children.Count) return;
            if (fromIndex == toIndex) return;

            var item = _children[fromIndex];
            _children.RemoveAt(fromIndex);
            _children.Insert(toIndex, item);
            OnPropertyChanged(nameof(Children));
        }

        /// <summary>
        /// Flattens this folder and all visible descendants into a list.
        /// </summary>
        public IEnumerable<GlyphSetItemBase> FlattenVisible()
        {
            yield return this;
            if (IsExpanded)
            {
                foreach (var child in _children)
                {
                    if (child is GlyphSetFolder folder)
                    {
                        foreach (var descendant in folder.FlattenVisible())
                            yield return descendant;
                    }
                    else
                    {
                        yield return child;
                    }
                }
            }
        }

        /// <summary>
        /// Gets all glyph set items (not folders) in this folder and descendants.
        /// </summary>
        public IEnumerable<GlyphSetItem> GetAllGlyphSets()
        {
            foreach (var child in _children)
            {
                if (child is GlyphSetItem item)
                {
                    yield return item;
                }
                else if (child is GlyphSetFolder folder)
                {
                    foreach (var descendant in folder.GetAllGlyphSets())
                        yield return descendant;
                }
            }
        }
    }

    /// <summary>
    /// Represents a single glyph set with character ramp and bitmap data.
    /// </summary>
    public sealed class GlyphSetItem : GlyphSetItemBase
    {
        private string _ramp = string.Empty;
        private int _glyphWidth = 4;
        private int _glyphHeight = 4;
        private bool _isBuiltIn;
        private string? _registeredName;
        private string? _filePath;

        /// <summary>
        /// The name this set is registered under in AsciiGlyphSets.
        /// May differ from Name if the user renamed it but hasn't saved yet.
        /// </summary>
        public string? RegisteredName
        {
            get => _registeredName;
            set { if (_registeredName != value) { _registeredName = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// The character ramp from light to dark.
        /// </summary>
        public string Ramp
        {
            get => _ramp;
            set { if (_ramp != value) { _ramp = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Width of each glyph in pixels.
        /// </summary>
        public int GlyphWidth
        {
            get => _glyphWidth;
            set { if (_glyphWidth != value) { _glyphWidth = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Height of each glyph in pixels.
        /// </summary>
        public int GlyphHeight
        {
            get => _glyphHeight;
            set { if (_glyphHeight != value) { _glyphHeight = value; OnPropertyChanged(); } }
        }

        /// <summary>
        /// Bitmap data for each glyph.
        /// </summary>
        public List<ulong> Bitmaps { get; set; } = new();

        /// <summary>
        /// Gets or sets whether this is a built-in glyph set (read-only).
        /// </summary>
        public bool IsBuiltInSet
        {
            get => _isBuiltIn;
            set { if (_isBuiltIn != value) { _isBuiltIn = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsBuiltIn)); } }
        }

        /// <inheritdoc/>
        public override bool IsBuiltIn => _isBuiltIn;

        /// <summary>
        /// File path for custom glyph sets.
        /// </summary>
        public string? FilePath
        {
            get => _filePath;
            set { if (_filePath != value) { _filePath = value; OnPropertyChanged(); } }
        }

        /// <inheritdoc/>
        public override bool CanHaveChildren => false;

        /// <summary>
        /// Creates a new glyph set item.
        /// </summary>
        public GlyphSetItem()
        {
        }

        /// <summary>
        /// Creates a new glyph set item with the specified name.
        /// </summary>
        public GlyphSetItem(string name, bool isBuiltIn = false)
        {
            Name = name;
            IsBuiltInSet = isBuiltIn;
        }
    }
}
