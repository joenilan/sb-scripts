using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

// CRNTLY Overlay(er) v2.0.0
// Streamer.bot editor reference required:
//   Newtonsoft.Json.dll
//
// Runtime dependency:
//   <Streamer.bot>\dlls\CRNTLY.StreamerBot.UI.dll
//
// Overlay(er) owns its layout and application behavior in this script. The shared
// CRNTLY DLL only supplies reusable WPF hosting, themes, controls and utilities.
public static class OverlayErBuild
{
    public const string ProductName = "Overlay(er)";
    public const string Version = "2.0.0";
    public static string DisplayVersion { get { return ProductName + " v" + Version; } }
}

public class CPHInline
{
    private OverlayerRuntime _runtime;

    public void Init()
    {
    }

    public bool Execute()
    {
        if (_runtime == null)
        {
            var log = new Action<string>(message => CPH.LogInfo("[CRNTLY " + OverlayErBuild.DisplayVersion + "] " + message));
            var logError = new Action<string>(message => CPH.LogError("[CRNTLY " + OverlayErBuild.DisplayVersion + "] " + message));

            CrntlyScriptWindowProxy window;
            if (!CrntlyDependencyBootstrap.TryCreateScriptWindow(log, logError, out window))
                return false;

            _runtime = new OverlayerRuntime(log, logError, window);
        }

        _runtime.Show();
        return true;
    }

    public void Dispose()
    {
        if (_runtime == null)
            return;

        _runtime.Dispose();
        _runtime = null;
    }
}

public static class CrntlyDependencyBootstrap
{
    private const string DllName = "CRNTLY.StreamerBot.UI.dll";
    private const string BridgeTypeName = "Crntly.StreamerBot.UI.ScriptHost.CrntlyScriptWindowBridge";

    public static bool TryCreateScriptWindow(
        Action<string> log,
        Action<string> logError,
        out CrntlyScriptWindowProxy window)
    {
        window = null;

        try
        {
            var streamerBotRoot = AppDomain.CurrentDomain.BaseDirectory;
            var dllPath = Path.Combine(streamerBotRoot, "dlls", DllName);

            if (!File.Exists(dllPath))
            {
                var message =
                    "CRNTLY " + OverlayErBuild.DisplayVersion + " needs CRNTLY.StreamerBot.UI.dll.\n\n" +
                    "Expected:\n" + dllPath + "\n\n" +
                    "Build/deploy it with build-ui.ps1, restart Streamer.bot, then run this action again.";

                ShowBootstrapMessage(message, "CRNTLY " + OverlayErBuild.ProductName + " - Component Missing");
                if (logError != null)
                    logError("Missing UI component: " + dllPath);
                return false;
            }

            var assembly = FindLoadedAssembly() ?? Assembly.LoadFrom(dllPath);
            var bridgeType = assembly.GetType(BridgeTypeName, false);
            if (bridgeType == null)
                throw new InvalidOperationException(
                    "The installed CRNTLY UI component does not contain " + BridgeTypeName +
                    ". Rebuild/deploy the latest DLL.");

            var bridge = Activator.CreateInstance(bridgeType);
            window = new CrntlyScriptWindowProxy(bridgeType, bridge);

            if (log != null)
                log("Loaded CRNTLY UI " + window.AssemblyVersion + " from " + dllPath);

            return true;
        }
        catch (Exception ex)
        {
            var message =
                "CRNTLY " + OverlayErBuild.DisplayVersion + " could not load its UI component.\n\n" +
                ex.Message + "\n\n" +
                "Re-run build-ui.ps1, restart Streamer.bot, then run the action again.";

            ShowBootstrapMessage(message, "CRNTLY " + OverlayErBuild.ProductName + " - Load Error");
            if (logError != null)
                logError("Unable to load CRNTLY UI: " + ex);
            return false;
        }
    }

    private static Assembly FindLoadedAssembly()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                if (string.Equals(assembly.GetName().Name, "CRNTLY.StreamerBot.UI", StringComparison.OrdinalIgnoreCase))
                    return assembly;
            }
            catch
            {
            }
        }

        return null;
    }

    private static void ShowBootstrapMessage(string message, string title)
    {
        try
        {
            var type = Type.GetType("System.Windows.Forms.MessageBox, System.Windows.Forms", false);
            if (type == null)
                return;

            var method = type.GetMethod("Show", new[] { typeof(string), typeof(string) });
            if (method != null)
                method.Invoke(null, new object[] { message, title });
        }
        catch
        {
        }
    }
}

public sealed class CrntlyScriptWindowProxy : IDisposable
{
    private readonly Type _bridgeType;
    private readonly object _bridge;
    private readonly MethodInfo _show;
    private readonly MethodInfo _hide;
    private readonly MethodInfo _bindEvent;
    private readonly MethodInfo _bindRoutedEvent;
    private readonly MethodInfo _getProperty;
    private readonly MethodInfo _setProperty;
    private readonly MethodInfo _setResourceProperty;
    private readonly MethodInfo _setItemsSource;
    private readonly MethodInfo _refreshItems;
    private readonly MethodInfo _getSelectedItem;
    private readonly MethodInfo _getSelectedIndex;
    private readonly MethodInfo _setSelectedIndex;
    private readonly MethodInfo _invokeMethod;
    private readonly MethodInfo _dispose;

    public CrntlyScriptWindowProxy(Type bridgeType, object bridge)
    {
        _bridgeType = bridgeType;
        _bridge = bridge;
        _show = RequireMethod("Show", typeof(string));
        _hide = RequireMethod("Hide");
        _bindEvent = RequireMethod("BindEvent", typeof(string), typeof(string), typeof(string));
        _bindRoutedEvent = RequireMethod("BindRoutedEvent", typeof(string), typeof(string), typeof(string));
        _getProperty = RequireMethod("GetProperty", typeof(string), typeof(string));
        _setProperty = RequireMethod("SetProperty", typeof(string), typeof(string), typeof(object));
        _setResourceProperty = RequireMethod("SetResourceProperty", typeof(string), typeof(string), typeof(string));
        _setItemsSource = RequireMethod("SetItemsSource", typeof(string), typeof(object));
        _refreshItems = RequireMethod("RefreshItems", typeof(string));
        _getSelectedItem = RequireMethod("GetSelectedItem", typeof(string));
        _getSelectedIndex = RequireMethod("GetSelectedIndex", typeof(string));
        _setSelectedIndex = RequireMethod("SetSelectedIndex", typeof(string), typeof(int));
        _invokeMethod = RequireMethod("InvokeMethod", typeof(string), typeof(string));
        _dispose = RequireMethod("Dispose");
    }

    public string AssemblyVersion
    {
        get
        {
            var property = _bridgeType.GetProperty("AssemblyVersion");
            return property == null ? "unknown" : Convert.ToString(property.GetValue(_bridge, null));
        }
    }

    public Action<string> EventRaised
    {
        set { SetCallback("EventRaised", value); }
    }

    public Action<string, object> RoutedEventRaised
    {
        set { SetCallback("RoutedEventRaised", value); }
    }

    public void Show(string xaml) { _show.Invoke(_bridge, new object[] { xaml }); }
    public void Hide() { _hide.Invoke(_bridge, null); }

    public void BindEvent(string controlName, string eventName, string eventKey)
    {
        _bindEvent.Invoke(_bridge, new object[] { controlName, eventName, eventKey });
    }

    public void BindRoutedEvent(string ownerTypeName, string routedEventFieldName, string eventKey)
    {
        _bindRoutedEvent.Invoke(_bridge, new object[] { ownerTypeName, routedEventFieldName, eventKey });
    }

    public object GetProperty(string controlName, string propertyName)
    {
        return _getProperty.Invoke(_bridge, new object[] { controlName, propertyName });
    }

    public T Get<T>(string controlName, string propertyName, T fallback)
    {
        var value = GetProperty(controlName, propertyName);
        if (value == null)
            return fallback;

        try
        {
            if (value is T)
                return (T)value;
            return (T)Convert.ChangeType(value, typeof(T), CultureInfo.InvariantCulture);
        }
        catch
        {
            return fallback;
        }
    }

    public void SetProperty(string controlName, string propertyName, object value)
    {
        _setProperty.Invoke(_bridge, new object[] { controlName, propertyName, value });
    }

    public void SetResourceProperty(string controlName, string propertyName, string resourceKey)
    {
        _setResourceProperty.Invoke(_bridge, new object[] { controlName, propertyName, resourceKey });
    }

    public void SetItemsSource(string controlName, object items)
    {
        _setItemsSource.Invoke(_bridge, new object[] { controlName, items });
    }

    public void RefreshItems(string controlName)
    {
        _refreshItems.Invoke(_bridge, new object[] { controlName });
    }

    public object GetSelectedItem(string controlName)
    {
        return _getSelectedItem.Invoke(_bridge, new object[] { controlName });
    }

    public int GetSelectedIndex(string controlName)
    {
        return Convert.ToInt32(_getSelectedIndex.Invoke(_bridge, new object[] { controlName }), CultureInfo.InvariantCulture);
    }

    public void SetSelectedIndex(string controlName, int index)
    {
        _setSelectedIndex.Invoke(_bridge, new object[] { controlName, index });
    }

    public object InvokeMethod(string controlName, string methodName)
    {
        return _invokeMethod.Invoke(_bridge, new object[] { controlName, methodName });
    }

    private MethodInfo RequireMethod(string name, params Type[] parameterTypes)
    {
        var method = _bridgeType.GetMethod(name, parameterTypes);
        if (method == null)
            throw new MissingMethodException(_bridgeType.FullName, name);
        return method;
    }

