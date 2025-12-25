using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PixlPunkt.Core.Document.Layer
{
    /// <summary>
    /// Abstract base class for all layer types providing common properties and change notification.
    /// </summary>
    /// <remarks>
    /// <para>
    /// LayerBase defines the fundamental interface shared by all layer implementations in PixlPunkt,
    /// including <see cref="RasterLayer"/> (pixel layers) and <see cref="LayerFolder"/> (grouping containers).
    /// It implements <see cref="INotifyPropertyChanged"/> to support data binding in the UI layer hierarchy.
    /// </para>
    /// <para><strong>Core Properties:</strong></para>
    /// <list type="bullet">
    /// <item><strong>Name</strong>: User-visible layer label displayed in the layers panel.</item>
    /// <item><strong>Visible</strong>: Controls whether the layer contributes to the final composite.
    /// Hidden layers are skipped during rendering but remain in the document.</item>
    /// <item><strong>Locked</strong>: When true, prevents editing of layer content (pixels, effects, etc.).
    /// Used to protect layers from accidental modification.</item>
    /// <item><strong>Parent</strong>: Reference to the containing folder, or null if at document root.</item>
    /// <item><strong>CanHaveChildren</strong>: Abstract property indicating whether this layer type
    /// supports child layers (true for <see cref="LayerFolder"/>, false for <see cref="RasterLayer"/>).</item>
    /// </list>
    /// <para><strong>Cascading Behavior:</strong></para>
    /// <para>
    /// Folders cascade their Locked/Visible state to children. Use <see cref="IsEffectivelyVisible"/>
    /// and <see cref="IsEffectivelyLocked"/> to check if a layer is truly editable/renderable considering
    /// all ancestor folders.
    /// </para>
    /// </remarks>
    /// <seealso cref="RasterLayer"/>
    /// <seealso cref="LayerFolder"/>
    public abstract class LayerBase : INotifyPropertyChanged, ICloneable
    {
        string _name = "Layer";
        bool _visible = true;
        bool _locked = false;
        LayerFolder? _parent;

        /// <summary>
        /// Gets the unique identifier for this layer.
        /// </summary>
        /// <value>
        /// A <see cref="Guid"/> that uniquely identifies this layer instance.
        /// This ID is stable across the lifetime of the layer and is used for
        /// animation track binding.
        /// </value>
        public Guid Id { get; private set; } = Guid.NewGuid();

        /// <summary>
        /// Restores the layer ID from a saved document.
        /// This is used during deserialization to maintain animation track binding.
        /// </summary>
        /// <param name="id">The saved layer ID to restore.</param>
        internal void RestoreId(Guid id)
        {
            Id = id;
        }

        /// <summary>
        /// Gets or sets the user-visible layer name.
        /// </summary>
        /// <value>
        /// A string representing the layer's display name. Default is "Layer".
        /// </value>
        public string Name
        {
            get => _name;
            set { if (value == _name) return; _name = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this layer is visible in the final composite.
        /// </summary>
        /// <value>
        /// <c>true</c> if the layer should be rendered; otherwise, <c>false</c>. Default is <c>true</c>.
        /// </value>
        /// <remarks>
        /// Hidden layers are excluded from compositing operations but remain in the document structure.
        /// Note: This is the layer's own visibility. Use <see cref="IsEffectivelyVisible"/> to check
        /// if the layer is visible considering ancestor folders.
        /// </remarks>
        public bool Visible
        {
            get => _visible;
            set { if (value == _visible) return; _visible = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this layer is locked from editing.
        /// </summary>
        /// <value>
        /// <c>true</c> if the layer is locked; otherwise, <c>false</c>. Default is <c>false</c>.
        /// </value>
        /// <remarks>
        /// Locked layers cannot be modified (pixels, effects, properties) but can still be viewed
        /// and included in compositing. Note: This is the layer's own lock state. Use
        /// <see cref="IsEffectivelyLocked"/> to check if the layer is locked considering ancestor folders.
        /// </remarks>
        public bool Locked
        {
            get => _locked;
            set { if (value == _locked) return; _locked = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets or sets the parent folder containing this layer, or null if at document root.
        /// </summary>
        /// <value>
        /// The <see cref="LayerFolder"/> that contains this layer, or null for root layers.
        /// </value>
        /// <remarks>
        /// This property is managed by <see cref="LayerFolder"/> when adding/removing children.
        /// It should not be set directly by external code.
        /// </remarks>
        public LayerFolder? Parent
        {
            get => _parent;
            internal set { if (value == _parent) return; _parent = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Gets a value indicating whether this layer type can contain child layers.
        /// </summary>
        /// <value>
        /// <c>true</c> if children are allowed (e.g., <see cref="LayerFolder"/>);
        /// <c>false</c> for leaf types (e.g., <see cref="RasterLayer"/>).
        /// </value>
        public abstract bool CanHaveChildren { get; }

        /// <summary>
        /// Determines if this layer is effectively visible considering all ancestor folder states.
        /// </summary>
        /// <returns><c>true</c> if this layer and all ancestors are visible; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// Used to determine rendering visibility. A layer with Visible=true but inside a
        /// hidden folder will return false. This implements Photoshop-style cascading visibility.
        /// </remarks>
        public bool IsEffectivelyVisible()
        {
            if (!Visible) return false;
            var current = Parent;
            while (current != null)
            {
                if (!current.Visible) return false;
                current = current.Parent;
            }
            return true;
        }

        /// <summary>
        /// Determines if this layer is effectively locked considering all ancestor folder states.
        /// </summary>
        /// <returns><c>true</c> if this layer OR any ancestor is locked; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// Used to determine edit permissions. A layer with Locked=false but inside a
        /// locked folder will return true. This implements Photoshop-style cascading lock state.
        /// </remarks>
        public bool IsEffectivelyLocked()
        {
            if (Locked) return true;
            var current = Parent;
            while (current != null)
            {
                if (current.Locked) return true;
                current = current.Parent;
            }
            return false;
        }

        /// <summary>
        /// Gets the depth of this layer in the layer tree (0 = root level).
        /// </summary>
        /// <value>The nesting depth, where 0 means no parent folder.</value>
        /// <remarks>
        /// Used for indentation in the layer panel UI. Each level adds ~16px of indent.
        /// </remarks>
        public int Depth => GetDepth();

        /// <summary>
        /// Finds the depth of this layer in the layer tree (0 = root level).
        /// </summary>
        /// <returns>The nesting depth, where 0 means no parent folder.</returns>
        /// <remarks>
        /// Used for indentation in the layer panel UI. Each level adds ~16px of indent.
        /// </remarks>
        public int GetDepth()
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

        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event for the specified property.
        /// </summary>
        /// <param name="p">Name of the property that changed. Automatically supplied by the compiler
        /// when called from a property setter using <c>[CallerMemberName]</c> attribute.</param>
        protected void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));


        /// <inheritdoc/>
        public object Clone()
        {
            var clone = (LayerBase)MemberwiseClone();
            clone.PropertyChanged = null;
            return clone;
        }
    }
}
