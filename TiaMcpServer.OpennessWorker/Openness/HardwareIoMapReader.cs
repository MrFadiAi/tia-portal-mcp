using System;
using System.Collections.Generic;
using System.Linq;
using Siemens.Engineering;
using Siemens.Engineering.HW;
using TiaMcpServer.Contracts;

namespace TiaMcpServer.OpennessWorker.Openness;

/// <summary>
/// Reads the structured I/O map for one device item (includeIoDetails): addresses (I/O type,
/// start, length, controller association) and channels (number, I/O type, type, dynamic bit
/// address + width → formatted logical address, exact tag matches). Each member is read
/// individually guarded; degradation notes go to stderr like the rest of our reader AND to the
/// caller's notes list (surfaced as HardwareConfigInfo.IoNotes).
/// </summary>
public static class HardwareIoMapReader
{
    public static DeviceItemIoDetailsInfo Read(
        DeviceItem item,
        string itemDescription,
        List<string> notes,
        IoTagIndex? tagIndex)
    {
        var details = new DeviceItemIoDetailsInfo();
        var addressRecords = new List<IoAddressRecord>();

        ReadAddresses(item, itemDescription, notes, details, addressRecords);
        ReadChannels(item, itemDescription, notes, tagIndex, addressRecords, details);

        details.Addresses = details.Addresses
            .OrderBy(address => address.IoType, StringComparer.Ordinal)
            .ThenBy(address => address.StartAddress)
            .ThenBy(address => address.Length)
            .ToList();
        details.Channels = details.Channels
            .OrderBy(channel => channel.Number)
            .ThenBy(channel => channel.Type, StringComparer.Ordinal)
            .ToList();

        return details;
    }

    /// <summary>Total entries a read contributed (for the caller's IoDetailBudget).</summary>
    public static int EntryCount(DeviceItemIoDetailsInfo details)
        => details.Addresses.Count + details.Channels.Count;

    private static void ReadAddresses(
        DeviceItem item,
        string itemDescription,
        List<string> notes,
        DeviceItemIoDetailsInfo details,
        List<IoAddressRecord> addressRecords)
    {
        try
        {
            foreach (Address address in item.Addresses)
            {
                try
                {
                    var info = new IoAddressInfo
                    {
                        IoType = ReadOptionalEnumName(
                            () => address.IoType,
                            $"device item '{itemDescription}' address I/O type",
                            notes),
                        StartAddress = ReadOptionalNonNegativeInt(
                            () => address.StartAddress,
                            $"device item '{itemDescription}' address start address",
                            notes),
                        Length = ReadOptionalNonNegativeInt(
                            () => address.Length,
                            $"device item '{itemDescription}' address length",
                            notes),
                    };

                    var record = new IoAddressRecord
                    {
                        IoType = info.IoType,
                        StartAddress = info.StartAddress,
                        Length = info.Length,
                    };

                    ReadAddressControllers(address, itemDescription, notes, info, record);

                    details.Addresses.Add(info);
                    addressRecords.Add(record);
                }
                catch (EngineeringException ex)
                {
                    AddNote(notes, $"Skipped an address while reading device item '{itemDescription}': {ex.Message}");
                }
            }
        }
        catch (EngineeringException ex)
        {
            AddNote(notes, $"Could not enumerate addresses while reading device item '{itemDescription}': {ex.Message}");
        }
    }

    private static void ReadAddressControllers(
        Address address,
        string itemDescription,
        List<string> notes,
        IoAddressInfo info,
        IoAddressRecord record)
    {
        var controllerReadable = true;
        try
        {
            foreach (var controller in address.AddressControllers)
            {
                var controllerName = FindParentDeviceName(controller.OwnedBy, notes);
                if (controllerName is null)
                {
                    controllerReadable = false;
                    continue;
                }

                info.ControllerNames.Add(controllerName);
            }
        }
        catch (EngineeringException ex)
        {
            controllerReadable = false;
            AddNote(notes, $"Could not read address controllers while reading device item '{itemDescription}': {ex.Message}");
        }

        info.ControllerNames = info.ControllerNames
            .OrderBy(name => name, StringComparer.Ordinal)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        record.ControllerNames = info.ControllerNames;
        record.ControllerAssociationReadable = controllerReadable;
    }

