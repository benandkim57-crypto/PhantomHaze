namespace PrivacyShieldPoc;

public class MainForm : Form
{
    private readonly Label _contentLabel = new();
    private readonly Label _statusLabel = new();
    private readonly Button _toggleButton = new();
    private readonly TextBox _logBox = new();

    private bool _protectionEnabled;

    public MainForm()
    {
        Text = "Privacy Shield — Phase 1 Proof of Concept";
        Width = 620;
        Height = 460;
        StartPosition = FormStartPosition.CenterScreen;

        BuildUi();

        // Protection is applied on load, not behind an opt-in step - this is
        // what proves the "always enabled" requirement rather than something
        // that only activates after detecting a recording tool.
        Load += (_, _) =>
        {
            LogTrueOsBuild();
            ApplyProtection(enable: true);
        };
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
            $"— {(meetsMinimum ? "meets minimum" : "BELOW MINIMUM, the API call below is expected to fail")}.");
    }

    private void BuildUi()
    {
        _contentLabel.Text = "SECRET TEST CONTENT — DO NOT CAPTURE";
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

        _logBox.Multiline = true;
        _logBox.ReadOnly = true;
        _logBox.ScrollBars = ScrollBars.Vertical;
        _logBox.Dock = DockStyle.Fill;
        _logBox.Font = new Font("Consolas", 9);

        // Docked children must be added in reverse visual order.
        Controls.Add(_logBox);
        Controls.Add(_toggleButton);
        Controls.Add(_statusLabel);
        Controls.Add(_contentLabel);
    }

    private void ApplyProtection(bool enable)
    {
        var (ok, error) = NativeMethods.ExcludeFromCapture(Handle, enable);
        _protectionEnabled = ok && enable;

        Log(ok
            ? $"SetWindowDisplayAffinity({(enable ? "WDA_EXCLUDEFROMCAPTURE" : "WDA_NONE")}) succeeded."
            : $"SetWindowDisplayAffinity failed. Win32 error {error}: {NativeMethods.DescribeError(error)}.");

        if (NativeMethods.TryGetCurrentAffinity(Handle, out uint current))
        {
            Log($"Current window affinity: {NativeMethods.Describe(current)}.");
        }
        else
        {
            Log("GetWindowDisplayAffinity failed to read back the current state.");
        }

        _statusLabel.Text = _protectionEnabled
            ? "PROTECTION ACTIVE — excluded from supported capture"
            : "PROTECTION DISABLED — visible to capture (testing only)";
        _statusLabel.BackColor = _protectionEnabled ? Color.LightGreen : Color.LightSalmon;

        _toggleButton.Text = _protectionEnabled
            ? "Disable Protection (testing only)"
            : "Enable Protection";
    }

    private void Log(string message)
        => _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
}
