namespace StructVault.Application.Persistence;

public sealed record ThemeSettingsRecord
{
    public const string LightBlueThemeName = "Light.Blue";
    public const string LightEmeraldThemeName = "Light.Emerald";
    public const string LightGreenThemeName = "Light.Green";
    public const string LightOrangeThemeName = "Light.Orange";
    public const string LightPurpleThemeName = "Light.Purple";
    public const string LightRedThemeName = "Light.Red";
    public const string LightTealThemeName = "Light.Teal";

    private static readonly string[] SupportedThemeNames =
    [
        LightBlueThemeName,
        LightEmeraldThemeName,
        LightGreenThemeName,
        LightOrangeThemeName,
        LightPurpleThemeName,
        LightRedThemeName,
        LightTealThemeName
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
