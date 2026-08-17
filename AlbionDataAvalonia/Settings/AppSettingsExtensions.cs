using System;

namespace AlbionDataAvalonia.Settings;

public static class AppSettingsExtensions
{
    public static Uri GetTrimsSilverBackendApiBaseUri(this AppSettings settings)
    {
        var value = settings.TrimsSilverBackendApiBase;
        if (string.IsNullOrWhiteSpace(value))
        {
            value = settings.TrimsSilverAuthApiUrl;
            if (value.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
            {
                value = value[..^"/api".Length];
            }
        }

        value = string.IsNullOrWhiteSpace(value)
            ? "https://api.albionfreemarket.com/be"
            : value;
        return new Uri(value.TrimEnd('/') + "/", UriKind.Absolute);
    }
}
