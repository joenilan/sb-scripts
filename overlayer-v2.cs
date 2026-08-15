using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Crntly.StreamerBot.UI.Overlayer;

// CRNTLY Overlayer v2 preview
// Streamer.bot references:
//   CRNTLY.StreamerBot.UI.dll
//   Newtonsoft.Json.dll
//
// This keeps the original Overlayer goal: many web/local overlays rendered through
// ONE OBS Browser Source at http://localhost:42069/.
public class CPHInline
{
    private OverlayerRuntime _runtime;

    public void Init()
    {
        _runtime = new OverlayerRuntime(
            message => CPH.LogInfo("[CRNTLY Overlayer] " + message),
            message => CPH.LogError("[CRNTLY Overlayer] " + message));
    }

    public bool Execute()
    {
        if (_runtime == null)
        {
            _runtime = new OverlayerRuntime(
                message => CPH.LogInfo("[CRNTLY Overlayer] " + message),
                message => CPH.LogError("[CRNTLY Overlayer] " + message));
        }

        _runtime.Show();
        return true;
    }

    public void Dispose()
    {
        if (_runtime != null)
        {
            _runtime.Dispose();
            _runtime = null;
        }
    }
}

public sealed class OverlayerRuntime : IDisposable
{
    private readonly object _gate = new object();
    private readonly Action<string> _log;
    private readonly Action<string> _logError;
    private readonly OverlayerUi _ui;
    private readonly OverlayerConfigStore _configStore;
    private readonly CompositeOverlayServer _server;
    private List<OverlayItem> _items;
    private bool _disposed;

    public OverlayerRuntime(Action<string> log, Action<string> logError)
    {
        _log = log ?? delegate { };
        _logError = logError ?? delegate { };
        _configStore = new OverlayerConfigStore();
        _items = _configStore.Load(_logError);
        _server = new CompositeOverlayServer(_logError);
        _server.UpdateItems(Snapshot());

        _ui = new OverlayerUi();
        _ui.StartServerRequested += OnStartServerRequested;
        _ui.StopServerRequested += OnStopServerRequested;
        _ui.OverlayChanged += OnOverlayChanged;
        _ui.OverlayDeleted += OnOverlayDeleted;
        _ui.OverlayOrderChanged += OnOverlayOrderChanged;
    }

    public void Show()
    {
        ThrowIfDisposed();
        _ui.Show(Snapshot(), _server.IsRunning, _server.Url);
    }

    private void OnStartServerRequested(object sender, EventArgs e)
    {
        try
        {
            _server.UpdateItems(Snapshot());
            _server.Start();
            _ui.SetServerState(true, _server.Url);
            _log("Server started at " + _server.Url);
        }
        catch (Exception ex)
        {
            _ui.SetServerState(false, _server.Url);
            _logError("Unable to start server: " + ex.Message);
        }
    }

    private void OnStopServerRequested(object sender, EventArgs e)
    {
        _server.Stop();
        _ui.SetServerState(false, _server.Url);
        _log("Server stopped.");
    }

    private void OnOverlayChanged(object sender, OverlayItemEventArgs e)
    {
        lock (_gate)
        {
            var existing = _items.FirstOrDefault(x => x.Id == e.Item.Id);
            if (existing == null)
                _items.Add(e.Item.Clone());
            else
                CopyItem(e.Item, existing);

            PersistAndRefreshLocked();
        }
    }

    private void OnOverlayDeleted(object sender, OverlayItemEventArgs e)
    {
        lock (_gate)
        {
            _items.RemoveAll(x => x.Id == e.Item.Id);
            PersistAndRefreshLocked();
        }
    }

    private void OnOverlayOrderChanged(object sender, OverlayOrderEventArgs e)
    {
        lock (_gate)
        {
            var byId = _items.ToDictionary(x => x.Id, x => x);
            var ordered = new List<OverlayItem>();

            foreach (var requested in e.Items)
            {
                OverlayItem current;
                if (byId.TryGetValue(requested.Id, out current))
                {
                    ordered.Add(current);
                    byId.Remove(requested.Id);
                }
            }

            ordered.AddRange(byId.Values);
            _items = ordered;
            PersistAndRefreshLocked();
        }
    }

