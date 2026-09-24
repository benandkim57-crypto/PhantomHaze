using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace PhantomHaze;

public class MainForm : Form
{
    private readonly Label _contentLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Button _toggleButton = new();
    private readonly Button _webViewToggleButton = new();
    private readonly Panel _addressBarPanel = new();
    private readonly TextBox _addressBarTextBox = new();
    private readonly Button _backButton = new();
    private readonly Button _goButton = new();
    private readonly Button _refreshButton = new();
    private readonly Button _historyButton = new();
    private readonly Label _zoomLabel = new();
    private readonly TrackBar _zoomSlider = new();
    private readonly Panel _contentHostPanel = new();
    private readonly Panel _historyPanel = new();
    private readonly ListBox _historyListBox = new();
    private readonly Button _historyClearButton = new();
    private readonly TextBox _logBox = new();
    private readonly WebView2 _webView = new();
    private readonly string _logFilePath;
    private readonly string _webSessionPath;
    private readonly List<string> _sessionHistory = new();

    private bool _protectionEnabled;
    private bool _webViewInitialized;
    private bool _webViewModeEnabled;

    private const string DefaultHomeUrl = "https://www.google.com";

    private const string DisableSiteDataScript = @"(() => {
try {
  Object.defineProperty(document, 'cookie', {
    configurable: false,
    enumerable: true,
    get: () => '',
    set: () => true
  });
} catch {}
    try {
        if (window.Storage && Storage.prototype) {
            Storage.prototype.setItem = function () { throw new Error('Storage disabled by PhantomHaze.'); };
            Storage.prototype.getItem = function () { return null; };
            Storage.prototype.removeItem = function () { };
            Storage.prototype.clear = function () { };
        }
    } catch {}
    try { if (window.localStorage) { window.localStorage.clear(); } } catch {}
    try { if (window.sessionStorage) { window.sessionStorage.clear(); } } catch {}
try {
  if (window.indexedDB) {
    window.indexedDB.open = function () { throw new Error('IndexedDB disabled by PhantomHaze.'); };
    window.indexedDB.deleteDatabase = function () { throw new Error('IndexedDB disabled by PhantomHaze.'); };
  }
} catch {}
    try {
        if (window.caches) {
            window.caches.open = function () { return Promise.reject(new Error('Cache storage disabled by PhantomHaze.')); };
            window.caches.match = function () { return Promise.resolve(undefined); };
            window.caches.keys = function () { return Promise.resolve([]); };
            window.caches.delete = function () { return Promise.resolve(false); };
        }
    } catch {}
})();";

    public MainForm()
    {
        Text = "PhantomHaze v1.2.1 Beta";
        Width = 980;
        Height = 620;
        StartPosition = FormStartPosition.CenterScreen;
        _logFilePath = InitializeLogFilePath();
        _webSessionPath = InitializeWebSessionPath();

        BuildUi();

        // Protection is applied on load, not behind an opt-in step - this is
        // what proves the "always enabled" requirement rather than something
        // that only activates after detecting a recording tool.
        Load += (_, _) =>
        {
            Log($"Log file: {_logFilePath}");
            Log($"Web session path: {_webSessionPath}");
            LogTrueOsBuild();
            ApplyProtection(enable: true);
        };

        FormClosing += (_, _) => CleanupWebSession();
    }

    private static string InitializeLogFilePath()
    {
        string logDir = Path.Combine(Path.GetTempPath(), "PhantomHaze", "logs");
        Directory.CreateDirectory(logDir);
        return Path.Combine(logDir, $"phantomhaze-{DateTime.UtcNow:yyyyMMdd}.log");
    }

    private static string InitializeWebSessionPath()
    {
        string sessionPath = Path.Combine(
            Path.GetTempPath(),
            "PhantomHaze",
            "webview-session",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(sessionPath);
        return sessionPath;
    }

    private void LogTrueOsBuild()
    {
        int build = NativeMethods.GetTrueOsBuildNumber();
        if (build < 0)
        {
            Log("Could not read the true OS build number via RtlGetVersion.");
            return;
        }

        bool meetsMinimum = build >= NativeMethods.MinimumBuildForExcludeFromCapture;
        Log($"Detected Windows build: {build} " +
            $"(minimum for WDA_EXCLUDEFROMCAPTURE is {NativeMethods.MinimumBuildForExcludeFromCapture}) " +
            $"- {(meetsMinimum ? "meets minimum" : "BELOW MINIMUM, the API call below is expected to fail")}.");
    }

    private void BuildUi()
    {
        _contentLabel.Text = "PhantomHaze Beta v1.2.1";
        _contentLabel.Font = new Font("Segoe UI", 16, FontStyle.Bold);
        _contentLabel.ForeColor = Color.Firebrick;
        _contentLabel.TextAlign = ContentAlignment.MiddleCenter;
        _contentLabel.Dock = DockStyle.Top;
        _contentLabel.Height = 80;

        _statusLabel.Dock = DockStyle.Top;
        _statusLabel.Height = 34;
        _statusLabel.TextAlign = ContentAlignment.MiddleCenter;
        _statusLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);

        _toggleButton.Text = "Disable Protection (testing only)";
        _toggleButton.Dock = DockStyle.Top;
        _toggleButton.Height = 36;
        _toggleButton.Click += (_, _) => ApplyProtection(enable: !_protectionEnabled);

        BuildAddressBar();
        BuildHistoryPanel();

        _webViewToggleButton.Text = "Web View";
        _webViewToggleButton.Dock = DockStyle.Bottom;
        _webViewToggleButton.Height = 38;
        _webViewToggleButton.Click += async (_, _) => await ToggleWebViewModeAsync();
        UpdateWebViewToggleVisual();

        _logBox.Multiline = true;
        _logBox.ReadOnly = true;
        _logBox.ScrollBars = ScrollBars.Vertical;
        _logBox.Dock = DockStyle.Fill;
        _logBox.Font = new Font("Consolas", 9);

        _webView.Dock = DockStyle.Fill;
        _webView.Visible = false;

        _contentHostPanel.Dock = DockStyle.Fill;
        _contentHostPanel.Controls.Add(_webView);
        _contentHostPanel.Controls.Add(_logBox);
        _contentHostPanel.Controls.Add(_historyPanel);

        // Docked children must be added in reverse visual order.
        Controls.Add(_contentHostPanel);
        Controls.Add(_addressBarPanel);
        Controls.Add(_toggleButton);
        Controls.Add(_statusLabel);
        Controls.Add(_contentLabel);
        Controls.Add(_webViewToggleButton);
    }

    private void BuildAddressBar()
    {
        _addressBarPanel.Dock = DockStyle.Top;
        _addressBarPanel.Height = 50;
        _addressBarPanel.Padding = new Padding(8, 8, 8, 8);
        _addressBarPanel.Visible = false;

        var stripLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 7,
            RowCount = 1,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };

        stripLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        stripLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64f));
        stripLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56f));
        stripLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80f));
        stripLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80f));
        stripLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82f));
        stripLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185f));

        _addressBarTextBox.Dock = DockStyle.Fill;
        _addressBarTextBox.PlaceholderText = DefaultHomeUrl;
        _addressBarTextBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                NavigateToAddressBarUrl();
            }
        };

        _backButton.Text = "Back";
        _backButton.Dock = DockStyle.Fill;
        _backButton.Enabled = false;
        _backButton.Click += (_, _) => GoBack();

        _goButton.Text = "Go";
        _goButton.Dock = DockStyle.Fill;
        _goButton.Click += (_, _) => NavigateToAddressBarUrl();

        _refreshButton.Text = "Refresh";
        _refreshButton.Dock = DockStyle.Fill;
        _refreshButton.Click += (_, _) => RefreshWebView();

        _historyButton.Text = "History";
        _historyButton.Dock = DockStyle.Fill;
        _historyButton.Click += (_, _) => ToggleHistoryPanel();

        _zoomLabel.Text = "Zoom 100%";
        _zoomLabel.Dock = DockStyle.Fill;
        _zoomLabel.TextAlign = ContentAlignment.MiddleCenter;

        _zoomSlider.Minimum = 50;
        _zoomSlider.Maximum = 200;
        _zoomSlider.Value = 100;
        _zoomSlider.TickFrequency = 10;
        _zoomSlider.AutoSize = false;
        _zoomSlider.Dock = DockStyle.Fill;
        _zoomSlider.Scroll += (_, _) => ApplyZoom();

        stripLayout.Controls.Add(_addressBarTextBox, 0, 0);
        stripLayout.Controls.Add(_backButton, 1, 0);
        stripLayout.Controls.Add(_goButton, 2, 0);
        stripLayout.Controls.Add(_refreshButton, 3, 0);
        stripLayout.Controls.Add(_historyButton, 4, 0);
        stripLayout.Controls.Add(_zoomLabel, 5, 0);
        stripLayout.Controls.Add(_zoomSlider, 6, 0);

        _addressBarPanel.Controls.Add(stripLayout);
    }

    private void BuildHistoryPanel()
    {
        _historyPanel.Dock = DockStyle.Right;
        _historyPanel.Width = 360;
        _historyPanel.Padding = new Padding(8);
        _historyPanel.Visible = false;
        _historyPanel.BackColor = Color.FromArgb(245, 248, 252);

        var historyTitle = new Label
        {
            Text = "Session History",
            Dock = DockStyle.Top,
            Height = 30,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
        };

        _historyClearButton.Text = "Clear";
        _historyClearButton.Dock = DockStyle.Bottom;
        _historyClearButton.Height = 32;
        _historyClearButton.Click += (_, _) =>
        {
            _sessionHistory.Clear();
            RefreshHistoryPanelItems();
            Log("Web history cleared manually.");
        };

        _historyListBox.Dock = DockStyle.Fill;
        _historyListBox.Font = new Font("Segoe UI", 9);
        _historyListBox.DoubleClick += (_, _) => NavigateFromHistorySelection();
        _historyListBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                NavigateFromHistorySelection();
            }
        };

        _historyPanel.Controls.Add(_historyListBox);
        _historyPanel.Controls.Add(_historyClearButton);
        _historyPanel.Controls.Add(historyTitle);
    }

    private async Task ToggleWebViewModeAsync()
    {
        bool enableWebView = !_webViewModeEnabled;
        if (enableWebView)
        {
            await EnsureWebViewInitializedAsync();
            if (!_webViewInitialized)
            {
                return;
            }

            _webViewModeEnabled = true;
            _addressBarPanel.Visible = true;
            _webView.Visible = true;
            _logBox.Visible = false;
            _webView.BringToFront();
            UpdateWebViewToggleVisual();

            if (string.IsNullOrWhiteSpace(_addressBarTextBox.Text))
            {
                _addressBarTextBox.Text = DefaultHomeUrl;
            }

            EnsureDefaultHomePageLoaded();

            Log("Web View mode enabled.");
            return;
        }

        _webViewModeEnabled = false;
        _historyPanel.Visible = false;
        _historyButton.BackColor = SystemColors.Control;
        _addressBarPanel.Visible = false;
        _webView.Visible = false;
        _logBox.Visible = true;
        _logBox.BringToFront();
        UpdateWebViewToggleVisual();
        Log("Web View mode disabled; log panel restored.");
    }

    private async Task EnsureWebViewInitializedAsync()
    {
        if (_webViewInitialized)
        {
            return;
        }

        try
        {
            var options = new CoreWebView2EnvironmentOptions("--inprivate");
            CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: _webSessionPath,
                options: options);

            await _webView.EnsureCoreWebView2Async(environment);

            CoreWebView2 core = _webView.CoreWebView2;
            core.Settings.IsScriptEnabled = true;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.IsStatusBarEnabled = true;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;

            core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            core.WebResourceRequested += OnWebResourceRequested;
            core.NavigationStarting += OnWebNavigationStarting;
            core.NavigationCompleted += OnWebNavigationCompleted;
            core.SourceChanged += OnWebSourceChanged;
            core.DocumentTitleChanged += OnWebDocumentTitleChanged;
            core.HistoryChanged += OnWebHistoryChanged;
            core.NewWindowRequested += OnWebNewWindowRequested;

            await core.AddScriptToExecuteOnDocumentCreatedAsync(DisableSiteDataScript);
            await ClearWebSiteDataAsync(core);

            _webView.ZoomFactor = _zoomSlider.Value / 100.0;
            _webViewInitialized = true;
            UpdateNavigationButtons();
            Log("Web View initialized. Cookies and site data are disabled for this session.");
        }
        catch (Exception ex)
        {
            Log($"Web View initialization failed: {ex.Message}");
            MessageBox.Show(
                "Web View could not be initialized. Install Microsoft Edge WebView2 Runtime on the Windows host.",
                "PhantomHaze",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private static async Task ClearWebSiteDataAsync(CoreWebView2 core)
    {
        core.CookieManager.DeleteAllCookies();
        await core.Profile.ClearBrowsingDataAsync();
    }

    private void NavigateToAddressBarUrl()
    {
        if (!_webViewInitialized || _webView.CoreWebView2 is null)
        {
            Log("Web navigation requested before Web View was initialized.");
            return;
        }

        string rawUrl = _addressBarTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return;
        }

        if (!rawUrl.Contains("://", StringComparison.Ordinal))
        {
            rawUrl = $"https://{rawUrl}";
        }

        if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out Uri? uri))
        {
            Log($"Invalid web address: {rawUrl}");
            return;
        }

        string normalized = uri.ToString();
        _addressBarTextBox.Text = normalized;
        _webView.CoreWebView2.Navigate(normalized);
        Log($"Navigate requested: {normalized}");
    }

    private void EnsureDefaultHomePageLoaded()
    {
        if (!_webViewInitialized || _webView.CoreWebView2 is null)
        {
            return;
        }

        string current = _webView.Source?.ToString() ?? _webView.CoreWebView2.Source;
        bool needsDefault = string.IsNullOrWhiteSpace(current) ||
                            current.Equals("about:blank", StringComparison.OrdinalIgnoreCase);

        if (!needsDefault)
        {
            return;
        }

        _addressBarTextBox.Text = DefaultHomeUrl;
        _webView.CoreWebView2.Navigate(DefaultHomeUrl);
        Log($"Default web page loaded: {DefaultHomeUrl}");
    }

    private void GoBack()
    {
        if (!_webViewInitialized || _webView.CoreWebView2 is null)
        {
            Log("Back navigation requested before Web View was initialized.");
            return;
        }

        if (!_webView.CoreWebView2.CanGoBack)
        {
            return;
        }

        _webView.CoreWebView2.GoBack();
        Log("Web back navigation requested.");
    }

    private void RefreshWebView()
    {
        if (!_webViewInitialized || _webView.CoreWebView2 is null)
        {
            Log("Refresh requested before Web View was initialized.");
            return;
        }

        _webView.Reload();
        Log("Web page refresh requested.");
    }

    private void ToggleHistoryPanel()
    {
        if (!_webViewInitialized || _webView.CoreWebView2 is null)
        {
            Log("History requested before Web View was initialized.");
            return;
        }

        if (_historyPanel.Visible)
        {
            _historyPanel.Visible = false;
            _historyButton.BackColor = SystemColors.Control;
            Log("Web history panel closed.");
            return;
        }

        RefreshHistoryPanelItems();
        _historyPanel.Visible = true;
        _historyPanel.BringToFront();
        _historyButton.BackColor = Color.LightSteelBlue;
        Log("Web history panel opened.");
    }

    private void NavigateFromHistorySelection()
    {
        if (_historyListBox.SelectedItem is not string selectedUrl)
        {
            return;
        }

        if (selectedUrl.StartsWith("(", StringComparison.Ordinal))
        {
            return;
        }

        NavigateToHistoryUrl(selectedUrl);
        _historyPanel.Visible = false;
        _historyButton.BackColor = SystemColors.Control;
    }

    private void NavigateToHistoryUrl(string url)
    {
        if (!_webViewInitialized || _webView.CoreWebView2 is null)
        {
            Log("History navigation requested before Web View was initialized.");
            return;
        }

        _addressBarTextBox.Text = url;
        _webView.CoreWebView2.Navigate(url);
        Log($"History navigation: {url}");
    }

    private void ApplyZoom()
    {
        _zoomLabel.Text = $"Zoom {_zoomSlider.Value}%";
        if (!_webViewInitialized)
        {
            return;
        }

        _webView.ZoomFactor = _zoomSlider.Value / 100.0;
        Log($"Web zoom set to {_zoomSlider.Value}%.");
    }

    private void UpdateWebViewToggleVisual()
    {
        _webViewToggleButton.BackColor = _webViewModeEnabled ? Color.LightSkyBlue : SystemColors.Control;
    }

    private void UpdateNavigationButtons()
    {
        _backButton.Enabled = _webViewInitialized &&
                              _webView.CoreWebView2 is not null &&
                              _webView.CoreWebView2.CanGoBack;
    }

    private void RefreshHistoryPanelItems()
    {
        _historyListBox.Items.Clear();
        if (_sessionHistory.Count == 0)
        {
            _historyListBox.Items.Add("(No history in this session)");
            return;
        }

        for (int i = _sessionHistory.Count - 1; i >= 0; i--)
        {
            _historyListBox.Items.Add(_sessionHistory[i]);
        }
    }

    private void OnWebNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        _addressBarTextBox.Text = e.Uri;
        _webView.CoreWebView2?.CookieManager.DeleteAllCookies();
        Log($"Web navigation starting: {e.Uri}");
    }

    private void OnWebNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        if (e.IsSuccess)
        {
            string currentUrl = _webView.Source?.ToString() ?? "(unknown)";
            AddHistoryEntry(currentUrl);
            Log($"Web navigation completed: {currentUrl}");
        }
        else
        {
            Log($"Web navigation failed: {e.WebErrorStatus}");
        }

        _webView.CoreWebView2?.CookieManager.DeleteAllCookies();
        UpdateNavigationButtons();
    }

    private void OnWebSourceChanged(object? sender, CoreWebView2SourceChangedEventArgs e)
    {
        string source = _webView.Source?.ToString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        _addressBarTextBox.Text = source;
        Log($"Web source changed: {source}");
    }

    private void OnWebDocumentTitleChanged(object? sender, object e)
    {
        string title = _webView.CoreWebView2?.DocumentTitle ?? "(untitled)";
        Log($"Web title: {title}");
    }

    private void OnWebHistoryChanged(object? sender, object e)
    {
        if (_webView.CoreWebView2 is null)
        {
            return;
        }

        Log($"Web history state changed (Back: {_webView.CoreWebView2.CanGoBack}, Forward: {_webView.CoreWebView2.CanGoForward}).");
        UpdateNavigationButtons();
    }

    private void OnWebNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (string.IsNullOrWhiteSpace(e.Uri))
        {
            Log("Web popup blocked (empty URI).");
            return;
        }

        Log($"Web popup redirected to current view: {e.Uri}");
        _addressBarTextBox.Text = e.Uri;
        _webView.CoreWebView2?.Navigate(e.Uri);
    }

    private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
    {
        if (!e.Request.Headers.Contains("Cookie"))
        {
            return;
        }

        e.Request.Headers.RemoveHeader("Cookie");
        if (e.ResourceContext == CoreWebView2WebResourceContext.Document)
        {
            Log($"Cookie header removed for: {e.Request.Uri}");
        }
    }

    private void AddHistoryEntry(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        if (_sessionHistory.Count == 0 ||
            !_sessionHistory[^1].Equals(url, StringComparison.OrdinalIgnoreCase))
        {
            _sessionHistory.Add(url);
            if (_historyPanel.Visible)
            {
                RefreshHistoryPanelItems();
            }
        }
    }

    private void CleanupWebSession()
    {
        _sessionHistory.Clear();
        _historyPanel.Visible = false;

        try
        {
            if (_webView.CoreWebView2 is not null)
            {
                _webView.CoreWebView2.CookieManager.DeleteAllCookies();
            }
        }
        catch
        {
            // Best effort cleanup only.
        }

        try
        {
            _webView.Dispose();
        }
        catch
        {
            // Best effort cleanup only.
        }

        TryDeleteDirectoryWithRetry(_webSessionPath);
    }

    private static void TryDeleteDirectoryWithRetry(string path)
    {
        const int attempts = 5;
        for (int i = 0; i < attempts; i++)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    return;
                }

                Directory.Delete(path, recursive: true);
                return;
            }
            catch
            {
                if (i == attempts - 1)
                {
                    return;
                }

                Thread.Sleep(120);
            }
        }
    }

    private void ApplyProtection(bool enable)
    {
        var (ok, error) = NativeMethods.ExcludeFromCapture(Handle, enable);

        Log(ok
            ? $"SetWindowDisplayAffinity({(enable ? "WDA_EXCLUDEFROMCAPTURE" : "WDA_NONE")}) succeeded."
            : $"SetWindowDisplayAffinity failed. Win32 error {error}: {NativeMethods.DescribeError(error)}.");

        if (NativeMethods.TryGetCurrentAffinity(Handle, out uint current))
        {
            _protectionEnabled = current == NativeMethods.WDA_EXCLUDEFROMCAPTURE;
            Log($"Current window affinity: {NativeMethods.Describe(current)}.");
        }
        else
        {
            // Fall back to call result only when we cannot read back state.
            _protectionEnabled = ok && enable;
            Log("GetWindowDisplayAffinity failed to read back the current state.");
        }

        _statusLabel.Text = _protectionEnabled
            ? "Protection Enabled - Your screen is hidden from capture software. yipee :D"
            : "Protection Disabled - Your screen is visible to capture software. oh noes :(";
        _statusLabel.BackColor = _protectionEnabled ? Color.LightGreen : Color.LightSalmon;

        _toggleButton.Text = _protectionEnabled
            ? "Disable Protection"
            : "Enable Protection";
    }

    private void Log(string message)
    {
        string entry = $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}";
        if (!_logBox.IsDisposed)
        {
            _logBox.AppendText(entry);
        }

        try
        {
            File.AppendAllText(_logFilePath, entry);
        }
        catch
        {
            // Keep the app functional if temp storage is unavailable.
        }
    }
}
