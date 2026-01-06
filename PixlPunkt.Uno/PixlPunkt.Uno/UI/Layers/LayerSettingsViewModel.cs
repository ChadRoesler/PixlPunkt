using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PixlPunkt.Uno.Core.Document;
using PixlPunkt.Uno.Core.Document.Layer;

namespace PixlPunkt.Uno.UI.Layers
{
    public sealed class LayerSettingsViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly CanvasDocument _doc;
        private readonly RasterLayer _layer;
        private LayerEffectBase? _selectedEffect;
        private bool _disposed;

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// Live list of effects for this layer (already pre-created on the layer)
        public ObservableCollection<LayerEffectBase> Effects { get; }

        public LayerEffectBase? SelectedEffect
        {
            get => _selectedEffect;
            set
            {
                if (ReferenceEquals(_selectedEffect, value)) return;
                _selectedEffect = value;
                OnPropertyChanged();
            }
        }

        public LayerSettingsViewModel(CanvasDocument doc, RasterLayer layer)
        {
            _doc = doc;
            _layer = layer;

            Effects = layer.Effects;

            Effects.CollectionChanged += Effects_CollectionChanged;
            foreach (var fx in Effects)
                fx.PropertyChanged += Fx_PropertyChanged;
        }

        private void Effects_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems is not null)
            {
                foreach (LayerEffectBase fx in e.NewItems)
                    fx.PropertyChanged += Fx_PropertyChanged;
            }

            if (e.OldItems is not null)
            {
                foreach (LayerEffectBase fx in e.OldItems)
                    fx.PropertyChanged -= Fx_PropertyChanged;
            }

            NotifyStructureChanged();
        }

        private void Fx_PropertyChanged(object? sender, PropertyChangedEventArgs e)
            => NotifyStructureChanged();

        private void NotifyStructureChanged() => _doc.RaiseStructureChanged();

        public void MoveEffectUp(LayerEffectBase fx)
        {
            int i = Effects.IndexOf(fx);
            if (i > 0)
                Effects.Move(i, i - 1);
        }

        public void MoveEffectDown(LayerEffectBase fx)
        {
            int i = Effects.IndexOf(fx);
            if (i >= 0 && i < Effects.Count - 1)
                Effects.Move(i, i + 1);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Effects.CollectionChanged -= Effects_CollectionChanged;
            foreach (var fx in Effects)
                fx.PropertyChanged -= Fx_PropertyChanged;
        }
    }
}