    private void SetCallback(string propertyName, object callback)
    {
        var property = _bridgeType.GetProperty(propertyName);
        if (property == null || !property.CanWrite)
            throw new MissingMemberException(_bridgeType.FullName, propertyName);
        property.SetValue(_bridge, callback, null);
    }

    public void Dispose()
    {
        try
        {
            EventRaised = null;
            RoutedEventRaised = null;
            _dispose.Invoke(_bridge, null);
        }
        catch
        {
        }
    }
}

public sealed class OverlayerScriptUi : IDisposable
{
    private const string WindowXaml = @"
<Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        xmlns:shell=""clr-namespace:System.Windows.Shell;assembly=PresentationFramework""
        Title=""CRNTLY • Overlay(er)"" Width=""780"" Height=""440"" MinWidth=""700"" MinHeight=""390""
        WindowStartupLocation=""CenterScreen"" WindowStyle=""None"" ResizeMode=""CanResize""
        Background=""{DynamicResource Crntly.Background}"">
  <Window.Resources>
    <ResourceDictionary><ResourceDictionary.MergedDictionaries>
      <ResourceDictionary Source=""/CRNTLY.StreamerBot.UI;component/Theme/CrntlyTheme.xaml"" />
    </ResourceDictionary.MergedDictionaries></ResourceDictionary>
  </Window.Resources>
  <shell:WindowChrome.WindowChrome>
    <shell:WindowChrome CaptionHeight=""0"" ResizeBorderThickness=""4"" CornerRadius=""3"" GlassFrameThickness=""0"" />
  </shell:WindowChrome.WindowChrome>
  <Border Background=""{DynamicResource Crntly.Background}"" BorderBrush=""{DynamicResource Crntly.Border}""
          BorderThickness=""1"" CornerRadius=""{DynamicResource Crntly.WindowRadius}"">
    <Grid>
      <Grid.RowDefinitions>
        <RowDefinition Height=""{DynamicResource Crntly.TitleBarHeight}"" /><RowDefinition Height=""Auto"" />
        <RowDefinition Height=""*"" /><RowDefinition Height=""16"" />
      </Grid.RowDefinitions>
      <Border x:Name=""TitleBar"" Grid.Row=""0"" Background=""{DynamicResource Crntly.Surface}""
              BorderBrush=""{DynamicResource Crntly.Border}"" BorderThickness=""0,0,0,1"" CornerRadius=""3,3,0,0"">
        <Grid Margin=""7,0,3,0"">
          <Grid.ColumnDefinitions><ColumnDefinition Width=""*"" /><ColumnDefinition Width=""Auto"" /><ColumnDefinition Width=""24"" /><ColumnDefinition Width=""24"" /></Grid.ColumnDefinitions>
          <StackPanel Orientation=""Horizontal"" VerticalAlignment=""Center"">
            <TextBlock Text=""CRNTLY"" Foreground=""{DynamicResource Crntly.TextMuted}"" FontWeight=""SemiBold"" FontSize=""9"" />
            <TextBlock Text=""  /  Overlay(er)"" Foreground=""{DynamicResource Crntly.Text}"" FontWeight=""SemiBold"" FontSize=""10"" />
          </StackPanel>
          <StackPanel Grid.Column=""1"" Orientation=""Horizontal"" Margin=""0,0,6,0"" VerticalAlignment=""Center"">
            <Ellipse x:Name=""ServerDot"" Width=""5"" Height=""5"" Fill=""{DynamicResource Crntly.TextMuted}"" Margin=""0,0,5,0"" />
            <TextBlock x:Name=""ServerBadgeText"" Text=""SERVER OFFLINE"" Foreground=""{DynamicResource Crntly.TextMuted}"" FontSize=""8"" FontWeight=""SemiBold"" />
          </StackPanel>
          <Button x:Name=""MinimizeButton"" Grid.Column=""2"" Content=""—"" Style=""{StaticResource Crntly.TitleButton}"" shell:WindowChrome.IsHitTestVisibleInChrome=""True"" />
          <Button x:Name=""CloseButton"" Grid.Column=""3"" Content=""×"" Style=""{StaticResource Crntly.TitleButton}"" shell:WindowChrome.IsHitTestVisibleInChrome=""True"" />
        </Grid>
      </Border>
      <Border Grid.Row=""1"" Style=""{StaticResource Crntly.Card}"" Margin=""7,5,7,4"" Padding=""5"">
        <Grid>
          <Grid.ColumnDefinitions><ColumnDefinition Width=""Auto"" /><ColumnDefinition Width=""6"" /><ColumnDefinition Width=""*"" /><ColumnDefinition Width=""4"" /><ColumnDefinition Width=""22"" /><ColumnDefinition Width=""2"" /><ColumnDefinition Width=""22"" /></Grid.ColumnDefinitions>
          <Button x:Name=""ServerButton"" Grid.Column=""0"" Content=""Start server"" Style=""{StaticResource Crntly.PrimaryButton}"" MinWidth=""84"" />
          <TextBox x:Name=""ServerUrlBox"" Grid.Column=""2"" IsReadOnly=""True"" Text=""http://localhost:42069/"" VerticalContentAlignment=""Center"" />
          <Button x:Name=""CopyUrlButton"" Grid.Column=""4"" Style=""{StaticResource Crntly.IconButton}"" ToolTip=""Copy compositor URL"">
            <Path Style=""{StaticResource Crntly.IconPath}"" Data=""M19,21H8C6.9,21 6,20.1 6,19V8C6,6.9 6.9,6 8,6H19C20.1,6 21,6.9 21,8V19C21,20.1 20.1,21 19,21 M16,3H5C3.9,3 3,3.9 3,5V16H5V5H16V3Z"" />
          </Button>
          <Button x:Name=""OpenPreviewButton"" Grid.Column=""6"" Style=""{StaticResource Crntly.IconButton}"" ToolTip=""Open compositor preview in browser"">
            <Path Style=""{StaticResource Crntly.IconPath}"" Data=""M14,3V5H17.59L7.76,14.83L9.17,16.24L19,6.41V10H21V3H14 M19,19H5V5H12V3H5C3.9,3 3,3.9 3,5V19C3,20.1 3.9,21 5,21H19C20.1,21 21,20.1 21,19V12H19V19Z"" />
          </Button>
        </Grid>
      </Border>
      <Grid Grid.Row=""2"" Margin=""7,0,7,4"">
        <Grid.ColumnDefinitions><ColumnDefinition Width=""225"" /><ColumnDefinition Width=""6"" /><ColumnDefinition Width=""*"" /></Grid.ColumnDefinitions>
        <Border Grid.Column=""0"" Style=""{StaticResource Crntly.Card}"" Padding=""6"">
          <Grid>
            <Grid.RowDefinitions><RowDefinition Height=""25"" /><RowDefinition Height=""*"" /></Grid.RowDefinitions>
            <Grid Grid.Row=""0"">
              <Grid.ColumnDefinitions><ColumnDefinition Width=""*"" /><ColumnDefinition Width=""Auto"" /></Grid.ColumnDefinitions>
              <TextBlock Text=""Overlays"" Foreground=""{DynamicResource Crntly.Text}"" FontWeight=""SemiBold"" FontSize=""11"" VerticalAlignment=""Center"" />
              <StackPanel Grid.Column=""1"" Orientation=""Horizontal"" VerticalAlignment=""Center"">
                <Button x:Name=""AddButton"" Style=""{StaticResource Crntly.IconButton}"" ToolTip=""Add overlay""><Path Style=""{StaticResource Crntly.IconPath}"" Data=""M11,5H13V11H19V13H13V19H11V13H5V11H11V5Z"" /></Button>
                <Button x:Name=""DuplicateButton"" Style=""{StaticResource Crntly.IconButton}"" ToolTip=""Duplicate selected overlay""><Path Style=""{StaticResource Crntly.IconPath}"" Data=""M19,21H8C6.9,21 6,20.1 6,19V8C6,6.9 6.9,6 8,6H19C20.1,6 21,6.9 21,8V19C21,20.1 20.1,21 19,21 M16,3H5C3.9,3 3,3.9 3,5V16H5V5H16V3Z"" /></Button>
                <Border Style=""{StaticResource Crntly.ToolbarSeparator}"" />
                <Button x:Name=""MoveUpButton"" Style=""{StaticResource Crntly.IconButton}"" ToolTip=""Move overlay up""><Path Style=""{StaticResource Crntly.IconPath}"" Data=""M7,14L12,9L17,14H7Z"" /></Button>
                <Button x:Name=""MoveDownButton"" Style=""{StaticResource Crntly.IconButton}"" ToolTip=""Move overlay down""><Path Style=""{StaticResource Crntly.IconPath}"" Data=""M7,10L12,15L17,10H7Z"" /></Button>
                <Button x:Name=""DeleteButton"" Style=""{StaticResource Crntly.DangerIconButton}"" ToolTip=""Delete selected overlay""><Path Style=""{StaticResource Crntly.IconPath}"" Data=""M19,4H15.5L14.5,3H9.5L8.5,4H5V6H19V4 M6,19C6,20.1 6.9,21 8,21H16C17.1,21 18,20.1 18,19V7H6V19Z"" /></Button>
              </StackPanel>
            </Grid>
            <ListBox Grid.Row=""1"" x:Name=""OverlayList"">
              <ListBox.ItemTemplate><DataTemplate>
                <Grid>
                  <Grid.ColumnDefinitions><ColumnDefinition Width=""30"" /><ColumnDefinition Width=""*"" /><ColumnDefinition Width=""Auto"" /></Grid.ColumnDefinitions>
                  <CheckBox Grid.Column=""0"" IsChecked=""{Binding Enabled, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"" VerticalAlignment=""Center"" />
                  <StackPanel Grid.Column=""1"" VerticalAlignment=""Center"">
                    <TextBlock Text=""{Binding Name}"" Foreground=""{DynamicResource Crntly.Text}"" FontWeight=""SemiBold"" FontSize=""10"" TextTrimming=""CharacterEllipsis"" />
                    <TextBlock Text=""{Binding Url}"" Foreground=""{DynamicResource Crntly.TextSubtle}"" FontSize=""8"" Margin=""0,1,5,0"" TextTrimming=""CharacterEllipsis"" />
                  </StackPanel>
                  <TextBlock Grid.Column=""2"" Text=""{Binding DisplaySourceKind}"" Foreground=""{DynamicResource Crntly.TextSubtle}"" FontSize=""7"" FontWeight=""SemiBold"" VerticalAlignment=""Top"" Margin=""3,1,0,0"" />
                </Grid>
              </DataTemplate></ListBox.ItemTemplate>
            </ListBox>
          </Grid>
        </Border>
        <Border Grid.Column=""2"" Style=""{StaticResource Crntly.Card}"" Padding=""7"">
          <ScrollViewer VerticalScrollBarVisibility=""Auto"" HorizontalScrollBarVisibility=""Disabled"">
            <StackPanel Margin=""0,0,2,0"">
              <Grid Margin=""0,0,0,6"">
                <Grid.ColumnDefinitions><ColumnDefinition Width=""*"" /><ColumnDefinition Width=""Auto"" /></Grid.ColumnDefinitions>
                <StackPanel Orientation=""Horizontal"" VerticalAlignment=""Center"">
                  <TextBlock Text=""Overlay settings"" Foreground=""{DynamicResource Crntly.Text}"" FontWeight=""SemiBold"" FontSize=""12"" />
                  <StackPanel Orientation=""Horizontal"" Margin=""7,0,0,0"" VerticalAlignment=""Center"">
                    <Ellipse x:Name=""EditorStatusDot"" Width=""4"" Height=""4"" Fill=""{DynamicResource Crntly.TextMuted}"" Margin=""0,0,4,0"" />
                    <TextBlock x:Name=""EditorStatusText"" Text=""Autosave"" Foreground=""{DynamicResource Crntly.TextMuted}"" FontSize=""8"" />
                  </StackPanel>
                </StackPanel>
                <StackPanel Grid.Column=""1"" Orientation=""Horizontal"" VerticalAlignment=""Center"">
                  <Button x:Name=""ResetLayoutButton"" Style=""{StaticResource Crntly.IconButton}"" ToolTip=""Reset size and position to defaults""><Path Style=""{StaticResource Crntly.IconPath}"" Data=""M12,5V2L8,6L12,10V7C15.31,7 18,9.69 18,13C18,16.31 15.31,19 12,19C8.69,19 6,16.31 6,13H4C4,17.42 7.58,21 12,21C16.42,21 20,17.42 20,13C20,8.58 16.42,5 12,5Z"" /></Button>
                </StackPanel>
              </Grid>
              <Grid Margin=""0,0,0,6"">
                <Grid.ColumnDefinitions><ColumnDefinition Width=""0.34*"" /><ColumnDefinition Width=""6"" /><ColumnDefinition Width=""0.66*"" /></Grid.ColumnDefinitions>
                <StackPanel Grid.Column=""0""><TextBlock Text=""NAME"" Style=""{StaticResource Crntly.Label}"" Margin=""0,0,0,2"" /><TextBox x:Name=""NameBox"" /></StackPanel>
                <StackPanel Grid.Column=""2""><TextBlock Text=""URL / LOCAL FILE"" Style=""{StaticResource Crntly.Label}"" Margin=""0,0,0,2"" />
                  <Grid><Grid.ColumnDefinitions><ColumnDefinition Width=""*"" /><ColumnDefinition Width=""3"" /><ColumnDefinition Width=""22"" /></Grid.ColumnDefinitions>
                    <TextBox x:Name=""UrlBox"" />
                    <Button x:Name=""OpenSourceButton"" Grid.Column=""2"" Style=""{StaticResource Crntly.IconButton}"" ToolTip=""Open overlay source in browser""><Path Style=""{StaticResource Crntly.IconPath}"" Data=""M14,3V5H17.59L7.76,14.83L9.17,16.24L19,6.41V10H21V3H14 M19,19H5V5H12V3H5C3.9,3 3,3.9 3,5V19C3,20.1 3.9,21 5,21H19C20.1,21 21,20.1 21,19V12H19V19Z"" /></Button>
                  </Grid>
                </StackPanel>
              </Grid>
              <Grid Margin=""0,0,0,6"">
                <Grid.ColumnDefinitions><ColumnDefinition Width=""*"" /><ColumnDefinition Width=""6"" /><ColumnDefinition Width=""*"" /></Grid.ColumnDefinitions>
                <StackPanel Grid.Column=""0""><TextBlock Text=""WIDTH"" Style=""{StaticResource Crntly.Label}"" Margin=""0,0,0,2"" />
                  <Grid><Grid.ColumnDefinitions><ColumnDefinition Width=""*"" /><ColumnDefinition Width=""3"" /><ColumnDefinition Width=""22"" /></Grid.ColumnDefinitions><TextBox x:Name=""WidthBox"" /><Button x:Name=""ResetWidthButton"" Grid.Column=""2"" Style=""{StaticResource Crntly.IconButton}"" ToolTip=""Reset width to 100%""><Path Style=""{StaticResource Crntly.IconPath}"" Data=""M12,5V2L8,6L12,10V7C15.31,7 18,9.69 18,13C18,16.31 15.31,19 12,19C8.69,19 6,16.31 6,13H4C4,17.42 7.58,21 12,21C16.42,21 20,17.42 20,13C20,8.58 16.42,5 12,5Z"" /></Button></Grid>
                </StackPanel>
                <StackPanel Grid.Column=""2""><TextBlock Text=""HEIGHT"" Style=""{StaticResource Crntly.Label}"" Margin=""0,0,0,2"" />
                  <Grid><Grid.ColumnDefinitions><ColumnDefinition Width=""*"" /><ColumnDefinition Width=""3"" /><ColumnDefinition Width=""22"" /></Grid.ColumnDefinitions><TextBox x:Name=""HeightBox"" /><Button x:Name=""ResetHeightButton"" Grid.Column=""2"" Style=""{StaticResource Crntly.IconButton}"" ToolTip=""Reset height to 100%""><Path Style=""{StaticResource Crntly.IconPath}"" Data=""M12,5V2L8,6L12,10V7C15.31,7 18,9.69 18,13C18,16.31 15.31,19 12,19C8.69,19 6,16.31 6,13H4C4,17.42 7.58,21 12,21C16.42,21 20,17.42 20,13C20,8.58 16.42,5 12,5Z"" /></Button></Grid>
                </StackPanel>
              </Grid>
              <Border Style=""{StaticResource Crntly.SubtleCard}"" Padding=""6""><StackPanel>
                <StackPanel Orientation=""Horizontal"" Margin=""0,0,0,4""><TextBlock Text=""Position"" Foreground=""{DynamicResource Crntly.Text}"" FontWeight=""SemiBold"" FontSize=""10"" /><TextBlock Text=""  ·  live"" Foreground=""{DynamicResource Crntly.TextSubtle}"" FontSize=""8"" /></StackPanel>
                <Grid Margin=""0,0,0,4""><Grid.ColumnDefinitions><ColumnDefinition Width=""14"" /><ColumnDefinition Width=""*"" /><ColumnDefinition Width=""6"" /><ColumnDefinition Width=""58"" /><ColumnDefinition Width=""3"" /><ColumnDefinition Width=""22"" /></Grid.ColumnDefinitions>
                  <TextBlock Grid.Column=""0"" Text=""X"" Foreground=""{DynamicResource Crntly.TextMuted}"" FontWeight=""SemiBold"" FontSize=""9"" VerticalAlignment=""Center"" />
                  <Slider Grid.Column=""1"" x:Name=""LeftSlider"" Minimum=""-1920"" Maximum=""3840"" SmallChange=""1"" LargeChange=""50"" IsMoveToPointEnabled=""True"" ToolTip=""X position in pixels. Range: -1920 to 3840."" />
                  <TextBox Grid.Column=""3"" x:Name=""LeftBox"" TextAlignment=""Right"" />
                  <Button x:Name=""ResetLeftButton"" Grid.Column=""5"" Style=""{StaticResource Crntly.IconButton}"" ToolTip=""Reset X to 0""><Path Style=""{StaticResource Crntly.IconPath}"" Data=""M12,5V2L8,6L12,10V7C15.31,7 18,9.69 18,13C18,16.31 15.31,19 12,19C8.69,19 6,16.31 6,13H4C4,17.42 7.58,21 12,21C16.42,21 20,17.42 20,13C20,8.58 16.42,5 12,5Z"" /></Button>
                </Grid>
                <Grid><Grid.ColumnDefinitions><ColumnDefinition Width=""14"" /><ColumnDefinition Width=""*"" /><ColumnDefinition Width=""6"" /><ColumnDefinition Width=""58"" /><ColumnDefinition Width=""3"" /><ColumnDefinition Width=""22"" /></Grid.ColumnDefinitions>
                  <TextBlock Grid.Column=""0"" Text=""Y"" Foreground=""{DynamicResource Crntly.TextMuted}"" FontWeight=""SemiBold"" FontSize=""9"" VerticalAlignment=""Center"" />
                  <Slider Grid.Column=""1"" x:Name=""TopSlider"" Minimum=""-1080"" Maximum=""2160"" SmallChange=""1"" LargeChange=""50"" IsMoveToPointEnabled=""True"" ToolTip=""Y position in pixels. Range: -1080 to 2160."" />
                  <TextBox Grid.Column=""3"" x:Name=""TopBox"" TextAlignment=""Right"" />
                  <Button x:Name=""ResetTopButton"" Grid.Column=""5"" Style=""{StaticResource Crntly.IconButton}"" ToolTip=""Reset Y to 0""><Path Style=""{StaticResource Crntly.IconPath}"" Data=""M12,5V2L8,6L12,10V7C15.31,7 18,9.69 18,13C18,16.31 15.31,19 12,19C8.69,19 6,16.31 6,13H4C4,17.42 7.58,21 12,21C16.42,21 20,17.42 20,13C20,8.58 16.42,5 12,5Z"" /></Button>
                </Grid>
              </StackPanel></Border>
            </StackPanel>
          </ScrollViewer>
        </Border>
      </Grid>
      <Grid Grid.Row=""3"" Margin=""8,0""><TextBlock Text=""CRNTLY • livestreaming.tools"" Foreground=""{DynamicResource Crntly.TextSubtle}"" FontSize=""7"" VerticalAlignment=""Center"" /><TextBlock x:Name=""VersionText"" Text=""Overlay(er) v2.0.0"" Foreground=""{DynamicResource Crntly.TextSubtle}"" FontSize=""7"" HorizontalAlignment=""Right"" VerticalAlignment=""Center"" /></Grid>
    </Grid>
  </Border>
</Window>";

    private readonly CrntlyScriptWindowProxy _window;
    private readonly Action<string> _logError;
    private readonly object _saveGate = new object();
    private readonly Timer _saveTimer;
    private List<OverlayRecord> _items = new List<OverlayRecord>();
    private OverlayRecord _editingItem;
    private bool _eventsBound;
    private bool _loadingEditor;
    private bool _syncingPosition;
    private bool _refreshingItems;
    private bool _savePending;
    private bool _disposed;

    public OverlayerScriptUi(CrntlyScriptWindowProxy window, Action<string> logError)
    {
        _window = window ?? throw new ArgumentNullException("window");
        _logError = logError ?? delegate { };
        _saveTimer = new Timer(OnSaveTimer, null, Timeout.Infinite, Timeout.Infinite);
    }

    public Action StartServerRequested { get; set; }
    public Action StopServerRequested { get; set; }
    public Action<OverlayRecord> OverlayChanged { get; set; }
    public Action<OverlayRecord> OverlayDeleted { get; set; }
    public Action<IList<OverlayRecord>> OverlayOrderChanged { get; set; }

    public void Show(IList<OverlayRecord> items, bool serverRunning, string serverUrl)
    {
        ThrowIfDisposed();
        _window.Show(WindowXaml);
        _window.SetProperty("$window", "Title", "CRNTLY • " + OverlayErBuild.DisplayVersion);
        _window.SetProperty("VersionText", "Text", OverlayErBuild.DisplayVersion + "  ·  UI " + _window.AssemblyVersion);
        _loadingEditor = true;
        try
        {
            SetItems(items);
            SetServerState(serverRunning, serverUrl);
        }
        finally
        {
            _loadingEditor = false;
        }

        if (!_eventsBound)
            BindEvents();
        LoadSelectedEditor();
        UpdateActionStates();
    }

    public void SetItems(IList<OverlayRecord> items)
    {
        _items = (items ?? new List<OverlayRecord>()).Select(x => x.Clone()).ToList();
        _window.SetItemsSource("OverlayList", _items);
        _window.SetSelectedIndex("OverlayList", _items.Count > 0 ? 0 : -1);
        _editingItem = _items.Count > 0 ? _items[0] : null;
        if (_editingItem == null)
            ClearEditor();
    }

    public void SetServerState(bool running, string serverUrl)
    {
        _window.SetProperty("ServerButton", "Content", running ? "Stop server" : "Start server");
        _window.SetProperty("ServerUrlBox", "Text", string.IsNullOrWhiteSpace(serverUrl) ? "http://localhost:42069/" : serverUrl);
        _window.SetProperty("ServerBadgeText", "Text", running ? "SERVER ONLINE" : "SERVER OFFLINE");
        _window.SetResourceProperty("ServerBadgeText", "Foreground", running ? "Crntly.Success" : "Crntly.TextMuted");
        _window.SetResourceProperty("ServerDot", "Fill", running ? "Crntly.Success" : "Crntly.TextMuted");
        _window.SetProperty("OpenPreviewButton", "IsEnabled", running);
    }

    private void BindEvents()
    {
        _window.EventRaised = OnUiEvent;
        _window.RoutedEventRaised = OnRoutedUiEvent;
        Bind("TitleBar", "MouseLeftButtonDown", "drag");
        Bind("MinimizeButton", "Click", "minimize");
        Bind("CloseButton", "Click", "close");
        Bind("ServerButton", "Click", "server");
        Bind("CopyUrlButton", "Click", "copy-url");
        Bind("OpenPreviewButton", "Click", "open-preview");
        Bind("AddButton", "Click", "add");
        Bind("DuplicateButton", "Click", "duplicate");
        Bind("MoveUpButton", "Click", "up");
        Bind("MoveDownButton", "Click", "down");
        Bind("DeleteButton", "Click", "delete");
        Bind("OverlayList", "SelectionChanged", "selection");
        Bind("ResetLayoutButton", "Click", "reset-all");
        Bind("NameBox", "TextChanged", "field");
        Bind("UrlBox", "TextChanged", "url-field");
        Bind("WidthBox", "TextChanged", "field");
        Bind("HeightBox", "TextChanged", "field");
        Bind("LeftBox", "TextChanged", "left-field");
        Bind("TopBox", "TextChanged", "top-field");
        Bind("LeftSlider", "ValueChanged", "left-slider");
        Bind("TopSlider", "ValueChanged", "top-slider");
        Bind("OpenSourceButton", "Click", "open-source");
        Bind("ResetWidthButton", "Click", "reset-width");
        Bind("ResetHeightButton", "Click", "reset-height");
        Bind("ResetLeftButton", "Click", "reset-left");
        Bind("ResetTopButton", "Click", "reset-top");
        _window.BindRoutedEvent("System.Windows.Controls.Primitives.ToggleButton, PresentationFramework", "CheckedEvent", "row-enabled-on");
        _window.BindRoutedEvent("System.Windows.Controls.Primitives.ToggleButton, PresentationFramework", "UncheckedEvent", "row-enabled-off");
        _eventsBound = true;
    }

    private void Bind(string control, string eventName, string key)
    {
        _window.BindEvent(control, eventName, key);
    }

    private void OnRoutedUiEvent(string key, object dataContext)
    {
        if (_disposed || _loadingEditor || _refreshingItems)
            return;

        var item = dataContext as OverlayRecord;
        if (item == null)
            return;

        if (key == "row-enabled-on") item.Enabled = true;
        else if (key == "row-enabled-off") item.Enabled = false;
        else return;

        item.IsPreview = false;
        SafeInvoke(OverlayChanged, item.Clone());
        if (ReferenceEquals(_editingItem, item))
            SetEditorStatus("Saved", "Crntly.Success");
    }

    private void OnUiEvent(string key)
    {
        if (_disposed)
            return;

        try
        {
            switch (key)
            {
                case "drag": try { _window.InvokeMethod("$window", "DragMove"); } catch { } break;
                case "minimize": _window.SetProperty("$window", "WindowState", "Minimized"); break;
                case "close": FlushPendingAutosave(); _window.Hide(); break;
                case "server":
                    FlushPendingAutosave();
                    if (string.Equals(_window.Get<string>("ServerButton", "Content", ""), "Stop server", StringComparison.OrdinalIgnoreCase)) SafeInvoke(StopServerRequested);
                    else SafeInvoke(StartServerRequested);
                    break;
                case "copy-url": TrySetClipboard(_window.Get<string>("ServerUrlBox", "Text", "")); break;
                case "open-preview": OpenExternal(_window.Get<string>("ServerUrlBox", "Text", ""), false); break;
                case "open-source": if (!OpenExternal(Text("UrlBox"), true)) SetEditorStatus("Check URL", "Crntly.Danger"); break;
                case "add": AddOverlay(); break;
                case "duplicate": DuplicateOverlay(); break;
                case "up": MoveSelected(-1); break;
                case "down": MoveSelected(1); break;
                case "delete": DeleteSelected(); break;
                case "selection": SelectionChanged(); break;
                case "reset-all": ResetAll(); break;
                case "reset-width": ResetField("WidthBox", "100%", null); break;
                case "reset-height": ResetField("HeightBox", "100%", null); break;
                case "reset-left": ResetField("LeftBox", "0", "LeftSlider"); break;
                case "reset-top": ResetField("TopBox", "0", "TopSlider"); break;
                case "field": FieldChanged(null); break;
                case "url-field": FieldChanged("url"); break;
                case "left-field": FieldChanged("left"); break;
                case "top-field": FieldChanged("top"); break;
                case "left-slider": SliderChanged("LeftSlider", "LeftBox"); break;
                case "top-slider": SliderChanged("TopSlider", "TopBox"); break;
            }
        }
        catch (Exception ex)
        {
            _logError("UI action failed (" + key + "): " + ex.Message);
        }
    }

    private void RefreshOverlayList()
    {
        _refreshingItems = true;
        try { _window.RefreshItems("OverlayList"); }
        finally { _refreshingItems = false; }
    }

    private void AddOverlay()
    {
        FlushPendingAutosave();
        var item = new OverlayRecord();
        _items.Add(item);
        RefreshOverlayList();
        _window.SetSelectedIndex("OverlayList", _items.Count - 1);
        _editingItem = item;
        LoadSelectedEditor();
        _window.InvokeMethod("NameBox", "Focus");
        _window.InvokeMethod("NameBox", "SelectAll");
        SetEditorStatus("Enter a URL to save", "Crntly.TextMuted");
        UpdateActionStates();
    }

    private void DuplicateOverlay()
    {
        var source = SelectedItem();
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
        var index = Math.Max(0, _window.GetSelectedIndex("OverlayList") + 1);
        _items.Insert(index, copy);
        RefreshOverlayList();
        _window.SetSelectedIndex("OverlayList", index);
        _editingItem = copy;
        SafeInvoke(OverlayChanged, copy.Clone());
        RaiseOrderChanged();
        LoadSelectedEditor();
        SetEditorStatus("Duplicated", "Crntly.Success");
    }

    private void MoveSelected(int direction)
    {
        var index = _window.GetSelectedIndex("OverlayList");
        var target = index + direction;
        if (index < 0 || target < 0 || target >= _items.Count)
            return;
        FlushPendingAutosave();
        var item = _items[index];
        _items.RemoveAt(index);
        _items.Insert(target, item);
        RefreshOverlayList();
        _window.SetSelectedIndex("OverlayList", target);
        _editingItem = item;
        RaiseOrderChanged();
        UpdateActionStates();
    }

    private void DeleteSelected()
    {
        var item = SelectedItem();
        if (item == null)
            return;
        if (!Confirm("Remove overlay", "Remove '" + item.Name + "'?"))
            return;
        CancelAutosave();
        var index = _window.GetSelectedIndex("OverlayList");
        _items.Remove(item);
        SafeInvoke(OverlayDeleted, item.Clone());
        RaiseOrderChanged();
        RefreshOverlayList();
        if (_items.Count > 0)
        {
            var next = Math.Min(index, _items.Count - 1);
            _window.SetSelectedIndex("OverlayList", next);
            _editingItem = _items[next];
            LoadSelectedEditor();
        }
        else
        {
            _window.SetSelectedIndex("OverlayList", -1);
            _editingItem = null;
            ClearEditor();
        }
        UpdateActionStates();
    }

    private void SelectionChanged()
    {
        if (_loadingEditor)
            return;
        FlushPendingAutosave();
        _editingItem = SelectedItem();
        LoadSelectedEditor();
        UpdateActionStates();
    }

    private void FieldChanged(string kind)
    {
        if (_loadingEditor || _editingItem == null)
            return;
        if (kind == "left") SyncPositionSlider("LeftBox", "LeftSlider");
        else if (kind == "top") SyncPositionSlider("TopBox", "TopSlider");
        if (kind == "url") UpdateActionStates();
        ScheduleAutosave();
    }

    private void SliderChanged(string sliderName, string textBoxName)
    {
        if (_loadingEditor || _syncingPosition || _editingItem == null)
            return;
        _syncingPosition = true;
        try { _window.SetProperty(textBoxName, "Text", FormatPositionNumber(_window.Get<double>(sliderName, "Value", 0))); }
        finally { _syncingPosition = false; }
        PreviewPosition();
        ScheduleAutosave();
    }

    private void ResetAll()
    {
        if (_editingItem == null)
            return;
        CancelAutosave();
        _loadingEditor = true;
        try
        {
            _window.SetProperty("WidthBox", "Text", "100%");
            _window.SetProperty("HeightBox", "Text", "100%");
            _window.SetProperty("LeftBox", "Text", "0");
            _window.SetProperty("TopBox", "Text", "0");
            SyncPositionSlider("LeftBox", "LeftSlider");
            SyncPositionSlider("TopBox", "TopSlider");
        }
        finally { _loadingEditor = false; }
        PreviewLayoutFromEditor();
        ScheduleAutosave();
    }

    private void ResetField(string textBoxName, string value, string sliderName)
    {
        if (_editingItem == null)
            return;
        CancelAutosave();
        var previous = _loadingEditor;
        _loadingEditor = true;
        try
        {
            _window.SetProperty(textBoxName, "Text", value);
            if (!string.IsNullOrWhiteSpace(sliderName)) SyncPositionSlider(textBoxName, sliderName);
        }
        finally { _loadingEditor = previous; }
        PreviewLayoutFromEditor();
        ScheduleAutosave();
    }

    private void PreviewPosition()
    {
        if (_editingItem == null)
            return;
        string left; string top;
        if (!TryNormalizePosition(Text("LeftBox"), "0px", out left) || !TryNormalizePosition(Text("TopBox"), "0px", out top))
            return;
        var preview = _editingItem.Clone();
        preview.Left = left; preview.Top = top; preview.IsPreview = true;
        SafeInvoke(OverlayChanged, preview);
    }

    private void PreviewLayoutFromEditor()
    {
        if (_editingItem == null)
            return;
        var preview = _editingItem.Clone();
        string normalized;
        if (TryNormalizeCssLength(Text("WidthBox"), preview.Width, false, out normalized)) preview.Width = normalized;
        if (TryNormalizeCssLength(Text("HeightBox"), preview.Height, false, out normalized)) preview.Height = normalized;
        if (TryNormalizePosition(Text("LeftBox"), preview.Left, out normalized)) preview.Left = normalized;
        if (TryNormalizePosition(Text("TopBox"), preview.Top, out normalized)) preview.Top = normalized;
        preview.IsPreview = true;
        SafeInvoke(OverlayChanged, preview);
    }

    private void ScheduleAutosave()
    {
        lock (_saveGate)
        {
            _savePending = true;
            _saveTimer.Change(500, Timeout.Infinite);
        }
        SetEditorStatus("Saving…", "Crntly.Accent");
    }

    private void CancelAutosave()
    {
        lock (_saveGate)
        {
            _savePending = false;
            _saveTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    private void FlushPendingAutosave()
    {
        bool commit;
        lock (_saveGate)
        {
            commit = _savePending;
            _savePending = false;
            _saveTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }
        if (commit) CommitEditorChanges();
    }

    private void OnSaveTimer(object state)
    {
        bool commit;
        lock (_saveGate)
        {
            commit = _savePending;
            _savePending = false;
        }
        if (commit && !_disposed) CommitEditorChanges();
    }

    private bool CommitEditorChanges()
    {
        if (_loadingEditor)
            return false;
        var item = _editingItem;
        if (item == null)
            return false;
        string width; string height; string left; string top; string message;
        if (!TryValidateEditor(out width, out height, out left, out top, out message))
        {
            SetEditorStatus(message, "Crntly.Danger");
            return false;
        }
        item.Name = Text("NameBox").Trim();
        item.Url = Text("UrlBox").Trim();
        item.Width = width; item.Height = height; item.Left = left; item.Top = top;
        item.IsPreview = false;
        SafeInvoke(OverlayChanged, item.Clone());
        _loadingEditor = true;
        try
        {
            _window.SetProperty("LeftBox", "Text", DisplayPosition(item.Left));
            _window.SetProperty("TopBox", "Text", DisplayPosition(item.Top));
            SyncPositionSlider("LeftBox", "LeftSlider");
            SyncPositionSlider("TopBox", "TopSlider");
            RefreshOverlayList();
        }
        finally { _loadingEditor = false; }
        SetEditorStatus("Saved", "Crntly.Success");
        UpdateActionStates();
        return true;
    }

    private bool TryValidateEditor(out string width, out string height, out string left, out string top, out string message)
    {
        width = null; height = null; left = null; top = null; message = null;
        if (string.IsNullOrWhiteSpace(Text("NameBox"))) { message = "Enter a name"; return false; }
        Uri uri;
        if (!TryGetSupportedUri(Text("UrlBox"), out uri)) { message = "Enter a valid URL"; return false; }
        if (!TryNormalizeCssLength(Text("WidthBox"), "100%", false, out width) || !TryNormalizeCssLength(Text("HeightBox"), "100%", false, out height)) { message = "Check width / height"; return false; }
        if (!TryNormalizePosition(Text("LeftBox"), "0px", out left) || !TryNormalizePosition(Text("TopBox"), "0px", out top)) { message = "Check x / y"; return false; }
        return true;
    }

    private void LoadSelectedEditor()
    {
        var item = _editingItem;
        _loadingEditor = true;
        try
        {
            SetEditorEnabled(item != null);
            if (item == null) { ClearEditorCore(); return; }
            _window.SetProperty("NameBox", "Text", item.Name ?? "");
            _window.SetProperty("UrlBox", "Text", item.Url ?? "");
            _window.SetProperty("WidthBox", "Text", item.Width ?? "100%");
            _window.SetProperty("HeightBox", "Text", item.Height ?? "100%");
            _window.SetProperty("LeftBox", "Text", DisplayPosition(item.Left));
            _window.SetProperty("TopBox", "Text", DisplayPosition(item.Top));
            SyncPositionSlider("LeftBox", "LeftSlider");
            SyncPositionSlider("TopBox", "TopSlider");
            SetEditorStatus("Autosave", "Crntly.TextMuted");
        }
        finally { _loadingEditor = false; }
    }

    private void ClearEditor()
    {
        _loadingEditor = true;
        try { ClearEditorCore(); SetEditorEnabled(false); }
        finally { _loadingEditor = false; }
    }

    private void ClearEditorCore()
    {
        _window.SetProperty("NameBox", "Text", ""); _window.SetProperty("UrlBox", "Text", "");
        _window.SetProperty("WidthBox", "Text", "100%"); _window.SetProperty("HeightBox", "Text", "100%");
        _window.SetProperty("LeftBox", "Text", "0"); _window.SetProperty("TopBox", "Text", "0");
        _window.SetProperty("LeftSlider", "Value", 0d); _window.SetProperty("TopSlider", "Value", 0d);
        SetEditorStatus("Autosave", "Crntly.TextMuted");
    }

    private void SetEditorEnabled(bool enabled)
    {
        foreach (var name in new[] { "NameBox", "UrlBox", "WidthBox", "HeightBox", "LeftBox", "TopBox", "ResetLayoutButton", "ResetWidthButton", "ResetHeightButton", "ResetLeftButton", "ResetTopButton" })
            _window.SetProperty(name, "IsEnabled", enabled);
        _window.SetProperty("OpenSourceButton", "IsEnabled", enabled && CanOpen(Text("UrlBox")));
        _window.SetProperty("LeftSlider", "IsEnabled", enabled); _window.SetProperty("TopSlider", "IsEnabled", enabled);
    }

    private void SyncPositionSlider(string textBox, string slider)
    {
        double pixels;
        if (!TryParsePixelPosition(Text(textBox), out pixels)) { _window.SetProperty(slider, "IsEnabled", false); return; }
        _window.SetProperty(slider, "IsEnabled", _editingItem != null);
        var min = _window.Get<double>(slider, "Minimum", 0); var max = _window.Get<double>(slider, "Maximum", 0);
        _syncingPosition = true;
        try { _window.SetProperty(slider, "Value", Math.Max(min, Math.Min(max, pixels))); }
        finally { _syncingPosition = false; }
    }

    private void UpdateActionStates()
    {
        var index = _window.GetSelectedIndex("OverlayList");
        var item = SelectedItem();
        var has = item != null;
        _window.SetProperty("DuplicateButton", "IsEnabled", has && CanOpen(item.Url));
        _window.SetProperty("MoveUpButton", "IsEnabled", has && index > 0);
        _window.SetProperty("MoveDownButton", "IsEnabled", has && index >= 0 && index < _items.Count - 1);
        _window.SetProperty("DeleteButton", "IsEnabled", has);
        _window.SetProperty("ResetLayoutButton", "IsEnabled", has);
        _window.SetProperty("OpenSourceButton", "IsEnabled", has && CanOpen(Text("UrlBox")));
    }

    private OverlayRecord SelectedItem() { return _window.GetSelectedItem("OverlayList") as OverlayRecord; }
    private string Text(string control) { return _window.Get<string>(control, "Text", "") ?? ""; }

    private void SetEditorStatus(string text, string resource)
    {
        _window.SetProperty("EditorStatusText", "Text", text);
        _window.SetResourceProperty("EditorStatusText", "Foreground", resource);
        _window.SetResourceProperty("EditorStatusDot", "Fill", resource);
    }

    private void RaiseOrderChanged()
    {
        var callback = OverlayOrderChanged;
        if (callback != null) callback(_items.Select(x => x.Clone()).ToList());
    }

    private static void SafeInvoke(Action callback) { if (callback != null) callback(); }
    private static void SafeInvoke(Action<OverlayRecord> callback, OverlayRecord item) { if (callback != null) callback(item); }
    private static bool CanOpen(string value) { Uri uri; return TryGetSupportedUri(value, out uri); }

    private bool OpenExternal(string value, bool reportEditorError)
    {
        Uri uri;
        if (!TryGetSupportedUri(value, out uri)) return false;
        try { Process.Start(uri.IsFile ? uri.LocalPath : uri.AbsoluteUri); return true; }
        catch { if (reportEditorError) SetEditorStatus("Could not open", "Crntly.Danger"); return false; }
    }

    private static bool Confirm(string title, string message)
    {
        try
        {
            var messageBox = Type.GetType("System.Windows.Forms.MessageBox, System.Windows.Forms", false);
            var buttonsType = Type.GetType("System.Windows.Forms.MessageBoxButtons, System.Windows.Forms", false);
            var iconType = Type.GetType("System.Windows.Forms.MessageBoxIcon, System.Windows.Forms", false);
            if (messageBox == null || buttonsType == null || iconType == null) return true;
            var method = messageBox.GetMethod("Show", new[] { typeof(string), typeof(string), buttonsType, iconType });
            if (method == null) return true;
            var result = method.Invoke(null, new[] { message, title, Enum.Parse(buttonsType, "YesNo"), Enum.Parse(iconType, "Question") });
            return string.Equals(Convert.ToString(result), "Yes", StringComparison.OrdinalIgnoreCase);
        }
        catch { return true; }
    }

    private static void TrySetClipboard(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        try
        {
            var clipboard = Type.GetType("System.Windows.Forms.Clipboard, System.Windows.Forms", false);
            var method = clipboard == null ? null : clipboard.GetMethod("SetText", new[] { typeof(string) });
            if (method != null) method.Invoke(null, new object[] { value });
        }
        catch { }
    }

    private static bool TryGetSupportedUri(string value, out Uri uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out uri)) return false;
        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeFile;
    }

    private static bool TryNormalizeCssLength(string value, string fallback, bool allowNegative, out string normalized)
    {
        normalized = fallback;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var input = value.Trim().ToLowerInvariant();
        if (input == "0") { normalized = "0px"; return true; }
        string unit = null;
        foreach (var candidate in new[] { "px", "%", "vw", "vh" }) if (input.EndsWith(candidate, StringComparison.Ordinal)) { unit = candidate; break; }
        if (unit == null) return false;
        double number;
        if (!TryParseNumber(input.Substring(0, input.Length - unit.Length), out number) || (!allowNegative && number < 0)) return false;
        normalized = FormatNumber(number) + unit;
        return true;
    }

    private static bool TryNormalizePosition(string value, string fallback, out string normalized)
    {
        normalized = fallback;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var input = value.Trim().ToLowerInvariant(); double bare;
        if (TryParseNumber(input, out bare)) { normalized = FormatNumber(bare) + "px"; return true; }
        return TryNormalizeCssLength(input, fallback, true, out normalized);
    }

    private static bool TryParsePixelPosition(string value, out double pixels)
    {
        pixels = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var input = value.Trim().ToLowerInvariant();
        if (input.EndsWith("px", StringComparison.Ordinal)) input = input.Substring(0, input.Length - 2);
        return TryParseNumber(input, out pixels);
    }

    private static bool TryParseNumber(string value, out double number)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) || double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out number);
    }

    private static string FormatNumber(double value)
    {
        if (Math.Abs(value - Math.Round(value)) < 0.000001) return Math.Round(value).ToString(CultureInfo.InvariantCulture);
        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string FormatPositionNumber(double value) { return FormatNumber(Math.Round(value)); }
    private static string DisplayPosition(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "0";
        var input = value.Trim();
        return input.EndsWith("px", StringComparison.OrdinalIgnoreCase) ? input.Substring(0, input.Length - 2) : input;
    }

    private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException("OverlayerScriptUi"); }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CancelAutosave();
        _saveTimer.Dispose();
        _window.Dispose();
    }
}