    private void PersistAndRefreshLocked()
    {
        try
        {
            _configStore.Save(_items);
        }
        catch (Exception ex)
        {
            _logError("Unable to save overlay configuration: " + ex.Message);
        }

        _server.UpdateItems(_items.Select(x => x.Clone()).ToList());
    }

    private List<OverlayItem> Snapshot()
    {
        lock (_gate)
            return _items.Select(x => x.Clone()).ToList();
    }

    private static void CopyItem(OverlayItem source, OverlayItem target)
    {
        target.Name = source.Name;
        target.Url = source.Url;
        target.Width = source.Width;
        target.Height = source.Height;
        target.Top = source.Top;
        target.Left = source.Left;
        target.Enabled = source.Enabled;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(OverlayerRuntime));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _ui.StartServerRequested -= OnStartServerRequested;
        _ui.StopServerRequested -= OnStopServerRequested;
        _ui.OverlayChanged -= OnOverlayChanged;
        _ui.OverlayDeleted -= OnOverlayDeleted;
        _ui.OverlayOrderChanged -= OnOverlayOrderChanged;
        _ui.Dispose();
        _server.Dispose();
    }
}

public sealed class OverlayerConfigStore
{
    private readonly string _path;

    public OverlayerConfigStore()
    {
        var folder = Path.Combine(Environment.CurrentDirectory, "overlayer");
        _path = Path.Combine(folder, "listview.json");
    }

    public List<OverlayItem> Load(Action<string> logError)
    {
        var result = new List<OrderedOverlay>();

        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            if (!File.Exists(_path))
            {
                File.WriteAllText(_path, "{}", Encoding.UTF8);
                return new List<OverlayItem>();
            }

            var json = File.ReadAllText(_path, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
                return new List<OverlayItem>();

            var data = JsonConvert.DeserializeObject<LegacyListViewData>(json) ?? new LegacyListViewData();
            Append(result, data.Enabled, true, 0);
            Append(result, data.Disabled, false, result.Count);

            return result
                .OrderBy(x => x.Order)
                .ThenBy(x => x.FallbackOrder)
                .Select(x => x.Item)
                .ToList();
        }
        catch (Exception ex)
        {
            if (logError != null)
                logError("Unable to load " + _path + ": " + ex.Message);
            return new List<OverlayItem>();
        }
    }

    public void Save(IList<OverlayItem> items)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var data = new LegacyListViewData
        {
            Enabled = new List<Dictionary<string, string>>(),
            Disabled = new List<Dictionary<string, string>>()
        };

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var row = new Dictionary<string, string>
            {
                { "Id", item.Id },
                { "Name", item.Name ?? string.Empty },
                { "URL", item.Url ?? string.Empty },
                { "Height", item.Height ?? "100%" },
                { "Width", item.Width ?? "100%" },
                { "Top", item.Top ?? "0px" },
                { "Left", item.Left ?? "0px" },
                { "Order", i.ToString() }
            };

