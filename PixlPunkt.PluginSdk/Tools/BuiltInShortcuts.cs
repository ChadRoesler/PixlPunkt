using PixlPunkt.PluginSdk.Settings;

namespace PixlPunkt.PluginSdk.Tools
{
    /// <summary>
    /// Defines the default keyboard shortcuts for all built-in PixlPunkt tools.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Plugin developers can reference these shortcuts to avoid conflicts with built-in tools.
    /// If a plugin defines a shortcut that conflicts with a built-in tool, a warning will be
    /// shown to the user at startup.
    /// </para>
    /// <para>
    /// <strong>Best Practices for Plugin Shortcuts:</strong>
    /// </para>
    /// <list type="bullet">
    /// <item>Avoid single-letter shortcuts without modifiers (reserved for built-in tools)</item>
    /// <item>Use Ctrl, Shift, or Alt modifiers for plugin shortcuts</item>
    /// <item>Check this class before defining shortcuts to avoid conflicts</item>
    /// <item>Use the <see cref="AllowShortcutOverrideAttribute"/> if intentionally overriding</item>
    /// </list>
    /// </remarks>
    public static class BuiltInShortcuts
    {
        // ====================================================================
        // BRUSH TOOLS
        // ====================================================================

        /// <summary>Brush tool shortcut: B</summary>
        public static readonly KeyBinding Brush = new(VirtualKey.B);

        /// <summary>Eraser tool shortcut: E</summary>
        public static readonly KeyBinding Eraser = new(VirtualKey.E);

        /// <summary>Fill (bucket) tool shortcut: G (as in "fill with color")</summary>
        public static readonly KeyBinding Fill = new(VirtualKey.G);

        /// <summary>Replacer tool shortcut: R</summary>
        public static readonly KeyBinding Replacer = new(VirtualKey.R);

        /// <summary>Blur tool shortcut: U</summary>
        public static readonly KeyBinding Blur = new(VirtualKey.U);

        /// <summary>Smudge tool shortcut: S + Shift</summary>
        public static readonly KeyBinding Smudge = new(VirtualKey.S, Shift: true);

        /// <summary>Jumble tool shortcut: J</summary>
        public static readonly KeyBinding Jumble = new(VirtualKey.J);

        /// <summary>Gradient tool shortcut: O (as in "gradient")</summary>
        public static readonly KeyBinding Gradient = new(VirtualKey.O);

        // ====================================================================
        // SELECTION TOOLS
        // ====================================================================

        /// <summary>Rectangle selection tool shortcut: M</summary>
        public static readonly KeyBinding SelectRect = new(VirtualKey.M);

        /// <summary>Magic wand tool shortcut: W</summary>
        public static readonly KeyBinding Wand = new(VirtualKey.W);

        /// <summary>Lasso tool shortcut: L</summary>
        public static readonly KeyBinding Lasso = new(VirtualKey.L);

        /// <summary>Paint selection tool shortcut: Q</summary>
        public static readonly KeyBinding PaintSelect = new(VirtualKey.Q);

        // ====================================================================
        // SHAPE TOOLS
        // ====================================================================

        /// <summary>Rectangle shape tool shortcut: I</summary>
        public static readonly KeyBinding ShapeRect = new(VirtualKey.I);

        /// <summary>Ellipse shape tool shortcut: P</summary>
        public static readonly KeyBinding ShapeEllipse = new(VirtualKey.P);

        // ====================================================================
        // UTILITY TOOLS
        // ====================================================================

        /// <summary>Pan tool shortcut: H (as in "hand")</summary>
        public static readonly KeyBinding Pan = new(VirtualKey.H);

        /// <summary>Zoom tool shortcut: Z</summary>
        public static readonly KeyBinding Zoom = new(VirtualKey.Z);

        /// <summary>Dropper (color picker) tool shortcut: D</summary>
        public static readonly KeyBinding Dropper = new(VirtualKey.D);

        // ====================================================================
        // TILE TOOLS
        // ====================================================================

        /// <summary>Tile stamper tool shortcut: Shift+T</summary>
        public static readonly KeyBinding TileStamper = new(VirtualKey.T, Shift: true);