public sealed class OverlayerRuntime : IDisposable
{
    private readonly object _gate = new object();
    private readonly Action<string> _log;
    private readonly Action<string> _logError;
    private readonly OverlayerScriptUi _ui;
    private readonly OverlayerConfigStore _configStore;
    private readonly CompositeOverlayServer _server;
    private List<OverlayRecord> _items;
    private bool _disposed;

    public OverlayerRuntime(Action<string> log, Action<string> logError, CrntlyScriptWindowProxy window)
    {
        _log = log ?? delegate { };
        _logError = logError ?? delegate { };
        _configStore = new OverlayerConfigStore();
        _items = _configStore.Load(_logError);
        _server = new CompositeOverlayServer(_logError);
        _server.UpdateItems(Snapshot());
        _ui = new OverlayerScriptUi(window, _logError);
        _ui.StartServerRequested = OnStartServerRequested;
        _ui.StopServerRequested = OnStopServerRequested;
        _ui.OverlayChanged = OnOverlayChanged;
        _ui.OverlayDeleted = OnOverlayDeleted;
        _ui.OverlayOrderChanged = OnOverlayOrderChanged;
    }

    public void Show() { ThrowIfDisposed(); _ui.Show(Snapshot(), _server.IsRunning, _server.Url); }

    private void OnStartServerRequested()
    {
        try
        {
            _server.UpdateItems(Snapshot()); _server.Start(); _ui.SetServerState(true, _server.Url); _log("Server started at " + _server.Url);
        }
        catch (Exception ex) { _ui.SetServerState(false, _server.Url); _logError("Unable to start server: " + ex.Message); }
    }

