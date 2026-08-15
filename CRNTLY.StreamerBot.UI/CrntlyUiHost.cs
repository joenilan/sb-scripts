using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows.Threading;

namespace Crntly.StreamerBot.UI
{
    /// <summary>
    /// Owns a dedicated STA dispatcher for CRNTLY WPF windows. Streamer.bot code can
    /// call into the UI without blocking the action queue with Window.ShowDialog().
    /// </summary>
    public static class CrntlyUiHost
    {
        private const int ErrorNotEnoughQuota = 1816;

        private static readonly object Gate = new object();
        private static Dispatcher _dispatcher;
        private static Thread _thread;
        private static int _recoverableExceptionCount;
        private static string _lastRecoverableException;

        public static bool IsRunning
        {
            get
            {
                lock (Gate)
                    return _dispatcher != null && !_dispatcher.HasShutdownStarted;
            }
        }

        /// <summary>
        /// Number of native WPF quota failures contained by the CRNTLY dispatcher.
        /// These failures are isolated so they cannot take down the Streamer.bot host.
        /// </summary>
        public static int RecoverableExceptionCount
        {
            get { return Volatile.Read(ref _recoverableExceptionCount); }
        }

        /// <summary>
        /// Most recent recoverable native WPF dispatcher failure, if one occurred.
        /// </summary>
        public static string LastRecoverableException
        {
            get
            {
                lock (Gate)
                    return _lastRecoverableException;
            }
        }

        public static void Invoke(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            EnsureStarted();
            _dispatcher.Invoke(action);
        }

        public static T Invoke<T>(Func<T> action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            EnsureStarted();
            return _dispatcher.Invoke(action);
        }

        public static void BeginInvoke(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            EnsureStarted();
            _dispatcher.BeginInvoke(action);
        }

        public static void Shutdown()
        {
            Dispatcher dispatcher;
            lock (Gate)
            {
                dispatcher = _dispatcher;
                _dispatcher = null;
                _thread = null;
            }

            if (dispatcher != null && !dispatcher.HasShutdownStarted)
                dispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
        }

        private static void EnsureStarted()
        {
            lock (Gate)
            {
                if (_dispatcher != null && !_dispatcher.HasShutdownStarted)
                    return;

                using (var ready = new ManualResetEventSlim(false))
                {
                    Exception startupError = null;
                    _thread = new Thread(() =>
                    {
                        try
                        {
                            _dispatcher = Dispatcher.CurrentDispatcher;
                            _dispatcher.UnhandledException += OnDispatcherUnhandledException;
                            SynchronizationContext.SetSynchronizationContext(
                                new DispatcherSynchronizationContext(_dispatcher));
                            ready.Set();
                            Dispatcher.Run();
                        }
                        catch (Exception ex)
                        {
                            startupError = ex;
                            ready.Set();
                        }
                    })
                    {
                        Name = "CRNTLY Streamer.bot UI",
                        IsBackground = true
                    };

                    _thread.SetApartmentState(ApartmentState.STA);
                    _thread.Start();
                    ready.Wait();

                    if (startupError != null)
                        throw new InvalidOperationException("Unable to start CRNTLY WPF dispatcher.", startupError);

                    if (_dispatcher == null)
                        throw new InvalidOperationException("Unable to start CRNTLY WPF dispatcher.");
                }
            }
        }

        private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            if (e == null || !IsRecoverableWpfQuotaException(e.Exception))
                return;

            var text = e.Exception == null ? "Unknown WPF quota failure." : e.Exception.ToString();
            Interlocked.Increment(ref _recoverableExceptionCount);
            lock (Gate)
                _lastRecoverableException = text;

            // CRNTLY is hosted inside Streamer.bot. A transient failure while WPF updates
            // this utility window must not become a fatal host-process exception. Only the
            // exact native quota/HwndTarget failure is contained here; every other
            // dispatcher exception retains normal WPF behavior and is allowed to surface.
            e.Handled = true;

            try
            {
                Trace.TraceWarning("[CRNTLY UI] Recovered WPF render/window quota failure: " + e.Exception.Message);
            }
            catch
            {
            }
        }

        private static bool IsRecoverableWpfQuotaException(Exception exception)
        {
            var win32 = exception as Win32Exception;
            if (win32 == null || win32.NativeErrorCode != ErrorNotEnoughQuota)
                return false;

            var stack = exception.StackTrace ?? string.Empty;
            return stack.IndexOf("System.Windows.Interop.HwndTarget", StringComparison.Ordinal) >= 0;
        }
    }
}
