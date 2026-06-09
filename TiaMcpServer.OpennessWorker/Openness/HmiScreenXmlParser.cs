using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Parses V16 HMI screen XML export into structured HmiScreenItemInfo objects.
/// V16 XML uses namespaced element names like "Hmi.Screen.IOField".
/// ALL element matching uses suffix comparison (after last dot) since
/// V16 prefixes everything: "Hmi.Event.Event", "Hmi.Globalization.FontItem", etc.
/// </summary>
public static class HmiScreenXmlParser
{
    /// <summary>
    /// Known screen item type names. The parser matches element names by:
    /// 1. Exact suffix match: "Hmi.Screen.IOField" → "IOField" → found
    /// 2. Ends-with match: "HmiButton" → ends with "Button" → found
    /// This handles V16 (Hmi.Screen.*), V18/Unified (HmiButton, HmiTextBox),
    /// and V21+ (plain Button or any future naming convention).
    /// </summary>
    private static readonly HashSet<string> ScreenItemTypes = new()
    {
        "IOField", "TextField", "Button", "StatusBar", "Bar",
        "SymbolicIOField", "DateTimeField", "GraphicView", "SwitchField",
        "ActiveXControl", "TrendView", "AlarmWindow", "RecipeView",
        "ScreenNavigator", "SlideIn", "MediaElement", "HtmlView",
        "Sm@rtServerView", "CameraView", "SunburstChart", "PieChart",
        "FuzzyChart", "ConveyorObject", "TankObject", "PipeObject",
        "ValveObject", "PumpObject", "HeatExchangerObject",
        "HistogramChart", "ScatterChart", "Table", "ComboBox",
        "ListBox", "CheckBox", "RadioButtonGroup", "Slider",
        "NumericInput", "SpinWheel", "TextList", "UserAdmin",
        "Schedule", "Cookie", "GraphicalObject", "Line",
        "Circle", "Ellipse", "Rectangle", "Polygon", "Arc", "Group",
        // V18/Unified HMI type names (WinCC Unified uses these directly)
        "HmiButton", "HmiTextBox", "HmiIOField", "HmiText",
        "HmiGraphicView", "HmiLine", "HmiCircle", "HmiRectangle",
        "HmiEllipse", "HmiGroup", "HmiSwitchField", "HmiBar",
        "HmiSlider", "HmiCheckBox", "HmiRadioButtonGroup",
        "HmiComboBox", "HmiListBox", "HmiNumericInput",
        "HmiSpinWheel", "HmiDateTimeField", "HmiSymbolicIOField",
        "HmiAlarmWindow", "HmiRecipeView", "HmiTrendView",
        "HmiTable", "HmiMediaElement", "HmiHtmlView",
        "HmiScreenNavigator", "HmiSlideIn", "HmiStatusBar",
        // Unified-specific
        "UnifiedButton", "UnifiedTextBox", "UnifiedIOField",
    };

    public static List<HmiScreenItemInfo> Parse(string xml)
    {
        var items = new List<HmiScreenItemInfo>();

        XDocument doc;
        try { doc = XDocument.Parse(xml); }
        catch { return items; }

        foreach (var element in doc.Root?.Descendants() ?? Enumerable.Empty<XElement>())
        {
            var suffix = Suffix(element.Name.LocalName);
            if (!ScreenItemTypes.Contains(suffix)) continue;

            var cleanType = CleanTypeName(suffix);
            var item = ParseScreenItem(element, cleanType);
            if (item is not null)
                items.Add(item);
        }

        return items;
    }

    /// <summary>Extract suffix after last dot: "Hmi.Screen.IOField" → "IOField"</summary>
    private static string Suffix(string localName)
    {
        var dot = localName.LastIndexOf('.');
        return dot >= 0 ? localName.Substring(dot + 1) : localName;
    }

    /// <summary>
    /// Strip "Hmi" prefix from type name for consistency:
    /// "HmiButton" → "Button", "HmiIOField" → "IOField", "Button" → "Button"
    /// </summary>
    private static string CleanTypeName(string suffix)
    {
        if (suffix.StartsWith("Hmi") && suffix.Length > 3 && char.IsUpper(suffix[3]))
            return suffix.Substring(3);
        if (suffix.StartsWith("Unified") && suffix.Length > 7 && char.IsUpper(suffix[7]))
            return suffix.Substring(7);
        return suffix;
    }

    /// <summary>Match element by suffix: "Hmi.Event.Event" matches "Event"</summary>
    private static bool Is(XElement el, string name) => Suffix(el.Name.LocalName) == name;