    private void OnStopServerRequested() { _server.Stop(); _ui.SetServerState(false, _server.Url); _log("Server stopped and compositor output cleared."); }

    private void OnOverlayChanged(OverlayRecord changed)
    {
        if (changed == null) return;
        lock (_gate)
        {
            if (changed.IsPreview)
            {
                var preview = _items.Select(x => x.Clone()).ToList();
                var previewItem = preview.FirstOrDefault(x => x.Id == changed.Id);
                if (previewItem != null) { CopyItem(changed, previewItem); previewItem.IsPreview = false; _server.UpdateItems(preview); }
                return;
            }
            changed.IsPreview = false;
            var existing = _items.FirstOrDefault(x => x.Id == changed.Id);
            if (existing == null) _items.Add(changed.Clone()); else CopyItem(changed, existing);
            PersistAndRefreshLocked();
        }
    }

    private void OnOverlayDeleted(OverlayRecord deleted)
    {
        if (deleted == null) return;
        lock (_gate) { _items.RemoveAll(x => x.Id == deleted.Id); PersistAndRefreshLocked(); }
    }

    private void OnOverlayOrderChanged(IList<OverlayRecord> requested)
    {
        lock (_gate)
        {
            var byId = _items.ToDictionary(x => x.Id, x => x);
            var ordered = new List<OverlayRecord>();
            foreach (var item in requested ?? new List<OverlayRecord>())
            {
                OverlayRecord current;
                if (item != null && byId.TryGetValue(item.Id, out current)) { ordered.Add(current); byId.Remove(item.Id); }
            }
            ordered.AddRange(byId.Values); _items = ordered; PersistAndRefreshLocked();
        }
    }

