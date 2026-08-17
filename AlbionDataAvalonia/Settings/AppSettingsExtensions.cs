using System;

namespace AlbionDataAvalonia.Settings;

public static class AppSettingsExtensions
{
    public static Uri GetTrimsSilverBackendApiBaseUri(this AppSettings settings)
    {
        var value = string.IsNullOrWhiteSpace(settings.TrimsSilverBackendApiBase)
            ? "https://api.albionfreemarket.com/be"
            : settings.TrimsSilverBackendApiBase;
        return new Uri(value.TrimEnd('/') + "/", UriKind.Absolute);
    }
}
