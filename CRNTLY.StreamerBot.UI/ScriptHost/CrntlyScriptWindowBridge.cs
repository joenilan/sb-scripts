using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Expression = System.Linq.Expressions.Expression;
using System.Reflection;
using System.Windows;
using System.Windows.Markup;

namespace Crntly.StreamerBot.UI.ScriptHost
{
    /// <summary>
    /// Reflection-friendly WPF host for Streamer.bot scripts.
    ///
    /// The script owns its application-specific XAML and behavior. This bridge only
    /// owns the WPF mechanics: STA dispatch, XAML loading, named-control access,
    /// theme-resource lookup, collection refresh and event forwarding. Scripts can
    /// therefore use CRNTLY WPF UI without adding a compile-time reference to
    /// PresentationFramework or this assembly.
    /// </summary>
    public sealed class CrntlyScriptWindowBridge : IDisposable
    {
        private readonly Dictionary<string, BoundEvent> _boundEvents =
            new Dictionary<string, BoundEvent>(StringComparer.Ordinal);
        private readonly Dictionary<string, BoundRoutedEvent> _boundRoutedEvents =
            new Dictionary<string, BoundRoutedEvent>(StringComparer.Ordinal);

        private Window _window;
        private string _loadedXaml;
        private bool _disposed;
        private bool _allowWindowClose;

        /// <summary>
        /// Receives the event key supplied to BindEvent. Event forwarding deliberately
        /// stays payload-free; scripts can query the named control after receiving it.
        /// </summary>
        public Action<string> EventRaised { get; set; }

        /// <summary>
        /// Receives routed/template events together with the OriginalSource DataContext.
        /// This is useful for controls created inside DataTemplates, where there is no
        /// single named control for the script to query.
        /// </summary>
        public Action<string, object> RoutedEventRaised { get; set; }

        /// <summary>
        /// Stores the most recent optional routed-event binding failure. Template-level
        /// event wiring is intentionally best-effort so it cannot abort an otherwise
        /// usable script window during startup.
        /// </summary>
        public string LastBindingError { get; private set; }

        /// <summary>
        /// Script tools normally behave like Streamer.bot utility panels: closing the
        /// window hides it while keeping its runtime alive. Set false for normal WPF
        /// close semantics. Dispose/Close always performs a real close.
        /// </summary>
        public bool HideOnUserClose { get; set; } = true;

        public string AssemblyVersion
        {
            get { return typeof(CrntlyScriptWindowBridge).Assembly.GetName().Version.ToString(); }
        }

        public bool IsLoaded
        {
            get { return CrntlyUiHost.Invoke(() => _window != null); }
        }

        public bool IsVisible
        {
            get { return CrntlyUiHost.Invoke(() => _window != null && _window.IsVisible); }
        }

        public void Show(string xaml)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(xaml))
                throw new ArgumentException("Window XAML cannot be empty.", nameof(xaml));

