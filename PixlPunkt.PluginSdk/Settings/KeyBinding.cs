using System.Text;

namespace PixlPunkt.PluginSdk.Settings
{
    /// <summary>
    /// Virtual key codes for keyboard shortcuts.
    /// </summary>
    /// <remarks>
    /// This enum mirrors Windows.System.VirtualKey for plugin compatibility without requiring
    /// platform-specific dependencies in the SDK. Values are identical to allow casting.
    /// </remarks>
    public enum VirtualKey
    {
        /// <summary>No key.</summary>
        None = 0,

        // Letters A-Z (65-90)
        A = 65, B = 66, C = 67, D = 68, E = 69, F = 70, G = 71, H = 72, I = 73,
        J = 74, K = 75, L = 76, M = 77, N = 78, O = 79, P = 80, Q = 81, R = 82,
        S = 83, T = 84, U = 85, V = 86, W = 87, X = 88, Y = 89, Z = 90,

        // Numbers 0-9 (48-57)
        Number0 = 48, Number1 = 49, Number2 = 50, Number3 = 51, Number4 = 52,
        Number5 = 53, Number6 = 54, Number7 = 55, Number8 = 56, Number9 = 57,

        // Function keys F1-F24 (112-135)
        F1 = 112, F2 = 113, F3 = 114, F4 = 115, F5 = 116, F6 = 117,
        F7 = 118, F8 = 119, F9 = 120, F10 = 121, F11 = 122, F12 = 123,
        F13 = 124, F14 = 125, F15 = 126, F16 = 127, F17 = 128, F18 = 129,
        F19 = 130, F20 = 131, F21 = 132, F22 = 133, F23 = 134, F24 = 135,

        // Common keys
        Space = 32,
        Enter = 13,
        Tab = 9,
        Escape = 27,
        Back = 8,
        Delete = 46,

        // Arrow keys
        Left = 37, Up = 38, Right = 39, Down = 40,

        // Numpad
        NumberPad0 = 96, NumberPad1 = 97, NumberPad2 = 98, NumberPad3 = 99,
        NumberPad4 = 100, NumberPad5 = 101, NumberPad6 = 102, NumberPad7 = 103,
        NumberPad8 = 104, NumberPad9 = 105,

        // Modifier keys
        Shift = 16, Control = 17, Menu = 18
    }

    /// <summary>
    /// Represents a keyboard shortcut binding with optional modifier keys.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="KeyBinding"/> encapsulates a keyboard shortcut with support for
    /// Ctrl, Shift, and Alt modifiers.
    /// </para>
    /// <para>
    /// <strong>Example:</strong>
    /// </para>
    /// <code>
    /// // Simple key binding
    /// public override KeyBinding? Shortcut => new(VirtualKey.B);
    /// 
    /// // With modifiers
    /// public override KeyBinding? Shortcut => new(VirtualKey.B, Ctrl: true, Shift: true);
    /// </code>
    /// </remarks>
    /// <param name="Key">The primary virtual key for the shortcut.</param>
    /// <param name="Ctrl">Whether Ctrl modifier is required. Default is false.</param>
    /// <param name="Shift">Whether Shift modifier is required. Default is false.</param>
    /// <param name="Alt">Whether Alt modifier is required. Default is false.</param>
    public record KeyBinding(VirtualKey Key, bool Ctrl = false, bool Shift = false, bool Alt = false)
    {
        /// <summary>
        /// Returns a human-readable string representation of the key binding.
        /// </summary>
        /// <returns>
        /// A formatted string like "B", "Ctrl+B", or "Ctrl+Shift+B".
        /// </returns>
        public override string ToString()
        {
            var sb = new StringBuilder();
            if (Ctrl) sb.Append("Ctrl+");
            if (Shift) sb.Append("Shift+");
            if (Alt) sb.Append("Alt+");
            sb.Append(GetKeyDisplayName(Key));
            return sb.ToString();
        }

        /// <summary>
        /// Gets a display-friendly name for common keys.
        /// </summary>
        private static string GetKeyDisplayName(VirtualKey key) => key switch
        {
            VirtualKey.Space => "Space",
            VirtualKey.Escape => "Esc",
            VirtualKey.Delete => "Del",
            VirtualKey.Back => "Backspace",
            VirtualKey.Enter => "Enter",
            VirtualKey.Tab => "Tab",
            >= VirtualKey.Number0 and <= VirtualKey.Number9 => ((char)('0' + (key - VirtualKey.Number0))).ToString(),
            >= VirtualKey.A and <= VirtualKey.Z => key.ToString(),
            >= VirtualKey.F1 and <= VirtualKey.F24 => key.ToString(),
            _ => key.ToString()
        };

        /// <summary>
        /// Checks if this key binding matches the given key state.
        /// </summary>
        /// <param name="key">The pressed key.</param>
        /// <param name="ctrl">Whether Ctrl is held.</param>
        /// <param name="shift">Whether Shift is held.</param>
        /// <param name="alt">Whether Alt is held.</param>
        /// <returns><c>true</c> if all components match; otherwise, <c>false</c>.</returns>
        public bool Matches(VirtualKey key, bool ctrl, bool shift, bool alt)
            => Key == key && Ctrl == ctrl && Shift == shift && Alt == alt;

        /// <summary>
        /// Checks if this key binding matches the given key state using an integer key code.
        /// </summary>
        /// <param name="keyCode">The pressed key code (same values as VirtualKey/Windows.System.VirtualKey).</param>
        /// <param name="ctrl">Whether Ctrl is held.</param>
        /// <param name="shift">Whether Shift is held.</param>
        /// <param name="alt">Whether Alt is held.</param>
        /// <returns><c>true</c> if all components match; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// This overload allows matching against platform-specific key types by casting to int.
        /// </remarks>
        public bool Matches(int keyCode, bool ctrl, bool shift, bool alt)
            => (int)Key == keyCode && Ctrl == ctrl && Shift == shift && Alt == alt;
    }
}