    private void PersistAndRefreshLocked()
    {
        try { _configStore.Save(_items); } catch (Exception ex) { _logError("Unable to save overlay configuration: " + ex.Message); }
        _server.UpdateItems(_items.Select(x => x.Clone()).ToList());
    }

    private List<OverlayRecord> Snapshot() { lock (_gate) return _items.Select(x => x.Clone()).ToList(); }

    private static void CopyItem(OverlayRecord source, OverlayRecord target)
    {
        target.Name = source.Name; target.Url = source.Url; target.Width = source.Width; target.Height = source.Height;
        target.Top = source.Top; target.Left = source.Left; target.Enabled = source.Enabled; target.SourceKind = source.SourceKind; target.IsPreview = source.IsPreview;
    }

    private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException("OverlayerRuntime"); }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _ui.StartServerRequested = null; _ui.StopServerRequested = null; _ui.OverlayChanged = null; _ui.OverlayDeleted = null; _ui.OverlayOrderChanged = null;
        _server.Dispose(); _ui.Dispose();
    }
}

public sealed class OverlayRecord
{
    public OverlayRecord()
    {
        Id = Guid.NewGuid().ToString("N"); Name = "New overlay"; Url = string.Empty; Width = "100%"; Height = "100%";
        Top = "0px"; Left = "0px"; Enabled = true; SourceKind = "Auto";
    }

