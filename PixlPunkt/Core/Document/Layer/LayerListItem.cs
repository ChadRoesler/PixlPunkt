using System.ComponentModel;
using System.Runtime.CompilerServices;
using FluentIcons.Common;
using Microsoft.UI.Xaml;

namespace PixlPunkt.Core.Document.Layer;

/// <summary>
/// View model wrapper for layers in the hierarchical UI layer list, providing display properties and tree state.
/// </summary>
/// <remarks>
/// <para>
/// LayerListItem bridges the gap between the core document layer structure (<see cref="LayerBase"/>)
/// and the UI tree view presentation. Each item in the layers panel is represented by a LayerListItem
/// that wraps the underlying layer data and adds UI-specific properties like depth, indentation, icons,
/// and expand/collapse state.
/// </para>
/// <para><strong>Hierarchy Properties:</strong></para>
/// <list type="bullet">
/// <item><strong>Item</strong>: The wrapped <see cref="LayerBase"/> (either <see cref="RasterLayer"/> or <see cref="LayerFolder"/>).</item>
/// <item><strong>Parent</strong>: The parent folder containing this item (null for root-level layers).</item>
/// <item><strong>Depth</strong>: Nesting level in the hierarchy (0 = root, 1 = first level children, etc.).
/// Drives the <see cref="Indent"/> property for visual tree structure.</item>
/// </list>
/// <para><strong>Visual Presentation:</strong></para>
/// <list type="bullet">
/// <item><see cref="IconGlyph"/>: Returns appropriate icon based on layer type and folder expansion state
/// (folder closed, folder open, or layer/page icon).</item>
/// <item><see cref="ExpandGlyph"/>: Chevron direction for expand/collapse UI (down when expanded, up when collapsed).</item>
/// <item><see cref="Indent"/>: <see cref="Thickness"/> for left margin based on <see cref="Depth"/> (16px per level).</item>
/// <item><see cref="ExpandButtonVisibility"/>: Controls whether expand/collapse button is shown (visible for folders, collapsed for layers).</item>
/// </list>
/// <para><strong>Data Binding:</strong></para>
/// <para>
/// Implements <see cref="INotifyPropertyChanged"/> to support WinUI 3 data binding. When <see cref="Depth"/>
/// changes, automatically raises property changed notifications for <see cref="Indent"/> to update UI indentation.
/// </para>
/// </remarks>
/// <seealso cref="LayerBase"/>
/// <seealso cref="LayerFolder"/>
/// <seealso cref="RasterLayer"/>
public sealed class LayerListItem : INotifyPropertyChanged
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LayerListItem"/> class.
    /// </summary>
    /// <param name="item">The layer being wrapped.</param>
    /// <param name="parent">The parent folder, or null if this is a root-level layer.</param>
    /// <param name="depth">The nesting depth in the layer hierarchy (0 = root).</param>
    public LayerListItem(LayerBase item, LayerFolder? parent, int depth)
    {
        Item = item;
        Parent = parent;
        _depth = depth;
    }

    /// <summary>
    /// Gets the wrapped layer instance.
    /// </summary>
    public LayerBase Item { get; }

    /// <summary>
    /// Gets the parent folder containing this layer, or null if this is a root-level layer.
    /// </summary>
    public LayerFolder? Parent { get; }

    int _depth;
    /// <summary>
    /// Gets or sets the nesting depth of this layer in the hierarchy.
    /// </summary>
    /// <value>
    /// An integer representing the tree depth: 0 for root layers, 1 for first-level children,
    /// 2 for second-level children, etc.
    /// </value>
    /// <remarks>
    /// Changing this property automatically updates <see cref="Indent"/> via property change notification.
    /// </remarks>
    public int Depth
    {
        get => _depth;
        set
        {
            if (_depth != value)
            {
                _depth = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(Indent));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether this item represents a folder layer.
    /// </summary>
    /// <value>
    /// <c>true</c> if <see cref="Item"/> is a <see cref="LayerFolder"/>; otherwise, <c>false</c>.
    /// </value>
    public bool IsFolder => Item is LayerFolder;

    /// <summary>
    /// Gets a value indicating whether this folder is expanded (if applicable).
    /// </summary>
    /// <value>
    /// <c>true</c> if this is an expanded folder; <c>false</c> if collapsed or not a folder.
    /// </value>
    public bool IsExpanded => (Item as LayerFolder)?.IsExpanded ?? false;

    /// <summary>
    /// Gets the left margin thickness for hierarchical indentation.
    /// </summary>
    /// <value>
    /// A <see cref="Thickness"/> with left margin = <see cref="Depth"/> × 16 pixels.
    /// Other margins are zero.
    /// </value>
    public Thickness Indent => new(Depth * 16.0, 0, 0, 0);

    /// <summary>
    /// Gets the icon glyph representing this layer's type and state.
    /// </summary>
    /// <value>
    /// <see cref="Icon.FolderOpen"/> for expanded folders,
    /// <see cref="Icon.Folder"/> for collapsed folders,
    /// or <see cref="Icon.Layer"/> for raster layers.
    /// </value>
    public Icon IconGlyph => IsFolder
        ? (IsExpanded ? Icon.FolderOpen /* OpenFolder */ : Icon.Folder /* Folder */)
        : Icon.Layer; // Picture / page-like icon

    /// <summary>
    /// Gets the expand/collapse chevron glyph for folders.
    /// </summary>
    /// <value>
    /// <see cref="Icon.ChevronDown"/> for expanded folders,
    /// <see cref="Icon.ChevronUp"/> for collapsed folders,
    /// or <see cref="Icon.LineHorizontal1"/> (placeholder) for non-folder layers.
    /// </value>
    public Icon ExpandGlyph => IsFolder
        ? (IsExpanded ? Icon.ChevronDown /* ChevronDown */ : Icon.ChevronUp /* ChevronRight */)
        : Icon.LineHorizontal1;

    /// <summary>
    /// Gets the visibility of the expand/collapse button.
    /// </summary>
    /// <value>
    /// <see cref="Visibility.Visible"/> for folders; <see cref="Visibility.Collapsed"/> for layers.
    /// </value>
    public Visibility ExpandButtonVisibility => IsFolder ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises the <see cref="PropertyChanged"/> event.
    /// </summary>
    /// <param name="name">The name of the property that changed.</param>
    void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
