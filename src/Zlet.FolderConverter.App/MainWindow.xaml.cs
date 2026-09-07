using System.Diagnostics;
using System.IO;
using System.Windows;
using Forms = System.Windows.Forms;
using Zlet.FolderConverter.App.ViewModels;
using Zlet.FolderConverter.App.Localization;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.App;

public partial class MainWindow : Window
{
    private static LocalizationService Loc => LocalizationService.Current;
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        Title = $"{ProductIdentity.Name} v{ProductIdentity.Version}";
        Width = Math.Min(1280, SystemParameters.WorkArea.Width * 0.94);
        Height = Math.Min(780, SystemParameters.WorkArea.Height * 0.94);
        var capabilityDetector = new MicrosoftOfficeCapabilityDetector();
        var workerRunner = new MicrosoftOfficeWorkerProcessRunner();
        var resolver = new DefaultConversionAdapterResolver(
            capabilityDetector,
            workerRunner);
        _viewModel = new MainWindowViewModel(
            new FileSystemFolderScanner(),
            new ConversionPlanner(resolver),
            new ConversionProcessor(resolver),
            capabilityDetector);
        DataContext = _viewModel;
    }

    private void ChooseFolderButton_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new Forms.FolderBrowserDialog
        {
            Description = Loc.Get("ChooseSourceDialog"),
            UseDescriptionForTitle = true
        };

        if (!string.IsNullOrWhiteSpace(_viewModel.SelectedFolder))
        {
            dialog.SelectedPath = _viewModel.SelectedFolder;
        }

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            _viewModel.SelectedFolder = dialog.SelectedPath;
        }
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.ScanAsync();
        }
        catch (OperationCanceledException)
        {
            _viewModel.SetLocalizedState("ScanCancelled");
        }
        catch (Exception)
        {
            _viewModel.AddLocalizedError("ScanFailed");
            _viewModel.SetLocalizedState("ScanFailedState");
        }
    }

    private async void SourcePathTextBox_KeyDown(
        object sender,
        System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != System.Windows.Input.Key.Enter || !_viewModel.CanScan)
        {
            return;
        }

        e.Handled = true;
        await _viewModel.ScanAsync();
    }

    private void SelectAllButton_Click(object sender, RoutedEventArgs e) =>
        _viewModel.SelectAll();

    private void ClearSelectionButton_Click(object sender, RoutedEventArgs e) =>
        _viewModel.ClearSelection();

    private void InvertSelectionButton_Click(object sender, RoutedEventArgs e) =>
        _viewModel.InvertSelection();

    private void CopyConversionListButton_Click(object sender, RoutedEventArgs e)
    {
        var text = _viewModel.BuildConversionList();
        if (string.IsNullOrEmpty(text))
            return;

        try
        {
            System.Windows.Clipboard.SetText(text);
            _viewModel.ConfirmConversionListCopied();
        }
        catch
        {
            _viewModel.AddLocalizedError("CopyListFailed");
        }
    }

    private void CopyFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.CanCopySelectedFolder)
        {
            return;
        }

        try
        {
            System.Windows.Clipboard.SetText(_viewModel.SelectedFolder);
        }
        catch
        {
            _viewModel.AddLocalizedError("CopyPathFailed");
        }
    }

    private async void ConvertButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _viewModel.ConvertAsync();
        }
        catch (OperationCanceledException)
        {
            _viewModel.SetLocalizedState("ConversionCancelled");
        }
        catch (Exception)
        {
            _viewModel.AddLocalizedError("ConversionFailed");
            _viewModel.SetLocalizedState("ConversionFailedState");
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e) =>
        _viewModel.StopConversion();

    private void ChooseOutputButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedOutputMode == OutputMode.Folder)
        {
            using var dialog = new Forms.FolderBrowserDialog
            {
                Description = Loc.Get("ChooseResultFolderDialog"),
                UseDescriptionForTitle = true,
                SelectedPath = Directory.Exists(_viewModel.OutputPath)
                    ? _viewModel.OutputPath
                    : string.Empty
            };
            if (dialog.ShowDialog() == Forms.DialogResult.OK)
            {
                _viewModel.OutputPath = dialog.SelectedPath;
            }
            return;
        }

        using var saveDialog = new Forms.SaveFileDialog
        {
            Title = Loc.Get("ChooseResultZipDialog"),
            Filter = Loc.Get("ZipFilter"),
            DefaultExt = "zip",
            AddExtension = true,
            OverwritePrompt = false,
            FileName = Path.GetFileName(_viewModel.OutputPath),
            InitialDirectory = Path.GetDirectoryName(_viewModel.OutputPath)
        };
        if (saveDialog.ShowDialog() == Forms.DialogResult.OK)
        {
            _viewModel.OutputPath = saveDialog.FileName;
        }
    }

    private void ResetOutputButton_Click(object sender, RoutedEventArgs e) =>
        _viewModel.ResetOutputPath();

    private void OpenResultButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.CanOpenResult)
        {
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = true
            };
            if (_viewModel.SelectedOutputMode == OutputMode.Zip)
            {
                startInfo.ArgumentList.Add("/select,");
            }
            startInfo.ArgumentList.Add(_viewModel.ResultFolder);
            Process.Start(startInfo);
        }
        catch
        {
            _viewModel.AddLocalizedError("OpenResultFailed");
        }
    }

    private void OpenReportButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.CanOpenReport)
        {
            return;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _viewModel.ReportPath,
                UseShellExecute = true
            };
            Process.Start(startInfo);
        }
        catch
        {
            _viewModel.AddLocalizedError("OpenResultFailed");
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) =>
        new SettingsWindow { Owner = this }.ShowDialog();

    public void ShowSettingsSaveFailure()
    {
        _viewModel.SetLocalizedState("SettingsSaveFailed");
        _viewModel.AddLocalizedError("SettingsSaveFailed");
    }
}
