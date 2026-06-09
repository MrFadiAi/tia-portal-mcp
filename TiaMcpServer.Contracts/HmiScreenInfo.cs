using System.Collections.Generic;

namespace TiaMcpServer.Contracts;

public class HmiScreenInfo
{
    public string ScreenName { get; set; } = string.Empty;
    public List<HmiScreenItemInfo> Items { get; set; } = new();
    public int? ItemCount { get; set; }
    /// <summary>
    /// Raw screen XML when the Openness API cannot read items directly (V16 limitation).
    /// </summary>
    public string? RawXml { get; set; }
}

public class HmiScreenItemInfo
{
    public string Name { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }

    // --- Style ---
    public string? BackColor { get; set; }
    public string? ForeColor { get; set; }
    public string? BorderColor { get; set; }
    public double? BorderWidth { get; set; }
    public bool? Enabled { get; set; }
    public bool? Visible { get; set; }
    public string? Mode { get; set; }
    public string? FillStyle { get; set; }

    // --- IOField formatting ---
    public string? DataFormat { get; set; }
    public string? FormatPattern { get; set; }
    public int? ShiftDecimalPoint { get; set; }
    public int? FieldLength { get; set; }
    public string? Unit { get; set; }
    public bool? HiddenInput { get; set; }

    // --- Line geometry ---
    public double? StartX { get; set; }
    public double? StartY { get; set; }
    public double? EndX { get; set; }
    public double? EndY { get; set; }
    public double? LineWidth { get; set; }
    public string? LineStyle { get; set; }

    // --- Circle ---
    public double? Radius { get; set; }

    // --- Font ---
    public string? FontFamily { get; set; }
    public double? FontSize { get; set; }
    public string? FontStyle { get; set; }

    // --- Navigation ---
    public string? HorizontalAlignment { get; set; }
    public string? VerticalAlignment { get; set; }
    public int? TabIndex { get; set; }

    // --- Rich data ---
    public List<HmiTagBindingInfo> TagBindings { get; set; } = new();
    public List<HmiAnimationInfo> Animations { get; set; } = new();
    public List<HmiEventInfo> Events { get; set; } = new();
    public List<HmiTextInfo> Texts { get; set; } = new();
    public List<HmiLinkInfo> Links { get; set; } = new();
}

public class HmiTagBindingInfo
{
    public string PropertyName { get; set; } = string.Empty;
    public string TagName { get; set; } = string.Empty;
}

public class HmiAnimationInfo
{
    public string AnimationType { get; set; } = string.Empty;
    public string? PropertyName { get; set; }
    public string? TagName { get; set; }
    public int? BitPosition { get; set; }
    public bool? VisibleWhenTrue { get; set; }
    public List<HmiRangeInfo> Ranges { get; set; } = new();
}

public class HmiRangeInfo
{
    public string? LowerLimit { get; set; }
    public string? UpperLimit { get; set; }
    public string? BackColor { get; set; }
    public string? ForeColor { get; set; }
    public string? FlashingType { get; set; }
}

public class HmiEventInfo
{
    public string EventName { get; set; } = string.Empty;
    public string? FunctionName { get; set; }
    public string? FunctionType { get; set; }
    public List<HmiEventParamInfo> Parameters { get; set; } = new();
}

public class HmiEventParamInfo
{
    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
}

public class HmiTextInfo
{
    public string Property { get; set; } = string.Empty;
    public string? Culture { get; set; }
    public string? Text { get; set; }
}

public class HmiLinkInfo
{
    public string LinkType { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
}

public class HmiDeviceInfo
{
    public string DeviceName { get; set; } = string.Empty;
    public string TypeIdentifier { get; set; } = string.Empty;
    public List<HmiScreenInfo> Screens { get; set; } = new();
}