    private static void ReadChannels(
        DeviceItem item,
        string itemDescription,
        List<string> notes,
        IoTagIndex? tagIndex,
        IReadOnlyList<IoAddressRecord> addressRecords,
        DeviceItemIoDetailsInfo details)
    {
        try
        {
            foreach (Channel channel in item.Channels)
            {
                try
                {
                    var channelInfo = new IoChannelInfo
                    {
                        Number = ReadOptionalInt(
                            () => channel.Number,
                            $"device item '{itemDescription}' channel number",
                            notes),
                        IoType = ReadOptionalEnumName(
                            () => channel.IoType,
                            $"device item '{itemDescription}' channel I/O type",
                            notes),
                        Type = ReadOptionalEnumName(
                            () => channel.Type,
                            $"device item '{itemDescription}' channel type",
                            notes),
                        ChannelAddressBits = ReadDynamicIntAttribute(
                            (IEngineeringObject)channel,
                            "ChannelAddress",
                            $"device item '{itemDescription}' channel address",
                            notes),
                        ChannelWidthBits = ReadDynamicUIntAttribute(
                            (IEngineeringObject)channel,
                            "ChannelWidth",
                            $"device item '{itemDescription}' channel width",
                            notes),
                    };
                    channelInfo.LogicalAddress = IoLogicalAddressFormatter.FormatLogicalAddress(
                        channelInfo.IoType,
                        channelInfo.ChannelAddressBits,
                        channelInfo.ChannelWidthBits);

                    channelInfo.TagMatches = ReadChannelTagMatches(
                        channelInfo,
                        addressRecords,
                        tagIndex,
                        itemDescription,
                        notes);

                    details.Channels.Add(channelInfo);
                }
                catch (EngineeringException ex)
                {
                    AddNote(notes, $"Skipped a channel while reading device item '{itemDescription}': {ex.Message}");
                }
            }
        }
        catch (EngineeringException ex)
        {
            AddNote(notes, $"Could not enumerate channels while reading device item '{itemDescription}': {ex.Message}");
        }
    }

    private static List<IoTagMatchInfo> ReadChannelTagMatches(
        IoChannelInfo channelInfo,
        IReadOnlyList<IoAddressRecord> addressRecords,
        IoTagIndex? tagIndex,
        string itemDescription,
        List<string> notes)
    {
        var matches = new List<IoTagMatchInfo>();
        if (tagIndex is null)
        {
            return matches;
        }

        var resolution = IoChannelControllerResolver.Resolve(
            channelInfo.IoType,
            channelInfo.ChannelAddressBits,
            channelInfo.ChannelWidthBits,
            addressRecords,
            itemDescription);

        if (resolution.DiagnosticMessage is not null)
        {
            AddNote(notes, resolution.DiagnosticMessage);
        }

        if (!resolution.IsTargetMatch(tagIndex.PlcDeviceName))
        {
            return matches;
        }

        var channelArea = IoLogicalAddressFormatter.NormalizeArea(channelInfo.IoType);
        if (channelArea is null
            || channelInfo.ChannelAddressBits is null
            || channelInfo.ChannelWidthBits is null
            || channelInfo.ChannelAddressBits < 0)
        {
            return matches;
        }

        var channelAddress = new IoAbsoluteIoAddress(
            channelArea,
            new IoAbsoluteBitInterval(
                channelInfo.ChannelAddressBits.Value,
                channelInfo.ChannelWidthBits.Value));
        foreach (var candidate in tagIndex.FindMatches(channelAddress))
        {
            matches.Add(new IoTagMatchInfo
            {
                Name = candidate.Name,
                DataType = candidate.DataType,
                LogicalAddress = candidate.LogicalAddress,
                TableName = candidate.TableName,
                FolderPath = candidate.FolderPath,
            });
        }

        return matches;
    }

