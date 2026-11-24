using System;
using System.Threading.Tasks;
using PRISM.Core.Common;
using PRISM.Core.Models;

namespace PRISM.Core.Interfaces;

public interface IWindowsBrightnessService
{
    WindowsBrightnessCapability QueryCapabilities(string? monitorHardwareKey = null);
    Task<Result<int>> GetWindowsBrightnessAsync(string? monitorHardwareKey = null);
    Task<Result> SetWindowsBrightnessAsync(int brightnessPercent, string? monitorHardwareKey = null);
    event EventHandler<int>? ExternalWindowsBrightnessChanged;
}