            if (item.Enabled)
                data.Enabled.Add(row);
            else
                data.Disabled.Add(row);
        }

        File.WriteAllText(_path, JsonConvert.SerializeObject(data, Formatting.Indented), Encoding.UTF8);
    }

    private static void Append(List<OrderedOverlay> output, List<Dictionary<string, string>> rows,
        bool enabled, int fallbackOffset)
    {
        if (rows == null)
            return;

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i] ?? new Dictionary<string, string>();
            int order;
            if (!int.TryParse(Get(row, "Order", null), out order))
                order = int.MaxValue;

            output.Add(new OrderedOverlay
            {
                Order = order,
                FallbackOrder = fallbackOffset + i,
                Item = new OverlayItem
                {
                    Id = Get(row, "Id", Guid.NewGuid().ToString("N")),
                    Name = Get(row, "Name", "Overlay"),
                    Url = Get(row, "URL", string.Empty),
                    Height = Get(row, "Height", "100%"),
                    Width = Get(row, "Width", "100%"),
                    Top = Get(row, "Top", "0px"),
                    Left = Get(row, "Left", "0px"),
                    Enabled = enabled
                }
            });
        }
    }

    private static string Get(Dictionary<string, string> row, string key, string fallback)
    {
        string value;
        return row.TryGetValue(key, out value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
    }

    private sealed class OrderedOverlay
    {
        public int Order { get; set; }
        public int FallbackOrder { get; set; }
        public OverlayItem Item { get; set; }
    }

    public sealed class LegacyListViewData
    {
        public List<Dictionary<string, string>> Enabled { get; set; }
        public List<Dictionary<string, string>> Disabled { get; set; }
    }
}

public sealed class CompositeOverlayServer : IDisposable
{
    private const string RootUrl = "http://localhost:42069/";
    private const string LocalUrl = "http://localhost:42070/";
    private const string LocalPrefix = "/local/";

    private readonly object _gate = new object();
    private readonly Action<string> _logError;
    private HttpListener _listener;
    private HttpListener _localListener;
    private string _stateJson = "[]";
    private Dictionary<string, string> _localRoots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private static readonly string ShellHtml = @"<!doctype html>
<html>
<head>
<meta charset=""utf-8"">
<meta name=""viewport"" content=""width=device-width,initial-scale=1"">
<title>CRNTLY Overlayer</title>
<style>
html,body,#crntly-root{margin:0;width:100%;height:100%;overflow:hidden;background:transparent;}
#crntly-root{position:relative;}
.crntly-overlay{position:absolute;border:0;margin:0;padding:0;overflow:hidden;background:transparent;}
</style>
</head>
<body>
<div id=""crntly-root""></div>
<script>
(() => {
  const root = document.getElementById('crntly-root');
  let lastState = '';

  function applyState(text) {
    if (text === lastState) return;
    lastState = text;

    let items;
    try { items = JSON.parse(text); } catch (_) { return; }
    const keep = new Set();

    for (const item of items) {
      const domId = 'ov-' + item.id;
      keep.add(domId);
      let frame = document.getElementById(domId);
      if (!frame) {
        frame = document.createElement('iframe');
        frame.id = domId;
        frame.className = 'crntly-overlay';
        frame.scrolling = 'no';
        frame.allow = 'autoplay';
        root.appendChild(frame);
      }

      if (frame.dataset.src !== item.src) {
        frame.dataset.src = item.src;
        frame.src = item.src;
      }

      frame.style.width = item.width;
      frame.style.height = item.height;
      frame.style.top = item.top;
      frame.style.left = item.left;

      // appendChild only when state changed; this preserves configured z-order.
      root.appendChild(frame);
    }

    for (const frame of Array.from(root.children)) {
      if (!keep.has(frame.id)) frame.remove();
    }
  }

  async function sync() {
    try {
      const response = await fetch('/state', { cache: 'no-store' });
      if (response.ok) applyState(await response.text());
    } catch (_) { }
  }

  sync();
  setInterval(sync, 1500);
})();
</script>
</body>
</html>";

    public CompositeOverlayServer(Action<string> logError)
    {
        _logError = logError ?? delegate { };
    }

    public string Url { get { return RootUrl; } }
    public bool IsRunning { get; private set; }

