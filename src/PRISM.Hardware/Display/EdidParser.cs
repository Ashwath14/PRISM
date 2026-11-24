using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;

namespace PRISM.Hardware.Display;

public class EdidInfo
{
    public string ModelName { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string ManufacturerCode { get; set; } = string.Empty;
    public int YearOfManufacture { get; set; } = 2025;
    public int WeekOfManufacture { get; set; } = 46;
}

public static class EdidParser
{
    public static EdidInfo? Parse(byte[]? edid)
    {
        if (edid == null || edid.Length < 128) return null;

        var info = new EdidInfo();

        // 1. Manufacturer Code (Bytes 8-9, compressed 5-bit ASCII)
        int mfgInt = (edid[8] << 8) | edid[9];
        char c1 = (char)(((mfgInt >> 10) & 0x1F) + '@');
        char c2 = (char)(((mfgInt >> 5) & 0x1F) + '@');
        char c3 = (char)((mfgInt & 0x1F) + '@');
        info.ManufacturerCode = $"{c1}{c2}{c3}".Trim();
        if (info.ManufacturerCode == "ACR") info.ManufacturerCode = "Acer";

        // 2. Week and Year of Manufacture
        info.WeekOfManufacture = edid[16];
        info.YearOfManufacture = 1990 + edid[17];

        // 3. Four 18-byte Descriptor Blocks (Offsets 54, 72, 90, 108)
        int[] descriptorOffsets = { 54, 72, 90, 108 };
        foreach (int off in descriptorOffsets)
        {
            if (off + 18 > edid.Length) break;

            // Display descriptor has 0x00 0x00 at first two bytes
            if (edid[off] == 0x00 && edid[off + 1] == 0x00)
            {
                byte tag = edid[off + 3];
                if (tag == 0xFC) // Monitor Model Name
                {
                    info.ModelName = Encoding.ASCII.GetString(edid, off + 5, 13).Trim('\0', '\r', '\n', ' ');
                }
                else if (tag == 0xFF) // Monitor Serial Number
                {
                    info.SerialNumber = Encoding.ASCII.GetString(edid, off + 5, 13).Trim('\0', '\r', '\n', ' ');
                }
            }
        }

        return info;
    }

    public static List<EdidInfo> GetConnectedMonitorEdids()
    {
        var list = new List<EdidInfo>();
        try
        {
            using var displayKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\DISPLAY");
            if (displayKey != null)
            {
                foreach (string deviceId in displayKey.GetSubKeyNames())
                {
                    using var subKey = displayKey.OpenSubKey(deviceId);
                    if (subKey == null) continue;

                    foreach (string instanceId in subKey.GetSubKeyNames())
                    {
                        using var instanceKey = subKey.OpenSubKey(instanceId + @"\Device Parameters");
                        if (instanceKey == null) continue;

                        if (instanceKey.GetValue("EDID") is byte[] edidBytes)
                        {
                            var parsed = Parse(edidBytes);
                            if (parsed != null && !string.IsNullOrEmpty(parsed.ModelName))
                            {
                                list.Add(parsed);
                            }
                        }
                    }
                }
            }
        }
        catch { }

        return list;
    }
}
