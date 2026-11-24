namespace PRISM.Core.Models;

public enum ParametricFilterType
{
    Peaking,    // PK
    LowShelf,   // LSC
    HighShelf,  // HSC
    Notch,      // NO
    HighPass,   // HP
    LowPass     // LP
}

public class ParametricFilter
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public bool IsEnabled { get; set; } = true;
    public ParametricFilterType Type { get; set; } = ParametricFilterType.Peaking;
    
    // Frequency in Hz (20 - 20,000 Hz)
    public double Frequency { get; set; } = 1000.0;

    // Gain in dB (-15.0 to +15.0 dB) - ignored for HighPass/LowPass/Notch
    public double GainDb { get; set; } = 0.0;

    // Q-factor / bandwidth (0.1 to 10.0)
    public double Q { get; set; } = 1.414;
}