    private static HmiScreenItemInfo? ParseScreenItem(XElement element, string typeName)
    {
        var attrs = element.Elements()
            .FirstOrDefault(e => Is(e, "AttributeList"));

        var name = GetText(attrs, "ObjectName");
        if (string.IsNullOrEmpty(name))
            name = $"{typeName}_{element.Attribute("ID")?.Value ?? "?"}";

        var item = new HmiScreenItemInfo
        {
            Name = name!,
            TypeName = typeName
        };

        if (attrs is not null)
        {
            // Position & size
            item.X = GetDouble(attrs, "Left");
            item.Y = GetDouble(attrs, "Top");
            item.Width = GetDouble(attrs, "Width");
            item.Height = GetDouble(attrs, "Height");

            // Style
            item.BackColor = GetText(attrs, "BackColor");
            item.ForeColor = GetText(attrs, "ForeColor");
            item.BorderColor = GetText(attrs, "BorderColor");
            item.BorderWidth = GetDouble(attrs, "BorderWidth");
            item.Enabled = GetBool(attrs, "Enabled");
            item.Visible = GetBool(attrs, "Visible");
            item.Mode = GetText(attrs, "Mode");
            item.FillStyle = GetText(attrs, "BackFillStyle");

            // IOField formatting
            item.DataFormat = GetText(attrs, "DataFormat");
            item.FormatPattern = GetText(attrs, "FormatPattern");
            item.ShiftDecimalPoint = GetInt(attrs, "ShiftDecimalPoint");
            item.FieldLength = GetInt(attrs, "FieldLength");
            item.Unit = GetText(attrs, "Unit");
            item.HiddenInput = GetBool(attrs, "HiddenInput");

            // Line geometry
            item.StartX = GetDouble(attrs, "StartLeft");
            item.StartY = GetDouble(attrs, "StartTop");
            item.EndX = GetDouble(attrs, "EndLeft");
            item.EndY = GetDouble(attrs, "EndTop");
            item.LineWidth = GetDouble(attrs, "LineWidth");
            item.LineStyle = GetText(attrs, "Style");

            // Circle
            item.Radius = GetDouble(attrs, "Radius");

            // Alignment
            item.HorizontalAlignment = GetText(attrs, "HorizontalAlignment");
            item.VerticalAlignment = GetText(attrs, "VerticalAlignment");
            item.TabIndex = GetInt(attrs, "TabIndex");
        }

        // Font (first FontItem from first MultiLingualFont)
        ParseFont(element, item);

        // Tag bindings (from TagConnectionDynamic and Property)
        ParseTagBindings(element, item);

        // Animations (RangeAppearanceAnimation, SingleBitVisibilityAnimation)
        ParseAnimations(element, item);

        // Events
        ParseEvents(element, item);

        // Multilingual texts
        ParseTexts(element, item);

        // Links (Picture, PictureOff, etc.)
        ParseLinks(element, item);

        return item;
    }

    private static void ParseFont(XElement itemElement, HmiScreenItemInfo item)
    {
        // FontItem elements are inside MultiLingualFont
        var fontItem = itemElement.Descendants()
            .FirstOrDefault(e => Is(e, "FontItem"));

        if (fontItem is null) return;

        var fontAttrs = fontItem.Elements()
            .FirstOrDefault(e => Is(e, "AttributeList"));
        if (fontAttrs is null) return;

        item.FontFamily = GetText(fontAttrs, "FontFamily");
        item.FontSize = GetDouble(fontAttrs, "FontSize");
        item.FontStyle = GetText(fontAttrs, "FontStyle");
    }

    private static void ParseTagBindings(XElement itemElement, HmiScreenItemInfo item)
    {
        // 1) Tags from TagConnectionDynamic — check Tag elements by suffix
        foreach (var tagEl in itemElement.Descendants()
            .Where(e => Is(e, "Tag")))
        {
            var targetId = tagEl.Attribute("TargetID")?.Value;
            if (targetId != "@OpenLink") continue;

            var tagName = tagEl.Elements()
                .FirstOrDefault(e => Is(e, "Name"))?.Value?.Trim();
            if (string.IsNullOrEmpty(tagName)) continue;

            var dynamicLink = tagEl.Ancestors()
                .FirstOrDefault(e => Is(e, "TagConnectionDynamic"));

            var propertyName = GetText(
                dynamicLink?.Elements().FirstOrDefault(e => Is(e, "AttributeList")),
                "Name") ?? "";

            if (item.TagBindings.Any(b => b.TagName == tagName && b.PropertyName == propertyName))
                continue;

            item.TagBindings.Add(new HmiTagBindingInfo
            {
                PropertyName = propertyName,
                TagName = tagName
            });
        }

        // 2) Tags from Hmi.Screen.Property elements
        foreach (var propEl in itemElement.Descendants()
            .Where(e => Is(e, "Property")))
        {
            var propAttrs = propEl.Elements()
                .FirstOrDefault(e => Is(e, "AttributeList"));
            var propName = GetText(propAttrs, "Name") ?? "";

            foreach (var tagEl in propEl.Descendants()
                .Where(e => Is(e, "Tag")))
            {
                if (tagEl.Attribute("TargetID")?.Value != "@OpenLink") continue;
                var tagName = tagEl.Elements()
                    .FirstOrDefault(e => Is(e, "Name"))?.Value?.Trim();
                if (string.IsNullOrEmpty(tagName)) continue;

                if (item.TagBindings.Any(b => b.TagName == tagName && b.PropertyName == propName))
                    continue;

                item.TagBindings.Add(new HmiTagBindingInfo
                {
                    PropertyName = propName,
                    TagName = tagName
                });
            }
        }
    }

