using System;
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
        private static readonly object Gate = new object();
        private static Dispatcher _dispatcher;
        private static Thread _thread;

        public static bool IsRunning
        {
            get
            {
                lock (Gate)
                    return _dispatcher != null && !_dispatcher.HasShutdownStarted;
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
    }
}