    public string Id { get; set; }
    public string Name { get; set; }
    public string Url { get; set; }
    public string Width { get; set; }
    public string Height { get; set; }
    public string Top { get; set; }
    public string Left { get; set; }
    public bool Enabled { get; set; }
    public string SourceKind { get; set; }
    public bool IsPreview { get; set; }
    public string DisplaySourceKind { get { Uri uri; return Uri.TryCreate(Url, UriKind.Absolute, out uri) && uri.IsFile ? "LOCAL" : "WEB"; } }

    public OverlayRecord Clone()
    {
        return new OverlayRecord { Id = Id, Name = Name, Url = Url, Width = Width, Height = Height, Top = Top, Left = Left, Enabled = Enabled, SourceKind = SourceKind, IsPreview = IsPreview };
    }
}

public sealed class OverlayerConfigStore
{
    private readonly string _path;
    public OverlayerConfigStore() { var folder = Path.Combine(Environment.CurrentDirectory, "overlayer"); _path = Path.Combine(folder, "listview.json"); }

    public List<OverlayRecord> Load(Action<string> logError)
    {
        var result = new List<OrderedOverlay>();
        try
        {
            var directory = Path.GetDirectoryName(_path); if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
            if (!File.Exists(_path)) { File.WriteAllText(_path, "{}", Encoding.UTF8); return new List<OverlayRecord>(); }
            var json = File.ReadAllText(_path, Encoding.UTF8); if (string.IsNullOrWhiteSpace(json)) return new List<OverlayRecord>();
            var data = JsonConvert.DeserializeObject<LegacyListViewData>(json) ?? new LegacyListViewData();
            Append(result, data.Enabled, true, 0); Append(result, data.Disabled, false, result.Count);
            return result.OrderBy(x => x.Order).ThenBy(x => x.FallbackOrder).Select(x => x.Item).ToList();
        }
        catch (Exception ex) { if (logError != null) logError("Unable to load " + _path + ": " + ex.Message); return new List<OverlayRecord>(); }
    }

