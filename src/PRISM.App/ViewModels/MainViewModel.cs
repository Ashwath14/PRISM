using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace PRISM.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private ObservableObject _currentView;

    [ObservableProperty]
    private string _currentSectionTitle = "Overview";

    [ObservableProperty]
    private string _activeEnvironmentName = "Daylight Focus";

    [ObservableProperty] private bool _isHomeSelected = true;
    [ObservableProperty] private bool _isDisplaySelected = false;
    [ObservableProperty] private bool _isAudioSelected = false;
    [ObservableProperty] private bool _isEnvironmentsSelected = false;
    [ObservableProperty] private bool _isSettingsSelected = false;
    [ObservableProperty] private bool _isDiagnosticsSelected = false;

    [ObservableProperty]
    private string _statusBusText = "Hardware Bus: I2C (100kHz) • Acer ED270 X0 Active";

    [ObservableProperty]
    private string _displayStatusText = "Connected";

    [ObservableProperty]
    private string _audioStatusText = "Active";

    [ObservableProperty]
    private string _systemStatusText = "All services are running normally.";

    [ObservableProperty]
    private string _appVersionText = "v2.0.0";

    public HomeViewModel HomeVm { get; }
    public DisplayStudioViewModel DisplayVm { get; }
    public AudioStudioViewModel AudioVm { get; }
    public EnvironmentsViewModel EnvironmentsVm { get; }
    public SettingsViewModel SettingsVm { get; }

    public MainViewModel(
        HomeViewModel homeVm,
        DisplayStudioViewModel displayVm,
        AudioStudioViewModel audioVm,
        EnvironmentsViewModel environmentsVm,
        SettingsViewModel settingsVm)
    {
        HomeVm = homeVm;
        DisplayVm = displayVm;
        AudioVm = audioVm;
        EnvironmentsVm = environmentsVm;
        SettingsVm = settingsVm;

        _currentView = HomeVm;

        // Synchronize studios with Home
        HomeVm.AttachStudios(DisplayVm, AudioVm);

        // Wire navigation from Home
        HomeVm.NavigationRequested += dest => Navigate(dest);

        // Wire environment selection from Home
        HomeVm.SelectEnvironmentRequested += async name =>
        {
            var preset = EnvironmentsVm.Presets.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                                                                   p.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase));
            if (preset != null)
            {
                await EnvironmentsVm.ApplyEnvironment(preset);
            }
        };

        // Wire environment change notifications
        EnvironmentsVm.EnvironmentApplied += (s, res) =>
        {
            ActiveEnvironmentName = res.EnvironmentName;
            HomeVm.UpdateActiveEnvironment(res.EnvironmentName);
        };
    }

    [RelayCommand]
    public void OpenDisplayStudio() => Navigate("Display");

    [RelayCommand]
    public void OpenAudioStudio() => Navigate("Audio");

    [RelayCommand]
    public void OpenEnvironments() => Navigate("Environments");

    [RelayCommand]
    public void ToggleNightMode()
    {
        DisplayVm.IsSubZeroEnabled = !DisplayVm.IsSubZeroEnabled;
        HomeVm.IsSubZeroDimEnabled = DisplayVm.IsSubZeroEnabled;
    }

    [RelayCommand]
    public async Task ToggleMute()
    {
        await AudioVm.ToggleMuteCommand.ExecuteAsync(null);
        HomeVm.IsMuted = AudioVm.IsMuted;
    }

    [RelayCommand]
    public void OpenDiagnostics() => Navigate("Diagnostics");

    [RelayCommand]
    public void Navigate(string destination)
    {
        try
        {
            IsHomeSelected = destination == "Home";
            IsDisplaySelected = destination == "Display";
            IsAudioSelected = destination == "Audio";
            IsEnvironmentsSelected = destination == "Environments";
            IsSettingsSelected = destination == "Settings";
            IsDiagnosticsSelected = destination == "Diagnostics";

            AudioVm.IsVisualizerActive = IsAudioSelected;

            switch (destination)
            {
                case "Home":
                    CurrentView = HomeVm;
                    CurrentSectionTitle = "Overview";
                    break;
                case "Display":
                    CurrentView = DisplayVm;
                    CurrentSectionTitle = "Display Studio";
                    break;
                case "Audio":
                    CurrentView = AudioVm;
                    CurrentSectionTitle = "Audio Studio";
                    break;
                case "Environments":
                    CurrentView = EnvironmentsVm;
                    CurrentSectionTitle = "Environments";
                    break;
                case "Settings":
                    CurrentView = SettingsVm;
                    CurrentSectionTitle = "Settings & Diagnostics";
                    break;
                case "Diagnostics":
                    CurrentView = SettingsVm;
                    CurrentSectionTitle = "Diagnostics";
                    break;
                default:
                    CurrentView = HomeVm;
                    CurrentSectionTitle = "Overview";
                    IsHomeSelected = true;
                    break;
            }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NAVIGATION FAILURE] Error navigating to '{destination}': {ex}");
            // Safe fallback to Home
            CurrentView = HomeVm;
            CurrentSectionTitle = "Overview";
            IsHomeSelected = true;
            IsDisplaySelected = false;
            IsAudioSelected = false;
            IsEnvironmentsSelected = false;
            IsSettingsSelected = false;
            IsDiagnosticsSelected = false;
            AudioVm.IsVisualizerActive = false;
        }
    }
}
