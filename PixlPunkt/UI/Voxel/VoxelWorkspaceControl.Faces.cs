using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using PixlPunkt.Core.Document;
using PixlPunkt.Core.Document.Layer;
using PixlPunkt.Core.Enums;
using PixlPunkt.Core.Imaging;
using PixlPunkt.Core.Logging;
using PixlPunkt.Core.Palette;
using PixlPunkt.Core.Tile;
using PixlPunkt.Core.Voxel;
using PixlPunkt.Core.Voxel.Editing;
using PixlPunkt.Core.Voxel.Tools;
using PixlPunkt.PluginSdk.Voxel;
using PixlPunkt.UI.Controls;
using PixlPunkt.UI.Helpers;
using PixlPunkt.UI.Voxel.Tools;
using Windows.System;
using Windows.Storage.Pickers;
using Windows.UI.Core;

namespace PixlPunkt.UI.Voxel
{
    public sealed partial class VoxelWorkspaceControl
    {
        // ════════════════════════════════════════════════════════════════════
        // FACE MODE TOGGLE
        // ════════════════════════════════════════════════════════════════════

        private void FaceModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ThreeFacePanel == null || SixFacePanel == null) return;

            bool isSixFace = FaceModeCombo.SelectedIndex == 1;
            ThreeFacePanel.Visibility = isSixFace ? Visibility.Collapsed : Visibility.Visible;
            SixFacePanel.Visibility = isSixFace ? Visibility.Visible : Visibility.Collapsed;

            if (_suppressVoxelUiEvents) return;
            PersistVoxelPreviewStateToDocument();
        }

        // ════════════════════════════════════════════════════════════════════
        // COLOR LINKING
        // ════════════════════════════════════════════════════════════════════

        private void ColorLinking_Changed(object sender, RoutedEventArgs e)
        {
            if (ColorTolerancePanel != null)
                ColorTolerancePanel.Visibility = ColorLinkingCheckBox.IsChecked == true
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            if (_suppressVoxelUiEvents) return;
            PersistVoxelPreviewStateToDocument();

            if (_lastVolume != null)
                BuildAndRender();
        }

        private void ColorTolerance_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_suppressVoxelUiEvents) return;
            PersistVoxelPreviewStateToDocument();

            if (_lastVolume != null)
                BuildAndRender();
        }

        /// <summary>
        /// Gets the current color tolerance value, or -1 if color linking is disabled.
        /// </summary>
        private int GetColorTolerance()
        {
            if (ColorLinkingCheckBox?.IsChecked != true) return -1;
            return (int)(ColorToleranceBox?.Value ?? 32);
        }
    }
}
