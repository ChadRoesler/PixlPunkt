using Microsoft.UI.Xaml;
using PixlPunkt.Core.Enums;

namespace PixlPunkt.UI
{
    /// <summary>
    /// Partial class for symmetry menu handlers.
    /// </summary>
    public sealed partial class PixlPunktMainWindow : Window
    {
        //////////////////////////////////////////////////////////////////
        // SYMMETRY MENU HANDLERS
        //////////////////////////////////////////////////////////////////

        private void Symmetry_Toggle_Click(object sender, RoutedEventArgs e)
        {
            _toolState?.Symmetry.Toggle();
        }

        private void Symmetry_CycleMode_Click(object sender, RoutedEventArgs e)
        {
            _toolState?.Symmetry.CycleMode();
        }

        private void Symmetry_SetHorizontal_Click(object sender, RoutedEventArgs e)
        {
            if (_toolState?.Symmetry == null) return;
            _toolState.Symmetry.Enabled = true;
            _toolState.Symmetry.Mode = SymmetryMode.Horizontal;
        }

        private void Symmetry_SetVertical_Click(object sender, RoutedEventArgs e)
        {
            if (_toolState?.Symmetry == null) return;
            _toolState.Symmetry.Enabled = true;
            _toolState.Symmetry.Mode = SymmetryMode.Vertical;
        }

        private void Symmetry_SetBoth_Click(object sender, RoutedEventArgs e)
        {
            if (_toolState?.Symmetry == null) return;
            _toolState.Symmetry.Enabled = true;
            _toolState.Symmetry.Mode = SymmetryMode.Both;
        }

        private void Symmetry_SetRadial4_Click(object sender, RoutedEventArgs e)
        {
            if (_toolState?.Symmetry == null) return;
            _toolState.Symmetry.Enabled = true;
            _toolState.Symmetry.Mode = SymmetryMode.Radial;
            _toolState.Symmetry.RadialSegments = 4;
        }

        private void Symmetry_SetRadial6_Click(object sender, RoutedEventArgs e)
        {
            if (_toolState?.Symmetry == null) return;
            _toolState.Symmetry.Enabled = true;
            _toolState.Symmetry.Mode = SymmetryMode.Radial;
            _toolState.Symmetry.RadialSegments = 6;
        }

        private void Symmetry_SetRadial8_Click(object sender, RoutedEventArgs e)
        {
            if (_toolState?.Symmetry == null) return;
            _toolState.Symmetry.Enabled = true;
            _toolState.Symmetry.Mode = SymmetryMode.Radial;
            _toolState.Symmetry.RadialSegments = 8;
        }

        private void Symmetry_SetKaleidoscope_Click(object sender, RoutedEventArgs e)
        {
            if (_toolState?.Symmetry == null) return;
            _toolState.Symmetry.Enabled = true;
            _toolState.Symmetry.Mode = SymmetryMode.Kaleidoscope;
        }

        private void Symmetry_CenterAxis_Click(object sender, RoutedEventArgs e)
        {
            _toolState?.Symmetry.CenterAxis();
        }

        private void Symmetry_ToggleAxisLines_Click(object sender, RoutedEventArgs e)
        {
            if (_toolState?.Symmetry == null) return;
            _toolState.Symmetry.ShowAxisLines = !_toolState.Symmetry.ShowAxisLines;
        }
    }
}