    private static void ParseAnimations(XElement itemElement, HmiScreenItemInfo item)
    {
        foreach (var animEl in itemElement.Descendants()
            .Where(e =>
            {
                var s = Suffix(e.Name.LocalName);
                return s == "RangeAppearanceAnimation" || s == "SingleBitVisibilityAnimation";
            }))
        {
            var animSuffix = Suffix(animEl.Name.LocalName);
            var isVisibility = animSuffix == "SingleBitVisibilityAnimation";

            var animAttrs = animEl.Elements()
                .FirstOrDefault(e => Is(e, "AttributeList"));

            var anim = new HmiAnimationInfo
            {
                AnimationType = isVisibility ? "Visibility" : "Appearance",
                PropertyName = GetText(animAttrs, "Name"),
                BitPosition = GetInt(animAttrs, "BitPosition"),
                VisibleWhenTrue = GetBool(animAttrs, "Visible")
            };

            // Find the trigger tag via TagElementTrigger
            var triggerEl = animEl.Descendants()
                .FirstOrDefault(e => Is(e, "TagElementTrigger"));
            if (triggerEl is not null)
            {
                foreach (var tagEl in triggerEl.Descendants()
                    .Where(e => Is(e, "Tag")))
                {
                    if (tagEl.Attribute("TargetID")?.Value == "@OpenLink")
                    {
                        anim.TagName = tagEl.Elements()
                            .FirstOrDefault(e => Is(e, "Name"))?.Value?.Trim();
                        break;
                    }
                }
            }

            // For appearance animations, extract Range values
            if (!isVisibility)
            {
                foreach (var rangeEl in animEl.Descendants()
                    .Where(e => Is(e, "Range")))
                {
                    var rangeAttrs = rangeEl.Elements()
                        .FirstOrDefault(e => Is(e, "AttributeList"));
                    if (rangeAttrs is null) continue;

                    anim.Ranges.Add(new HmiRangeInfo
                    {
                        LowerLimit = GetText(rangeAttrs, "LowerLimit"),
                        UpperLimit = GetText(rangeAttrs, "UpperLimit"),
                        BackColor = GetText(rangeAttrs, "BackColor"),
                        ForeColor = GetText(rangeAttrs, "ForeColor"),
                        FlashingType = GetText(rangeAttrs, "FlashingType")
                    });
                }
            }

            item.Animations.Add(anim);
        }
    }

    private static void ParseEvents(XElement itemElement, HmiScreenItemInfo item)
    {
        foreach (var eventEl in itemElement.Descendants()
            .Where(e => Is(e, "Event")))
        {
            var eventAttrs = eventEl.Elements()
                .FirstOrDefault(e => Is(e, "AttributeList"));
            var eventName = GetText(eventAttrs, "Name");
            if (string.IsNullOrEmpty(eventName)) continue;

            var evt = new HmiEventInfo { EventName = eventName };

            var funcEntry = eventEl.Descendants()
                .FirstOrDefault(e => Is(e, "FunctionListEntry"));
            if (funcEntry is not null)
            {
                var funcAttrs = funcEntry.Elements()
                    .FirstOrDefault(e => Is(e, "AttributeList"));
                evt.FunctionName = GetText(funcAttrs, "Name");
                evt.FunctionType = GetText(funcAttrs, "Type");

                foreach (var paramEl in funcEntry.Descendants()
                    .Where(e => Is(e, "FunctionListEntryParameter")))
                {
                    var paramAttrs = paramEl.Elements()
                        .FirstOrDefault(e => Is(e, "AttributeList"));
                    var paramName = GetText(paramAttrs, "Name");
                    string? paramValue = null;

                    // Value element in AttributeList
                    var valueEl = paramAttrs?.Elements()
                        .FirstOrDefault(e => Is(e, "Value"));
                    if (valueEl is not null)
                        paramValue = valueEl.Value?.Trim();

                    // LinkList for linked values (screen names, etc.)
                    var linkList = paramEl.Elements()
                        .FirstOrDefault(e => Is(e, "LinkList"));
                    if (linkList is not null)
                    {
                        var linkedEl = linkList.Elements().FirstOrDefault();
                        if (linkedEl is not null)
                        {
                            var linkName = linkedEl.Elements()
                                .FirstOrDefault(e => Is(e, "Name"));
                            if (linkName is not null)
                                paramValue = linkName.Value?.Trim();
                        }
                    }

                    evt.Parameters.Add(new HmiEventParamInfo
                    {
                        Name = paramName ?? "",
                        Value = paramValue
                    });
                }
            }

            item.Events.Add(evt);
        }
    }

