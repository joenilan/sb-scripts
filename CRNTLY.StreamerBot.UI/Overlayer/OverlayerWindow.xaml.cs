using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Crntly.StreamerBot.UI.Overlayer
{
    public partial class OverlayerWindow : Window
    {
        private readonly DispatcherTimer _saveDebounceTimer;
        private OverlayItem _editingItem;
        private bool _allowClose;
        private bool _loadingItems;
        private bool _loadingEditor;
        private bool _syncingPositionControls;

        public OverlayerWindow()
        {
            _loadingEditor = true;
            _saveDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _saveDebounceTimer.Tick += AutosaveTimer_Tick;

            InitializeComponent();
            Items = new ObservableCollection<OverlayItem>();
            OverlayList.ItemsSource = Items;
            Closing += OnClosing;
            SetEditorEnabled(false);
            OpenPreviewButton.IsEnabled = false;
            UpdateActionStates();
            _loadingEditor = false;
            SetEditorStatus("Autosave", "Crntly.TextMuted");
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
            _saveDebounceTimer.Stop();
            _editingItem = null;
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
                    ClearEditorSafely();
                    SetEditorEnabled(false);
                }
            }
            finally
            {
                _loadingItems = false;
                UpdateActionStates();
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
            OpenPreviewButton.IsEnabled = running;
        }

        public void CloseForShutdown()
        {
            FlushPendingAutosave();
            _saveDebounceTimer.Stop();
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
            FlushPendingAutosave();

            if (IsServerRunning)
                StopServerRequested?.Invoke(this, EventArgs.Empty);
            else
                StartServerRequested?.Invoke(this, EventArgs.Empty);
        }

        private void CopyUrl_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ServerUrlBox.Text))
                return;

            try
            {
                Clipboard.SetText(ServerUrlBox.Text);
            }
            catch
            {
                // Clipboard can briefly be locked by another process. A failed copy
                // should never interfere with the compositor or editor state.
            }
        }

        private void OpenPreview_Click(object sender, RoutedEventArgs e)
        {
            if (!IsServerRunning)
                return;

            OpenExternal(ServerUrlBox.Text, false);
        }

        private void OpenSource_Click(object sender, RoutedEventArgs e)
        {
            if (!OpenExternal(UrlBox.Text, true))
                SetEditorStatus("Check URL", "Crntly.Danger");
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            FlushPendingAutosave();
            var item = new OverlayItem();
            Items.Add(item);
            OverlayList.SelectedItem = item;
            NameBox.Focus();
            NameBox.SelectAll();
            SetEditorStatus("Enter a URL to save", "Crntly.TextMuted");
            UpdateActionStates();
        }

        private void Duplicate_Click(object sender, RoutedEventArgs e)
        {
            var source = OverlayList.SelectedItem as OverlayItem;
            if (source == null)
                return;

            FlushPendingAutosave();

            Uri ignored;
            if (!TryGetSupportedUri(source.Url, out ignored))
            {
                SetEditorStatus("Add a valid URL first", "Crntly.Danger");
                return;
            }

            var copy = source.Clone();
            copy.Id = Guid.NewGuid().ToString("N");
            copy.Name = string.IsNullOrWhiteSpace(source.Name) ? "Overlay copy" : source.Name + " copy";
            copy.IsPreview = false;

            var insertAt = Math.Max(0, OverlayList.SelectedIndex + 1);
            Items.Insert(insertAt, copy);
            OverlayChanged?.Invoke(this, new OverlayItemEventArgs(copy.Clone()));
            RaiseOrderChanged();
            OverlayList.SelectedItem = copy;
            SetEditorStatus("Duplicated", "Crntly.Success");
            UpdateActionStates();
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var item = OverlayList.SelectedItem as OverlayItem;
            if (item == null)
                return;

            if (MessageBox.Show(this, $"Remove '{item.Name}'?", "Remove overlay", MessageBoxButton.YesNo,
                    MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            _saveDebounceTimer.Stop();
            var index = OverlayList.SelectedIndex;
            Items.Remove(item);
            if (ReferenceEquals(_editingItem, item))
                _editingItem = null;

            OverlayDeleted?.Invoke(this, new OverlayItemEventArgs(item.Clone()));
            RaiseOrderChanged();

            if (Items.Count > 0)
                OverlayList.SelectedIndex = Math.Min(index, Items.Count - 1);
            else
            {
                ClearEditorSafely();
                SetEditorEnabled(false);
            }

            UpdateActionStates();
        }

        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            var index = OverlayList.SelectedIndex;
            if (index <= 0)
                return;

            FlushPendingAutosave();
            Items.Move(index, index - 1);
            OverlayList.SelectedIndex = index - 1;
            RaiseOrderChanged();
            UpdateActionStates();
        }

        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            var index = OverlayList.SelectedIndex;
            if (index < 0 || index >= Items.Count - 1)
                return;

            FlushPendingAutosave();
            Items.Move(index, index + 1);
            OverlayList.SelectedIndex = index + 1;
            RaiseOrderChanged();
            UpdateActionStates();
        }

        private void ResetLayout_Click(object sender, RoutedEventArgs e)
        {
            var item = _editingItem;
            if (item == null)
                return;

            _saveDebounceTimer.Stop();
            _loadingEditor = true;
            try
            {
                WidthBox.Text = "100%";
                HeightBox.Text = "100%";
                LeftBox.Text = "0";
                TopBox.Text = "0";
                SyncPositionSlider(LeftBox, LeftSlider);
                SyncPositionSlider(TopBox, TopSlider);
            }
            finally
            {
                _loadingEditor = false;
            }

            var preview = item.Clone();
            preview.Width = "100%";
            preview.Height = "100%";
            preview.Left = "0px";
            preview.Top = "0px";
            preview.IsPreview = true;
            OverlayChanged?.Invoke(this, new OverlayItemEventArgs(preview));
            ScheduleAutosave();
        }

        private void OverlayList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_loadingItems)
                FlushPendingAutosave();

            var item = OverlayList.SelectedItem as OverlayItem;
            _editingItem = item;

            if (item == null)
            {
                ClearEditorSafely();
                SetEditorEnabled(false);
                UpdateActionStates();
                return;
            }

            _loadingEditor = true;
            try
            {
                SetEditorEnabled(true);
                NameBox.Text = item.Name;
                UrlBox.Text = item.Url;
                WidthBox.Text = item.Width;
                HeightBox.Text = item.Height;
                LeftBox.Text = DisplayPosition(item.Left);
                TopBox.Text = DisplayPosition(item.Top);
                EnabledBox.IsChecked = item.Enabled;
                SyncPositionSlider(LeftBox, LeftSlider);
                SyncPositionSlider(TopBox, TopSlider);
                SetEditorStatus("Autosave", "Crntly.TextMuted");
            }
            finally
            {
                _loadingEditor = false;
                UpdateActionStates();
            }
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
            if (ReferenceEquals(_editingItem, item))
                SetEditorStatus("Saved", "Crntly.Success");
        }

        private void EditorEnabledChanged(object sender, RoutedEventArgs e)
        {
            if (_loadingEditor || _loadingItems)
                return;

            var item = _editingItem;
            if (item == null)
                return;

            item.Enabled = EnabledBox.IsChecked == true;
            OverlayChanged?.Invoke(this, new OverlayItemEventArgs(item.Clone()));
            OverlayList.Items.Refresh();
            SetEditorStatus("Saved", "Crntly.Success");
        }

        private void EditorField_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loadingEditor || _editingItem == null)
                return;

            if (ReferenceEquals(sender, LeftBox))
                SyncPositionSlider(LeftBox, LeftSlider);
            else if (ReferenceEquals(sender, TopBox))
                SyncPositionSlider(TopBox, TopSlider);
            else if (ReferenceEquals(sender, UrlBox))
                UpdateActionStates();

            ScheduleAutosave();
        }

        private void PositionSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_loadingEditor || _syncingPositionControls || _editingItem == null)
                return;

            _syncingPositionControls = true;
            try
            {
                if (ReferenceEquals(sender, LeftSlider))
                    LeftBox.Text = FormatPositionNumber(LeftSlider.Value);
                else if (ReferenceEquals(sender, TopSlider))
                    TopBox.Text = FormatPositionNumber(TopSlider.Value);
            }
            finally
            {
                _syncingPositionControls = false;
            }

            PreviewPosition();
            ScheduleAutosave();
        }

        private void PreviewPosition()
        {
            var item = _editingItem;
            if (item == null)
                return;

            string left;
            string top;
            if (!TryNormalizePosition(LeftBox.Text, "0px", out left) ||
                !TryNormalizePosition(TopBox.Text, "0px", out top))
                return;

            var preview = item.Clone();
            preview.Left = left;
            preview.Top = top;
            preview.IsPreview = true;
            OverlayChanged?.Invoke(this, new OverlayItemEventArgs(preview));
        }

        private void ScheduleAutosave()
        {
            _saveDebounceTimer.Stop();
            _saveDebounceTimer.Start();
            SetEditorStatus("Saving…", "Crntly.Accent");
        }

        private void AutosaveTimer_Tick(object sender, EventArgs e)
        {
            _saveDebounceTimer.Stop();
            CommitEditorChanges();
        }

        private void FlushPendingAutosave()
        {
            if (!_saveDebounceTimer.IsEnabled)
                return;

            _saveDebounceTimer.Stop();
            CommitEditorChanges();
        }

        private bool CommitEditorChanges()
        {
            if (_loadingEditor)
                return false;

            var item = _editingItem;
            if (item == null)
                return false;

            string normalizedWidth;
            string normalizedHeight;
            string normalizedLeft;
            string normalizedTop;
            string validationMessage;

            if (!TryValidateEditor(out normalizedWidth, out normalizedHeight, out normalizedLeft, out normalizedTop, out validationMessage))
            {
                SetEditorStatus(validationMessage, "Crntly.Danger");
                return false;
            }

            item.Name = NameBox.Text.Trim();
            item.Url = UrlBox.Text.Trim();
            item.Width = normalizedWidth;
            item.Height = normalizedHeight;
            item.Left = normalizedLeft;
            item.Top = normalizedTop;
            item.Enabled = EnabledBox.IsChecked == true;
            item.IsPreview = false;

            OverlayChanged?.Invoke(this, new OverlayItemEventArgs(item.Clone()));
            OverlayList.Items.Refresh();

            _loadingEditor = true;
            try
            {
                LeftBox.Text = DisplayPosition(item.Left);
                TopBox.Text = DisplayPosition(item.Top);
                SyncPositionSlider(LeftBox, LeftSlider);
                SyncPositionSlider(TopBox, TopSlider);
            }
            finally
            {
                _loadingEditor = false;
            }

            SetEditorStatus("Saved", "Crntly.Success");
            UpdateActionStates();
            return true;
        }

        private bool TryValidateEditor(out string width, out string height, out string left, out string top, out string message)
        {
            width = null;
            height = null;
            left = null;
            top = null;
            message = null;

            if (string.IsNullOrWhiteSpace(NameBox.Text))
            {
                message = "Enter a name";
                return false;
            }

            Uri uri;
            if (!TryGetSupportedUri(UrlBox.Text, out uri))
            {
                message = "Enter a valid URL";
                return false;
            }

            if (!TryNormalizeCssLength(WidthBox.Text, "100%", false, out width) ||
                !TryNormalizeCssLength(HeightBox.Text, "100%", false, out height))
            {
                message = "Check width / height";
                return false;
            }

            if (!TryNormalizePosition(LeftBox.Text, "0px", out left) ||
                !TryNormalizePosition(TopBox.Text, "0px", out top))
            {
                message = "Check x / y";
                return false;
            }

            return true;
        }

        private void RaiseOrderChanged()
        {
            OverlayOrderChanged?.Invoke(this,
                new OverlayOrderEventArgs(Items.Select(x => x.Clone()).ToList()));
        }

        private void UpdateActionStates()
        {
            if (DuplicateButton == null || MoveUpButton == null || MoveDownButton == null ||
                DeleteButton == null || ResetLayoutButton == null || OpenSourceButton == null)
                return;

            var item = OverlayList == null ? null : OverlayList.SelectedItem as OverlayItem;
            var index = OverlayList == null ? -1 : OverlayList.SelectedIndex;
            var hasSelection = item != null;

            Uri uri;
            var currentUrl = UrlBox == null ? null : UrlBox.Text;
            var canOpenSource = hasSelection && TryGetSupportedUri(currentUrl, out uri);
            var canDuplicate = hasSelection && TryGetSupportedUri(item.Url, out uri);

            DuplicateButton.IsEnabled = canDuplicate;
            MoveUpButton.IsEnabled = hasSelection && index > 0;
            MoveDownButton.IsEnabled = hasSelection && index >= 0 && index < Items.Count - 1;
            DeleteButton.IsEnabled = hasSelection;
            ResetLayoutButton.IsEnabled = hasSelection;
            OpenSourceButton.IsEnabled = canOpenSource;
        }

        private bool OpenExternal(string value, bool reportEditorError)
        {
            Uri uri;
            if (!TryGetSupportedUri(value, out uri))
                return false;

            try
            {
                Process.Start(uri.IsFile ? uri.LocalPath : uri.AbsoluteUri);
                return true;
            }
            catch
            {
                if (reportEditorError)
                    SetEditorStatus("Could not open", "Crntly.Danger");
                return false;
            }
        }

        private static bool TryGetSupportedUri(string value, out Uri uri)
        {
            uri = null;
            if (string.IsNullOrWhiteSpace(value) ||
                !Uri.TryCreate(value.Trim(), UriKind.Absolute, out uri))
                return false;

            return uri.Scheme == Uri.UriSchemeHttp ||
                   uri.Scheme == Uri.UriSchemeHttps ||
                   uri.Scheme == Uri.UriSchemeFile;
        }

        private static bool TryNormalizeCssLength(string value, string fallback, bool allowNegative, out string normalized)
        {
            normalized = fallback;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var input = value.Trim().ToLowerInvariant();
            if (input == "0")
            {
                normalized = "0px";
                return true;
            }

            string unit = null;
            foreach (var candidate in new[] { "px", "%", "vw", "vh" })
            {
                if (input.EndsWith(candidate, StringComparison.Ordinal))
                {
                    unit = candidate;
                    break;
                }
            }

            if (unit == null)
                return false;

            double number;
            if (!TryParseNumber(input.Substring(0, input.Length - unit.Length), out number))
                return false;

            if (!allowNegative && number < 0)
                return false;

            normalized = FormatNumber(number) + unit;
            return true;
        }

        private static bool TryNormalizePosition(string value, string fallback, out string normalized)
        {
            normalized = fallback;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var input = value.Trim().ToLowerInvariant();
            double bareNumber;
            if (TryParseNumber(input, out bareNumber))
            {
                normalized = FormatNumber(bareNumber) + "px";
                return true;
            }

            return TryNormalizeCssLength(input, fallback, true, out normalized);
        }

        private static bool TryParsePixelPosition(string value, out double pixels)
        {
            pixels = 0;
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var input = value.Trim().ToLowerInvariant();
            if (input.EndsWith("px", StringComparison.Ordinal))
                input = input.Substring(0, input.Length - 2);

            return TryParseNumber(input, out pixels);
        }

        private static bool TryParseNumber(string value, out double number)
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) ||
                   double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out number);
        }

        private static string FormatNumber(double value)
        {
            if (Math.Abs(value - Math.Round(value)) < 0.000001)
                return Math.Round(value).ToString(CultureInfo.InvariantCulture);

            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string FormatPositionNumber(double value)
        {
            return FormatNumber(Math.Round(value));
        }

        private static string DisplayPosition(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "0";

            var input = value.Trim();
            if (input.EndsWith("px", StringComparison.OrdinalIgnoreCase))
                return input.Substring(0, input.Length - 2);

            return input;
        }

        private void SyncPositionSlider(TextBox textBox, Slider slider)
        {
            if (textBox == null || slider == null)
                return;

            double pixels;
            if (!TryParsePixelPosition(textBox.Text, out pixels))
            {
                slider.IsEnabled = false;
                return;
            }

            slider.IsEnabled = true;
            _syncingPositionControls = true;
            try
            {
                slider.Value = Math.Max(slider.Minimum, Math.Min(slider.Maximum, pixels));
            }
            finally
            {
                _syncingPositionControls = false;
            }
        }

        private void SetEditorStatus(string text, string brushResource)
        {
            if (EditorStatusText == null || EditorStatusDot == null)
                return;

            EditorStatusText.Text = text;
            var brush = (Brush)FindResource(brushResource);
            EditorStatusText.Foreground = brush;
            EditorStatusDot.Fill = brush;
        }

        private void ClearEditorSafely()
        {
            var previous = _loadingEditor;
            _loadingEditor = true;
            try
            {
                NameBox.Text = string.Empty;
                UrlBox.Text = string.Empty;
                WidthBox.Text = "100%";
                HeightBox.Text = "100%";
                LeftBox.Text = "0";
                TopBox.Text = "0";
                EnabledBox.IsChecked = true;
                LeftSlider.Value = 0;
                TopSlider.Value = 0;
                SetEditorStatus("Autosave", "Crntly.TextMuted");
            }
            finally
            {
                _loadingEditor = previous;
            }
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
            ResetLayoutButton.IsEnabled = enabled;
            OpenSourceButton.IsEnabled = enabled && TryGetSupportedUri(UrlBox.Text, out _);

            if (!enabled)
            {
                LeftSlider.IsEnabled = false;
                TopSlider.IsEnabled = false;
            }
            else
            {
                SyncPositionSlider(LeftBox, LeftSlider);
                SyncPositionSlider(TopBox, TopSlider);
            }
        }
    }
}