            CrntlyUiHost.Invoke(() =>
            {
                EnsureWindow(xaml);
                if (!_window.IsVisible)
                    _window.Show();
                if (_window.WindowState == WindowState.Minimized)
                    _window.WindowState = WindowState.Normal;
                _window.Activate();
            });
        }

        public void Hide()
        {
            ThrowIfDisposed();
            CrntlyUiHost.Invoke(() =>
            {
                if (_window != null)
                    _window.Hide();
            });
        }

        public void Close()
        {
            ThrowIfDisposed();
            CrntlyUiHost.Invoke(CloseWindowCore);
        }

        public void BindEvent(string controlName, string eventName, string eventKey)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(eventName))
                throw new ArgumentException("Event name cannot be empty.", nameof(eventName));
            if (string.IsNullOrWhiteSpace(eventKey))
                throw new ArgumentException("Event key cannot be empty.", nameof(eventKey));

            CrntlyUiHost.Invoke(() =>
            {
                EnsureLoaded();
                var target = FindTarget(controlName);
                var eventInfo = target.GetType().GetEvent(eventName, BindingFlags.Instance | BindingFlags.Public);
                if (eventInfo == null)
                    throw new MissingMemberException(target.GetType().FullName, eventName);

                var bindingKey = MakeBindingKey(controlName, eventName, eventKey);
                BoundEvent existing;
                if (_boundEvents.TryGetValue(bindingKey, out existing))
                    return;

                var handler = CreateEventForwarder(eventInfo.EventHandlerType, eventKey);
                eventInfo.AddEventHandler(target, handler);
                _boundEvents[bindingKey] = new BoundEvent(target, eventInfo, handler);
            });
        }

        /// <summary>
        /// Binds a routed event at the Window root with handledEventsToo enabled. Pass
        /// an assembly-qualified WPF owner type plus its public static RoutedEvent field,
        /// for example ToggleButton + CheckedEvent. The callback receives eventKey and
        /// the OriginalSource DataContext, which is typically the script-owned row item.
        ///
        /// Routed/template event binding is best-effort. A failure is recorded in
        /// LastBindingError instead of aborting the entire script window initialization.
        /// </summary>
        public void BindRoutedEvent(string ownerTypeName, string routedEventFieldName, string eventKey)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(ownerTypeName))
                throw new ArgumentException("Owner type cannot be empty.", nameof(ownerTypeName));
            if (string.IsNullOrWhiteSpace(routedEventFieldName))
                throw new ArgumentException("Routed-event field cannot be empty.", nameof(routedEventFieldName));
            if (string.IsNullOrWhiteSpace(eventKey))
                throw new ArgumentException("Event key cannot be empty.", nameof(eventKey));

            CrntlyUiHost.Invoke(() =>
            {
                EnsureLoaded();
                var bindingKey = ownerTypeName + "\u001f" + routedEventFieldName + "\u001f" + eventKey;
                BoundRoutedEvent existing;
                if (_boundRoutedEvents.TryGetValue(bindingKey, out existing))
                    return;

                try
                {
                    var ownerType = ResolveType(ownerTypeName);
                    if (ownerType == null)
                        throw new TypeLoadException("Unable to resolve routed-event owner type '" + ownerTypeName + "'.");

                    var field = ownerType.GetField(
                        routedEventFieldName,
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                    var routedEvent = field == null ? null : field.GetValue(null) as RoutedEvent;
                    if (routedEvent == null)
                        throw new MissingMemberException(ownerType.FullName, routedEventFieldName);

                    RoutedEventHandler handler = (sender, args) => RaiseRoutedEvent(eventKey, args);
                    _window.AddHandler(routedEvent, handler, true);
                    _boundRoutedEvents[bindingKey] = new BoundRoutedEvent(routedEvent, handler);
                    LastBindingError = null;
                }
                catch (Exception ex)
                {
                    LastBindingError =
                        "Unable to bind routed event '" + routedEventFieldName +
                        "' for '" + ownerTypeName + "': " + ex.Message;
                }
            });
        }

        public void UnbindAllEvents()
        {
            ThrowIfDisposed();
            CrntlyUiHost.Invoke(UnbindAllEventsCore);
        }

        public object GetProperty(string controlName, string propertyName)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(propertyName))
                throw new ArgumentException("Property name cannot be empty.", nameof(propertyName));

            return CrntlyUiHost.Invoke(() =>
            {
                EnsureLoaded();
                var target = FindTarget(controlName);
                var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
                if (property == null || !property.CanRead)
                    throw new MissingMemberException(target.GetType().FullName, propertyName);
                return property.GetValue(target, null);
            });
        }

        public void SetProperty(string controlName, string propertyName, object value)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(propertyName))
                throw new ArgumentException("Property name cannot be empty.", nameof(propertyName));

            CrntlyUiHost.Invoke(() =>
            {
                EnsureLoaded();
                SetPropertyCore(FindTarget(controlName), propertyName, value);
            });
        }

        public void SetResourceProperty(string controlName, string propertyName, string resourceKey)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(resourceKey))
                throw new ArgumentException("Resource key cannot be empty.", nameof(resourceKey));

            CrntlyUiHost.Invoke(() =>
            {
                EnsureLoaded();
                var value = _window.TryFindResource(resourceKey);
                if (value == null)
                    throw new MissingMemberException("The script window cannot resolve resource '" + resourceKey + "'.");
                SetPropertyCore(FindTarget(controlName), propertyName, value);
            });
        }

        public void SetItemsSource(string controlName, object itemsSource)
        {
            SetProperty(controlName, "ItemsSource", itemsSource);
        }

        public void RefreshItems(string controlName)
        {
            ThrowIfDisposed();
            CrntlyUiHost.Invoke(() =>
            {
                EnsureLoaded();
                var target = FindTarget(controlName);
                var itemsProperty = target.GetType().GetProperty("Items", BindingFlags.Instance | BindingFlags.Public);
                var items = itemsProperty == null ? null : itemsProperty.GetValue(target, null);
                if (items == null)
                    throw new MissingMemberException(target.GetType().FullName, "Items");

                var refresh = items.GetType().GetMethod("Refresh", BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                if (refresh == null)
                    throw new MissingMethodException(items.GetType().FullName, "Refresh");
                refresh.Invoke(items, null);
            });
        }

        public object GetSelectedItem(string controlName)
        {
            return GetProperty(controlName, "SelectedItem");
        }

        public int GetSelectedIndex(string controlName)
        {
            var value = GetProperty(controlName, "SelectedIndex");
            return value == null ? -1 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        public void SetSelectedIndex(string controlName, int index)
        {
            SetProperty(controlName, "SelectedIndex", index);
        }

        public object InvokeMethod(string controlName, string methodName)
        {
            ThrowIfDisposed();
            if (string.IsNullOrWhiteSpace(methodName))
                throw new ArgumentException("Method name cannot be empty.", nameof(methodName));

            return CrntlyUiHost.Invoke(() =>
            {
                EnsureLoaded();
                var target = FindTarget(controlName);
                var method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
                if (method == null)
                    throw new MissingMethodException(target.GetType().FullName, methodName);
                return method.Invoke(target, null);
            });
        }

        private void EnsureWindow(string xaml)
        {
            if (_window != null && string.Equals(_loadedXaml, xaml, StringComparison.Ordinal))
                return;

            CloseWindowCore();
            object parsed;
            try
            {
                parsed = XamlReader.Parse(xaml);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Unable to parse script-owned CRNTLY window XAML.", ex);
            }

            _window = parsed as Window;
            if (_window == null)
                throw new InvalidOperationException("Script-owned CRNTLY XAML must have a Window as its root element.");

            _window.Closing += OnWindowClosing;
            _loadedXaml = xaml;
        }

        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_allowWindowClose || !HideOnUserClose)
                return;
            e.Cancel = true;
            if (_window != null)
                _window.Hide();
        }

        private object FindTarget(string controlName)
        {
            if (_window == null)
                throw new InvalidOperationException("No script window is loaded.");
            if (string.IsNullOrWhiteSpace(controlName) || string.Equals(controlName, "$window", StringComparison.Ordinal))
                return _window;

            var target = _window.FindName(controlName);
            if (target == null)
                throw new MissingMemberException("The script window does not contain a named element '" + controlName + "'.");
            return target;
        }

        private static void SetPropertyCore(object target, string propertyName, object value)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (string.IsNullOrWhiteSpace(propertyName))
                throw new ArgumentException("Property name cannot be empty.", nameof(propertyName));

            var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property == null || !property.CanWrite)
                throw new MissingMemberException(target.GetType().FullName, propertyName);
            property.SetValue(target, Coerce(value, property.PropertyType), null);
        }

        private static Type ResolveType(string typeName)
        {
            var type = Type.GetType(typeName, false);
            if (type != null)
                return type;

            var comma = typeName.IndexOf(',');
            var fullName = comma >= 0 ? typeName.Substring(0, comma).Trim() : typeName.Trim();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    type = assembly.GetType(fullName, false);
                    if (type != null)
                        return type;
                }
                catch
                {
                }
            }

            return null;
        }

        private Delegate CreateEventForwarder(Type handlerType, string eventKey)
        {
            if (handlerType == null)
                throw new InvalidOperationException("The requested event does not expose a handler type.");
            var invoke = handlerType.GetMethod("Invoke");
            if (invoke == null || invoke.ReturnType != typeof(void))
                throw new NotSupportedException("Only void-returning UI events can be forwarded.");

            var parameters = invoke.GetParameters()
                .Select((parameter, index) => Expression.Parameter(parameter.ParameterType, parameter.Name ?? "arg" + index))
                .ToArray();
            var callback = typeof(CrntlyScriptWindowBridge).GetMethod(nameof(RaiseEvent), BindingFlags.Instance | BindingFlags.NonPublic);
            var body = Expression.Call(Expression.Constant(this), callback, Expression.Constant(eventKey));
            return Expression.Lambda(handlerType, body, parameters).Compile();
        }

        private void RaiseEvent(string eventKey)
        {
            var callback = EventRaised;
            if (callback != null)
                callback(eventKey);
        }

        private void RaiseRoutedEvent(string eventKey, RoutedEventArgs args)
        {
            var callback = RoutedEventRaised;
            if (callback == null)
                return;

            var source = args == null ? null : args.OriginalSource as FrameworkElement;
            callback(eventKey, source == null ? null : source.DataContext);
        }

        private void UnbindAllEventsCore()
        {
            foreach (var binding in _boundEvents.Values)
            {
                try { binding.EventInfo.RemoveEventHandler(binding.Target, binding.Handler); } catch { }
            }
            _boundEvents.Clear();

            if (_window != null)
            {
                foreach (var binding in _boundRoutedEvents.Values)
                {
                    try { _window.RemoveHandler(binding.RoutedEvent, binding.Handler); } catch { }
                }
            }
            _boundRoutedEvents.Clear();
        }

        private void CloseWindowCore()
        {
            UnbindAllEventsCore();
            if (_window != null)
            {
                try
                {
                    _allowWindowClose = true;
                    _window.Closing -= OnWindowClosing;
                    _window.Close();
                }
                catch { }
                finally { _allowWindowClose = false; }
            }
            _window = null;
            _loadedXaml = null;
        }

        private void EnsureLoaded()
        {
            if (_window == null)
                throw new InvalidOperationException("No script window is loaded. Call Show(xaml) first.");
        }

        private static string MakeBindingKey(string controlName, string eventName, string eventKey)
        {
            return (controlName ?? "$window") + "\u001f" + eventName + "\u001f" + eventKey;
        }

        private static object Coerce(object value, Type targetType)
        {
            if (value == null)
            {
                if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
                    return Activator.CreateInstance(targetType);
                return null;
            }

            var effectiveType = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (effectiveType.IsInstanceOfType(value))
                return value;
            if (effectiveType.IsEnum)
            {
                if (value is string)
                    return Enum.Parse(effectiveType, (string)value, true);
                return Enum.ToObject(effectiveType, value);
            }
            return Convert.ChangeType(value, effectiveType, CultureInfo.InvariantCulture);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CrntlyScriptWindowBridge));
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            EventRaised = null;
            RoutedEventRaised = null;
            try { CrntlyUiHost.Invoke(CloseWindowCore); } catch { }
        }

        private sealed class BoundEvent
        {
            public BoundEvent(object target, EventInfo eventInfo, Delegate handler)
            {
                Target = target;
                EventInfo = eventInfo;
                Handler = handler;
            }
            public object Target { get; }
            public EventInfo EventInfo { get; }
            public Delegate Handler { get; }
        }

        private sealed class BoundRoutedEvent
        {
            public BoundRoutedEvent(RoutedEvent routedEvent, RoutedEventHandler handler)
            {
                RoutedEvent = routedEvent;
                Handler = handler;
            }
            public RoutedEvent RoutedEvent { get; }
            public RoutedEventHandler Handler { get; }
        }
    }
}
