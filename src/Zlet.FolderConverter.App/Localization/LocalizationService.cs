using System.Globalization;
using System.Windows;

namespace Zlet.FolderConverter.App.Localization;

public sealed class LocalizationService
{
    private const string DictionaryPrefix = "Resources/Strings.";
    private static readonly Lazy<LocalizationService> LazyCurrent = new(() => new(standalone: false));
    private static readonly object DictionaryLoadLock = new();

    private readonly bool _standalone;
    private IReadOnlyDictionary<string, string> _strings;
    private ResourceDictionary _activeDictionary;

    private LocalizationService(bool standalone = false)
    {
        _standalone = standalone;
        var dictionary = LoadDictionary(AppLanguage.Russian);
        _activeDictionary = dictionary;
        _strings = CopyStrings(dictionary);
        if (!_standalone && System.Windows.Application.Current?.Resources is { } resources)
        {
            SynchronizeDictionaries(resources.MergedDictionaries, dictionary);
        }
    }

    public static LocalizationService Current => LazyCurrent.Value;
    public static LocalizationService CreateStandalone(string language)
    {
        var service = new LocalizationService(standalone: true);
        service.Apply(language);
        return service;
    }
    public string Language { get; private set; } = AppLanguage.Russian;
    public CultureInfo Culture => CultureInfo.GetCultureInfo(Language);
    public event EventHandler? LanguageChanged;
    public ResourceDictionary ActiveDictionary => _activeDictionary;

    public void Apply(string language) =>
        Apply(language, _standalone ? null : System.Windows.Application.Current?.Resources);

    public void Apply(string language, ResourceDictionary? targetResources)
    {
        if (!AppLanguage.IsSupported(language)) throw new ArgumentOutOfRangeException(nameof(language));
        language = AppLanguage.Normalize(language);
        var replacement = LoadDictionary(language);
        _activeDictionary = replacement;
        _strings = CopyStrings(replacement);

        if (targetResources is not null)
        {
            SynchronizeDictionaries(targetResources.MergedDictionaries, replacement);
        }

        Language = language;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public static void SynchronizeDictionaries(ICollection<ResourceDictionary> dictionaries, ResourceDictionary replacement)
    {
        if (dictionaries is null) throw new ArgumentNullException(nameof(dictionaries));
        if (replacement is null) throw new ArgumentNullException(nameof(replacement));

        var list = dictionaries as IList<ResourceDictionary>;
        var firstIndex = -1;
        var index = 0;
        foreach (var dictionary in dictionaries)
        {
            if (IsLanguageDictionary(dictionary))
            {
                firstIndex = index;
                break;
            }
            index++;
        }

        if (firstIndex >= 0 && list is not null)
        {
            list[firstIndex] = replacement;
            for (var i = list.Count - 1; i > firstIndex; i--)
            {
                if (IsLanguageDictionary(list[i])) list.RemoveAt(i);
            }
            for (var i = firstIndex - 1; i >= 0; i--)
            {
                if (IsLanguageDictionary(list[i])) list.RemoveAt(i);
            }
        }
        else if (list is not null)
        {
            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (IsLanguageDictionary(list[i])) list.RemoveAt(i);
            }
            list.Insert(0, replacement);
        }
        else
        {
            var toRemove = dictionaries.Where(IsLanguageDictionary).ToList();
            foreach (var d in toRemove) dictionaries.Remove(d);
            dictionaries.Add(replacement);
        }
    }

    public static bool IsLanguageDictionary(ResourceDictionary dictionary)
    {
        if (dictionary is null) return false;
        if (dictionary.Source is not null)
        {
            var original = dictionary.Source.OriginalString;
            if (original.Contains("Strings.", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return dictionary.Contains("SettingsTitle") && dictionary.Contains("LanguageLabel");
    }

    public static Uri GetDictionaryUri(string language) =>
        new($"/{typeof(LocalizationService).Assembly.GetName().Name};component/Resources/Strings.{language}.xaml", UriKind.Relative);

    public static ResourceDictionary CreateDictionary(string language) =>
        LoadDictionary(language);

    public string Get(string key)
    {
        return _strings.TryGetValue(key, out var value)
            ? value
            : throw new KeyNotFoundException($"Localization key '{key}' was not found for {Language}.");
    }

    public string Format(string key, params object[] arguments) =>
        string.Format(Culture, Get(key), arguments);

    public string FileWord(int count)
        => Get(LocalizationFormatting.FileWordResourceKey(count, Language));

    public string FormatFileSize(long bytes)
    {
        return LocalizationFormatting.FormatFileSize(bytes, Culture, Get("MegabyteUnit"));
    }

    public string FormatExecutionTime(TimeSpan elapsed)
    {
        return LocalizationFormatting.FormatExecutionTime(elapsed, Culture, Get("SecondUnit"));
    }

    private static ResourceDictionary LoadDictionary(string language)
    {
        lock (DictionaryLoadLock)
        {
            var uri = GetDictionaryUri(language);
            try
            {
                return new ResourceDictionary { Source = uri };
            }
            catch
            {
                var dict = (ResourceDictionary)System.Windows.Application.LoadComponent(uri);
                try { dict.Source = uri; } catch { }
                return dict;
            }
        }
    }

    private static IReadOnlyDictionary<string, string> CopyStrings(ResourceDictionary dictionary) =>
        dictionary.Keys.Cast<object>().ToDictionary(
            key => key.ToString()!,
            key => dictionary[key] as string
                ?? throw new System.IO.InvalidDataException($"Localization value '{key}' is not a string."),
            StringComparer.Ordinal);
}