    /// <summary>Walk up the engineering-parent chain to the owning device's name (the identity
    /// controller association is compared against).</summary>
    private static string? FindParentDeviceName(IEngineeringObject? candidate, List<string> notes)
    {
        var current = candidate;
        while (current is not null)
        {
            if (current is Device device)
            {
                try
                {
                    return device.Name;
                }
                catch (EngineeringException ex)
                {
                    AddNote(notes, $"Could not read the owning device name of an address controller: {ex.Message}");
                    return null;
                }
            }

            try
            {
                current = current.Parent;
            }
            catch (EngineeringException ex)
            {
                AddNote(notes, $"Could not read parent while resolving an address controller's device: {ex.Message}");
                return null;
            }
        }

        return null;
    }

    private static int? ReadDynamicIntAttribute(
        IEngineeringObject engineeringObject,
        string attributeName,
        string description,
        List<string> notes)
    {
        try
        {
            var value = engineeringObject.GetAttribute(attributeName);
            var coerced = CoerceInt32(value);
            if (coerced is null && value is not null)
            {
                AddNote(notes, $"Could not read {description}: attribute '{attributeName}' had an unexpected CLR type or value.");
                return null;
            }

            return coerced;
        }
        catch (EngineeringException ex)
        {
            AddNote(notes, $"Could not read {description}: {ex.Message}");
            return null;
        }
    }

    private static uint? ReadDynamicUIntAttribute(
        IEngineeringObject engineeringObject,
        string attributeName,
        string description,
        List<string> notes)
    {
        try
        {
            var value = engineeringObject.GetAttribute(attributeName);
            var coerced = CoerceUInt32(value);
            if (coerced is null && value is not null)
            {
                AddNote(notes, $"Could not read {description}: attribute '{attributeName}' had an unexpected CLR type or value.");
                return null;
            }

            return coerced;
        }
        catch (EngineeringException ex)
        {
            AddNote(notes, $"Could not read {description}: {ex.Message}");
            return null;
        }
    }

    private static int? CoerceInt32(object? value)
        => value switch
        {
            null => null,
            int v => v,
            byte v => v,
            sbyte v => v,
            short v => v,
            ushort v => v,
            long v when v is >= int.MinValue and <= int.MaxValue => (int)v,
            ulong v when v <= int.MaxValue => (int)v,
            _ => null,
        };

    private static uint? CoerceUInt32(object? value)
        => value switch
        {
            null => null,
            uint v => v,
            byte v => v,
            sbyte v when v >= 0 => (uint)v,
            ushort v => v,
            short v when v >= 0 => (uint)v,
            int v when v >= 0 => (uint)v,
            long v when v is >= 0 and <= uint.MaxValue => (uint)v,
            ulong v when v <= uint.MaxValue => (uint)v,
            _ => null,
        };

    private static int? ReadOptionalNonNegativeInt(
        Func<int> read,
        string description,
        List<string> notes)
    {
        try
        {
            var value = read();
            if (value < 0)
            {
                AddNote(notes, $"Could not read {description}: the reported value was negative.");
                return null;
            }

            return value;
        }
        catch (EngineeringException ex)
        {
            AddNote(notes, $"Could not read {description}: {ex.Message}");
            return null;
        }
    }

    private static int? ReadOptionalInt(
        Func<int> read,
        string description,
        List<string> notes)
    {
        try
        {
            return read();
        }
        catch (EngineeringException ex)
        {
            AddNote(notes, $"Could not read {description}: {ex.Message}");
            return null;
        }
    }

    private static string? ReadOptionalEnumName<TEnum>(
        Func<TEnum> read,
        string description,
        List<string> notes)
        where TEnum : struct, Enum
    {
        try
        {
            return Enum.Format(typeof(TEnum), read(), "G");
        }
        catch (Exception ex)
        {
            AddNote(notes, $"Could not read {description}: {ex.Message}");
            return null;
        }
    }

    private static void AddNote(List<string> notes, string message)
    {
        notes.Add(message);
        Console.Error.WriteLine(message);
    }
}
