using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Crntly.StreamerBot.UI.Overlayer
{
    public partial class OverlayerWindow : Window
    {
        private bool _allowClose;
        private bool _loadingItems;

        public OverlayerWindow()
        {
            InitializeComponent();
            Items = new ObservableCollection<OverlayItem>();
            OverlayList.ItemsSource = Items;
            Closing += OnClosing;
            SetEditorEnabled(false);
        }

        public ObservableCollection<OverlayItem> Items { get; }
        public bool IsServerRunning { get; private set; }

        public event EventHandler StartServerRequested;
        public event EventHandler StopServerRequested;
        public event EventHandler<OverlayItemEventArgs> OverlayChanged;
        public event EventHandler<OverlayItemEventArgs> OverlayDeleted;
        public event EventHandler<OverlayOrderEventArgs> OverlayOrderChanged;

        public void SetItems(IEnumerable<OverlayItem> items)
        {
            _loadingItems = true;
            try
            {
                Items.Clear();
                foreach (var item in items ?? Enumerable.Empty<OverlayItem>())
                    Items.Add(item.Clone());

                if (Items.Count > 0)
                    OverlayList.SelectedIndex = 0;
                else
                {
                    OverlayList.SelectedItem = null;
                    ClearEditor();
                    SetEditorEnabled(false);
                }
            }
            finally
            {
                _loadingItems = false;
            }
        }

        public void SetServerState(bool running, string url)
        {
            IsServerRunning = running;
            ServerButton.Content = running ? "Stop server" : "Start server";
            ServerUrlBox.Text = string.IsNullOrWhiteSpace(url) ? "http://localhost:42069/" : url;
            ServerBadgeText.Text = running ? "SERVER ONLINE" : "SERVER OFFLINE";
            ServerBadgeText.Foreground = (Brush)FindResource(running ? "Crntly.Success" : "Crntly.TextMuted");
            ServerDot.Fill = (Brush)FindResource(running ? "Crntly.Success" : "Crntly.TextMuted");
        }

        public void CloseForShutdown()
        {
            _allowClose = true;
            Close();
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_allowClose)
                return;

            e.Cancel = true;
            Hide();
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
                return;
            }

            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Close_Click(object sender, RoutedEventArgs e) => Hide();

        private void ServerButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsServerRunning)
                StopServerRequested?.Invoke(this, EventArgs.Empty);
            else
                StartServerRequested?.Invoke(this, EventArgs.Empty);
        }

        private void CopyUrl_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(ServerUrlBox.Text))
                Clipboard.SetText(ServerUrlBox.Text);
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            var item = new OverlayItem();
            Items.Add(item);
            OverlayList.SelectedItem = item;
            NameBox.Focus();
            NameBox.SelectAll();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var item = OverlayList.SelectedItem as OverlayItem;
            if (item == null)
                return;

            if (MessageBox.Show(this, $"Remove '{item.Name}'?", "Remove overlay", MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            var index = OverlayList.SelectedIndex;
            Items.Remove(item);
            OverlayDeleted?.Invoke(this, new OverlayItemEventArgs(item.Clone()));
            RaiseOrderChanged();

            if (Items.Count > 0)
                OverlayList.SelectedIndex = Math.Min(index, Items.Count - 1);
            else
            {
                ClearEditor();
                SetEditorEnabled(false);
            }
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            var index = OverlayList.SelectedIndex;
            if (index <= 0)
                return;

            Items.Move(index, index - 1);
            OverlayList.SelectedIndex = index - 1;
            RaiseOrderChanged();
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            var index = OverlayList.SelectedIndex;
            if (index < 0 || index >= Items.Count - 1)
                return;

            Items.Move(index, index + 1);
            OverlayList.SelectedIndex = index + 1;
            RaiseOrderChanged();
        }

        private void OverlayList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = OverlayList.SelectedItem as OverlayItem;
            if (item == null)
            {
                ClearEditor();
                SetEditorEnabled(false);
                return;
            }

            SetEditorEnabled(true);
            NameBox.Text = item.Name;
            UrlBox.Text = item.Url;
            WidthBox.Text = item.Width;
            HeightBox.Text = item.Height;
            LeftBox.Text = item.Left;
            TopBox.Text = item.Top;
            EnabledBox.IsChecked = item.Enabled;
        }

        private void OverlayEnabledChanged(object sender, RoutedEventArgs e)
        {
            if (_loadingItems)
                return;

            var checkBox = sender as CheckBox;
            var item = checkBox?.DataContext as OverlayItem;
            if (item == null)
                return;

            OverlayChanged?.Invoke(this, new OverlayItemEventArgs(item.Clone()));
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var item = OverlayList.SelectedItem as OverlayItem;
            if (item == null)
                return;

            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                ShowValidation("Give this overlay a name.");
                return;
            }

            Uri uri;
            if (!Uri.TryCreate(UrlBox.Text.Trim(), UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeFile))
            {
                ShowValidation("URL must be http://, https://, or file:///.");
                return;
            }

            if (!IsCssLength(WidthBox.Text) || !IsCssLength(HeightBox.Text) ||
                !IsCssLength(LeftBox.Text) || !IsCssLength(TopBox.Text))
            {
                ShowValidation("Width, height, left, and top must use px, %, vw, or vh (or 0).");
                return;
            }

            item.Name = NameBox.Text.Trim();
            item.Url = UrlBox.Text.Trim();
            item.Width = NormalizeCssLength(WidthBox.Text, "100%");
            item.Height = NormalizeCssLength(HeightBox.Text, "100%");
            item.Left = NormalizeCssLength(LeftBox.Text, "0px");
            item.Top = NormalizeCssLength(TopBox.Text, "0px");
            item.Enabled = EnabledBox.IsChecked == true;

            OverlayChanged?.Invoke(this, new OverlayItemEventArgs(item.Clone()));
            OverlayList.Items.Refresh();
        }

        private void RaiseOrderChanged()
        {
            OverlayOrderChanged?.Invoke(this,
                new OverlayOrderEventArgs(Items.Select(x => x.Clone()).ToList()));
        }

        private static bool IsCssLength(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return true;

            value = value.Trim().ToLowerInvariant();
            if (value == "0")
                return true;

            return value.EndsWith("px") || value.EndsWith("%") || value.EndsWith("vw") || value.EndsWith("vh");
        }

        private static string NormalizeCssLength(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;
            return value.Trim().ToLowerInvariant() == "0" ? "0px" : value.Trim().ToLowerInvariant();
        }

        private void ShowValidation(string message)
        {
            MessageBox.Show(this, message, "Check overlay settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ClearEditor()
        {
            NameBox.Text = string.Empty;
            UrlBox.Text = string.Empty;
            WidthBox.Text = "100%";
            HeightBox.Text = "100%";
            LeftBox.Text = "0px";
            TopBox.Text = "0px";
            EnabledBox.IsChecked = true;
        }

        private void SetEditorEnabled(bool enabled)
        {
            NameBox.IsEnabled = enabled;
            UrlBox.IsEnabled = enabled;
            WidthBox.IsEnabled = enabled;
            HeightBox.IsEnabled = enabled;
            LeftBox.IsEnabled = enabled;
            TopBox.IsEnabled = enabled;
            EnabledBox.IsEnabled = enabled;
        }
    }
}
