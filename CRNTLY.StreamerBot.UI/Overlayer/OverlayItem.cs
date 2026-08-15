using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Crntly.StreamerBot.UI.Overlayer
{
    public sealed class OverlayItem : INotifyPropertyChanged
    {
        private string _id = Guid.NewGuid().ToString("N");
        private string _name = "New overlay";
        private string _url = string.Empty;
        private string _width = "100%";
        private string _height = "100%";
        private string _top = "0px";
        private string _left = "0px";
        private bool _enabled = true;
        private bool _isPreview;

        public string Id { get => _id; set => Set(ref _id, string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value); }
        public string Name { get => _name; set => Set(ref _name, value ?? string.Empty); }
        public string Url { get => _url; set => Set(ref _url, value ?? string.Empty); }
        public string Width { get => _width; set => Set(ref _width, value ?? "100%"); }
        public string Height { get => _height; set => Set(ref _height, value ?? "100%"); }
        public string Top { get => _top; set => Set(ref _top, value ?? "0px"); }
        public string Left { get => _left; set => Set(ref _left, value ?? "0px"); }
        public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }

        /// <summary>
        /// Transient UI-to-script hint. Preview changes update the live compositor but are
        /// not persisted until the normal debounced edit is committed.
        /// </summary>
        public bool IsPreview { get => _isPreview; set => Set(ref _isPreview, value); }

        public string SourceKind => Url != null && Url.StartsWith("file:///", StringComparison.OrdinalIgnoreCase)
            ? "LOCAL"
            : "WEB";

        public OverlayItem Clone()
        {
            return new OverlayItem
            {
                Id = Id,
                Name = Name,
                Url = Url,
                Width = Width,
                Height = Height,
                Top = Top,
                Left = Left,
                Enabled = Enabled,
                IsPreview = IsPreview
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void Set<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (propertyName == nameof(Url))
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SourceKind)));
        }
    }

    public sealed class OverlayItemEventArgs : EventArgs
    {
        public OverlayItemEventArgs(OverlayItem item) => Item = item;
        public OverlayItem Item { get; }
    }

    public sealed class OverlayOrderEventArgs : EventArgs
    {
        public OverlayOrderEventArgs(IReadOnlyList<OverlayItem> items) => Items = items;
        public IReadOnlyList<OverlayItem> Items { get; }
    }
}