    public void Save(IList<OverlayRecord> items)
    {
        var directory = Path.GetDirectoryName(_path); if (!Directory.Exists(directory)) Directory.CreateDirectory(directory);
        var data = new LegacyListViewData { Enabled = new List<Dictionary<string, string>>(), Disabled = new List<Dictionary<string, string>>() };
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var row = new Dictionary<string, string> { { "Id", item.Id }, { "Name", item.Name ?? string.Empty }, { "URL", item.Url ?? string.Empty }, { "Height", item.Height ?? "100%" }, { "Width", item.Width ?? "100%" }, { "Top", item.Top ?? "0px" }, { "Left", item.Left ?? "0px" }, { "Order", i.ToString() } };
            if (item.Enabled) data.Enabled.Add(row); else data.Disabled.Add(row);
        }
        File.WriteAllText(_path, JsonConvert.SerializeObject(data, Formatting.Indented), Encoding.UTF8);
    }

    private static void Append(List<OrderedOverlay> output, List<Dictionary<string, string>> rows, bool enabled, int fallbackOffset)
    {
        if (rows == null) return;
        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i] ?? new Dictionary<string, string>(); int order; if (!int.TryParse(Get(row, "Order", null), out order)) order = int.MaxValue;
            output.Add(new OrderedOverlay { Order = order, FallbackOrder = fallbackOffset + i, Item = new OverlayRecord { Id = Get(row, "Id", Guid.NewGuid().ToString("N")), Name = Get(row, "Name", "Overlay"), Url = Get(row, "URL", string.Empty), Height = Get(row, "Height", "100%"), Width = Get(row, "Width", "100%"), Top = Get(row, "Top", "0px"), Left = Get(row, "Left", "0px"), Enabled = enabled, SourceKind = "Auto", IsPreview = false } });
        }
    }

    private static string Get(Dictionary<string, string> row, string key, string fallback) { string value; return row.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value) ? value : fallback; }
    private sealed class OrderedOverlay { public int Order { get; set; } public int FallbackOrder { get; set; } public OverlayRecord Item { get; set; } }
    public sealed class LegacyListViewData { public List<Dictionary<string, string>> Enabled { get; set; } public List<Dictionary<string, string>> Disabled { get; set; } }
}

public sealed class CompositeOverlayServer : IDisposable
{
    private const string RootUrl = "http://localhost:42069/";
    private const string LocalUrl = "http://localhost:42070/";
    private const string LocalPrefix = "/local/";
    private readonly object _gate = new object();
    private readonly Action<string> _logError;
    private readonly List<HttpListenerResponse> _eventClients = new List<HttpListenerResponse>();
    private HttpListener _listener;
    private HttpListener _localListener;
    private string _stateJson = "[]";
    private Dictionary<string, string> _localRoots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static readonly string ShellHtml = @"<!doctype html>
<html><head><meta charset=""utf-8""><meta name=""viewport"" content=""width=device-width,initial-scale=1""><title>CRNTLY Overlay(er)</title><style>html,body,#crntly-root{margin:0;width:100%;height:100%;overflow:hidden;background:transparent;}#crntly-root{position:relative;}.crntly-overlay{position:absolute;border:0;margin:0;padding:0;overflow:hidden;background:transparent;}</style></head><body><div id=""crntly-root""></div><script>
(() => {const root=document.getElementById('crntly-root');let lastState='';let disconnectTimer=null;let events=null;function applyState(text){if(text===lastState)return;lastState=text;let items;try{items=JSON.parse(text);}catch(_){return;}const keep=new Set();for(const item of items){const domId='ov-'+item.id;keep.add(domId);let frame=document.getElementById(domId);if(!frame){frame=document.createElement('iframe');frame.id=domId;frame.className='crntly-overlay';frame.scrolling='no';frame.allow='autoplay';root.appendChild(frame);}if(frame.dataset.src!==item.src){frame.dataset.src=item.src;frame.src=item.src;}frame.style.width=item.width;frame.style.height=item.height;frame.style.top=item.top;frame.style.left=item.left;root.appendChild(frame);}for(const frame of Array.from(root.children)){if(!keep.has(frame.id))frame.remove();}}function clearOutput(){applyState('[]');}async function sync(){try{const response=await fetch('/state',{cache:'no-store'});if(response.ok)applyState(await response.text());}catch(_){}}sync();if(window.EventSource){try{events=new EventSource('/events');events.onopen=()=>{if(disconnectTimer){clearTimeout(disconnectTimer);disconnectTimer=null;}};events.onmessage=event=>applyState(event.data);events.addEventListener('shutdown',()=>{if(disconnectTimer){clearTimeout(disconnectTimer);disconnectTimer=null;}clearOutput();});events.onerror=()=>{if(disconnectTimer)clearTimeout(disconnectTimer);disconnectTimer=setTimeout(()=>{if(!events||events.readyState!==EventSource.OPEN)clearOutput();},350);};}catch(_){}}setInterval(sync,5000);})();
</script></body></html>";

    public CompositeOverlayServer(Action<string> logError) { _logError = logError ?? delegate { }; }
    public string Url { get { return RootUrl; } }
    public bool IsRunning { get; private set; }

