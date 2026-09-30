using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Speech.Synthesis;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

// Mr. Operator v3.1.0
// Streamer.bot editor reference required:
//   System.Speech.dll
//
// Runtime dependency:
//   <Streamer.bot>\dlls\CRNTLY.StreamerBot.UI.dll
//
// Mr. Operator owns its switchboard layout and behavior. CRNTLY supplies the
// shared visual system and controls, along with WPF hosting and script bridges.
public static class MrOperatorBuild
{
    public const string ProductName = "Mr. Operator";
    public const string Version = "3.1.0";
    public const string RewardName = "Call In";
    public const string PhoneEmoteName = "Phone";
    public const string HangUpEmoteName = "Hangup";
}

public enum MrOperatorChatCommand
{
    None,
    RequestCall,
    HangUp
}

public static class MrOperatorChatCommandParser
{
    public static MrOperatorChatCommand Parse(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return MrOperatorChatCommand.None;

        var emote = message.Trim();
        if (string.Equals(emote, MrOperatorBuild.PhoneEmoteName, StringComparison.OrdinalIgnoreCase))
            return MrOperatorChatCommand.RequestCall;
        if (string.Equals(emote, MrOperatorBuild.HangUpEmoteName, StringComparison.OrdinalIgnoreCase))
            return MrOperatorChatCommand.HangUp;

        return MrOperatorChatCommand.None;
    }
}

public class CPHInline
{
    private MrOperatorRuntime _runtime;

    public void Init()
    {
    }

    public bool Execute()
    {
        try
        {
            // One Streamer.bot Action can own both Twitch triggers. Keep the
            // event routing here so it needs only this Execute C# Code block.
            var eventType = Convert.ToString(CPH.GetEventType(), CultureInfo.InvariantCulture);
            if (string.Equals(eventType, "TwitchRewardRedemption", StringComparison.Ordinal))
                return RewardRedeemed();
            if (string.Equals(eventType, "TwitchChatMessage", StringComparison.Ordinal))
                return HandleChatMessage();

            if (_runtime != null)
            {
                if (!_runtime.IsDisposed)
                {
                    _runtime.Show();
                    return true;
                }
                _runtime = null;
            }

            Action<string> log = message => CPH.LogInfo("[CRNTLY " + MrOperatorBuild.ProductName + "] " + message);
            Action<string> logError = message => CPH.LogError("[CRNTLY " + MrOperatorBuild.ProductName + "] " + message);

            string rewardId = null;
            try
            {
                rewardId = FindCallInRewardId();
            }
            catch (Exception ex)
            {
                logError("Could not check for the optional Call In reward; Phone emote calls remain available: " + ex);
            }

            if (string.IsNullOrEmpty(rewardId))
                log("No Call In reward was found. The Phone and Hangup chat emotes will still work.");

            MrOperatorScriptWindowProxy window;
            if (!MrOperatorDependencyBootstrap.TryCreateScriptWindow(log, logError, out window))
                return false;

            _runtime = new MrOperatorRuntime(
                rewardId,
                window,
                message => CPH.SendMessage(message, false),
                id => CPH.EnableReward(id),
                id => CPH.DisableReward(id),
                log,
                logError);

            _runtime.Start();
            CPH.SendMessage("Mr. Operator is ready. Open lines, then send the Phone emote to request a call.", false);
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError("[CRNTLY " + MrOperatorBuild.ProductName + "] Startup failed: " + ex);
            CPH.SendMessage("Mr. Operator could not start: " + ex.Message, false);
            if (_runtime != null)
            {
                _runtime.Dispose();
                _runtime = null;
            }
            return false;
        }
    }

    public bool RewardRedeemed()
    {
        if (_runtime == null || _runtime.IsDisposed)
            return false;

        return _runtime.HandleRewardRedemption(
            GetArgument("userName"),
            GetArgument("rewardName"));
    }

    public bool HandleChatMessage()
    {
        if (_runtime == null || _runtime.IsDisposed)
            return false;

        var message = GetArgument("rawInput", "message");
        return _runtime.HandleChatMessage(GetArgument("userName"), message);
    }

    public void Dispose()
    {
        if (_runtime == null)
            return;

        _runtime.Dispose();
        _runtime = null;
    }

