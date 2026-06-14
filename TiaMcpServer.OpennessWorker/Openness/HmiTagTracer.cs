using System;
using System.Collections.Generic;
using System.Linq;
using Siemens.Engineering;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Traces HMI screen elements → PLC tags → block code references in one pass.
/// Reads screen tag bindings (and animation tags) via HmiScreenReader, builds the
/// block-code index ONCE, then resolves every bound tag against it.
/// </summary>
public static class HmiTagTracer
{
    private const int MaxTraces = 150;

    public static HmiTagTraceResultInfo Trace(
        Project project, string? projectPath, string? deviceName, string? screenName, string? plcNameFilter)
    {
        var result = new HmiTagTraceResultInfo
        {
            DeviceName = deviceName,
            PlcName = plcNameFilter,
        };

        var devices = HmiScreenReader.Read(project, deviceName, "detail", screenName);
        result.ScreenCount = devices.Sum(d => d.Screens.Count);

        // Collect (screen, element, tag) bindings from direct tag bindings and animations.
        var bindings = new List<Binding>();
        foreach (var dev in devices)
        {
            foreach (var scr in dev.Screens)
            {
                foreach (var item in scr.Items)
                {
                    if (item.TagBindings is not null)
                    {
                        foreach (var b in item.TagBindings)
                        {
                            if (!string.IsNullOrWhiteSpace(b.TagName))
                            {
                                bindings.Add(new Binding(scr.ScreenName, item.Name, item.TypeName,
                                    "TagBinding", b.PropertyName, b.TagName!));
                            }
                        }
                    }

                    if (item.Animations is not null)
                    {
                        foreach (var a in item.Animations)
                        {
                            if (!string.IsNullOrWhiteSpace(a.TagName))
                            {
                                bindings.Add(new Binding(scr.ScreenName, item.Name, item.TypeName,
                                    "Animation", a.AnimationType, a.TagName!));
                            }
                        }
                    }
                }
            }
        }

        result.BindingCount = bindings.Count;
        if (bindings.Count == 0)
        {
            return result;
        }

        // Build the block-code index once and reuse it for every tag.
        var index = BlockCodeIndexer.GetOrBuild(project, projectPath, plcNameFilter);
        result.TracedBlockCount = index.Blocks.Count;
        result.SkippedProtectedCount = index.SkippedProtected;

        var noRef = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var emitted = 0;
        foreach (var b in bindings)
        {
            if (emitted >= MaxTraces)
            {
                break;
            }

            var usage = CodeSearcher.TagUsageInIndex(index, plcNameFilter, b.Tag);
            result.Traces.Add(new HmiTagTraceEntryInfo
            {
                ScreenName = b.Screen,
                ElementName = b.Element,
                ElementType = b.ElementType,
                Source = b.Source,
                PropertyName = b.Property,
                Tag = b.Tag,
                ReferenceCount = usage.ReferenceCount,
                References = usage.References,
            });
            emitted++;

            if (usage.ReferenceCount == 0)
            {
                noRef.Add(b.Tag);
            }
        }

        result.TagsWithoutReferences = noRef.OrderBy(t => t).ToList();
        return result;
    }

    private sealed class Binding
    {
        public Binding(string screen, string element, string elementType, string source, string property, string tag)
        {
            Screen = screen;
            Element = element;
            ElementType = elementType;
            Source = source;
            Property = property;
            Tag = tag;
        }

        public string Screen { get; }
        public string Element { get; }
        public string ElementType { get; }
        public string Source { get; }
        public string Property { get; }
        public string Tag { get; }
    }
}