    public void UpdateItems(IList<OverlayRecord> items)
    {
        var state = new List<Dictionary<string, string>>();
        var localRoots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items ?? new List<OverlayRecord>())
        {
            if (!item.Enabled || string.IsNullOrWhiteSpace(item.Url)) continue;
            var source = item.Url.Trim(); Uri uri; if (!Uri.TryCreate(source, UriKind.Absolute, out uri)) continue;
            if (uri.IsFile)
            {
                var localPath = Path.GetFullPath(uri.LocalPath); var directory = Path.GetDirectoryName(localPath); var fileName = Path.GetFileName(localPath);
                if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName)) continue;
                localRoots[item.Id] = directory; source = LocalUrl + "local/" + Uri.EscapeDataString(item.Id) + "/" + Uri.EscapeDataString(fileName);
            }
            state.Add(new Dictionary<string, string> { { "id", item.Id }, { "src", source }, { "width", SafeCss(item.Width, "100%") }, { "height", SafeCss(item.Height, "100%") }, { "top", SafeCss(item.Top, "0px") }, { "left", SafeCss(item.Left, "0px") } });
        }
        var json = JsonConvert.SerializeObject(state, Formatting.None); lock (_gate) { _stateJson = json; _localRoots = localRoots; } BroadcastState(json);
    }

    public void Start()
    {
        if (IsRunning) return;
        HttpListener main = null; HttpListener local = null;
        try
        {
            main = new HttpListener(); main.Prefixes.Add(RootUrl); main.Start(); local = new HttpListener(); local.Prefixes.Add(LocalUrl); local.Start();
            _listener = main; _localListener = local; IsRunning = true; _listener.BeginGetContext(OnMainContext, _listener); _localListener.BeginGetContext(OnLocalContext, _localListener);
        }
        catch { try { if (main != null) main.Close(); } catch { } try { if (local != null) local.Close(); } catch { } _listener = null; _localListener = null; IsRunning = false; throw; }
    }

    public void Stop()
    {
        var main = _listener; var local = _localListener; if (!IsRunning && main == null && local == null) return;
        List<HttpListenerResponse> eventClients;
        lock (_gate) { _stateJson = "[]"; _localRoots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); eventClients = _eventClients.ToList(); }
        foreach (var response in eventClients) { try { WriteEvent(response, "[]", "shutdown"); } catch { RemoveEventClient(response); } }
        if (eventClients.Count > 0) Thread.Sleep(100);
        IsRunning = false; _listener = null; _localListener = null;
        lock (_gate) { eventClients = _eventClients.ToList(); _eventClients.Clear(); }
        foreach (var response in eventClients) SafeClose(response);
        try { if (main != null) main.Close(); } catch { } try { if (local != null) local.Close(); } catch { }
    }

    private void OnMainContext(IAsyncResult ar)
    {
        var listener = ar.AsyncState as HttpListener; if (listener == null) return; HttpListenerContext context;
        try { context = listener.EndGetContext(ar); } catch { return; } Rearm(listener, OnMainContext);
        try { HandleMain(context); } catch (Exception ex) { _logError("HTTP request failed: " + ex.Message); SafeClose(context.Response); }
    }

    private void OnLocalContext(IAsyncResult ar)
    {
        var listener = ar.AsyncState as HttpListener; if (listener == null) return; HttpListenerContext context;
        try { context = listener.EndGetContext(ar); } catch { return; } Rearm(listener, OnLocalContext);
        try { HandleLocal(context); } catch (Exception ex) { _logError("Local asset request failed: " + ex.Message); SafeClose(context.Response); }
    }

    private static void Rearm(HttpListener listener, AsyncCallback callback) { try { if (listener.IsListening) listener.BeginGetContext(callback, listener); } catch { } }

    private void HandleMain(HttpListenerContext context)
    {
        var path = context.Request.Url.AbsolutePath;
        if (path == "/" || string.IsNullOrEmpty(path)) { WriteText(context, ShellHtml, "text/html; charset=utf-8", HttpStatusCode.OK); return; }
        if (string.Equals(path, "/state", StringComparison.OrdinalIgnoreCase)) { string json; lock (_gate) json = _stateJson; WriteText(context, json, "application/json; charset=utf-8", HttpStatusCode.OK); return; }
        if (string.Equals(path, "/events", StringComparison.OrdinalIgnoreCase)) { HandleEventStream(context); return; }
        if (string.Equals(path, "/health", StringComparison.OrdinalIgnoreCase)) { WriteText(context, "ok", "text/plain; charset=utf-8", HttpStatusCode.OK); return; }
        WriteText(context, "Not found", "text/plain; charset=utf-8", HttpStatusCode.NotFound);
    }

    private void HandleEventStream(HttpListenerContext context)
    {
        var response = context.Response; response.StatusCode = (int)HttpStatusCode.OK; response.ContentType = "text/event-stream; charset=utf-8"; response.SendChunked = true; response.KeepAlive = true; response.Headers["Cache-Control"] = "no-cache";
        string json; lock (_gate) { json = _stateJson; _eventClients.Add(response); }
        try { WriteRetry(response, 750); WriteEvent(response, json, null); } catch { RemoveEventClient(response); }
    }

    private void BroadcastState(string json)
    {
        if (!IsRunning) return; List<HttpListenerResponse> clients; lock (_gate) clients = _eventClients.ToList();
        foreach (var response in clients) { try { WriteEvent(response, json, null); } catch { RemoveEventClient(response); } }
    }

    private static void WriteRetry(HttpListenerResponse response, int milliseconds)
    {
        var buffer = Encoding.UTF8.GetBytes("retry: " + Math.Max(250, milliseconds) + "\n\n"); response.OutputStream.Write(buffer, 0, buffer.Length); response.OutputStream.Flush();
    }

    private static void WriteEvent(HttpListenerResponse response, string json, string eventName)
    {
        var builder = new StringBuilder(); if (!string.IsNullOrWhiteSpace(eventName)) builder.Append("event: ").Append(eventName).Append('\n'); builder.Append("data: ").Append(json ?? "[]").Append("\n\n");
        var buffer = Encoding.UTF8.GetBytes(builder.ToString()); response.OutputStream.Write(buffer, 0, buffer.Length); response.OutputStream.Flush();
    }

    private void RemoveEventClient(HttpListenerResponse response) { lock (_gate) _eventClients.Remove(response); SafeClose(response); }

    private void HandleLocal(HttpListenerContext context)
    {
        var path = context.Request.Url.AbsolutePath;
        if (!path.StartsWith(LocalPrefix, StringComparison.OrdinalIgnoreCase)) { WriteText(context, "Not found", "text/plain; charset=utf-8", HttpStatusCode.NotFound); return; }
        var remainder = path.Substring(LocalPrefix.Length); var separator = remainder.IndexOf('/');
        if (separator <= 0 || separator == remainder.Length - 1) { WriteText(context, "Not found", "text/plain; charset=utf-8", HttpStatusCode.NotFound); return; }
        var id = Uri.UnescapeDataString(remainder.Substring(0, separator)); var relative = Uri.UnescapeDataString(remainder.Substring(separator + 1)).Replace('/', Path.DirectorySeparatorChar); string root;
        lock (_gate) { if (!_localRoots.TryGetValue(id, out root)) { WriteText(context, "Not found", "text/plain; charset=utf-8", HttpStatusCode.NotFound); return; } }
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(normalizedRoot, relative));
        if (!candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(candidate)) { WriteText(context, "Not found", "text/plain; charset=utf-8", HttpStatusCode.NotFound); return; }
        var response = context.Response;
        try
        {
            using (var stream = new FileStream(candidate, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                response.StatusCode = (int)HttpStatusCode.OK; response.ContentType = GetMimeType(candidate); response.ContentLength64 = stream.Length; response.Headers["Cache-Control"] = "no-cache"; response.Headers["Access-Control-Allow-Origin"] = "*";
                if (!string.Equals(context.Request.HttpMethod, "HEAD", StringComparison.OrdinalIgnoreCase)) { var buffer = new byte[64 * 1024]; int read; while ((read = stream.Read(buffer, 0, buffer.Length)) > 0) response.OutputStream.Write(buffer, 0, read); }
            }
        }
        finally { SafeClose(response); }
    }

    private static void WriteText(HttpListenerContext context, string text, string contentType, HttpStatusCode status)
    {
        var buffer = Encoding.UTF8.GetBytes(text ?? string.Empty); var response = context.Response;
        try { response.StatusCode = (int)status; response.ContentType = contentType; response.ContentLength64 = buffer.Length; response.Headers["Cache-Control"] = "no-store"; if (!string.Equals(context.Request.HttpMethod, "HEAD", StringComparison.OrdinalIgnoreCase)) response.OutputStream.Write(buffer, 0, buffer.Length); }
        finally { SafeClose(response); }
    }

    private static void SafeClose(HttpListenerResponse response)
    {
        if (response == null) return; try { response.OutputStream.Close(); } catch { } try { response.Close(); } catch { }
    }

    private static string SafeCss(string value, string fallback) { return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim(); }

    private static string GetMimeType(string path)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".html": case ".htm": return "text/html; charset=utf-8"; case ".css": return "text/css; charset=utf-8"; case ".js": return "application/javascript; charset=utf-8"; case ".json": return "application/json; charset=utf-8";
            case ".svg": return "image/svg+xml"; case ".png": return "image/png"; case ".jpg": case ".jpeg": return "image/jpeg"; case ".gif": return "image/gif"; case ".webp": return "image/webp"; case ".ico": return "image/x-icon";
            case ".woff": return "font/woff"; case ".woff2": return "font/woff2"; case ".ttf": return "font/ttf"; case ".otf": return "font/otf"; case ".mp3": return "audio/mpeg"; case ".wav": return "audio/wav"; case ".ogg": return "audio/ogg"; case ".mp4": return "video/mp4"; case ".webm": return "video/webm"; default: return "application/octet-stream";
        }
    }

    public void Dispose() { Stop(); }
}
