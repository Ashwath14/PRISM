using System.Collections.Generic;

namespace PRISM.Core.Models;

public class MonitorInfo
{
    public string Id { get; set; } = string.Empty;
    public string DevicePath { get; set; } = string.Empty;
    public string Name { get; set; } = "Acer ED270 X0";
    public string Manufacturer { get; set; } = "Acer";
    public string ConnectionType { get; set; } = "DisplayPort";
    public string SerialNumber { get; set; } = "154604D5A3W01";
    public string PnPId { get; set; } = "ACR0F95";
    public string HardwareKey => !string.IsNullOrEmpty(SerialNumber) && SerialNumber != "Unknown"
        ? $"{PnPId}_{SerialNumber}"
        : $"{PnPId}_{DevicePath}";

    public bool IsPrimary { get; set; } = true;
    public bool IsHardwareDdcCi { get; set; } = true;

    public int X { get; set; } = 0;
    public int Y { get; set; } = 0;
    public int Width { get; set; } = 1920;
    public int Height { get; set; } = 1080;
    
    public VcpFeatureReport Capabilities { get; set; } = new();
    public HardwareStatus Status { get; set; } = new();

    public int CurrentBrightness { get; set; } = 44;
    public int MinBrightness { get; set; } = 0;
    public int MaxBrightness { get; set; } = 100;

    public int CurrentContrast { get; set; } = 50;
    public int MinContrast { get; set; } = 0;
    public int MaxContrast { get; set; } = 100;

    public int RedGain { get; set; } = 50;
    public int GreenGain { get; set; } = 50;
    public int BlueGain { get; set; } = 50;

    public bool IsSubZeroDimEnabled { get; set; }
    public int SubZeroDimPercent { get; set; } = 0;

    public string CurrentInputSource { get; set; } = "DisplayPort-1";
    public List<string> SupportedInputs { get; set; } = new() { "DisplayPort-1", "HDMI-1", "HDMI-2" };

    public string PhysicalHandleStatus { get; set; } = "Handle Healthy (Allocated on-demand)";
    public int EdidWeek { get; set; } = 46;
    public int EdidYear { get; set; } = 2025;
}
