using System.Globalization;
using System.Xml.Linq;

namespace StructVault.Desktop.Services;

public sealed class FileSystemApplicationSettingsService : IApplicationSettingsService
{
    private const string RootElementName = "StructVaultApplicationSettings";
    private readonly string settingsFilePath;

    public FileSystemApplicationSettingsService()
        : this(FileSystemApplicationSettingsPathProvider.GetDefaultSettingsFilePath())
    {
    }

    public FileSystemApplicationSettingsService(string settingsFilePath)
    {
        if (string.IsNullOrWhiteSpace(settingsFilePath))
        {
            throw new ArgumentException("An application settings file path is required.", nameof(settingsFilePath));
        }

        this.settingsFilePath = settingsFilePath;
    }

    public ApplicationSettings Load()
    {
        if (!File.Exists(settingsFilePath))
        {
            return ApplicationSettings.Default;
        }

        try
        {
            XDocument document = XDocument.Load(settingsFilePath, LoadOptions.None);
            XElement? root = document.Element(RootElementName);
            if (root is null)
            {
                return ApplicationSettings.Default;
            }

            ApplicationSettings settings = new()
            {
                LastVaultFilePath = ReadString(root, nameof(ApplicationSettings.LastVaultFilePath)),
                ThemeName = ReadString(root, nameof(ApplicationSettings.ThemeName)) ?? ApplicationSettings.Default.ThemeName,
                ClipboardAutoClearEnabled = ReadBool(root, nameof(ApplicationSettings.ClipboardAutoClearEnabled), ApplicationSettings.Default.ClipboardAutoClearEnabled),
                ClipboardAutoClearDelaySeconds = ReadInt(root, nameof(ApplicationSettings.ClipboardAutoClearDelaySeconds), ApplicationSettings.Default.ClipboardAutoClearDelaySeconds),
                IdleLockEnabled = ReadBool(root, nameof(ApplicationSettings.IdleLockEnabled), ApplicationSettings.Default.IdleLockEnabled),
                IdleLockTimeoutSeconds = ReadInt(root, nameof(ApplicationSettings.IdleLockTimeoutSeconds), ApplicationSettings.Default.IdleLockTimeoutSeconds)
            };

            return settings.Normalize();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException or ArgumentException)
        {
            return ApplicationSettings.Default;
        }
    }

    public void Save(ApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ApplicationSettings normalizedSettings = settings.Normalize();
        string? directoryPath = Path.GetDirectoryName(settingsFilePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        XDocument document = new(
            new XElement(
                RootElementName,
                new XElement(nameof(ApplicationSettings.LastVaultFilePath), normalizedSettings.LastVaultFilePath ?? string.Empty),
                new XElement(nameof(ApplicationSettings.ThemeName), normalizedSettings.ThemeName),
                new XElement(nameof(ApplicationSettings.ClipboardAutoClearEnabled), normalizedSettings.ClipboardAutoClearEnabled),
                new XElement(nameof(ApplicationSettings.ClipboardAutoClearDelaySeconds), normalizedSettings.ClipboardAutoClearDelaySeconds),
                new XElement(nameof(ApplicationSettings.IdleLockEnabled), normalizedSettings.IdleLockEnabled),
                new XElement(nameof(ApplicationSettings.IdleLockTimeoutSeconds), normalizedSettings.IdleLockTimeoutSeconds)));

        document.Save(settingsFilePath);
    }

    private static string? ReadString(XElement root, string elementName)
    {
        string? value = root.Element(elementName)?.Value;
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool ReadBool(XElement root, string elementName, bool defaultValue)
    {
        string? value = ReadString(root, elementName);
        return bool.TryParse(value, out bool parsedValue) ? parsedValue : defaultValue;
    }

    private static int ReadInt(XElement root, string elementName, int defaultValue)
    {
        string? value = ReadString(root, elementName);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedValue) ? parsedValue : defaultValue;
    }
}
