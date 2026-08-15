using System;
using System.Collections.Generic;
using System.Linq;

namespace Crntly.StreamerBot.UI.Overlayer
{
    /// <summary>
    /// Thread-safe facade used by a Streamer.bot C# action. No Streamer.bot types leak into the DLL.
    /// </summary>
    public sealed class OverlayerUi : IDisposable
    {
        private OverlayerWindow _window;

        public event EventHandler StartServerRequested;
        public event EventHandler StopServerRequested;
        public event EventHandler<OverlayItemEventArgs> OverlayChanged;
        public event EventHandler<OverlayItemEventArgs> OverlayDeleted;
        public event EventHandler<OverlayOrderEventArgs> OverlayOrderChanged;

        public void Show(IEnumerable<OverlayItem> items, bool serverRunning, string serverUrl)
        {
            var snapshot = (items ?? Enumerable.Empty<OverlayItem>()).Select(x => x.Clone()).ToList();
            CrntlyUiHost.BeginInvoke(() =>
            {
                EnsureWindow();
                _window.SetItems(snapshot);
                _window.SetServerState(serverRunning, serverUrl);

                if (!_window.IsVisible)
                    _window.Show();

                if (_window.WindowState == System.Windows.WindowState.Minimized)
                    _window.WindowState = System.Windows.WindowState.Normal;

                _window.Activate();
            });
        }

        public void SetItems(IEnumerable<OverlayItem> items)
        {
            var snapshot = (items ?? Enumerable.Empty<OverlayItem>()).Select(x => x.Clone()).ToList();
            CrntlyUiHost.BeginInvoke(() =>
            {
                EnsureWindow();
                _window.SetItems(snapshot);
            });
        }

        public void SetServerState(bool running, string serverUrl)
        {
            CrntlyUiHost.BeginInvoke(() =>
            {
                EnsureWindow();
                _window.SetServerState(running, serverUrl);
            });
        }

        public void Dispose()
        {
            CrntlyUiHost.BeginInvoke(() =>
            {
                if (_window == null)
                    return;

                Unwire(_window);
                _window.CloseForShutdown();
                _window = null;
            });
        }

        private void EnsureWindow()
        {
            if (_window != null)
                return;

            _window = new OverlayerWindow();
            _window.StartServerRequested += Window_StartServerRequested;
            _window.StopServerRequested += Window_StopServerRequested;
            _window.OverlayChanged += Window_OverlayChanged;
            _window.OverlayDeleted += Window_OverlayDeleted;
            _window.OverlayOrderChanged += Window_OverlayOrderChanged;
        }

        private void Unwire(OverlayerWindow window)
        {
            window.StartServerRequested -= Window_StartServerRequested;
            window.StopServerRequested -= Window_StopServerRequested;
            window.OverlayChanged -= Window_OverlayChanged;
            window.OverlayDeleted -= Window_OverlayDeleted;
            window.OverlayOrderChanged -= Window_OverlayOrderChanged;
        }

        private void Window_StartServerRequested(object sender, EventArgs e) => StartServerRequested?.Invoke(this, e);
        private void Window_StopServerRequested(object sender, EventArgs e) => StopServerRequested?.Invoke(this, e);
        private void Window_OverlayChanged(object sender, OverlayItemEventArgs e) => OverlayChanged?.Invoke(this, e);
        private void Window_OverlayDeleted(object sender, OverlayItemEventArgs e) => OverlayDeleted?.Invoke(this, e);
        private void Window_OverlayOrderChanged(object sender, OverlayOrderEventArgs e) => OverlayOrderChanged?.Invoke(this, e);
    }
}
