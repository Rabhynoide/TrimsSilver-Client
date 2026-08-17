using System.Collections.Generic;

namespace AlbionDataAvalonia.Settings;

public class AppSettings
{
    public string? NPCapDownloadUrl { get; set; }
    public string? PacketFilterPortText { get; set; }
    public string? MarketOrdersIngestSubject { get; set; }
    public string? MarketHistoriesIngestSubject { get; set; }
    public string? GoldDataIngestSubject { get; set; }
    public string? BanditEventIngestSubject { get; set; }
    public string? LatestVersionUrl { get; set; }
    public string? LatesVersionDownloadUrl { get; set; }
    public string? FileNameFormat { get; set; }
    public double FirstUpdateCheckDelayMins { get; set; }
    public double UpdateCheckIntervalHours { get; set; }
    public int NetworkDevicesStartDelaySecs { get; set; }
    public int NetworkDevicesIdleMinutes { get; set; }
    public int NetworkDevicesIdleCheckMinutes { get; set; }

    public string TrimsSilverAuthUrl { get; set; } = string.Empty;
    public string TrimsSilverAuthRedirectUri { get; set; } = string.Empty;
    public string TrimsSilverBackendApiBase { get; set; } = string.Empty;
    public string TrimsSilverTopItemsApiBase { get; set; } = string.Empty;
    public string TrimsSilverIngestApiBase { get; set; } = string.Empty;

    public List<string> ItemsToUploadToTrimsSilver { get; set; } = new List<string>();
}