    public void UpdateItems(IList<OverlayItem> items)
    {
        var state = new List<Dictionary<string, string>>();
        var localRoots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items ?? new List<OverlayItem>())
        {
            if (!item.Enabled || string.IsNullOrWhiteSpace(item.Url))
                continue;

            string source = item.Url.Trim();
            Uri uri;
            if (!Uri.TryCreate(source, UriKind.Absolute, out uri))
                continue;

            if (uri.IsFile)
            {
                var localPath = Path.GetFullPath(uri.LocalPath);
                var directory = Path.GetDirectoryName(localPath);
                var fileName = Path.GetFileName(localPath);
                if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
                    continue;

                localRoots[item.Id] = directory;
                source = LocalUrl + "local/" + Uri.EscapeDataString(item.Id) + "/" + Uri.EscapeDataString(fileName);
            }

            state.Add(new Dictionary<string, string>
            {
                { "id", item.Id },
                { "src", source },
                { "width", SafeCss(item.Width, "100%") },
                { "height", SafeCss(item.Height, "100%") },
                { "top", SafeCss(item.Top, "0px") },
                { "left", SafeCss(item.Left, "0px") }
            });
        }

        lock (_gate)
        {
            _stateJson = JsonConvert.SerializeObject(state, Formatting.None);
            _localRoots = localRoots;
        }
    }

    public void Start()
    {
        if (IsRunning)
            return;

        HttpListener main = null;
        HttpListener local = null;
        try
        {
            main = new HttpListener();
            main.Prefixes.Add(RootUrl);
            main.Start();

            local = new HttpListener();
            local.Prefixes.Add(LocalUrl);
            local.Start();

            _listener = main;
            _localListener = local;
            IsRunning = true;

            _listener.BeginGetContext(OnMainContext, _listener);
            _localListener.BeginGetContext(OnLocalContext, _localListener);
        }
        catch
        {
            try { if (main != null) main.Close(); } catch { }
            try { if (local != null) local.Close(); } catch { }
            _listener = null;
            _localListener = null;
            IsRunning = false;
            throw;
        }
    }

    public void Stop()
    {
        IsRunning = false;
        var main = _listener;
        var local = _localListener;
        _listener = null;
        _localListener = null;

        try { if (main != null) main.Close(); } catch { }
        try { if (local != null) local.Close(); } catch { }
    }

    private void OnMainContext(IAsyncResult ar)
    {
        var listener = ar.AsyncState as HttpListener;
        if (listener == null)
            return;

        HttpListenerContext context;
        try
        {
            context = listener.EndGetContext(ar);
        }
        catch
        {
            return;
        }

        Rearm(listener, OnMainContext);
        try
        {
            HandleMain(context);
        }
        catch (Exception ex)
        {
            _logError("HTTP request failed: " + ex.Message);
            SafeClose(context.Response);
        }
    }

    private void OnLocalContext(IAsyncResult ar)
    {
        var listener = ar.AsyncState as HttpListener;
        if (listener == null)
            return;

        HttpListenerContext context;
        try
        {
            context = listener.EndGetContext(ar);
        }
        catch
        {
            return;
        }

        Rearm(listener, OnLocalContext);
        try
        {
            HandleLocal(context);
        }
        catch (Exception ex)
        {
            _logError("Local asset request failed: " + ex.Message);
            SafeClose(context.Response);
        }
    }

    private static void Rearm(HttpListener listener, AsyncCallback callback)
    {
        try
        {
            if (listener.IsListening)
                listener.BeginGetContext(callback, listener);
        }
        catch { }
    }

    private void HandleMain(HttpListenerContext context)
    {
        var path = context.Request.Url.AbsolutePath;
        if (path == "/" || string.IsNullOrEmpty(path))
        {
            WriteText(context, ShellHtml, "text/html; charset=utf-8", HttpStatusCode.OK);
            return;
        }

        if (string.Equals(path, "/state", StringComparison.OrdinalIgnoreCase))
        {
            string json;
            lock (_gate)
                json = _stateJson;
            WriteText(context, json, "application/json; charset=utf-8", HttpStatusCode.OK);
            return;
        }

        if (string.Equals(path, "/health", StringComparison.OrdinalIgnoreCase))
        {
            WriteText(context, "ok", "text/plain; charset=utf-8", HttpStatusCode.OK);
            return;
        }

        WriteText(context, "Not found", "text/plain; charset=utf-8", HttpStatusCode.NotFound);
    }

    private void HandleLocal(HttpListenerContext context)
    {
        var path = context.Request.Url.AbsolutePath;
        if (!path.StartsWith(LocalPrefix, StringComparison.OrdinalIgnoreCase))
        {
            WriteText(context, "Not found", "text/plain; charset=utf-8", HttpStatusCode.NotFound);
            return;
        }

        var remainder = path.Substring(LocalPrefix.Length);
        var separator = remainder.IndexOf('/');
        if (separator <= 0 || separator == remainder.Length - 1)
        {
            WriteText(context, "Not found", "text/plain; charset=utf-8", HttpStatusCode.NotFound);
            return;
        }

        var id = Uri.UnescapeDataString(remainder.Substring(0, separator));
        var relative = Uri.UnescapeDataString(remainder.Substring(separator + 1)).Replace('/', Path.DirectorySeparatorChar);

        string root;
        lock (_gate)
        {
            if (!_localRoots.TryGetValue(id, out root))
            {
                WriteText(context, "Not found", "text/plain; charset=utf-8", HttpStatusCode.NotFound);
                return;
            }
        }

        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                             + Path.DirectorySeparatorChar;
        var candidate = Path.GetFullPath(Path.Combine(normalizedRoot, relative));

        if (!candidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(candidate))
        {
            WriteText(context, "Not found", "text/plain; charset=utf-8", HttpStatusCode.NotFound);
            return;
        }

        var response = context.Response;
        try
        {
            using (var stream = new FileStream(candidate, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                response.StatusCode = (int)HttpStatusCode.OK;
                response.ContentType = GetMimeType(candidate);
                response.ContentLength64 = stream.Length;
                response.Headers["Cache-Control"] = "no-cache";
                response.Headers["Access-Control-Allow-Origin"] = "*";

                if (!string.Equals(context.Request.HttpMethod, "HEAD", StringComparison.OrdinalIgnoreCase))
                {
                    var buffer = new byte[64 * 1024];
                    int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                        response.OutputStream.Write(buffer, 0, read);
                }
            }
        }
        finally
        {
            SafeClose(response);
        }
    }

    private static void WriteText(HttpListenerContext context, string text, string contentType, HttpStatusCode status)
    {
        var buffer = Encoding.UTF8.GetBytes(text ?? string.Empty);
        var response = context.Response;
        try
        {
            response.StatusCode = (int)status;
            response.ContentType = contentType;
            response.ContentLength64 = buffer.Length;
            response.Headers["Cache-Control"] = "no-store";
            if (!string.Equals(context.Request.HttpMethod, "HEAD", StringComparison.OrdinalIgnoreCase))
                response.OutputStream.Write(buffer, 0, buffer.Length);
        }
        finally
        {
            SafeClose(response);
        }
    }

    private static void SafeClose(HttpListenerResponse response)
    {
        if (response == null)
            return;
        try { response.OutputStream.Close(); } catch { }
        try { response.Close(); } catch { }
    }

    private static string SafeCss(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string GetMimeType(string path)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".html":
            case ".htm": return "text/html; charset=utf-8";
            case ".css": return "text/css; charset=utf-8";
            case ".js": return "application/javascript; charset=utf-8";
            case ".json": return "application/json; charset=utf-8";
            case ".svg": return "image/svg+xml";
            case ".png": return "image/png";
            case ".jpg":
            case ".jpeg": return "image/jpeg";
            case ".gif": return "image/gif";
            case ".webp": return "image/webp";
            case ".ico": return "image/x-icon";
            case ".woff": return "font/woff";
            case ".woff2": return "font/woff2";
            case ".ttf": return "font/ttf";
            case ".otf": return "font/otf";
            case ".mp3": return "audio/mpeg";
            case ".wav": return "audio/wav";
            case ".ogg": return "audio/ogg";
            case ".mp4": return "video/mp4";
            case ".webm": return "video/webm";
            default: return "application/octet-stream";
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
