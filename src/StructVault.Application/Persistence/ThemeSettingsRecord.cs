namespace StructVault.Application.Persistence;

public sealed record ThemeSettingsRecord
{
    public const string LightBlueThemeName = "Light.Blue";
    public const string DarkBlueThemeName = "Dark.Blue";

    private static readonly string[] SupportedThemeNames =
    [
        LightBlueThemeName,
        DarkBlueThemeName
    ];

    public ThemeSettingsRecord(string themeName)
    {
        ThemeName = NormalizeThemeName(themeName);
    }

    public string ThemeName { get; }

    public static ThemeSettingsRecord Default { get; } = new(LightBlueThemeName);

    public static IReadOnlyList<string> SupportedThemes => SupportedThemeNames;

    public static string NormalizeThemeName(string themeName)
    {
        if (themeName is null)
        {
            throw new ArgumentNullException(nameof(themeName));
        }

        string normalizedThemeName = themeName.Trim();
        if (normalizedThemeName.Length == 0)
        {
            throw new ArgumentException("Theme name cannot be empty or whitespace.", nameof(themeName));
        }

        string? supportedThemeName = SupportedThemeNames.FirstOrDefault(
            supportedTheme => string.Equals(supportedTheme, normalizedThemeName, StringComparison.Ordinal));
        if (supportedThemeName is null)
        {
            throw new ArgumentOutOfRangeException(nameof(themeName), themeName, "Theme name is not supported.");
        }

        return supportedThemeName;
    }
}
