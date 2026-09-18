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
        // OUTLINE
        // ════════════════════════════════════════════════════════════════════

        private void OutlineCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (OutlineColorPanel != null)
                OutlineColorPanel.Visibility = OutlineCheckBox.IsChecked == true
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            if (_suppressVoxelUiEvents) return;
            PersistVoxelPreviewStateToDocument();
            RenderViewport();
        }

        private void OutlineColor_Changed(object sender, uint e)
        {
            if (_suppressVoxelUiEvents) return;
            PersistVoxelPreviewStateToDocument();
            if (OutlineCheckBox?.IsChecked == true)
                RenderViewport();
        }

        private void OutlineSize_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_suppressVoxelUiEvents) return;
            PersistVoxelPreviewStateToDocument();
            if (OutlineCheckBox?.IsChecked == true)
                RenderViewport();
        }

        private void PixelBaseSize_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_suppressVoxelUiEvents) return;
            PersistVoxelPreviewStateToDocument();
            if (_lastVolume != null)
                RenderViewport();
        }

        private void BackdropGrid_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressVoxelUiEvents) return;
            PersistVoxelPreviewStateToDocument();
            RenderViewport();
        }

        private void BackdropProjectionTiles_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressVoxelUiEvents) return;
            PersistVoxelPreviewStateToDocument();
            RenderViewport();
        }

        private void BackdropCageScale_Changed(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_suppressVoxelUiEvents) return;
            PersistVoxelPreviewStateToDocument();
            RenderViewport();
        }

        private void SurfaceVoxelGrid_Changed(object sender, RoutedEventArgs e)
        {
            if (_suppressVoxelUiEvents) return;
            PersistVoxelPreviewStateToDocument();
            RenderViewport();
        }
    }
}
