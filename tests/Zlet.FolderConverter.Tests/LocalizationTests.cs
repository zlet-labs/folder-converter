using System.Text.Json.Nodes;
using System.Xml.Linq;
using Zlet.FolderConverter.App.Localization;
using Zlet.FolderConverter.App.Settings;
using Zlet.FolderConverter.App.ViewModels;
using Zlet.FolderConverter.Core.Models;
using Zlet.FolderConverter.Core.Services;

namespace Zlet.FolderConverter.Tests;

public sealed class LocalizationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "zlet-localization-tests", Guid.NewGuid().ToString("N"));
    public LocalizationTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void Resource_sets_have_equal_unique_nonempty_keys_and_required_startup_keys()
    {
        var en = ReadResources("en-US");
        var ru = ReadResources("ru-RU");
        Assert.Equal(en.Keys.Order(), ru.Keys.Order());
        Assert.All(en.Values.Concat(ru.Values), value => Assert.False(string.IsNullOrWhiteSpace(value)));
        foreach (var key in new[] { "SettingsTitle", "LanguageLabel", "LanguageRussian", "LanguageEnglish", "InitialState", "FolderUnavailable", "StatusConverted", "OutputFolder" })
        {
            Assert.Contains(key, en.Keys);
            Assert.Contains(key, ru.Keys);
        }
    }

    [Fact]
    public void Settings_path_uses_current_product_identity() =>
        Assert.EndsWith(Path.Combine("Zlet Labs", "Zlet Converter", "settings.json"), AppSettingsStore.DefaultPath, StringComparison.Ordinal);

    [Theory]
    [InlineData("ru-RU")]
    [InlineData("en-US")]
    public void Valid_saved_language_round_trips(string language)
    {
        var store = Store(); Assert.True(store.TrySaveLanguage(language).Success); Assert.Equal(language, store.LoadLanguage());
    }

    [Fact]
    public void Missing_corrupted_and_unknown_settings_require_a_choice()
    {
        var store = Store();
        Assert.Null(store.LoadLanguage());
        Directory.CreateDirectory(Path.GetDirectoryName(store.SettingsPath)!);
        File.WriteAllText(store.SettingsPath, "{broken");
        Assert.Null(store.LoadLanguage());
        File.WriteAllText(store.SettingsPath, "{\"language\":\"xx-YY\"}");
        Assert.Null(store.LoadLanguage());
    }

    [Fact]
    public void Valid_cli_overrides_saved_language_and_requests_persistence()
    {
        var decision = StartupLanguageResolver.Resolve("en-US", "ru-RU");
        Assert.Equal("en-US", decision.Language);
        Assert.True(decision.PersistExplicit);
        Assert.False(decision.ChooserRequired);
    }

    [Fact]
    public void Invalid_cli_keeps_valid_saved_language_without_overwrite()
    {
        var decision = StartupLanguageResolver.Resolve("xx-YY", "ru-RU");
        Assert.Equal("ru-RU", decision.Language);
        Assert.False(decision.PersistExplicit);
        Assert.False(decision.ChooserRequired);
    }

    [Fact]
    public void Invalid_cli_without_saved_language_requires_chooser() =>
        Assert.True(StartupLanguageResolver.Resolve("xx-YY", null).ChooserRequired);

    [Fact]
    public void Resource_dictionary_remains_complete_without_wpf_application()
    {
        Assert.Null(System.Windows.Application.Current);
        var localization = LocalizationService.CreateStandalone(AppLanguage.English);
        Assert.Equal("The folder does not exist or is unavailable.", localization.Get("FolderUnavailable"));
        Assert.Equal("Ready to process.", localization.Get("OperationReady"));
        localization.Apply(AppLanguage.Russian);
        Assert.Equal("Папка не существует или недоступна.", localization.Get("FolderUnavailable"));
        Assert.Equal("Готово к обработке.", localization.Get("OperationReady"));
    }

    [Fact]
    public void Applying_english_changes_language_and_active_resource_dictionary()
    {
        var localization = LocalizationService.CreateStandalone(AppLanguage.Russian);
        Assert.Equal("ru-RU", localization.Language);
        Assert.Equal("Настройки", (string)localization.ActiveDictionary["SettingsTitle"]);

        localization.Apply(AppLanguage.English);
        Assert.Equal("en-US", localization.Language);
        Assert.Equal("Settings", (string)localization.ActiveDictionary["SettingsTitle"]);
        Assert.Equal("Browse", (string)localization.ActiveDictionary["Browse"]);
        Assert.Equal("Rules", (string)localization.ActiveDictionary["Rules"]);
        Assert.Equal("Destination", (string)localization.ActiveDictionary["DestinationTitle"]);
        Assert.Equal("Copy", (string)localization.ActiveDictionary["Copy"]);
    }

    [Fact]
    public void Active_wpf_resource_dictionary_resolves_dynamic_resources_after_apply()
    {
        var root = new System.Windows.ResourceDictionary();
        var stylesUri = new Uri("/ZletConverter;component/Resources/AppStyles.xaml", UriKind.Relative);
        root.MergedDictionaries.Add(new System.Windows.ResourceDictionary { Source = stylesUri });

        var localization = LocalizationService.CreateStandalone(AppLanguage.Russian);
        localization.Apply(AppLanguage.Russian, root);

        Assert.Equal("Настройки", (string)root["SettingsTitle"]);
        Assert.Equal("Обзор", (string)root["Browse"]);
        Assert.Equal("Правила", (string)root["Rules"]);
        Assert.Equal("Назначение", (string)root["DestinationTitle"]);

        localization.Apply(AppLanguage.English, root);

        Assert.Equal("Settings", (string)root["SettingsTitle"]);
        Assert.Equal("Browse", (string)root["Browse"]);
        Assert.Equal("Rules", (string)root["Rules"]);
        Assert.Equal("Destination", (string)root["DestinationTitle"]);
    }

    [Fact]
    public void Applying_russian_replaces_english_rather_than_leaving_both_active()
    {
        var root = new System.Windows.ResourceDictionary();
        var stylesUri = new Uri("/ZletConverter;component/Resources/AppStyles.xaml", UriKind.Relative);
        root.MergedDictionaries.Add(new System.Windows.ResourceDictionary { Source = stylesUri });

        var localization = LocalizationService.CreateStandalone(AppLanguage.Russian);
        localization.Apply(AppLanguage.English, root);

        Assert.Single(root.MergedDictionaries.Where(LocalizationService.IsLanguageDictionary));
        Assert.Equal("Settings", (string)root["SettingsTitle"]);

        localization.Apply(AppLanguage.Russian, root);

        Assert.Single(root.MergedDictionaries.Where(LocalizationService.IsLanguageDictionary));
        Assert.Equal("Настройки", (string)root["SettingsTitle"]);
        Assert.DoesNotContain(root.MergedDictionaries, d =>
            d.Source is not null && d.Source.OriginalString.Contains("Strings.en-US", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Only_one_strings_resource_dictionary_is_active_even_with_legacy_duplicate_input()
    {
        var root = new System.Windows.ResourceDictionary();
        var stylesUri = new Uri("/ZletConverter;component/Resources/AppStyles.xaml", UriKind.Relative);
        root.MergedDictionaries.Add(new System.Windows.ResourceDictionary { Source = stylesUri });
        root.MergedDictionaries.Add(LocalizationService.CreateDictionary(AppLanguage.English));
        root.MergedDictionaries.Add(LocalizationService.CreateDictionary(AppLanguage.Russian));

        Assert.Equal(2, root.MergedDictionaries.Count(LocalizationService.IsLanguageDictionary));

        LocalizationService.SynchronizeDictionaries(root.MergedDictionaries, LocalizationService.CreateDictionary(AppLanguage.English));

        Assert.Single(root.MergedDictionaries.Where(LocalizationService.IsLanguageDictionary));
        Assert.Equal("Settings", (string)root["SettingsTitle"]);
        Assert.Equal(2, root.MergedDictionaries.Count);

        LocalizationService.SynchronizeDictionaries(root.MergedDictionaries, LocalizationService.CreateDictionary(AppLanguage.Russian));

        Assert.Single(root.MergedDictionaries.Where(LocalizationService.IsLanguageDictionary));
        Assert.Equal("Настройки", (string)root["SettingsTitle"]);
        Assert.Equal(2, root.MergedDictionaries.Count);
    }

    [Fact]
    public void Switching_en_ru_en_remains_deterministic_and_keeps_single_dictionary()
    {
        var root = new System.Windows.ResourceDictionary();
        var stylesUri = new Uri("/ZletConverter;component/Resources/AppStyles.xaml", UriKind.Relative);
        root.MergedDictionaries.Add(new System.Windows.ResourceDictionary { Source = stylesUri });

        var localization = LocalizationService.CreateStandalone(AppLanguage.Russian);

        for (var i = 0; i < 6; i++)
        {
            var targetLang = i % 2 == 0 ? AppLanguage.English : AppLanguage.Russian;
            var expectedTitle = targetLang == AppLanguage.English ? "Settings" : "Настройки";
            var expectedBrowse = targetLang == AppLanguage.English ? "Browse" : "Обзор";

            localization.Apply(targetLang, root);

            Assert.Equal(targetLang, localization.Language);
            Assert.Single(root.MergedDictionaries.Where(LocalizationService.IsLanguageDictionary));
            Assert.Equal(2, root.MergedDictionaries.Count);
            Assert.Equal(expectedTitle, (string)root["SettingsTitle"]);
            Assert.Equal(expectedBrowse, (string)root["Browse"]);
            Assert.Equal(expectedTitle, (string)localization.ActiveDictionary["SettingsTitle"]);
        }
    }

    [Theory]
    [InlineData("ru-RU", "Настройки", "Обзор")]
    [InlineData("en-US", "Settings", "Browse")]
    public void Saved_language_startup_resolves_to_the_same_active_resource_language(string savedLang, string expectedTitle, string expectedBrowse)
    {
        var decision = StartupLanguageResolver.Resolve(null, savedLang);
        Assert.Equal(savedLang, decision.Language);
        Assert.False(decision.ChooserRequired);

        var root = new System.Windows.ResourceDictionary();
        var stylesUri = new Uri("/ZletConverter;component/Resources/AppStyles.xaml", UriKind.Relative);
        root.MergedDictionaries.Add(new System.Windows.ResourceDictionary { Source = stylesUri });

        var localization = LocalizationService.CreateStandalone(AppLanguage.Russian);
        localization.Apply(decision.Language!, root);

        Assert.Equal(savedLang, localization.Language);
        Assert.Single(root.MergedDictionaries.Where(LocalizationService.IsLanguageDictionary));
        Assert.Equal(expectedTitle, (string)root["SettingsTitle"]);
        Assert.Equal(expectedBrowse, (string)root["Browse"]);
    }

    [Fact]
    public void IsLanguageDictionary_accurately_discriminates_strings_from_styles()
    {
        var styles = new System.Windows.ResourceDictionary
        {
            Source = new Uri("/ZletConverter;component/Resources/AppStyles.xaml", UriKind.Relative)
        };
        var en = LocalizationService.CreateDictionary(AppLanguage.English);
        var ru = LocalizationService.CreateDictionary(AppLanguage.Russian);

        Assert.False(LocalizationService.IsLanguageDictionary(styles));
        Assert.True(LocalizationService.IsLanguageDictionary(en));
        Assert.True(LocalizationService.IsLanguageDictionary(ru));
    }

    [Fact]
    public void Open_report_failed_and_destination_keys_are_localized_and_accurate()
    {
        var localization = LocalizationService.CreateStandalone(AppLanguage.Russian);
        Assert.Equal("Не удалось открыть отчёт.", localization.Get("OpenReportFailed"));
        Assert.Equal("Назначение", localization.Get("DestinationTitle"));
        Assert.Equal("Куда сохранить результаты", localization.Get("DestinationDescription"));
        localization.Apply(AppLanguage.English);
        Assert.Equal("Could not open the report.", localization.Get("OpenReportFailed"));
        Assert.Equal("Destination", localization.Get("DestinationTitle"));
        Assert.Equal("Where to save results", localization.Get("DestinationDescription"));
    }

    [Fact]
    public void Settings_update_preserves_unrelated_values()
    {
        var store = Store();
        Directory.CreateDirectory(Path.GetDirectoryName(store.SettingsPath)!);
        File.WriteAllText(store.SettingsPath, "{\"futureSetting\":42,\"language\":\"ru-RU\"}");
        Assert.True(store.TrySaveLanguage("en-US").Success);
        var json = JsonNode.Parse(File.ReadAllText(store.SettingsPath))!.AsObject();
        Assert.Equal(42, json["futureSetting"]!.GetValue<int>());
        Assert.Equal("en-US", json["language"]!.GetValue<string>());
    }

    [Fact]
    public void Unknown_language_in_valid_settings_can_be_replaced_without_losing_other_values()
    {
        var store = Store();
        Directory.CreateDirectory(Path.GetDirectoryName(store.SettingsPath)!);
        File.WriteAllText(store.SettingsPath, "{\"futureSetting\":42,\"language\":\"xx-YY\"}");
        Assert.Null(store.LoadLanguage());
        Assert.True(StartupLanguageResolver.Resolve(null, store.LoadLanguage()).ChooserRequired);

        Assert.True(store.TrySaveLanguage(AppLanguage.English).Success);

        var json = JsonNode.Parse(File.ReadAllText(store.SettingsPath))!.AsObject();
        Assert.Equal(42, json["futureSetting"]!.GetValue<int>());
        Assert.Equal(AppLanguage.English, json["language"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(1, "файл")][InlineData(2, "файла")][InlineData(5, "файлов")][InlineData(11, "файлов")][InlineData(21, "файл")][InlineData(22, "файла")][InlineData(25, "файлов")]
    public void Russian_pluralization(int count, string expected) =>
        Assert.Equal(expected, LocalizationService.CreateStandalone(AppLanguage.Russian).FileWord(count));

    [Theory][InlineData(1, "file")][InlineData(2, "files")]
    public void English_pluralization(int count, string expected) =>
        Assert.Equal(expected, LocalizationService.CreateStandalone(AppLanguage.English).FileWord(count));

    [Fact]
    public void Sizes_and_times_follow_selected_language()
    {
        var ru = LocalizationService.CreateStandalone(AppLanguage.Russian);
        var en = LocalizationService.CreateStandalone(AppLanguage.English);
        Assert.Equal("0,84 МБ", ru.FormatFileSize(880804));
        Assert.Equal("0.84 MB", en.FormatFileSize(880804));
        Assert.Equal("3,2 с", ru.FormatExecutionTime(TimeSpan.FromSeconds(3.2)));
        Assert.Equal("3.2 s", en.FormatExecutionTime(TimeSpan.FromSeconds(3.2)));
    }

    [Fact]
    public void Language_switch_preserves_semantic_operation_state()
    {
        var localization = LocalizationService.CreateStandalone(AppLanguage.English);
        var operation = new PlannedOperation("C:\\source\\a.doc", "a.doc", SourceFormat.Doc,
            ConversionTarget.Docx, ".docx", "C:\\result\\a.docx", true,
            OperationStatus.Succeeded, "ok", "C:\\result", "C:\\source", 880804);
        var row = new OperationRowViewModel(operation, new ConversionResult(operation, OperationStatus.Succeeded, "ok"), localization: localization);
        Assert.Equal("Converted", row.Status);
        Assert.Equal(OperationStatus.Succeeded, row.Operation.Status);
        Assert.Equal("a.doc", row.FilePath);
        localization.Apply(AppLanguage.Russian);
        Assert.Equal("Преобразовано", row.Status);
        Assert.Equal(OperationStatus.Succeeded, row.Operation.Status);
        Assert.Equal("a.doc", row.FilePath);
    }

    [Fact]
    public void Status_filter_output_and_office_labels_exist_in_both_languages()
    {
        var en = ReadResources("en-US"); var ru = ReadResources("ru-RU");
        Assert.Equal("Converted", en["StatusConverted"]); Assert.Equal("Преобразовано", ru["StatusConverted"]);
        Assert.Equal("To process", en["FilterConvert"]); Assert.Equal("К обработке", ru["FilterConvert"]);
        Assert.Equal("Folder", en["OutputFolder"]); Assert.Equal("Папка", ru["OutputFolder"]);
        Assert.Equal("available", en["OfficeAvailable"]); Assert.Equal("доступен", ru["OfficeAvailable"]);
    }

    [Theory]
    [InlineData("unsafe_target", "Недопустимый путь результата.", "The result path is invalid.")]
    [InlineData("target_conflict", "Файл результата уже существует.", "The result file already exists.")]
    [InlineData("office_application_missing", "Требуемое приложение Microsoft Office не установлено.", "The required Microsoft Office application is not installed.")]
    [InlineData("worker_timeout", "Преобразование превысило допустимое время.", "Conversion exceeded the allowed time.")]
    public void Known_operation_error_codes_localize_in_both_languages(string errorCode, string ru, string en)
    {
        var localization = LocalizationService.CreateStandalone(AppLanguage.Russian);
        Assert.Equal(ru, OperationMessageLocalizer.Localize(OperationStatus.Failed, ConversionTarget.Docx, "raw", errorCode, localization));
        localization.Apply(AppLanguage.English);
        Assert.Equal(en, OperationMessageLocalizer.Localize(OperationStatus.Failed, ConversionTarget.Docx, "raw", errorCode, localization));
    }

    [Fact]
    public void Operation_message_raises_change_and_relocalizes_without_changing_diagnostics()
    {
        var localization = LocalizationService.CreateStandalone(AppLanguage.Russian);
        var operation = new PlannedOperation("C:\\source\\nested\\legacy.doc", "nested\\legacy.doc", SourceFormat.Doc,
            ConversionTarget.Docx, ".docx", "C:\\result\\nested\\legacy.docx", true,
            OperationStatus.Failed, "Недопустимый путь результата.", "C:\\result", "C:\\source", 1);
        var result = new ConversionResult(operation, OperationStatus.Failed, operation.Message,
            new ConversionDiagnostic("unsafe_target"));
        var row = new OperationRowViewModel(operation, result, localization: localization);
        var changedProperties = new List<string?>();
        row.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);

        Assert.Equal("Недопустимый путь результата.", row.Message);
        Assert.Equal("Ошибка: Недопустимый путь результата.", row.Status);

        localization.Apply(AppLanguage.English);
        row.RefreshLocalization();

        Assert.Contains(nameof(OperationRowViewModel.Message), changedProperties);
        Assert.Equal("The result path is invalid.", row.Message);
        Assert.Equal("Failed: The result path is invalid.", row.Status);
        Assert.Equal("C:\\source\\nested\\legacy.doc", row.SourcePath);
        Assert.Equal("nested\\legacy.docx", row.ResultPath);
        Assert.Equal("unsafe_target", row.Result!.Diagnostic!.ErrorCode);

        localization.Apply(AppLanguage.Russian);
        row.RefreshLocalization();
        Assert.Equal("Недопустимый путь результата.", row.Message);
        Assert.Equal("Ошибка: Недопустимый путь результата.", row.Status);
    }

    [Fact]
    public void Runtime_converting_message_relocalizes_from_semantic_status()
    {
        var localization = LocalizationService.CreateStandalone(AppLanguage.Russian);
        var row = RuntimeRow(localization);

        row.BeginExecution(0, 10);
        Assert.Equal(OperationStatus.Converting, row.Operation.Status);
        Assert.Equal(string.Empty, row.Operation.Message);
        Assert.Equal("Выполняется операция.", row.Message);
        Assert.Equal("В процессе · 10%", row.Status);

        localization.Apply(AppLanguage.English); row.RefreshLocalization();
        Assert.Equal("Operation in progress.", row.Message);
        Assert.Equal("In progress · 10%", row.Status);
        localization.Apply(AppLanguage.Russian); row.RefreshLocalization();
        Assert.Equal("Выполняется операция.", row.Message);
        Assert.Equal("C:\\source\\runtime.doc", row.SourcePath);
        Assert.Equal("runtime.docx", row.ResultPath);
    }

    [Fact]
    public void Runtime_cancelled_message_relocalizes_from_semantic_status()
    {
        var localization = LocalizationService.CreateStandalone(AppLanguage.English);
        var row = RuntimeRow(localization);

        row.CancelExecution(TimeProvider.System, 0);
        Assert.Equal(OperationStatus.Cancelled, row.Operation.Status);
        Assert.Equal(string.Empty, row.Operation.Message);
        Assert.Equal("Cancelled by user.", row.Message);
        Assert.Equal("Cancelled", row.Status);

        localization.Apply(AppLanguage.Russian); row.RefreshLocalization();
        Assert.Equal("Отменено пользователем.", row.Message);
        Assert.Equal("Отменено", row.Status);
        localization.Apply(AppLanguage.English); row.RefreshLocalization();
        Assert.Equal("Cancelled by user.", row.Message);
        Assert.Equal("C:\\source\\runtime.doc", row.SourcePath);
        Assert.Equal("runtime.docx", row.ResultPath);
    }

    [Fact]
    public void Runtime_not_processed_message_relocalizes_from_semantic_status()
    {
        var localization = LocalizationService.CreateStandalone(AppLanguage.English);
        var row = RuntimeRow(localization);

        row.MarkNotProcessed();
        Assert.Equal(OperationStatus.NotProcessed, row.Operation.Status);
        Assert.Equal(string.Empty, row.Operation.Message);
        Assert.Equal("Not processed.", row.Message);
        Assert.Equal("Not processed", row.Status);

        localization.Apply(AppLanguage.Russian); row.RefreshLocalization();
        Assert.Equal("Не обработано.", row.Message);
        Assert.Equal("Не обработано", row.Status);
    }

    [Theory]
    [InlineData(OperationStatus.Ready, ConversionTarget.Copy, "Будет скопирован без изменений.", "Ready to copy unchanged.")]
    [InlineData(OperationStatus.Ready, ConversionTarget.Docx, "Готово к преобразованию.", "Ready to process.")]
    [InlineData(OperationStatus.Skipped, ConversionTarget.Skip, "Файл не будет изменён.", "The file will not be changed.")]
    [InlineData(OperationStatus.Failed, ConversionTarget.Docx, "Выбранное преобразование не поддерживается.", "The selected conversion is not supported.")]
    public void Known_planner_messages_localize_without_error_codes(
        OperationStatus status,
        ConversionTarget target,
        string sourceMessage,
        string expectedEnglish)
    {
        var localization = LocalizationService.CreateStandalone(AppLanguage.English);
        Assert.Equal(expectedEnglish, OperationMessageLocalizer.Localize(status, target, sourceMessage, localization: localization));
    }

    [Fact]
    public void Rule_labels_and_extensionless_breakdown_relocalize_without_rescan()
    {
        var localization = LocalizationService.CreateStandalone(AppLanguage.Russian);
        var files = new[] { new ScannedFile("C:\\source\\README", "README", SourceFormat.Unknown, 1) };
        var unknown = new RuleRowViewModel(FormatCapabilityCatalog.Get(SourceFormat.Unknown), 1,
            ConversionTarget.Skip, (_, _) => { }, files, localization);
        var image = new RuleRowViewModel(FormatCapabilityCatalog.Get(SourceFormat.Image), 1,
            ConversionTarget.Skip, (_, _) => { }, localization: localization);
        var archive = new RuleRowViewModel(FormatCapabilityCatalog.Get(SourceFormat.Archive), 1,
            ConversionTarget.Skip, (_, _) => { }, localization: localization);
        unknown.RefreshLocalization(); image.RefreshLocalization(); archive.RefreshLocalization();
        Assert.Equal("Другие", unknown.FormatLabel); Assert.Equal("Без расширения: 1", unknown.ExtensionBreakdown);
        Assert.Equal("Изображения", image.FormatLabel); Assert.Equal("Архивы", archive.FormatLabel);
        localization.Apply(AppLanguage.English); unknown.RefreshLocalization(); image.RefreshLocalization(); archive.RefreshLocalization();
        Assert.Equal("Other", unknown.FormatLabel); Assert.Equal("No extension: 1", unknown.ExtensionBreakdown);
        Assert.Equal("Images", image.FormatLabel); Assert.Equal("Archives", archive.FormatLabel);
        localization.Apply(AppLanguage.Russian); unknown.RefreshLocalization(); image.RefreshLocalization(); archive.RefreshLocalization();
        Assert.Equal("Другие", unknown.FormatLabel); Assert.Equal("Без расширения: 1", unknown.ExtensionBreakdown);
        Assert.Equal("Архивы", archive.FormatLabel);
    }

    [Fact]
    public void Existing_semantic_errors_relocalize_and_keep_raw_values()
    {
        var localization = LocalizationService.CreateStandalone(AppLanguage.Russian);
        var viewModel = new MainWindowViewModel(new EmptyScanner(), new EmptyPlanner(), localization: localization);
        viewModel.AddLocalizedError("ScanReadErrorFormat", "raw-folder");
        Assert.Equal("Не удалось прочитать папку: raw-folder.", Assert.Single(viewModel.ErrorMessages));
        localization.Apply(AppLanguage.English);
        Assert.Equal("Could not read folder: raw-folder.", Assert.Single(viewModel.ErrorMessages));
        localization.Apply(AppLanguage.Russian);
        Assert.Equal("Не удалось прочитать папку: raw-folder.", Assert.Single(viewModel.ErrorMessages));
    }

    [Fact]
    public void Conversion_errors_relocalize_without_losing_raw_diagnostics()
    {
        var localization = LocalizationService.CreateStandalone(AppLanguage.Russian);
        var viewModel = new MainWindowViewModel(new EmptyScanner(), new EmptyPlanner(), localization: localization);
        var operation = new PlannedOperation("C:\\source\\raw.doc", "raw.doc", SourceFormat.Doc,
            ConversionTarget.Docx, ".docx", "C:\\result\\raw.docx", true,
            OperationStatus.Failed, "raw core message", "C:\\result", "C:\\source", 1);
        var result = new ConversionResult(operation, OperationStatus.Failed, "raw core message",
            new ConversionDiagnostic("worker_timeout", HResult: unchecked((int)0x80004005)));

        viewModel.AddConversionError(result);
        Assert.Equal("raw.doc: Преобразование превысило допустимое время. (код: worker_timeout, HRESULT 0x80004005)",
            Assert.Single(viewModel.ErrorMessages));
        localization.Apply(AppLanguage.English);
        Assert.Equal("raw.doc: Conversion exceeded the allowed time. (code: worker_timeout, HRESULT 0x80004005)",
            Assert.Single(viewModel.ErrorMessages));
        localization.Apply(AppLanguage.Russian);
        Assert.Equal("raw.doc: Преобразование превысило допустимое время. (код: worker_timeout, HRESULT 0x80004005)",
            Assert.Single(viewModel.ErrorMessages));
    }

    [Fact]
    public void Docling_conversion_errors_relocalize_properly()
    {
        var localization = LocalizationService.CreateStandalone(AppLanguage.Russian);
        var viewModel = new MainWindowViewModel(new EmptyScanner(), new EmptyPlanner(), localization: localization);
        var operation = new PlannedOperation("C:\\source\\scanned.pdf", "scanned.pdf", SourceFormat.Pdf,
            ConversionTarget.Markdown, ".md", "C:\\result\\scanned.md", true,
            OperationStatus.Failed, "raw core message", "C:\\result", "C:\\source", 1);
        var result = new ConversionResult(operation, OperationStatus.Failed, "raw core message",
            new ConversionDiagnostic("scanned_pdf_unsupported"));

        viewModel.AddConversionError(result);
        Assert.Equal("scanned.pdf: PDF не содержит извлекаемого текста (возможно, отсканированный документ). Оптическое распознавание текста (OCR) не поддерживается. (код: scanned_pdf_unsupported)",
            Assert.Single(viewModel.ErrorMessages));
        localization.Apply(AppLanguage.English);
        Assert.Equal("scanned.pdf: The PDF does not contain extractable text (possibly a scanned document). Optical character recognition (OCR) is not supported. (code: scanned_pdf_unsupported)",
            Assert.Single(viewModel.ErrorMessages));
    }

    [Fact]
    public void Invalid_settings_destination_returns_failure_without_partial_file()
    {
        var blocker = Path.Combine(_root, "blocker"); File.WriteAllText(blocker, "not a directory");
        var store = new AppSettingsStore(Path.Combine(blocker, "settings.json"));
        var result = store.TrySaveLanguage(AppLanguage.English);
        Assert.False(result.Success);
        Assert.Equal("not a directory", File.ReadAllText(blocker));
        Assert.Empty(Directory.EnumerateFiles(_root, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void Corrupted_settings_are_not_mutated_or_replaced_when_save_is_attempted()
    {
        var store = Store();
        Directory.CreateDirectory(Path.GetDirectoryName(store.SettingsPath)!);
        const string corrupted = "{broken future settings";
        File.WriteAllText(store.SettingsPath, corrupted);

        Assert.Null(store.LoadLanguage());
        Assert.Equal(corrupted, File.ReadAllText(store.SettingsPath));
        Assert.True(StartupLanguageResolver.Resolve(null, store.LoadLanguage()).ChooserRequired);

        var result = store.TrySaveLanguage(AppLanguage.English);

        Assert.False(result.Success);
        Assert.Equal("settings_corrupted", result.ErrorCode);
        Assert.Equal(corrupted, File.ReadAllText(store.SettingsPath));
        Assert.Empty(Directory.EnumerateFiles(_root, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void Missing_settings_file_can_be_created()
    {
        var store = Store();
        Assert.False(File.Exists(store.SettingsPath));
        Assert.True(store.TrySaveLanguage(AppLanguage.English).Success);
        Assert.Equal(AppLanguage.English, store.LoadLanguage());
    }

    [Fact]
    public void Empty_path_label_comes_from_localization_resources()
    {
        var ru = LocalizationService.CreateStandalone(AppLanguage.Russian);
        var en = LocalizationService.CreateStandalone(AppLanguage.English);
        Assert.Equal("Папка не выбрана", PathDisplayFormatter.Format(string.Empty, localization: ru));
        Assert.Equal("No folder selected", PathDisplayFormatter.Format(string.Empty, localization: en));

        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "src", "Zlet.FolderConverter.App", "ViewModels", "PathDisplayFormatter.cs"));
        Assert.DoesNotContain("Папка не выбрана", source);
        Assert.DoesNotContain("No folder selected", source);
    }

    [Fact]
    public void Locked_valid_settings_remain_intact_when_save_fails()
    {
        var store = Store(); Assert.True(store.TrySaveLanguage(AppLanguage.Russian).Success);
        var before = File.ReadAllText(store.SettingsPath);
        using (File.Open(store.SettingsPath, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var result = store.TrySaveLanguage(AppLanguage.English);
            Assert.False(result.Success);
        }
        Assert.Equal(before, File.ReadAllText(store.SettingsPath));
        Assert.Equal(AppLanguage.Russian, store.LoadLanguage());
        Assert.Empty(Directory.EnumerateFiles(_root, "*.tmp", SearchOption.AllDirectories));
    }

    [Fact]
    public void No_stale_product_identity_is_present_in_current_ui()
    {
        var root = RepositoryRoot();
        var files = Directory.EnumerateFiles(Path.Combine(root, "src", "Zlet.FolderConverter.App"), "*", SearchOption.AllDirectories)
            .Where(path => Path.GetExtension(path) is ".cs" or ".xaml");
        Assert.All(files, path => Assert.DoesNotContain("Title=\"Zlet Batch Converter", File.ReadAllText(path)));
    }

    private AppSettingsStore Store() => new(Path.Combine(_root, "settings.json"));

    private static OperationRowViewModel RuntimeRow(LocalizationService localization)
    {
        var operation = new PlannedOperation("C:\\source\\runtime.doc", "runtime.doc", SourceFormat.Doc,
            ConversionTarget.Docx, ".docx", "C:\\result\\runtime.docx", true,
            OperationStatus.Ready, "Готово к преобразованию.", "C:\\result", "C:\\source", 1);
        return new OperationRowViewModel(operation, localization: localization);
    }

    private static Dictionary<string, string> ReadResources(string language)
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var path = Path.Combine(RepositoryRoot(), "src", "Zlet.FolderConverter.App", "Resources", $"Strings.{language}.xaml");
        var entries = XDocument.Load(path).Root!.Elements().Select(element => (Key: (string?)element.Attribute(x + "Key"), Value: element.Value)).ToArray();
        Assert.All(entries, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Key)));
        Assert.Equal(entries.Length, entries.Select(entry => entry.Key).Distinct(StringComparer.Ordinal).Count());
        return entries.ToDictionary(entry => entry.Key!, entry => entry.Value, StringComparer.Ordinal);
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "FolderConverter.sln"))) return directory.FullName;
        throw new DirectoryNotFoundException();
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    private sealed class EmptyScanner : IFolderScanner
    {
        public Task<ScanResult> ScanAsync(string rootPath, bool includeSubfolders, CancellationToken cancellationToken) =>
            Task.FromResult(new ScanResult(rootPath, [], []));
    }

    private sealed class EmptyPlanner : IConversionPlanner
    {
        public IReadOnlyList<PlannedOperation> CreatePlan(ScanResult scanResult, string rootPath, RuleSet ruleSet) => [];
    }
}
