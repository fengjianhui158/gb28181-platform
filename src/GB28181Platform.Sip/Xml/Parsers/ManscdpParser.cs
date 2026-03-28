using System.Xml.Linq;

namespace GB28181Platform.Sip.Xml.Parsers;

/// <summary>
/// MANSCDP XML 消息解析器
/// </summary>
public static class ManscdpParser
{
    /// <summary>
    /// 获取消息类型 (如 Keepalive, Catalog, DeviceInfo 等)
    /// </summary>
    public static string? GetCmdType(string xmlContent)
    {
        try
        {
            var doc = XDocument.Parse(xmlContent);
            return doc.Root?.Element("CmdType")?.Value;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 获取设备编号
    /// </summary>
    public static string? GetDeviceId(string xmlContent)
    {
        try
        {
            var doc = XDocument.Parse(xmlContent);
            return doc.Root?.Element("DeviceID")?.Value;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 获取根元素名称 (Notify, Response, Control, Query)
    /// </summary>
    public static string? GetRootName(string xmlContent)
    {
        try
        {
            var doc = XDocument.Parse(xmlContent);
            return doc.Root?.Name.LocalName;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 解析目录响应中的设备列表
    /// </summary>
    public static List<CatalogItem> ParseCatalogResponse(string xmlContent)
    {
        var items = new List<CatalogItem>();
        try
        {
            var doc = XDocument.Parse(xmlContent);
            var deviceList = doc.Root?.Element("DeviceList");
            if (deviceList == null) return items;

            foreach (var item in deviceList.Elements("Item"))
            {
                items.Add(new CatalogItem
                {
                    DeviceID = item.Element("DeviceID")?.Value ?? "",
                    Name = item.Element("Name")?.Value ?? "",
                    Manufacturer = item.Element("Manufacturer")?.Value ?? "",
                    Status = item.Element("Status")?.Value ?? "",
                    Longitude = double.TryParse(item.Element("Longitude")?.Value, out var lng) ? lng : 0,
                    Latitude = double.TryParse(item.Element("Latitude")?.Value, out var lat) ? lat : 0,
                    PTZType = int.TryParse(item.Element("PTZType")?.Value, out var ptz) ? ptz : 0,
                    ParentID = item.Element("ParentID")?.Value ?? "",
                    IPAddress = item.Element("IPAddress")?.Value ?? "",
                    Port = int.TryParse(item.Element("Port")?.Value, out var port) ? port : 0
                });
            }
        }
        catch { }
        return items;
    }
}

public class CatalogItem
{
    public string DeviceID { get; set; } = "";
    public string Name { get; set; } = "";
    public string Manufacturer { get; set; } = "";
    public string Status { get; set; } = "";
    public double Longitude { get; set; }
    public double Latitude { get; set; }
    public int PTZType { get; set; }
    public string ParentID { get; set; } = "";
    public string IPAddress { get; set; } = "";
    public int Port { get; set; }
}
