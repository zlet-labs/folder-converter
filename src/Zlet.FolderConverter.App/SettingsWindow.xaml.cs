using System.Diagnostics;
using System.Net.Http;
using System.Windows;
using Zlet.FolderConverter.App.Localization;
using Zlet.FolderConverter.App.Settings;
using Zlet.FolderConverter.Core.Services;
using Zlet.FolderConverter.Core.Models;

namespace Zlet.FolderConverter.App;

public partial class SettingsWindow : Window
{
    private bool _initialized;
    private readonly AppSettingsStore _store = new();
    private readonly HttpClient? _client;
    private readonly IUpdateChecker _checker;
    private readonly CancellationTokenSource _closed = new();
    private readonly IReadOnlyList<OfficeApplicationAvailability> _office;
    private readonly bool _doclingAvailable;
    private UpdateResult _update = new("UpdateIdle");
    private string? _resetKey, _copyKey, _browserKey;
    private LocalizationService Localization => LocalizationService.Current;

    public SettingsWindow() : this(null) { }

    public SettingsWindow(IUpdateChecker? checker)
    {
        if (checker is null)
        {
            _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
            checker = new GitHubUpdateChecker(_client);
        }
        _checker = checker;
        _office = new MicrosoftOfficeCapabilityDetector().Detect();
        _doclingAvailable = new DoclingWorkerProcessRunner().IsAvailable;
        InitializeComponent();
        (Localization.Language == AppLanguage.Russian ? RussianButton : EnglishButton).IsChecked = true;
        Localization.LanguageChanged += LanguageChanged;
        Closed += (_, _) =>
        {
            Localization.LanguageChanged -= LanguageChanged;
            _closed.Cancel();
            _client?.Dispose();
        };
        RefreshText();
        _initialized = true;
    }

    private void LanguageChanged(object? sender, EventArgs e) => RefreshText();

    private void RefreshText()
    {
        VersionText.Text = Localization.Format("SettingsVersion", ProductIdentity.Version);
        ProductText.Text = $"{ProductIdentity.Name} {ProductIdentity.Version}";
        DiagnosticsBlock.Text = DiagnosticsText.Create(Localization, _office, _doclingAvailable);
        UpdateStatus.Text = Localization.Format(_update.ResourceKey, _update.Release?.Version.ToString() ?? ProductIdentity.Version);
        ReleaseButton.Visibility = _update.Release is null ? Visibility.Collapsed : Visibility.Visible;
        ResetStatus.Text = _resetKey is null ? "" : Localization.Get(_resetKey);
        CopyStatus.Text = _copyKey is null ? "" : Localization.Get(_copyKey);
        BrowserStatus.Text = _browserKey is null ? "" : Localization.Get(_browserKey);
    }

    private void Language_Checked(object sender, RoutedEventArgs e)
    {
        if (!_initialized || sender is not System.Windows.Controls.RadioButton { Tag: string language }) return;
        Localization.Apply(language);
        var result = _store.TrySaveLanguage(language);
        SaveErrorText.Visibility = result.Success ? Visibility.Collapsed : Visibility.Visible;
        _resetKey = null;
        RefreshText();
    }

    private async void Check_Click(object sender, RoutedEventArgs e)
    {
        if (!CheckButton.IsEnabled) return;
        CheckButton.IsEnabled = false;
        _update = new("UpdateChecking");
        RefreshText();
        try { _update = await _checker.CheckAsync(ProductIdentity.Version, _closed.Token); }
        finally { CheckButton.IsEnabled = true; }
        RefreshText();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        var confirmed = System.Windows.MessageBox.Show(this, Localization.Get("ResetConfirm"), Localization.Get("ResetSettings"),
            MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
        if (!confirmed) return;
        _resetKey = _store.TryReset(confirmed).Success ? "ResetSuccess" : "ResetFailed";
        RefreshText();
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        try { System.Windows.Clipboard.SetText(DiagnosticsBlock.Text); _copyKey = "DiagnosticsCopied"; }
        catch (System.Runtime.InteropServices.ExternalException) { _copyKey = "DiagnosticsCopyFailed"; }
        RefreshText();
    }

    private void Release_Click(object sender, RoutedEventArgs e)
    {
        if (_update.Release is not null) OpenBrowser(_update.Release.Page.AbsoluteUri);
    }

    private void Link_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button { Tag: string url }) OpenBrowser(url);
    }

    private void OpenBrowser(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); _browserKey = null; }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        { _browserKey = "BrowserFailed"; }
        RefreshText();
    }
}
