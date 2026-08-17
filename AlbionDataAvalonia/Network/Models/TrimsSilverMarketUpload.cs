namespace AlbionDataAvalonia.Network.Models;

public class TrimsSilverMarketUpload : MarketUpload
{
    public int ServerId { get; set; }
    public string UploaderId { get; set; }

    public TrimsSilverMarketUpload(MarketUpload marketUpload, int serverId, string uploaderId)
    {
        Orders = marketUpload.Orders;
        ServerId = serverId;
        UploaderId = uploaderId;
    }
}
