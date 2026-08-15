using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Crntly.StreamerBot.UI.Overlayer
{
    /// <summary>
    /// Reflection-friendly boundary for Streamer.bot scripts. The script can load this
    /// type dynamically, so CRNTLY.StreamerBot.UI.dll does not need to exist when the
    /// Streamer.bot C# action is compiled.
    /// </summary>
    public sealed class OverlayerScriptBridge : IDisposable
    {
        private readonly OverlayerUi _ui = new OverlayerUi();
        private bool _disposed;

        public OverlayerScriptBridge()
        {
            _ui.StartServerRequested += Ui_StartServerRequested;
            _ui.StopServerRequested += Ui_StopServerRequested;
            _ui.OverlayChanged += Ui_OverlayChanged;
            _ui.OverlayDeleted += Ui_OverlayDeleted;
            _ui.OverlayOrderChanged += Ui_OverlayOrderChanged;
        }

        public Action StartServerRequested { get; set; }
        public Action StopServerRequested { get; set; }
        public Action<string> OverlayChanged { get; set; }
        public Action<string> OverlayDeleted { get; set; }
        public Action<string> OverlayOrderChanged { get; set; }

        public string AssemblyVersion
        {
            get { return typeof(OverlayerScriptBridge).Assembly.GetName().Version.ToString(); }
        }

        public void ShowJson(string itemsJson, bool serverRunning, string serverUrl)
        {
            ThrowIfDisposed();
            _ui.Show(ParseItems(itemsJson), serverRunning, serverUrl);
        }

        public void SetItemsJson(string itemsJson)
        {
            ThrowIfDisposed();
            _ui.SetItems(ParseItems(itemsJson));
        }

        public void SetServerState(bool running, string serverUrl)
        {
            ThrowIfDisposed();
            _ui.SetServerState(running, serverUrl);
        }

        private static List<OverlayItem> ParseItems(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<OverlayItem>();

            try
            {
                return Deserialize<List<OverlayItem>>(json) ?? new List<OverlayItem>();
            }
            catch
            {
                return new List<OverlayItem>();
            }
        }

        private static string SerializeItem(OverlayItem item)
        {
            return Serialize(item == null ? null : item.Clone());
        }

        private static string SerializeItems(IEnumerable<OverlayItem> items)
        {
            return Serialize((items ?? Enumerable.Empty<OverlayItem>()).Select(x => x.Clone()).ToList());
        }

        private static T Deserialize<T>(string json)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            var bytes = Encoding.UTF8.GetBytes(json ?? string.Empty);
            using (var stream = new MemoryStream(bytes))
                return (T)serializer.ReadObject(stream);
        }

        private static string Serialize<T>(T value)
        {
            var serializer = new DataContractJsonSerializer(typeof(T));
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private void Ui_StartServerRequested(object sender, EventArgs e)
        {
            var callback = StartServerRequested;
            if (callback != null)
                callback();
        }

        private void Ui_StopServerRequested(object sender, EventArgs e)
        {
            var callback = StopServerRequested;
            if (callback != null)
                callback();
        }

        private void Ui_OverlayChanged(object sender, OverlayItemEventArgs e)
        {
            var callback = OverlayChanged;
            if (callback != null)
                callback(SerializeItem(e.Item));
        }

        private void Ui_OverlayDeleted(object sender, OverlayItemEventArgs e)
        {
            var callback = OverlayDeleted;
            if (callback != null)
                callback(SerializeItem(e.Item));
        }

        private void Ui_OverlayOrderChanged(object sender, OverlayOrderEventArgs e)
        {
            var callback = OverlayOrderChanged;
            if (callback != null)
                callback(SerializeItems(e.Items));
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(OverlayerScriptBridge));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _ui.StartServerRequested -= Ui_StartServerRequested;
            _ui.StopServerRequested -= Ui_StopServerRequested;
            _ui.OverlayChanged -= Ui_OverlayChanged;
            _ui.OverlayDeleted -= Ui_OverlayDeleted;
            _ui.OverlayOrderChanged -= Ui_OverlayOrderChanged;
            _ui.Dispose();
        }
    }
}