    private static void ParseTexts(XElement itemElement, HmiScreenItemInfo item)
    {
        // MultilingualText is a plain element name (no Hmi prefix)
        foreach (var mlText in itemElement.Elements()
            .Where(e => Is(e, "MultilingualText")))
        {
            var propName = mlText.Attribute("CompositionName")?.Value ?? "Text";

            foreach (var textItem in mlText.Descendants()
                .Where(e => Is(e, "MultilingualTextItem")))
            {
                var textAttrs = textItem.Elements()
                    .FirstOrDefault(e => Is(e, "AttributeList"));
                var culture = GetText(textAttrs, "Culture");
                var text = GetHtmlText(textAttrs, "Text");

                if (!string.IsNullOrEmpty(text))
                {
                    item.Texts.Add(new HmiTextInfo
                    {
                        Property = propName,
                        Culture = culture ?? "",
                        Text = text
                    });
                }
            }
        }
    }

    private static void ParseLinks(XElement itemElement, HmiScreenItemInfo item)
    {
        var linkList = itemElement.Elements()
            .FirstOrDefault(e => Is(e, "LinkList"));
        if (linkList is null) return;

        foreach (var link in linkList.Elements())
        {
            var linkType = Suffix(link.Name.LocalName);
            if (linkType == "Tag") continue; // handled by ParseTagBindings

            var targetName = link.Elements()
                .FirstOrDefault(e => Is(e, "Name"))?.Value?.Trim();

            item.Links.Add(new HmiLinkInfo
            {
                LinkType = linkType,
                Target = targetName ?? ""
            });
        }
    }

    // --- Helpers ---

    private static string? GetText(XElement? attributeList, string elementName)
    {
        if (attributeList is null) return null;
        var el = attributeList.Elements()
            .FirstOrDefault(e => e.Name.LocalName == elementName);
        return el?.Value?.Trim();
    }

    /// <summary>
    /// Extract text from a Text element that may contain HTML like
    /// &lt;Text&gt;&lt;body&gt;&lt;p&gt;DISC CURRENT&lt;/p&gt;&lt;/body&gt;&lt;/Text&gt;
    /// The XML parser treats body/p as child elements, so .Value concatenates all text.
    /// </summary>
    private static string? GetHtmlText(XElement? attributeList, string elementName)
    {
        if (attributeList is null) return null;
        var el = attributeList.Elements()
            .FirstOrDefault(e => e.Name.LocalName == elementName);
        if (el is null) return null;

        // .Value concatenates all descendant text nodes (including body/p content)
        var text = el.Value?.Trim();

        // Filter out empty HTML like "<p />" or "<p><br /></p>"
        if (string.IsNullOrEmpty(text)) return null;
        if (text == "<p />" || text == "<p><br /></p>" || text == "<br />") return null;
        if (text.Replace("<p />", "").Replace("<br />", "").Trim() == "") return null;

        return text;
    }

    private static double? GetDouble(XElement? attrs, string elementName)
    {
        var val = GetText(attrs, elementName);
        if (val is not null && double.TryParse(val, NumberStyles.Float,
            CultureInfo.InvariantCulture, out var result))
            return result;
        return null;
    }

    private static int? GetInt(XElement? attrs, string elementName)
    {
        var val = GetText(attrs, elementName);
        if (val is not null && int.TryParse(val, NumberStyles.Integer,
            CultureInfo.InvariantCulture, out var result))
            return result;
        return null;
    }

    private static bool? GetBool(XElement? attrs, string elementName)
    {
        var val = GetText(attrs, elementName);
        if (val is null) return null;
        if (string.Equals(val, "true", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(val, "false", StringComparison.OrdinalIgnoreCase)) return false;
        return null;
    }
}
