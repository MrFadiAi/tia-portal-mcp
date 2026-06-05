using System.Collections.Generic;

namespace TiaMcpServer.Contracts;

public class HmiScreenInfo
{
    public string ScreenName { get; set; } = string.Empty;
    public List<HmiScreenItemInfo> Items { get; set; } = new();
}

public class HmiScreenItemInfo
{
    public string Name { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }
    public List<HmiTagBindingInfo> TagBindings { get; set; } = new();
    public List<HmiEventInfo> Events { get; set; } = new();
}

public class HmiTagBindingInfo
{
    public string PropertyName { get; set; } = string.Empty;
    public string TagName { get; set; } = string.Empty;
}

public class HmiEventInfo
{
    public string EventName { get; set; } = string.Empty;
    public string? ActionType { get; set; }
    public string? Description { get; set; }
}

public class HmiDeviceInfo
{
    public string DeviceName { get; set; } = string.Empty;
    public string TypeIdentifier { get; set; } = string.Empty;
    public List<HmiScreenInfo> Screens { get; set; } = new();
}