    private string FindCallInRewardId()
    {
        var rewards = CPH.TwitchGetRewards();
        if (rewards == null)
            return null;

        foreach (var reward in rewards)
        {
            if (reward != null &&
                string.Equals(Convert.ToString(reward.Title), MrOperatorBuild.RewardName, StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToString(reward.Id);
            }
        }

        return null;
    }

    private string GetArgument(params string[] keys)
    {
        foreach (var key in keys)
        {
            try
            {
                object value;
                if (CPH.TryGetArg(key, out value) && value != null)
                    return Convert.ToString(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                // The alternate name may not exist for this trigger type.
            }
        }

        return string.Empty;
    }
}

public static class MrOperatorDependencyBootstrap
{
    private const string DllName = "CRNTLY.StreamerBot.UI.dll";
    private const string BridgeTypeName = "Crntly.StreamerBot.UI.ScriptHost.CrntlyScriptWindowBridge";

    public static bool TryCreateScriptWindow(
        Action<string> log,
        Action<string> logError,
        out MrOperatorScriptWindowProxy window)
    {
        window = null;

        try
        {
            var dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dlls", DllName);
            if (!File.Exists(dllPath))
            {
                var message =
                    MrOperatorBuild.ProductName + " v" + MrOperatorBuild.Version + " needs the shared CRNTLY UI component.\n\n" +
                    "Expected:\n" + dllPath + "\n\n" +
                    "Deploy CRNTLY.StreamerBot.UI.dll to Streamer.bot\\dlls, restart Streamer.bot, and run this action again.";

                ShowBootstrapMessage(message, "CRNTLY " + MrOperatorBuild.ProductName + " - Component Missing");
                if (logError != null)
                    logError("Missing UI component: " + dllPath);
                return false;
            }

            var assembly = FindLoadedAssembly() ?? Assembly.LoadFrom(dllPath);
            var bridgeType = assembly.GetType(BridgeTypeName, false);
            if (bridgeType == null)
                throw new InvalidOperationException(
                    "The installed CRNTLY UI component does not contain " + BridgeTypeName +
                    ". Deploy the current CRNTLY.StreamerBot.UI.dll and restart Streamer.bot.");

            var bridge = Activator.CreateInstance(bridgeType);
            window = new MrOperatorScriptWindowProxy(bridgeType, bridge);

            if (log != null)
                log("Loaded CRNTLY UI " + window.AssemblyVersion + " from " + dllPath);

            return true;
        }
        catch (Exception ex)
        {
            var message =
                MrOperatorBuild.ProductName + " v" + MrOperatorBuild.Version + " could not load the CRNTLY UI component.\n\n" +
                ex.Message + "\n\n" +
                "Deploy the current CRNTLY.StreamerBot.UI.dll to Streamer.bot\\dlls, restart Streamer.bot, and run this action again.";

            ShowBootstrapMessage(message, "CRNTLY " + MrOperatorBuild.ProductName + " - Load Error");
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

public sealed class MrOperatorScriptWindowProxy : IDisposable
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
    private readonly MethodInfo _invokeMethod;
    private readonly MethodInfo _dispose;

    public MrOperatorScriptWindowProxy(Type bridgeType, object bridge)
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
        _invokeMethod = RequireMethod("InvokeMethod", typeof(string), typeof(string));
        _dispose = RequireMethod("Dispose");

        var hideOnClose = _bridgeType.GetProperty("HideOnUserClose");
        if (hideOnClose != null && hideOnClose.CanWrite)
            hideOnClose.SetValue(_bridge, true, null);
    }

    public string AssemblyVersion
    {
        get
        {
            var property = _bridgeType.GetProperty("AssemblyVersion");
            return property == null ? "unknown" : Convert.ToString(property.GetValue(_bridge, null), CultureInfo.InvariantCulture);
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

public sealed class MrOperatorLineCard
{
    public int LineNumber { get; set; }
    public string LineLabel { get; set; }
    public string CallerId { get; set; }
    public string CallerName { get; set; }
    public string Initials { get; set; }
    public string StateLabel { get; set; }
    public string Subtitle { get; set; }
    public string ActionToolTip { get; set; }
    public bool IsActive { get; set; }
    public bool IsWaiting { get; set; }
    public bool IsActionEnabled { get; set; }
    public bool IsOpen { get; set; }
    public bool IsEmpty { get; set; }
}

public sealed class MrOperatorRuntime : IDisposable
{
    private const int LineCount = 9;

    private const string WindowXaml = @"<Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        xmlns:shell=""clr-namespace:System.Windows.Shell;assembly=PresentationFramework""
        Title=""Mr. Operator"" Width=""1040"" Height=""680"" MinWidth=""900"" MinHeight=""600""
        WindowStartupLocation=""CenterScreen"" WindowStyle=""None"" ResizeMode=""CanResize""
        Background=""{DynamicResource Crntly.Background}"">
  <Window.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceDictionary Source=""/CRNTLY.StreamerBot.UI;component/Theme/CrntlyTheme.xaml"" />
      </ResourceDictionary.MergedDictionaries>
      <CornerRadius x:Key=""Crntly.WindowRadius"">6</CornerRadius>
      <CornerRadius x:Key=""Crntly.CardRadius"">12</CornerRadius>
      <CornerRadius x:Key=""Crntly.ControlRadius"">8</CornerRadius>
      <Geometry x:Key=""MRO.Phone"">M6.62,10.79 C8.06,13.62 10.38,15.94 13.21,17.38 L15.41,15.18 C15.68,14.91 16.08,14.82 16.43,14.94 C17.55,15.31 18.75,15.51 20,15.51 C20.55,15.51 21,15.96 21,16.51 V20 C21,20.55 20.55,21 20,21 C10.61,21 3,13.39 3,4 C3,3.45 3.45,3 4,3 H7.5 C8.05,3 8.5,3.45 8.5,4 C8.5,5.25 8.7,6.45 9.07,7.57 C9.18,7.92 9.1,8.31 8.82,8.59 Z</Geometry>
      <Geometry x:Key=""MRO.Plus"">M11,5 H13 V11 H19 V13 H13 V19 H11 V13 H5 V11 H11 Z</Geometry>
      <Geometry x:Key=""MRO.Filter"">M12,2 L21,6 V11 C21,16.5 17.2,20.4 12,22 C6.8,20.4 3,16.5 3,11 V6 Z M12,4.2 L5,7.3 V11 C5,15.3 7.8,18.4 12,19.8 C16.2,18.4 19,15.3 19,11 V7.3 Z M11,8 H13 V13 H11 Z M11,15 H13 V17 H11 Z</Geometry>
      <Geometry x:Key=""MRO.Arrow"">M4,11 H16.2 L10.6,5.4 L12,4 L20,12 L12,20 L10.6,18.6 L16.2,13 H4 Z</Geometry>
      <Style x:Key=""MRO.LineButton"" TargetType=""Button"" BasedOn=""{StaticResource Crntly.Button}"">
        <Setter Property=""Background"" Value=""{DynamicResource Crntly.SurfaceRaised}"" />
        <Setter Property=""BorderBrush"" Value=""{DynamicResource Crntly.BorderStrong}"" />
        <Setter Property=""Foreground"" Value=""{DynamicResource Crntly.Text}"" />
        <Setter Property=""Padding"" Value=""12,8"" />
        <Setter Property=""MinHeight"" Value=""94"" />
        <Setter Property=""HorizontalContentAlignment"" Value=""Stretch"" />
        <Setter Property=""VerticalContentAlignment"" Value=""Stretch"" />
        <Style.Triggers>
          <DataTrigger Binding=""{Binding IsWaiting}"" Value=""True""><Setter Property=""Background"" Value=""{DynamicResource Crntly.AccentMuted}"" /><Setter Property=""BorderBrush"" Value=""{DynamicResource Crntly.Accent}"" /><Setter Property=""Foreground"" Value=""{DynamicResource Crntly.AccentHover}"" /></DataTrigger>
          <DataTrigger Binding=""{Binding IsActive}"" Value=""True""><Setter Property=""Background"" Value=""{DynamicResource Crntly.SuccessMuted}"" /><Setter Property=""BorderBrush"" Value=""{DynamicResource Crntly.Success}"" /><Setter Property=""Foreground"" Value=""{DynamicResource Crntly.Danger}"" /></DataTrigger>
        </Style.Triggers>
      </Style>
      <Style x:Key=""MRO.Avatar"" TargetType=""Border""><Setter Property=""Background"" Value=""{DynamicResource Crntly.AccentMuted}"" /><Setter Property=""BorderBrush"" Value=""{DynamicResource Crntly.Accent}"" /><Setter Property=""BorderThickness"" Value=""1"" /><Style.Triggers><DataTrigger Binding=""{Binding IsActive}"" Value=""True""><Setter Property=""Background"" Value=""{DynamicResource Crntly.SuccessMuted}"" /><Setter Property=""BorderBrush"" Value=""{DynamicResource Crntly.Success}"" /></DataTrigger></Style.Triggers></Style>
      <Style x:Key=""MRO.OccupiedPanel"" TargetType=""Grid""><Setter Property=""Visibility"" Value=""Collapsed"" /><Style.Triggers><DataTrigger Binding=""{Binding IsWaiting}"" Value=""True""><Setter Property=""Visibility"" Value=""Visible"" /></DataTrigger><DataTrigger Binding=""{Binding IsActive}"" Value=""True""><Setter Property=""Visibility"" Value=""Visible"" /></DataTrigger></Style.Triggers></Style>
      <Style x:Key=""MRO.EmptyPanel"" TargetType=""Border"" BasedOn=""{StaticResource Crntly.SubtleCard}""><Setter Property=""BorderBrush"" Value=""{DynamicResource Crntly.BorderStrong}"" /><Setter Property=""Padding"" Value=""12,8"" /><Setter Property=""Visibility"" Value=""Collapsed"" /><Style.Triggers><DataTrigger Binding=""{Binding IsEmpty}"" Value=""True""><Setter Property=""Visibility"" Value=""Visible"" /></DataTrigger></Style.Triggers></Style>
      <Style x:Key=""MRO.StateText"" TargetType=""TextBlock""><Setter Property=""Foreground"" Value=""{DynamicResource Crntly.TextSubtle}"" /><Setter Property=""FontSize"" Value=""10"" /><Setter Property=""FontWeight"" Value=""SemiBold"" /><Style.Triggers><DataTrigger Binding=""{Binding IsWaiting}"" Value=""True""><Setter Property=""Foreground"" Value=""{DynamicResource Crntly.AccentHover}"" /></DataTrigger><DataTrigger Binding=""{Binding IsActive}"" Value=""True""><Setter Property=""Foreground"" Value=""{DynamicResource Crntly.Success}"" /></DataTrigger></Style.Triggers></Style>
      <Style x:Key=""MRO.Dot"" TargetType=""Ellipse""><Setter Property=""Fill"" Value=""{DynamicResource Crntly.TextSubtle}"" /><Style.Triggers><DataTrigger Binding=""{Binding IsWaiting}"" Value=""True""><Setter Property=""Fill"" Value=""{DynamicResource Crntly.Accent}"" /></DataTrigger><DataTrigger Binding=""{Binding IsActive}"" Value=""True""><Setter Property=""Fill"" Value=""{DynamicResource Crntly.Success}"" /></DataTrigger></Style.Triggers></Style>
      <Style x:Key=""MRO.Initials"" TargetType=""TextBlock""><Setter Property=""Foreground"" Value=""{DynamicResource Crntly.AccentHover}"" /><Style.Triggers><DataTrigger Binding=""{Binding IsActive}"" Value=""True""><Setter Property=""Foreground"" Value=""{DynamicResource Crntly.Success}"" /></DataTrigger></Style.Triggers></Style>
      <Style x:Key=""MRO.AnswerAction"" TargetType=""StackPanel""><Setter Property=""Visibility"" Value=""Collapsed"" /><Style.Triggers><DataTrigger Binding=""{Binding IsWaiting}"" Value=""True""><Setter Property=""Visibility"" Value=""Visible"" /></DataTrigger></Style.Triggers></Style>
      <Style x:Key=""MRO.EndAction"" TargetType=""StackPanel""><Setter Property=""Visibility"" Value=""Collapsed"" /><Style.Triggers><DataTrigger Binding=""{Binding IsActive}"" Value=""True""><Setter Property=""Visibility"" Value=""Visible"" /></DataTrigger></Style.Triggers></Style>
    </ResourceDictionary>
  </Window.Resources>
  <shell:WindowChrome.WindowChrome><shell:WindowChrome CaptionHeight=""38"" ResizeBorderThickness=""5"" CornerRadius=""6"" GlassFrameThickness=""0"" /></shell:WindowChrome.WindowChrome>
  <Border Background=""{StaticResource Crntly.Background}"" BorderBrush=""{StaticResource Crntly.Border}"" BorderThickness=""1"" CornerRadius=""6""><Grid>
    <Grid.RowDefinitions><RowDefinition Height=""38"" /><RowDefinition Height=""80"" /><RowDefinition Height=""*"" /><RowDefinition Height=""24"" /></Grid.RowDefinitions>
    <Border x:Name=""TitleBar"" Grid.Row=""0"" Background=""{StaticResource Crntly.Surface}"" BorderBrush=""{StaticResource Crntly.Border}"" BorderThickness=""0,0,0,1"" CornerRadius=""6,6,0,0""><Grid Margin=""20,0,8,0""><Grid.ColumnDefinitions><ColumnDefinition Width=""22"" /><ColumnDefinition Width=""*"" /><ColumnDefinition Width=""Auto"" /><ColumnDefinition Width=""38"" /><ColumnDefinition Width=""30"" /></Grid.ColumnDefinitions><Path Data=""{StaticResource MRO.Phone}"" Fill=""{StaticResource Crntly.AccentHover}"" Width=""15"" Height=""15"" Stretch=""Uniform"" VerticalAlignment=""Center"" /><TextBlock Grid.Column=""1"" Text=""MR. OPERATOR"" Foreground=""{StaticResource Crntly.TextMuted}"" FontSize=""10"" FontWeight=""Bold"" VerticalAlignment=""Center"" IsHitTestVisible=""False"" /><StackPanel Grid.Column=""2"" Orientation=""Horizontal"" VerticalAlignment=""Center"" Margin=""0,0,16,0""><Ellipse x:Name=""TitleStateDot"" Width=""7"" Height=""7"" Fill=""{StaticResource Crntly.TextSubtle}"" Margin=""0,0,7,0"" /><TextBlock x:Name=""TitleStateText"" Text=""LINES CLOSED"" Foreground=""{StaticResource Crntly.TextSubtle}"" FontSize=""9"" FontWeight=""Bold"" /></StackPanel><Button x:Name=""MinimizeButton"" Grid.Column=""3"" Style=""{StaticResource Crntly.TitleButton}"" ToolTip=""Minimize"" shell:WindowChrome.IsHitTestVisibleInChrome=""True""><TextBlock Text=""—"" FontSize=""15"" /></Button><Button x:Name=""CloseButton"" Grid.Column=""4"" Content=""×"" FontSize=""17"" Style=""{StaticResource Crntly.TitleButton}"" ToolTip=""Close panel"" shell:WindowChrome.IsHitTestVisibleInChrome=""True"" /></Grid></Border>
    <Grid Grid.Row=""1"" Margin=""20,8,20,8""><Grid.ColumnDefinitions><ColumnDefinition Width=""*"" /><ColumnDefinition Width=""Auto"" /></Grid.ColumnDefinitions><StackPanel VerticalAlignment=""Center""><TextBlock Text=""CALL CONTROL"" Foreground=""{StaticResource Crntly.AccentHover}"" FontSize=""10"" FontWeight=""Bold"" /><TextBlock Text=""Mr. Operator"" FontSize=""26"" FontWeight=""SemiBold"" Margin=""0,0,0,-2"" /><TextBlock x:Name=""NoticeText"" Text=""Ready when you are. Open lines to accept callers."" Foreground=""{StaticResource Crntly.TextSubtle}"" FontSize=""11"" Margin=""0,4,0,0"" TextTrimming=""CharacterEllipsis"" /></StackPanel><StackPanel Grid.Column=""1"" Orientation=""Horizontal"" VerticalAlignment=""Center"" Margin=""16,0,0,0""><StackPanel VerticalAlignment=""Center"" Margin=""0,0,22,0""><TextBlock Text=""WAITING"" Foreground=""{StaticResource Crntly.TextSubtle}"" FontSize=""9"" FontWeight=""Bold"" /><TextBlock x:Name=""QueueCountText"" Text=""0"" Foreground=""{StaticResource Crntly.AccentHover}"" FontSize=""20"" FontWeight=""SemiBold"" Margin=""0,1,0,0"" /></StackPanel><Border Width=""1"" Height=""38"" Background=""{StaticResource Crntly.Border}"" Margin=""0,0,22,0"" /><StackPanel VerticalAlignment=""Center"" Margin=""0,0,24,0""><TextBlock Text=""LINES TAKEN"" Foreground=""{StaticResource Crntly.TextSubtle}"" FontSize=""9"" FontWeight=""Bold"" /><TextBlock x:Name=""CapacityText"" Text=""0 / 9"" Foreground=""{StaticResource Crntly.Text}"" FontSize=""18"" FontWeight=""SemiBold"" Margin=""0,2,0,0"" /></StackPanel><Button x:Name=""ToggleLinesButton"" Content=""OPEN LINES"" Style=""{StaticResource Crntly.PrimaryButton}"" MinWidth=""130"" MinHeight=""38"" /></StackPanel></Grid>
    <Grid Grid.Row=""2"" Margin=""20,0,20,10""><Grid.ColumnDefinitions><ColumnDefinition Width=""*"" /><ColumnDefinition Width=""18"" /><ColumnDefinition Width=""1"" /><ColumnDefinition Width=""16"" /><ColumnDefinition Width=""280"" /></Grid.ColumnDefinitions>
      <Grid Grid.Column=""0""><Grid.RowDefinitions><RowDefinition Height=""Auto"" /><RowDefinition Height=""*"" /><RowDefinition Height=""Auto"" /></Grid.RowDefinitions><Grid Grid.Row=""0"" Margin=""0,0,0,10""><StackPanel><TextBlock Text=""CALL DECK"" FontSize=""18"" FontWeight=""SemiBold"" /><TextBlock Text=""Select a waiting caller to go live. Choose any line."" Foreground=""{StaticResource Crntly.TextSubtle}"" FontSize=""11"" Margin=""0,4,0,0"" /></StackPanel><TextBlock Text=""9 LINES"" Foreground=""{StaticResource Crntly.TextMuted}"" FontSize=""10"" FontWeight=""Bold"" VerticalAlignment=""Center"" HorizontalAlignment=""Right"" /></Grid>
        <ItemsControl x:Name=""LineGrid"" Grid.Row=""1"" VerticalAlignment=""Stretch""><ItemsControl.ItemsPanel><ItemsPanelTemplate><UniformGrid Columns=""3"" HorizontalAlignment=""Stretch"" VerticalAlignment=""Stretch"" /></ItemsPanelTemplate></ItemsControl.ItemsPanel><ItemsControl.ItemTemplate>
              <DataTemplate>
                <Grid Margin=""4"">
                  <Button x:Name=""CallerButton"" Style=""{StaticResource MRO.LineButton}"" IsEnabled=""{Binding IsActionEnabled}"" ToolTip=""{Binding ActionToolTip}"">
                    <Grid>
                      <Grid.RowDefinitions><RowDefinition Height=""Auto"" /><RowDefinition Height=""*"" /></Grid.RowDefinitions>
                      <Grid>
                        <Grid.ColumnDefinitions><ColumnDefinition Width=""*"" /><ColumnDefinition Width=""Auto"" /></Grid.ColumnDefinitions>
                        <TextBlock Text=""{Binding LineLabel}"" Foreground=""{DynamicResource Crntly.TextSubtle}"" FontSize=""9"" FontWeight=""Bold"" />
                        <StackPanel Grid.Column=""1"" Orientation=""Horizontal"" VerticalAlignment=""Center"">
                          <Ellipse Style=""{StaticResource MRO.Dot}"" Width=""7"" Height=""7"" Margin=""0,0,6,0"" />
                          <TextBlock Text=""{Binding StateLabel}"" Style=""{StaticResource MRO.StateText}"" />
                        </StackPanel>
                      </Grid>
                      <Grid Grid.Row=""1"" Style=""{StaticResource MRO.OccupiedPanel}"" Margin=""0,10,0,0"">
                        <Grid.ColumnDefinitions><ColumnDefinition Width=""36"" /><ColumnDefinition Width=""*"" /></Grid.ColumnDefinitions>
                        <Border Style=""{StaticResource MRO.Avatar}"" Width=""30"" Height=""30"" CornerRadius=""9"" VerticalAlignment=""Center"">
                          <TextBlock Text=""{Binding Initials}"" Style=""{StaticResource MRO.Initials}"" FontSize=""11"" FontWeight=""Bold"" HorizontalAlignment=""Center"" VerticalAlignment=""Center"" />
                        </Border>
                        <StackPanel Grid.Column=""1"" VerticalAlignment=""Center"" Margin=""8,0,0,0"">
                          <TextBlock Text=""{Binding CallerName}"" Foreground=""{DynamicResource Crntly.Text}"" FontSize=""13"" FontWeight=""SemiBold"" TextTrimming=""CharacterEllipsis"" />
                          <Grid Margin=""0,4,0,0"">
                            <Grid.ColumnDefinitions><ColumnDefinition Width=""*"" /><ColumnDefinition Width=""Auto"" /></Grid.ColumnDefinitions>
                            <TextBlock Text=""{Binding Subtitle}"" Foreground=""{DynamicResource Crntly.TextMuted}"" FontSize=""10"" TextTrimming=""CharacterEllipsis"" />
                            <StackPanel Grid.Column=""1"" Style=""{StaticResource MRO.AnswerAction}"" Orientation=""Horizontal"" VerticalAlignment=""Center"" Margin=""8,0,0,0"">
                              <TextBlock Text=""ANSWER"" Foreground=""{DynamicResource Crntly.AccentHover}"" FontSize=""9"" FontWeight=""Bold"" Margin=""0,0,5,0"" />
                              <Path Style=""{StaticResource Crntly.IconPath}"" Data=""{StaticResource MRO.Arrow}"" Width=""11"" Height=""11"" />
                            </StackPanel>
                            <StackPanel Grid.Column=""1"" Style=""{StaticResource MRO.EndAction}"" Orientation=""Horizontal"" VerticalAlignment=""Center"" Margin=""8,0,0,0"">
                              <TextBlock Text=""END CALL"" Foreground=""{DynamicResource Crntly.Danger}"" FontSize=""9"" FontWeight=""Bold"" />
                            </StackPanel>
                          </Grid>
                        </StackPanel>
                      </Grid>
                    </Grid>
                  </Button>
                  <Border Style=""{StaticResource MRO.EmptyPanel}""><Grid>
                    <Grid.RowDefinitions><RowDefinition Height=""Auto"" /><RowDefinition Height=""*"" /></Grid.RowDefinitions>
                    <Grid>
                      <Grid.ColumnDefinitions><ColumnDefinition Width=""*"" /><ColumnDefinition Width=""Auto"" /></Grid.ColumnDefinitions>
                      <TextBlock Text=""{Binding LineLabel}"" Foreground=""{DynamicResource Crntly.TextSubtle}"" FontSize=""9"" FontWeight=""Bold"" />
                      <StackPanel Grid.Column=""1"" Orientation=""Horizontal"" VerticalAlignment=""Center"">
                        <Ellipse Style=""{StaticResource MRO.Dot}"" Width=""7"" Height=""7"" Margin=""0,0,6,0"" />
                        <TextBlock Text=""{Binding StateLabel}"" Style=""{StaticResource MRO.StateText}"" />
                      </StackPanel>
                    </Grid>
                    <Grid Grid.Row=""1"" Margin=""0,10,0,0"">
                      <Grid.ColumnDefinitions><ColumnDefinition Width=""18"" /><ColumnDefinition Width=""*"" /></Grid.ColumnDefinitions>
                      <Path Data=""{StaticResource MRO.Plus}"" Fill=""{DynamicResource Crntly.Accent}"" Width=""13"" Height=""13"" Stretch=""Uniform"" VerticalAlignment=""Center"" />
                      <StackPanel Grid.Column=""1"" Margin=""8,0,0,0"" VerticalAlignment=""Center"">
                        <TextBlock Text=""{Binding CallerName}"" Foreground=""{DynamicResource Crntly.TextMuted}"" FontSize=""11"" FontWeight=""SemiBold"" />
                        <TextBlock Text=""{Binding Subtitle}"" Foreground=""{DynamicResource Crntly.TextSubtle}"" FontSize=""10"" Margin=""0,3,0,0"" TextTrimming=""CharacterEllipsis"" />
                      </StackPanel>
                    </Grid>
                  </Grid>
                </Border>
                </Grid>
                <DataTemplate.Triggers>
                  <DataTrigger Binding=""{Binding IsEmpty}"" Value=""True"">
                    <Setter TargetName=""CallerButton"" Property=""Visibility"" Value=""Collapsed"" />
                  </DataTrigger>
                </DataTemplate.Triggers>
              </DataTemplate>
            </ItemsControl.ItemTemplate></ItemsControl>
        <TextBlock Grid.Row=""2"" Text=""WAITING CALLERS MOVE FORWARD WHEN A CALL ENDS  ·  YOU CHOOSE WHO GOES LIVE"" Foreground=""{StaticResource Crntly.TextSubtle}"" FontSize=""9"" FontWeight=""SemiBold"" Margin=""4,10,0,0"" TextWrapping=""Wrap"" />
      </Grid>
      <Border Grid.Column=""2"" Background=""{StaticResource Crntly.Border}"" />
      <ScrollViewer Grid.Column=""4"" VerticalScrollBarVisibility=""Auto"" HorizontalScrollBarVisibility=""Disabled""><StackPanel><Border x:Name=""CurrentCallCard"" Style=""{StaticResource Crntly.Card}"" Padding=""12"" Margin=""0,0,0,14""><Grid><Grid.RowDefinitions><RowDefinition Height=""Auto"" /><RowDefinition Height=""Auto"" /></Grid.RowDefinitions><Grid><Grid.ColumnDefinitions><ColumnDefinition Width=""*"" /><ColumnDefinition Width=""Auto"" /></Grid.ColumnDefinitions><StackPanel Orientation=""Horizontal"" VerticalAlignment=""Center""><Ellipse x:Name=""CallStateDot"" Width=""8"" Height=""8"" Fill=""{StaticResource Crntly.TextSubtle}"" Margin=""0,0,8,0"" /><TextBlock x:Name=""CallStateText"" Text=""STANDBY"" Foreground=""{StaticResource Crntly.TextSubtle}"" FontSize=""10"" FontWeight=""Bold"" /></StackPanel><StackPanel Grid.Column=""1"" HorizontalAlignment=""Right""><TextBlock Text=""CALL TIME"" Foreground=""{StaticResource Crntly.TextSubtle}"" FontSize=""9"" FontWeight=""Bold"" HorizontalAlignment=""Right"" /><TextBlock x:Name=""CallDurationText"" Text=""00:00"" Foreground=""{StaticResource Crntly.AccentHover}"" FontFamily=""Consolas"" FontSize=""20"" FontWeight=""SemiBold"" Margin=""0,2,0,0"" /></StackPanel></Grid><Grid Grid.Row=""1"" Margin=""0,12,0,0""><Grid.ColumnDefinitions><ColumnDefinition Width=""42"" /><ColumnDefinition Width=""*"" /></Grid.ColumnDefinitions><Border x:Name=""CurrentCallerBadge"" Width=""36"" Height=""36"" CornerRadius=""11"" Background=""{StaticResource Crntly.AccentMuted}"" BorderBrush=""{StaticResource Crntly.Accent}"" BorderThickness=""1"" VerticalAlignment=""Center""><Grid><Path x:Name=""CurrentCallerIcon"" Data=""{StaticResource MRO.Phone}"" Fill=""{StaticResource Crntly.AccentHover}"" Width=""16"" Height=""16"" Stretch=""Uniform"" HorizontalAlignment=""Center"" VerticalAlignment=""Center"" /><TextBlock x:Name=""CurrentCallerInitials"" Text="""" Foreground=""{StaticResource Crntly.Text}"" FontSize=""12"" FontWeight=""Bold"" HorizontalAlignment=""Center"" VerticalAlignment=""Center"" Visibility=""Collapsed"" /></Grid></Border><StackPanel Grid.Column=""1"" VerticalAlignment=""Center"" Margin=""10,0,0,0""><TextBlock Text=""ON AIR NOW"" Foreground=""{StaticResource Crntly.TextSubtle}"" FontSize=""9"" FontWeight=""Bold"" /><TextBlock x:Name=""CurrentCallerText"" Text=""No caller connected"" FontSize=""15"" FontWeight=""SemiBold"" TextTrimming=""CharacterEllipsis"" Margin=""0,2,0,0"" /><TextBlock x:Name=""CurrentCallMetaText"" Text=""Pick any waiting line when ready"" Foreground=""{StaticResource Crntly.TextMuted}"" FontSize=""10"" Margin=""0,3,0,0"" TextWrapping=""Wrap"" /></StackPanel></Grid></Grid></Border>
        <TextBlock Text=""VOICE"" Foreground=""{StaticResource Crntly.TextMuted}"" FontSize=""12"" FontWeight=""Bold"" Margin=""2,0,0,6"" /><ComboBox x:Name=""VoiceSelect"" Style=""{StaticResource Crntly.ComboBox}"" MinHeight=""34"" /><Grid Margin=""2,9,2,0""><Grid.ColumnDefinitions><ColumnDefinition Width=""*"" /><ColumnDefinition Width=""Auto"" /></Grid.ColumnDefinitions><TextBlock Text=""OUTPUT LEVEL"" Foreground=""{StaticResource Crntly.TextSubtle}"" FontSize=""9"" FontWeight=""Bold"" /><TextBlock x:Name=""VolumeValueText"" Grid.Column=""1"" Text=""80%"" Foreground=""{StaticResource Crntly.TextMuted}"" FontSize=""10"" FontWeight=""Bold"" /></Grid><Slider x:Name=""VolumeSlider"" Style=""{StaticResource Crntly.Slider}"" Minimum=""0"" Maximum=""100"" Value=""80"" TickFrequency=""10"" IsSnapToTickEnabled=""False"" Background=""{StaticResource Crntly.Border}"" Foreground=""{StaticResource Crntly.AccentHover}"" Margin=""0,2,0,0"" /><CheckBox x:Name=""MuteToggle"" Style=""{StaticResource Crntly.Toggle}"" Content=""Mute caller speech"" Margin=""0,6,0,0"" /><Border Height=""1"" Background=""{StaticResource Crntly.Border}"" Margin=""0,12,0,12"" />
        <StackPanel Orientation=""Horizontal"" Margin=""0,0,0,6""><Path Data=""{StaticResource MRO.Filter}"" Fill=""{StaticResource Crntly.AccentHover}"" Width=""15"" Height=""15"" Stretch=""Uniform"" VerticalAlignment=""Center"" Margin=""0,0,9,0"" /><StackPanel><TextBlock Text=""CHAT FILTER"" FontSize=""12"" FontWeight=""Bold"" /><TextBlock Text=""Clean words before they are spoken"" Foreground=""{StaticResource Crntly.TextSubtle}"" FontSize=""10"" Margin=""0,3,0,0"" /></StackPanel></StackPanel><CheckBox x:Name=""FilterToggle"" Style=""{StaticResource Crntly.Toggle}"" Content=""Replace filtered words"" Margin=""0,2,0,6"" /><TextBox x:Name=""FilterWordsBox"" Style=""{StaticResource Crntly.TextBox}"" MinHeight=""60"" AcceptsReturn=""True"" TextWrapping=""Wrap"" VerticalScrollBarVisibility=""Auto"" ToolTip=""Separate words with commas, semicolons or new lines."" /><TextBlock Text=""Use commas, semicolons, or a new line between words."" Foreground=""{StaticResource Crntly.TextSubtle}"" FontSize=""10"" TextWrapping=""Wrap"" Margin=""0,5,0,0"" />
      </StackPanel></ScrollViewer>
    </Grid>
    <Grid Grid.Row=""3"" Margin=""20,0""><TextBlock Text=""STREAMER.BOT  /  CALL DESK"" Foreground=""{StaticResource Crntly.TextSubtle}"" FontSize=""9"" VerticalAlignment=""Center"" /><TextBlock x:Name=""VersionText"" Text=""Mr. Operator v3.0.0"" Foreground=""{StaticResource Crntly.TextSubtle}"" FontSize=""9"" HorizontalAlignment=""Right"" VerticalAlignment=""Center"" /></Grid>
  </Grid></Border>
</Window>";

    private readonly object _stateGate = new object();
    private readonly object _speechGate = new object();
    private readonly SemaphoreSlim _rewardSyncGate = new SemaphoreSlim(1, 1);
    private readonly List<string> _queuedCallers = new List<string>();
    private readonly List<string> _voiceNames = new List<string>();
    private readonly string _rewardId;
    private readonly MrOperatorScriptWindowProxy _window;
    private readonly Action<string> _sendMessage;
    private readonly Action<string> _log;
    private readonly Action<string> _logError;
    private readonly Action<string> _enableReward;
    private readonly Action<string> _disableReward;
    private readonly System.Threading.Timer _callTimer;

    private SpeechSynthesizer _synth;
    private string _currentCaller;
    private string _selectedVoice;
    private string _notice = "Open lines to accept callers. Use ANSWER and HANG UP on the line.";
    private List<string> _filteredWords = new List<string>();
    private DateTime _callStartTime;
    private int _activeLine;
    private int _volume = 80;
    private bool _linesOpen;
    private bool _muted;
    private bool _filterEnabled;
    private bool _started;
    private bool _disposed;
    private long _rewardVersion;

    public MrOperatorRuntime(
        string rewardId,
        MrOperatorScriptWindowProxy window,
        Action<string> sendMessage,
        Action<string> enableReward,
        Action<string> disableReward,
        Action<string> log,
        Action<string> logError)
    {
        _rewardId = rewardId;
        _window = window;
        _sendMessage = sendMessage;
        _enableReward = enableReward;
        _disableReward = disableReward;
        _log = log;
        _logError = logError;
        _callTimer = new System.Threading.Timer(OnCallTimer, null, Timeout.Infinite, Timeout.Infinite);
    }

    public bool IsDisposed
    {
        get { lock (_stateGate) return _disposed; }
    }

    public void Start()
    {
        lock (_stateGate)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MrOperatorRuntime));
            if (_started)
                return;
            _started = true;
        }

        InitializeSpeech();

        _window.EventRaised = HandleUiEvent;
        _window.RoutedEventRaised = HandleRoutedUiEvent;
        _window.Show(WindowXaml);
        _window.SetProperty("$window", "Title", MrOperatorBuild.ProductName);
        _window.SetProperty("VersionText", "Text",
            MrOperatorBuild.ProductName + " v" + MrOperatorBuild.Version + "  ·  UI " + _window.AssemblyVersion);
        _window.SetItemsSource("VoiceSelect", _voiceNames);

        lock (_stateGate)
            _selectedVoice = _voiceNames.Count == 0 ? null : _voiceNames[0];

        if (_voiceNames.Count > 0)
            _window.SetProperty("VoiceSelect", "SelectedIndex", 0);

        BindUiEvents();
        Render();

        long initialRewardVersion;
        lock (_stateGate)
            initialRewardVersion = _rewardVersion;
        RequestRewardSync(false, initialRewardVersion, false);
    }

    public void Show()
    {
        if (IsDisposed)
            return;
        if (!_started)
            Start();

        _window.Show(WindowXaml);
        Render();
    }

    private void InitializeSpeech()
    {
        try
        {
            _synth = new SpeechSynthesizer();
            _synth.SetOutputToDefaultAudioDevice();

            foreach (var voice in _synth.GetInstalledVoices())
            {
                if (voice != null && voice.Enabled && voice.VoiceInfo != null &&
                    !string.IsNullOrWhiteSpace(voice.VoiceInfo.Name))
                    _voiceNames.Add(voice.VoiceInfo.Name);
            }
        }
        catch (Exception ex)
        {
            _synth = null;
            _notice = "Windows speech output is unavailable. Switchboard controls are still active.";
            if (_logError != null)
                _logError("Speech synthesis initialization failed: " + ex);
        }
    }

    private void BindUiEvents()
    {
        _window.BindEvent("ToggleLinesButton", "Click", "toggle-lines");
        _window.BindEvent("MinimizeButton", "Click", "minimize-window");
        _window.BindEvent("CloseButton", "Click", "close-panel");
        _window.BindEvent("$window", "Closing", "window-closing");
        _window.BindEvent("MuteToggle", "Checked", "mute-on");
        _window.BindEvent("MuteToggle", "Unchecked", "mute-off");
        _window.BindEvent("FilterToggle", "Checked", "filter-on");
        _window.BindEvent("FilterToggle", "Unchecked", "filter-off");
        _window.BindEvent("FilterWordsBox", "TextChanged", "filter-text");
        _window.BindEvent("VolumeSlider", "ValueChanged", "volume-changed");
        _window.BindEvent("VoiceSelect", "SelectionChanged", "voice-changed");
        _window.BindRoutedEvent(
            "System.Windows.Controls.Primitives.ButtonBase, PresentationFramework",
            "ClickEvent",
            "line-click");
    }

    private void HandleUiEvent(string eventKey)
    {
        try
        {
            switch (eventKey)
            {
                case "toggle-lines":
                    ToggleLines();
                    break;
                case "minimize-window":
                    _window.SetProperty("$window", "WindowState", "Minimized");
                    break;
                case "close-panel":
                case "window-closing":
                    ClosePanel();
                    break;
                case "mute-on":
                    SetMuted(true);
                    break;
                case "mute-off":
                    SetMuted(false);
                    break;
                case "filter-on":
                    SetFilterEnabled(true);
                    break;
                case "filter-off":
                    SetFilterEnabled(false);
                    break;
                case "filter-text":
                    ReadFilterText();
                    break;
                case "volume-changed":
                    ReadVolume();
                    break;
                case "voice-changed":
                    ReadVoice();
                    break;
            }
        }
        catch (Exception ex)
        {
            if (_logError != null)
                _logError("UI event '" + eventKey + "' failed: " + ex);
            SetNotice("That control could not be updated. Check the Streamer.bot log.");
        }
    }

    private void HandleRoutedUiEvent(string eventKey, object dataContext)
    {
        if (!string.Equals(eventKey, "line-click", StringComparison.Ordinal))
            return;

        var line = dataContext as MrOperatorLineCard;
        if (line != null)
            SelectLine(line);
    }

    private void ToggleLines()
    {
        bool desiredState;
        long version;
        lock (_stateGate)
        {
            if (_disposed)
                return;
            _linesOpen = !_linesOpen;
            desiredState = _linesOpen;
            version = ++_rewardVersion;
            _notice = desiredState
                ? "Lines are opening. " + JoinInstructionText()
                : "Lines are closing. The active call can finish.";
        }

        Render();
        RequestRewardSync(desiredState, version, true);
    }

    private string JoinInstructionText()
    {
        return string.IsNullOrWhiteSpace(_rewardId)
            ? "Send the Phone emote by itself in chat to join."
            : "Send the Phone emote by itself or redeem Call In to join.";
    }

    private void RequestRewardSync(bool desiredState, long version, bool announce)
    {
        Task.Run(() =>
        {
            _rewardSyncGate.Wait();
            try
            {
                lock (_stateGate)
                {
                    if (_disposed || version != _rewardVersion || desiredState != _linesOpen)
                        return;
                }

                if (!string.IsNullOrWhiteSpace(_rewardId))
                {
                    if (desiredState)
                        _enableReward(_rewardId);
                    else
                        _disableReward(_rewardId);
                }

                bool stillCurrent;
                lock (_stateGate)
                {
                    stillCurrent = !_disposed && version == _rewardVersion && desiredState == _linesOpen;
                    if (stillCurrent)
                        _notice = desiredState
                            ? (string.IsNullOrWhiteSpace(_rewardId)
                                ? "Lines are open. " + JoinInstructionText()
                                : "Call In is enabled. " + JoinInstructionText())
                            : (string.IsNullOrWhiteSpace(_rewardId)
                                ? "Lines are closed. The active call can finish."
                                : "Call In is disabled. The active call can finish.");
                }

                if (stillCurrent && announce && _sendMessage != null)
                {
                    try
                    {
                        _sendMessage(desiredState
                            ? "📞 Switchboard is OPEN! " + JoinInstructionText()
                            : "📞 Switchboard is CLOSED.");
                    }
                    catch (Exception ex)
                    {
                        if (_logError != null)
                            _logError("Unable to announce the switchboard state: " + ex);
                    }
                }

                if (stillCurrent)
                    Render();
            }
            catch (Exception ex)
            {
                bool shouldRender = false;
                lock (_stateGate)
                {
                    if (!_disposed && version == _rewardVersion)
                    {
                        if (desiredState)
                        {
                            _notice = string.IsNullOrWhiteSpace(_rewardId)
                                ? "Lines are open. " + JoinInstructionText()
                                : "Phone calls are open, but Call In could not be enabled. Check Twitch and Streamer.bot.";
                        }
                        else
                        {
                            _notice = "Lines are closed, but Call In could not be disabled. Check Twitch and Streamer.bot.";
                        }
                        shouldRender = true;
                    }
                }

                if (_logError != null)
                    _logError("Unable to sync Call In reward state: " + ex);
                if (shouldRender)
                    Render();
            }
            finally
            {
                _rewardSyncGate.Release();
            }
        });
    }

    public bool HandleRewardRedemption(string userName, string rewardName)
    {
        if (string.IsNullOrWhiteSpace(userName) ||
            !string.Equals(rewardName, MrOperatorBuild.RewardName, StringComparison.OrdinalIgnoreCase))
            return false;

        return JoinCallQueue(userName);
    }

    private bool JoinCallQueue(string userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return false;

        string response = null;
        bool accepted = false;
        lock (_stateGate)
        {
            if (_disposed)
                return false;

            if (!_linesOpen)
            {
                response = "@" + userName + " the switchboard is closed. Wait for the lines to open before calling.";
            }
            else
            {
                var duplicateIndex = _queuedCallers.FindIndex(
                    caller => string.Equals(caller, userName, StringComparison.OrdinalIgnoreCase));
                if (duplicateIndex >= 0)
                {
                    response = "@" + userName + " you're already waiting at position #" + (duplicateIndex + 1) + ".";
                }
                else if (string.Equals(_currentCaller, userName, StringComparison.OrdinalIgnoreCase))
                {
                    response = "@" + userName + " you're already on the line.";
                }
                else if (_queuedCallers.Count + (_currentCaller == null ? 0 : 1) >= LineCount)
                {
                    response = "@" + userName + " the switchboard is full right now. Please try again later.";
                }
                else
                {
                    _queuedCallers.Add(userName);
                    _notice = "@" + userName + " joined the queue.";
                    response = "📞 @" + userName + " joined the queue! Position #" + _queuedCallers.Count + ".";
                    accepted = true;
                }
            }
        }

        if (_sendMessage != null && response != null)
        {
            try { _sendMessage(response); }
            catch (Exception ex)
            {
                if (_logError != null)
                    _logError("Unable to announce the caller queue update: " + ex);
            }
        }
        if (accepted)
            Render();

        return accepted;
    }

    public bool HandleChatMessage(string userName, string message)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(message))
            return false;

        var command = MrOperatorChatCommandParser.Parse(message);
        if (command == MrOperatorChatCommand.RequestCall)
            return JoinCallQueue(userName);
        if (command == MrOperatorChatCommand.HangUp)
            return HandleHangUpEmote(userName);

        string caller;
        string selectedVoice;
        string[] words;
        int volume;
        lock (_stateGate)
        {
            if (_disposed ||
                _currentCaller == null ||
                !string.Equals(userName, _currentCaller, StringComparison.OrdinalIgnoreCase) ||
                _muted)
                return false;

            caller = _currentCaller;
            selectedVoice = _selectedVoice;
            volume = _volume;
            words = _filterEnabled ? _filteredWords.ToArray() : new string[0];
        }

        var spokenMessage = message;
        foreach (var word in words)
        {
            if (string.IsNullOrWhiteSpace(word))
                continue;

            spokenMessage = Regex.Replace(
                spokenMessage,
                Regex.Escape(word),
                match => new string('*', match.Length),
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }

        Speak(spokenMessage, selectedVoice, volume, false);
        if (_log != null)
            _log("Speaking a chat message from the active caller " + caller + ".");

        return true;
    }

    private bool HandleHangUpEmote(string userName)
    {
        string endedCaller = null;
        TimeSpan callDuration = TimeSpan.Zero;
        string queueMessage = null;

        lock (_stateGate)
        {
            if (_disposed)
                return false;

            if (string.Equals(_currentCaller, userName, StringComparison.OrdinalIgnoreCase))
            {
                endedCaller = _currentCaller;
                callDuration = DateTime.UtcNow - _callStartTime;
                _currentCaller = null;
                _activeLine = 0;
                _callTimer.Change(Timeout.Infinite, Timeout.Infinite);
                _notice = "The call with @" + endedCaller + " has ended.";
            }
            else
            {
                var queueIndex = _queuedCallers.FindIndex(
                    caller => string.Equals(caller, userName, StringComparison.OrdinalIgnoreCase));
                if (queueIndex < 0)
                    return false;

                _queuedCallers.RemoveAt(queueIndex);
                _notice = "@" + userName + " left the call queue.";
                queueMessage = "📞 @" + userName + " left the call queue.";
            }
        }

        Render();
        if (endedCaller != null)
        {
            AnnounceCallEnded(endedCaller, callDuration);
            if (_log != null)
                _log("The active caller " + endedCaller + " ended the call with the Hangup emote.");
        }
        else if (queueMessage != null && _sendMessage != null)
        {
            try { _sendMessage(queueMessage); }
            catch (Exception ex)
            {
                if (_logError != null)
                    _logError("Unable to announce a caller leaving the queue: " + ex);
            }
        }

        return true;
    }

    private void SelectLine(MrOperatorLineCard line)
    {
        if (line == null || line.IsEmpty)
            return;

        string connectedCaller = null;
        string endedCaller = null;
        TimeSpan callDuration = TimeSpan.Zero;
        bool startedCall = false;
        bool endedCall = false;

        lock (_stateGate)
        {
            if (_disposed)
                return;

            if (_currentCaller != null)
            {
                if (line.IsActive && line.LineNumber == _activeLine)
                {
                    endedCaller = _currentCaller;
                    callDuration = DateTime.UtcNow - _callStartTime;
                    _currentCaller = null;
                    _activeLine = 0;
                    _callTimer.Change(Timeout.Infinite, Timeout.Infinite);
                    _notice = "The call with @" + endedCaller + " has ended.";
                    endedCall = true;
                }
                else
                {
                    _notice = "Finish the active call before connecting another caller.";
                }
            }
            else
            {
                var queueIndex = _queuedCallers.FindIndex(
                    caller => string.Equals(caller, line.CallerId, StringComparison.OrdinalIgnoreCase));

                if (queueIndex >= 0)
                {
                    connectedCaller = _queuedCallers[queueIndex];
                    _queuedCallers.RemoveAt(queueIndex);
                    _currentCaller = connectedCaller;
                    _activeLine = line.LineNumber;
                    _callStartTime = DateTime.UtcNow;
                    _callTimer.Change(0, 1000);
                    _notice = "@" + connectedCaller + " is live. Their chat is read aloud; send Hangup or click the active line to end.";
                    startedCall = true;
                }
                else
                {
                    _notice = "Line " + line.LineNumber.ToString("00", CultureInfo.InvariantCulture) +
                        " is open. " + JoinInstructionText();
                }
            }
        }

        Render();

        if (startedCall)
        {
            StopSpeech();
            var caller = connectedCaller;
            var lineNumber = line.LineNumber;
            Task.Run(() =>
            {
                try
                {
                    if (_sendMessage != null)
                        _sendMessage("📞 Line " + lineNumber + " connected to @" + caller + "! Type in chat to speak.");
                    Speak(caller + " is now on line " + lineNumber + ".", null, -1, false);
                }
                catch (Exception ex)
                {
                    if (_logError != null)
                        _logError("Unable to announce the active caller: " + ex);
                }
            });
        }
        else if (endedCall)
        {
            AnnounceCallEnded(endedCaller, callDuration);
        }
    }

    private void AnnounceCallEnded(string caller, TimeSpan callDuration)
    {
        StopSpeech();
        var duration = FormatDuration(callDuration);
        Task.Run(() =>
        {
            try
            {
                if (_sendMessage != null)
                    _sendMessage("📞 Call ended with @" + caller + " (Duration: " + duration + "). Thanks for calling!");
                Speak("Call ended.", null, -1, false);
            }
            catch (Exception ex)
            {
                if (_logError != null)
                    _logError("Unable to announce the call ending: " + ex);
            }
        });
    }

    private void ClosePanel()
    {
        string endedCaller;
        int waitingCount;
        long version;
        lock (_stateGate)
        {
            if (_disposed)
                return;

            endedCaller = _currentCaller;
            waitingCount = _queuedCallers.Count;
            _currentCaller = null;
            _activeLine = 0;
            _queuedCallers.Clear();
            _linesOpen = false;
            _callTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _notice = "Switchboard stopped. Reopen the panel and lines to take more calls.";
            version = ++_rewardVersion;
        }

        StopSpeech();
        Render();
        RequestRewardSync(false, version, false);

        try { _window.Hide(); } catch { }

        string closingMessage = null;
        if (endedCaller != null)
            closingMessage = "📞 The switchboard closed. The call with @" + endedCaller + " has ended.";
        else if (waitingCount > 0)
            closingMessage = "📞 The switchboard closed and cleared " + waitingCount + " waiting caller(s).";

        if (closingMessage != null && _sendMessage != null)
        {
            Task.Run(() =>
            {
                try { _sendMessage(closingMessage); }
                catch (Exception ex)
                {
                    if (_logError != null)
                        _logError("Unable to announce switchboard shutdown: " + ex);
                }
            });
        }
    }

    private void SetMuted(bool muted)
    {
        lock (_stateGate)
        {
            if (_disposed)
                return;
            _muted = muted;
            _notice = muted ? "Caller speech is muted." : "Caller speech is unmuted.";
        }

        if (muted)
            StopSpeech();
        Render();
    }

    private void SetFilterEnabled(bool enabled)
    {
        lock (_stateGate)
        {
            if (_disposed)
                return;
            _filterEnabled = enabled;
            _notice = enabled
                ? "The word filter is enabled for caller speech."
                : "The word filter is disabled.";
        }
        Render();
    }

    private void ReadFilterText()
    {
        var text = _window.Get<string>("FilterWordsBox", "Text", string.Empty);
        var words = text
            .Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Trim())
            .Where(word => word.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        lock (_stateGate)
        {
            if (_disposed)
                return;
            _filteredWords = words;
        }
    }

    private void ReadVolume()
    {
        var rawValue = _window.Get<double>("VolumeSlider", "Value", 80.0);
        var volume = Math.Max(0, Math.Min(100, (int)Math.Round(rawValue)));

        lock (_stateGate)
        {
            if (_disposed)
                return;
            _volume = volume;
        }

        try { _window.SetProperty("VolumeValueText", "Text", volume.ToString(CultureInfo.InvariantCulture) + "%"); } catch { }
    }

    private void ReadVoice()
    {
        var selected = _window.GetProperty("VoiceSelect", "SelectedItem");
        var voice = selected == null ? null : Convert.ToString(selected, CultureInfo.InvariantCulture);

        lock (_stateGate)
        {
            if (_disposed)
                return;
            _selectedVoice = voice;
        }
    }

    private void SetNotice(string notice)
    {
        lock (_stateGate)
        {
            if (_disposed)
                return;
            _notice = notice;
        }
        Render();
    }

    private void Render()
    {
        MrOperatorLineCard[] lines;
        string currentCaller;
        string notice;
        string duration;
        string callMeta;
        int waitingCount;
        int activeLine;
        int connectedCount;
        bool linesOpen;

        lock (_stateGate)
        {
            if (_disposed || !_started)
                return;

            currentCaller = _currentCaller;
            waitingCount = _queuedCallers.Count;
            activeLine = _activeLine;
            connectedCount = waitingCount + (currentCaller == null ? 0 : 1);
            linesOpen = _linesOpen;
            notice = _notice;
            duration = currentCaller == null
                ? "00:00"
                : FormatDuration(DateTime.UtcNow - _callStartTime);
            callMeta = currentCaller == null
                ? (waitingCount == 0 ? "Waiting for the first caller" : "Click a waiting line to connect")
                : "Connected on Line " + activeLine.ToString("00", CultureInfo.InvariantCulture) + " · click or send Hangup to end";

            var assigned = new Dictionary<int, MrOperatorLineCard>();
            if (currentCaller != null && activeLine >= 1 && activeLine <= LineCount)
                assigned[activeLine] = CreateLineCard(activeLine, currentCaller, true, false, 0, linesOpen, false);

            var queuePosition = 0;
            for (var i = 0; i < _queuedCallers.Count; i++)
            {
                var slot = 1;
                while (slot <= LineCount && assigned.ContainsKey(slot))
                    slot++;
                if (slot > LineCount)
                    break;

                queuePosition++;
                assigned[slot] = CreateLineCard(slot, _queuedCallers[i], false, true, queuePosition, linesOpen, currentCaller == null);
            }

            var list = new List<MrOperatorLineCard>(LineCount);
            for (var lineNumber = 1; lineNumber <= LineCount; lineNumber++)
            {
                MrOperatorLineCard card;
                if (assigned.TryGetValue(lineNumber, out card))
                    list.Add(card);
                else
                    list.Add(CreateLineCard(lineNumber, null, false, false, 0, linesOpen, false));
            }
            lines = list.ToArray();
        }

        try
        {
            _window.SetItemsSource("LineGrid", lines);
            _window.SetProperty("TitleStateText", "Text", linesOpen ? "LINES OPEN" : "LINES CLOSED");
            _window.SetResourceProperty("TitleStateDot", "Fill", linesOpen ? "Crntly.Success" : "Crntly.TextSubtle");
            _window.SetResourceProperty("TitleStateText", "Foreground", linesOpen ? "Crntly.Success" : "Crntly.TextSubtle");
            _window.SetProperty("ToggleLinesButton", "Content", linesOpen ? "CLOSE LINES" : "OPEN LINES");
            _window.SetResourceProperty("ToggleLinesButton", "Style", linesOpen ? "Crntly.DangerButton" : "Crntly.PrimaryButton");
            _window.SetProperty("QueueCountText", "Text",
                waitingCount.ToString(CultureInfo.InvariantCulture));
            _window.SetProperty("CapacityText", "Text",
                connectedCount.ToString(CultureInfo.InvariantCulture) + " / " + LineCount.ToString(CultureInfo.InvariantCulture));
            _window.SetProperty("CallStateText", "Text", currentCaller == null ? "STANDBY" : "ON AIR");
            _window.SetResourceProperty("CallStateDot", "Fill", currentCaller == null ? "Crntly.TextSubtle" : "Crntly.Success");
            _window.SetResourceProperty("CallStateText", "Foreground", currentCaller == null ? "Crntly.TextSubtle" : "Crntly.Success");
            _window.SetResourceProperty("CurrentCallCard", "BorderBrush", currentCaller == null ? "Crntly.Border" : "Crntly.Success");
            _window.SetResourceProperty("CurrentCallCard", "Background", currentCaller == null ? "Crntly.Surface" : "Crntly.SuccessMuted");
            _window.SetProperty("CurrentCallerInitials", "Text", currentCaller == null ? string.Empty : GetInitials(currentCaller));
            _window.SetProperty("CurrentCallerInitials", "Visibility", currentCaller == null ? "Collapsed" : "Visible");
            _window.SetProperty("CurrentCallerIcon", "Visibility", currentCaller == null ? "Visible" : "Collapsed");
            _window.SetResourceProperty("CurrentCallerBadge", "Background", currentCaller == null ? "Crntly.AccentMuted" : "Crntly.SuccessMuted");
            _window.SetResourceProperty("CurrentCallerBadge", "BorderBrush", currentCaller == null ? "Crntly.Accent" : "Crntly.Success");
            _window.SetResourceProperty("CurrentCallerIcon", "Fill", currentCaller == null ? "Crntly.AccentHover" : "Crntly.Success");
            _window.SetResourceProperty("CurrentCallerInitials", "Foreground", currentCaller == null ? "Crntly.Text" : "Crntly.Success");
            _window.SetProperty("CurrentCallerText", "Text",
                currentCaller == null ? "No caller on the air" : "@" + currentCaller);
            _window.SetProperty("CurrentCallMetaText", "Text", callMeta);
            _window.SetProperty("CallDurationText", "Text", duration);
            _window.SetResourceProperty("CallDurationText", "Foreground",
                currentCaller == null ? "Crntly.AccentHover" : "Crntly.Success");
            _window.SetProperty("NoticeText", "Text", notice);
        }
        catch (Exception ex)
        {
            if (_logError != null)
                _logError("Unable to refresh the switchboard window: " + ex);
        }
    }

    private MrOperatorLineCard CreateLineCard(
        int lineNumber,
        string caller,
        bool active,
        bool waiting,
        int queuePosition,
        bool linesOpen,
        bool canAnswer)
    {
        var label = "LINE " + lineNumber.ToString("00", CultureInfo.InvariantCulture);
        if (active)
        {
            return new MrOperatorLineCard
            {
                LineNumber = lineNumber,
                LineLabel = label,
                CallerId = caller,
                CallerName = "Call on air",
                Initials = "LIVE",
                StateLabel = "ACTIVE",
                Subtitle = "Click or send Hangup to end",
                ActionToolTip = "End the active call, or let the caller send Hangup",
                IsActive = true,
                IsWaiting = false,
                IsActionEnabled = true,
                IsOpen = false
            };
        }

        if (waiting)
        {
            return new MrOperatorLineCard
            {
                LineNumber = lineNumber,
                LineLabel = label,
                CallerId = caller,
                CallerName = "@" + caller,
                Initials = GetInitials(caller),
                StateLabel = "WAITING",
                Subtitle = "Queue #" + queuePosition.ToString(CultureInfo.InvariantCulture),
                ActionToolTip = canAnswer ? "Answer this caller" : "Finish the active call before answering",
                IsActive = false,
                IsWaiting = true,
                IsActionEnabled = canAnswer,
                IsOpen = false
            };
        }

        return new MrOperatorLineCard
        {
            LineNumber = lineNumber,
            LineLabel = label,
            CallerId = null,
            CallerName = linesOpen ? "Ready for callers" : "Line standing by",
            Initials = string.Empty,
            StateLabel = linesOpen ? "READY" : "STANDBY",
            Subtitle = linesOpen
                ? (string.IsNullOrWhiteSpace(_rewardId) ? "Send Phone emote to join" : "Send Phone or redeem Call In")
                : "Open lines to accept callers",
            IsActive = false,
            IsWaiting = false,
            IsActionEnabled = false,
            IsOpen = linesOpen,
            IsEmpty = true
        };
    }

    private static string GetInitials(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "—";

        var cleaned = name.Trim().TrimStart('@');
        if (cleaned.Length <= 2)
            return cleaned.ToUpperInvariant();
        return cleaned.Substring(0, 2).ToUpperInvariant();
    }

    private void OnCallTimer(object state)
    {
        if (IsDisposed)
            return;

        string duration;
        lock (_stateGate)
        {
            if (_disposed || _currentCaller == null)
                return;
            duration = FormatDuration(DateTime.UtcNow - _callStartTime);
        }

        try { _window.SetProperty("CallDurationText", "Text", duration); } catch { }
    }

    private void Speak(string text, string voiceName, int volume, bool cancelCurrent)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (volume < 0 || string.IsNullOrWhiteSpace(voiceName))
        {
            lock (_stateGate)
            {
                if (volume < 0)
                    volume = _volume;
                if (string.IsNullOrWhiteSpace(voiceName))
                    voiceName = _selectedVoice;
            }
        }

        var selectedVoice = voiceName;
        var selectedVolume = Math.Max(0, Math.Min(100, volume));
        lock (_speechGate)
        {
            if (IsDisposed || _synth == null)
                return;

            try
            {
                if (cancelCurrent)
                    _synth.SpeakAsyncCancelAll();
                _synth.Volume = selectedVolume;
                if (!string.IsNullOrWhiteSpace(selectedVoice))
                {
                    try { _synth.SelectVoice(selectedVoice); } catch { }
                }
                _synth.SpeakAsync(text);
            }
            catch (Exception ex)
            {
                if (_logError != null)
                    _logError("Speech output failed: " + ex.Message);
            }
        }
    }

    private void StopSpeech()
    {
        lock (_speechGate)
        {
            try
            {
                if (_synth != null)
                    _synth.SpeakAsyncCancelAll();
            }
            catch
            {
            }
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
            return ((int)duration.TotalHours).ToString(CultureInfo.InvariantCulture) + ":" +
                duration.Minutes.ToString("00", CultureInfo.InvariantCulture) + ":" +
                duration.Seconds.ToString("00", CultureInfo.InvariantCulture);

        return ((int)duration.TotalMinutes).ToString("00", CultureInfo.InvariantCulture) + ":" +
            duration.Seconds.ToString("00", CultureInfo.InvariantCulture);
    }

    public void Dispose()
    {
        lock (_stateGate)
        {
            if (_disposed)
                return;

            _disposed = true;
            _linesOpen = false;
            _currentCaller = null;
            _queuedCallers.Clear();
            _activeLine = 0;
            _rewardVersion++;
        }

        _callTimer.Dispose();
        StopSpeech();

        lock (_speechGate)
        {
            try
            {
                if (_synth != null)
                {
                    _synth.Dispose();
                    _synth = null;
                }
            }
            catch
            {
            }
        }

        if (!string.IsNullOrWhiteSpace(_rewardId))
        {
            Task.Run(() =>
            {
                _rewardSyncGate.Wait();
                try { _disableReward(_rewardId); }
                catch (Exception ex)
                {
                    if (_logError != null)
                        _logError("Unable to disable the Call In reward during shutdown: " + ex);
                }
                finally { _rewardSyncGate.Release(); }
            });
        }

        if (_window != null)
            _window.Dispose();
    }
}
