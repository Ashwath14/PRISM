using System;
using System.Collections.Generic;

namespace PRISM.Core.Models;

public class HeadphoneProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Brand { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string Name => string.IsNullOrWhiteSpace(Brand) ? Model : $"{Brand} {Model}";
    public string TargetCurve { get; set; } = "Harman Over-Ear 2018";
    public string Source { get; set; } = "AutoEq / Oratory1990";
    public string Description { get; set; } = "Measurement-based acoustic correction to neutral Harman target.";
    public double PreampDb { get; set; } = -3.0;
    public List<ParametricFilter> ParametricFilters { get; set; } = new();
    public bool IsMeasurementCorrection { get; set; } = true;
    public bool IsCustom { get; set; } = false;
    public string? BoundDeviceId { get; set; }
}