        /// <summary>Tile modifier tool shortcut: Ctrl+T</summary>
        public static readonly KeyBinding TileModifier = new(VirtualKey.T, Ctrl: true);

        // ====================================================================
        // RESERVED KEYS
        // ====================================================================

        /// <summary>
        /// Gets all built-in shortcut key bindings.
        /// </summary>
        /// <returns>An array of all built-in shortcuts.</returns>
        public static KeyBinding[] GetAll() =>
        [
            Brush, Eraser, Fill, Replacer, Blur, Smudge, Jumble, Gradient,
            SelectRect, Wand, Lasso, PaintSelect,
            ShapeRect, ShapeEllipse,
            Pan, Zoom, Dropper,
            TileStamper, TileModifier
        ];

        /// <summary>
        /// Checks if a key binding conflicts with any built-in shortcut.
        /// </summary>
        /// <param name="binding">The key binding to check.</param>
        /// <returns>True if the binding conflicts with a built-in shortcut.</returns>
        public static bool ConflictsWithBuiltIn(KeyBinding binding)
        {
            if (binding == null) return false;

            foreach (var builtIn in GetAll())
            {
                if (builtIn.Matches((int)binding.Key, binding.Ctrl, binding.Shift, binding.Alt))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the name of the built-in tool that uses the specified shortcut.
        /// </summary>
        /// <param name="binding">The key binding to check.</param>
        /// <returns>The tool name, or null if no built-in tool uses this shortcut.</returns>
        public static string? GetConflictingToolName(KeyBinding binding)
        {
            if (binding == null) return null;

            if (Brush.Matches((int)binding.Key, binding.Ctrl, binding.Shift, binding.Alt))
                return "Brush";
            if (Eraser.Matches((int)binding.Key, binding.Ctrl, binding.Shift, binding.Alt))
                return "Eraser";
            if (Fill.Matches((int)binding.Key, binding.Ctrl, binding.Shift, binding.Alt))
                return "Fill";
            if (Replacer.Matches((int)binding.Key, binding.Ctrl, binding.Shift, binding.Alt))
                return "Replacer";
            if (Blur.Matches((int)binding.Key, binding.Ctrl, binding.Shift, binding.Alt))
                return "Blur";
            if (Smudge.Matches((int)binding.Key, binding.Ctrl, binding.Shift, binding.Alt))
                return "Smudge";
            if (Jumble.Matches((int)binding.Key, binding.Ctrl, binding.Shift, binding.Alt))
                return "Jumble";
            if (Gradient.Matches((int)binding.Key, binding.Ctrl, binding.Shift, binding.Alt))
                return "Gradient";
            if (SelectRect.Matches((int)binding.Key, binding.Ctrl, binding.Shift, binding.Alt))
                return "Rectangle Selection";
            if (Wand.Matches((int)binding.Key, binding.Ctrl, binding.Shift, binding.Alt))
                return "Magic Wand";
            if (Lasso.Matches((int)binding.Key, binding.Ctrl, binding.Shift, binding.Alt))
                return "Lasso";
            if (PaintSelect.Matches((int)binding.Key, binding.Ctrl, binding.Shift, binding.Alt))
                return "Paint Selection";
            if (ShapeRect.Matches((int)binding.Key, binding.Ctrl, binding.Shift, binding.Alt))
                return "Rectangle Shape";
            if (ShapeEllipse.Matches((int)binding.Key, binding.Ctrl, binding.Shift, binding.Alt))
                return "Ellipse Shape";
            if (Pan.Matches((int)binding.Key, binding.Ctrl, binding.Shift, binding.Alt))
                return "Pan";
            if (Zoom.Matches((int)binding.Key, binding.Ctrl, binding.Shift, binding.Alt))
                return "Zoom";
            if (Dropper.Matches((int)binding.Key, binding.Ctrl, binding.Shift, binding.Alt))
                return "Dropper";
            if (TileStamper.Matches((int)binding.Key, binding.Ctrl, binding.Shift, binding.Alt))
                return "Tile Stamper";
            if (TileModifier.Matches((int)binding.Key, binding.Ctrl, binding.Shift, binding.Alt))
                return "Tile Modifier";

            return null;
        }
    }
}
