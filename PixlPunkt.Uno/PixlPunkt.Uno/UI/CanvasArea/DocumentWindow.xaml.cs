using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.Palette;
using PixlPunkt.Uno.Core.Tools;
using PixlPunkt.Uno.UI.CanvasHost;
using PixlPunkt.Uno.UI.Helpers;

namespace PixlPunkt.Uno.UI.CanvasArea
{
    public sealed partial class DocumentWindow : Window
    {
        public CanvasViewHost Host { get; }

        private readonly PaletteService _palette;
        private readonly ToolState _toolState;

        public DocumentWindow(CanvasDocument doc, PaletteService palette, ToolState toolState)
        {
            InitializeComponent();
            _palette = palette;
            _toolState = toolState;
            WindowHost.ApplyChrome(this, resizable: true, alwaysOnTop: false, title: doc.Name ?? "New Canvas", owner: App.PixlPunktMainWindow);

            Host = new CanvasViewHost(doc);
            HostPlaceholder.Child = Host;


            Host.BindToolState(_toolState, _palette);

            var fgMerged = MergeFgWithOpacity(_palette.Foreground);
            Host.ApplyBrush(_toolState.Brush, fgMerged);


            WireEventHandlers();
            AddAccelerators();
        }

        private void WireEventHandlers()
        {
            _palette.ForegroundChanged += Palette_ForegroundChanged;
            Closed += (_, __) => _palette.ForegroundChanged -= Palette_ForegroundChanged;

            Host.ForegroundSampledLive += Host_ForegroundSampledLive;
            Host.BackgroundSampledLive += Host_BackgroundSampledLive;
            Closed += (_, __) =>
            {
                Host.ForegroundSampledLive -= Host_ForegroundSampledLive;
                Host.BackgroundSampledLive -= Host_BackgroundSampledLive;
            };

            Host.HistoryStateChanged += Host_HistoryStateChanged;
            Closed += (_, __) => Host.HistoryStateChanged -= Host_HistoryStateChanged;
            UpdateHistoryUI();

            _toolState.BrushChanged += ToolState_BrushChanged;
            Closed += (_, __) =>
            {
                _toolState.BrushChanged -= ToolState_BrushChanged;
            };
        }

        private void Palette_ForegroundChanged(uint c)
        {
            Host.SetForeground(MergeFgWithOpacity(c));
        }

        private void Host_ForegroundSampledLive(uint bgra)
        {
            _palette.SetForeground(bgra);
        }

        private void Host_BackgroundSampledLive(uint bgra)
        {
            _palette.SetBackground(bgra);
        }



        private void ToolState_BrushChanged(BrushSettings s)
        {
            Host.ApplyBrush(s, MergeFgWithOpacity(_palette.Foreground));
        }

        private void Host_HistoryStateChanged()
        {
            UpdateHistoryUI();
        }

        private void UpdateHistoryUI()
        {
            UndoBtn.IsEnabled = Host.CanUndo;
            RedoBtn.IsEnabled = Host.CanRedo;
        }

        private void UndoBtn_Click(object sender, RoutedEventArgs e)
        {
            Host.Undo();
            UpdateHistoryUI();
        }

        private void RedoBtn_Click(object sender, RoutedEventArgs e)
        {
            Host.Redo();
            UpdateHistoryUI();
        }

        private void File_Close_Click(object sender, RoutedEventArgs e) => Close();

        private void View_Fit_Click(object sender, RoutedEventArgs e) => Host.Fit();

        private void View_Actual_Click(object sender, RoutedEventArgs e) => Host.CanvasActualSize();

        private void View_TogglePixelGrid_Click(object sender, RoutedEventArgs e) => Host.TogglePixelGrid();

        private void View_ToggleTileGrid_Click(object sender, RoutedEventArgs e) => Host.ToggleTileGrid();

        private void AddAccelerators()
        {
            var undo = new KeyboardAccelerator
            {
                Key = Windows.System.VirtualKey.Z,
                Modifiers = Windows.System.VirtualKeyModifiers.Control
            };
            undo.Invoked += (_, a) =>
            {
                if (Host.CanUndo)
                {
                    Host.Undo();
                    UpdateHistoryUI();
                    a.Handled = true;
                }
            };

            var redo = new KeyboardAccelerator
            {
                Key = Windows.System.VirtualKey.Y,
                Modifiers = Windows.System.VirtualKeyModifiers.Control
            };
            redo.Invoked += (_, a) =>
            {
                if (Host.CanRedo)
                {
                    Host.Redo();
                    UpdateHistoryUI();
                    a.Handled = true;
                }
            };

            var redoAlt = new KeyboardAccelerator
            {
                Key = Windows.System.VirtualKey.Z,
                Modifiers = Windows.System.VirtualKeyModifiers.Control | Windows.System.VirtualKeyModifiers.Shift
            };
            redoAlt.Invoked += (_, a) =>
            {
                if (Host.CanRedo)
                {
                    Host.Redo();
                    UpdateHistoryUI();
                    a.Handled = true;
                }
            };

            var fit = new KeyboardAccelerator
            {
                Key = Windows.System.VirtualKey.Number0,
                Modifiers = Windows.System.VirtualKeyModifiers.Control
            };
            fit.Invoked += (_, a) =>
            {
                Host.Fit();
                a.Handled = true;
            };

            var actual = new KeyboardAccelerator
            {
                Key = Windows.System.VirtualKey.Number1,
                Modifiers = Windows.System.VirtualKeyModifiers.Control
            };
            actual.Invoked += (_, a) =>
            {
                Host.CanvasActualSize();
                a.Handled = true;
            };

            Root.KeyboardAccelerators.Add(undo);
            Root.KeyboardAccelerators.Add(redo);
            Root.KeyboardAccelerators.Add(redoAlt);
            Root.KeyboardAccelerators.Add(fit);
            Root.KeyboardAccelerators.Add(actual);
        }

        private uint MergeFgWithOpacity(uint paletteFg)
            => (paletteFg & 0x00FFFFFFu) | ((uint)_toolState.Brush.Opacity << 24);
    }
}