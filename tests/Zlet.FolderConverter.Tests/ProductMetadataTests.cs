using System.Reflection;

namespace Zlet.FolderConverter.Tests;

public sealed class ProductMetadataTests
{
    private static readonly Assembly AppAssembly =
        typeof(global::Zlet.FolderConverter.App.App).Assembly;

    [Fact]
    public void App_assembly_uses_public_product_identity_and_version()
    {
        Assert.Equal("ZletConverter", AppAssembly.GetName().Name);
        Assert.Equal(new Version(0, 0, 3, 0), AppAssembly.GetName().Version);
        Assert.Equal("Zlet Converter",
            AppAssembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product);
        Assert.Equal("0.0.3.0",
            AppAssembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version);
        Assert.Equal("0.0.3",
            AppAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
    }

    [Fact]
    public void App_assembly_exposes_display_and_portable_package_names()
    {
        var metadata = AppAssembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .ToDictionary(attribute => attribute.Key, attribute => attribute.Value);
        Assert.Equal("v0.0.3", metadata["DisplayVersion"]);
        Assert.Equal("ZletConverter-v0.0.3-win-x64", metadata["PortablePackageName"]);
    }

    [Fact]
    public void Public_identity_drives_ui_and_result_zip_names()
    {
        Assert.Equal("Zlet Converter", global::Zlet.FolderConverter.App.ProductIdentity.Name);
        Assert.Equal("ZletConverter", global::Zlet.FolderConverter.App.ProductIdentity.ExecutableName);
        Assert.Equal("0.0.3", global::Zlet.FolderConverter.App.ProductIdentity.Version);
        Assert.Equal(
            "ZletConverter-v0.0.3-results.zip",
            global::Zlet.FolderConverter.App.ProductIdentity.ResultZipFileName);
    }

    [Fact]
    public void Repository_documents_use_current_product_and_repository_url()
    {
        var root = FindRepositoryRoot();
        var readme = File.ReadAllText(Path.Combine(root, "README.md"));
        var portableReadme = File.ReadAllText(Path.Combine(root, "README_PORTABLE.txt"));

        Assert.Contains("# Zlet Converter", readme);
        Assert.Contains("v0.0.2", readme);
        Assert.Contains("https://github.com/zlet-labs/zlet-converter", readme);
        Assert.Contains("ZletConverter-v0.0.3-win-x64.zip", portableReadme);
        Assert.Contains("previous public name `Zlet Batch Converter`", readme);
        Assert.DoesNotContain("Zlet Folder Converter", readme);
    }

    [Fact]
    public void Main_window_renders_progress_timing_and_file_specific_errors()
    {
        var root = FindRepositoryRoot();
        var mainWindow = File.ReadAllText(Path.Combine(
            root,
            "src",
            "Zlet.FolderConverter.App",
            "MainWindow.xaml"));

        Assert.Contains("{Binding ProgressPercentText}", mainWindow);
        Assert.Contains("{Binding ProgressCountText}", mainWindow);
        Assert.Contains("{Binding ElapsedTimeText}", mainWindow);
        Assert.Contains("{Binding RemainingTimeText}", mainWindow);
        Assert.Contains("{Binding FinalDurationText}", mainWindow);
        Assert.Contains("ItemsSource=\"{Binding ErrorMessages}\"", mainWindow);
    }

    [Fact]
    public void Main_window_and_styles_declare_format_and_office_badges()
    {
        var root = FindRepositoryRoot();
        var appDirectory = Path.Combine(root, "src", "Zlet.FolderConverter.App");
        var mainWindow = File.ReadAllText(Path.Combine(appDirectory, "MainWindow.xaml"));
        var styles = File.ReadAllText(Path.Combine(appDirectory, "Resources", "AppStyles.xaml"));

        Assert.Contains("RuleFormatBadgeStyle", mainWindow);
        Assert.Contains("RuleFormatIconStyle", mainWindow);
        Assert.Contains("OfficeWordBadgeStyle", mainWindow);
        Assert.Contains("OfficeExcelBadgeStyle", mainWindow);
        Assert.Contains("OfficePowerPointBadgeStyle", mainWindow);

        Assert.Contains("DocumentIconGeometry", styles);
        Assert.Contains("SpreadsheetIconGeometry", styles);
        Assert.Contains("PresentationIconGeometry", styles);
        Assert.Contains("PdfIconGeometry", styles);
        Assert.Contains("JsonIconGeometry", styles);
        Assert.Contains("TextDataIconGeometry", styles);
        Assert.Contains("ImageIconGeometry", styles);
        Assert.Contains("EbookIconGeometry", styles);
        Assert.Contains("GenericFileIconGeometry", styles);

        Assert.DoesNotContain("RowDetailsTemplate", mainWindow);
        Assert.DoesNotContain("ExtensionBreakdown", mainWindow);
    }

    [Fact]
    public void App_uses_one_product_icon_for_executable_window_and_taskbar()
    {
        var root = FindRepositoryRoot();
        var appDirectory = Path.Combine(root, "src", "Zlet.FolderConverter.App");
        var project = File.ReadAllText(Path.Combine(
            appDirectory,
            "Zlet.FolderConverter.App.csproj"));
        var mainWindow = File.ReadAllText(Path.Combine(appDirectory, "MainWindow.xaml"));
        var iconPath = Path.Combine(appDirectory, "Assets", "ZletBatchConverter.ico");

        Assert.Contains("<ApplicationIcon>Assets\\ZletBatchConverter.ico</ApplicationIcon>", project);
        Assert.Contains("Icon=\"Assets/ZletBatchConverter.ico\"", mainWindow);
        Assert.True(File.Exists(iconPath));
        Assert.True(new FileInfo(iconPath).Length > 1_000);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FolderConverter.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
