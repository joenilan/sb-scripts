using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows;
using System.Windows.Markup;

namespace Crntly.StreamerBot.UI.ScriptHost
{
    /// <summary>
    /// Reflection-friendly WPF host for Streamer.bot scripts.
    ///
    /// The script owns its application-specific XAML and behavior. This bridge only
    /// owns the WPF mechanics: STA dispatch, XAML loading, named-control access and
    /// event forwarding. Scripts can therefore use CRNTLY WPF UI without adding a
    /// compile-time reference to PresentationFramework or this assembly.
    /// </summary>
    public sealed class CrntlyScriptWindowBridge : IDisposable
    {
        private readonly Dictionary<string, BoundEvent> _boundEvents =
            new Dictionary<string, BoundEvent>(StringComparer.Ordinal);

        private Window _window;
        private string _loadedXaml;
        private bool _disposed;

        /// <summary>
        /// Receives the event key supplied to BindEvent. Event forwarding deliberately
        /// stays payload-free; scripts can query the named control after receiving it.
        /// </summary>
        public Action<string> EventRaised { get; set; }

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

        /// <summary>
        /// Loads script-owned XAML and shows its Window. Reusing the exact same XAML
        /// reuses the existing Window; supplying different XAML safely rebuilds it.
        /// </summary>
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

        /// <summary>
        /// Forwards a WPF event from a named control to EventRaised using eventKey.
        /// Any standard void-returning CLR event can be bound without the script
        /// referencing the event's WPF EventArgs type.
        /// </summary>
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
                var target = FindTarget(controlName);
                var property = target.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
                if (property == null || !property.CanWrite)
                    throw new MissingMemberException(target.GetType().FullName, propertyName);

                property.SetValue(target, Coerce(value, property.PropertyType), null);
            });
        }

        /// <summary>
        /// Convenience wrapper for ItemsControl.ItemsSource. The object can be a
        /// script-owned IList/collection; WPF binds to its public properties normally.
        /// </summary>
        public void SetItemsSource(string controlName, object itemsSource)
        {
            SetProperty(controlName, "ItemsSource", itemsSource);
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

        /// <summary>
        /// Calls a public parameterless method such as Focus(), SelectAll(), or
        /// Items.Refresh() without introducing WPF compile-time types in the script.
        /// </summary>
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

            _loadedXaml = xaml;
        }

        private object FindTarget(string controlName)
        {
            if (_window == null)
                throw new InvalidOperationException("No script window is loaded.");

            if (string.IsNullOrWhiteSpace(controlName) ||
                string.Equals(controlName, "$window", StringComparison.Ordinal))
                return _window;

            var target = _window.FindName(controlName);
            if (target == null)
                throw new MissingMemberException("The script window does not contain a named element '" + controlName + "'.");
            return target;
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

            var callback = typeof(CrntlyScriptWindowBridge).GetMethod(
                nameof(RaiseEvent), BindingFlags.Instance | BindingFlags.NonPublic);

            var body = Expression.Call(
                Expression.Constant(this),
                callback,
                Expression.Constant(eventKey));

            return Expression.Lambda(handlerType, body, parameters).Compile();
        }

        private void RaiseEvent(string eventKey)
        {
            var callback = EventRaised;
            if (callback != null)
                callback(eventKey);
        }

        private void UnbindAllEventsCore()
        {
            foreach (var binding in _boundEvents.Values)
            {
                try
                {
                    binding.EventInfo.RemoveEventHandler(binding.Target, binding.Handler);
                }
                catch
                {
                }
            }

            _boundEvents.Clear();
        }

        private void CloseWindowCore()
        {
            UnbindAllEventsCore();

            if (_window != null)
            {
                try
                {
                    _window.Close();
                }
                catch
                {
                }
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

            try
            {
                CrntlyUiHost.Invoke(CloseWindowCore);
            }
            catch
            {
            }
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
    }
}
