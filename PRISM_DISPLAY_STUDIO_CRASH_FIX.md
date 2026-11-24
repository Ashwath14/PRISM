# PRISM — Display Studio Crash Fix Report
**Defect Severity:** P0  
**Resolution Status:** Resolved & Verified  
**Date:** November 20, 2026  

---

## 1. Overview of Changes

To permanently eliminate the `StaticResourceExtension` crash and guarantee that UI layout failures can never induce a recursive modal crash cascade, changes were implemented across three core layers:

1. **XAML Resource Layer (`PrismIcons.xaml`)**: Defined `WarningIconGeometry` vector geometry.
2. **Application Lifecycle & Error Trapping (`App.xaml.cs`)**: Re-engineered `DispatcherUnhandledException` to prevent modal re-entrancy, throttle identical signatures, dispatch at `ApplicationIdle`, and safely roll back to `HomeView`.
3. **Navigation & ViewModel Layer (`MainViewModel.cs`)**: Added guarded state transition wrappers with automatic fallback.

---

## 2. File-by-File Modifications

### A. `src/PRISM.App/Styles/PrismIcons.xaml`
Added the vector definition for `WarningIconGeometry`:
```xml
    <!-- System & Status Alert Icons -->
    <Geometry x:Key="WarningIconGeometry">
        M 12 2 L 1 21 H 23 Z M 12 9 V 14 M 12 17 H 12.01
    </Geometry>
```
*Purpose:* Provides the required 24x24 standard SVG triangle alert icon path required by `{StaticResource WarningIconGeometry}` in `DisplayStudioView.xaml`.

---

### B. `src/PRISM.App/App.xaml.cs`
Completely restructured the unhandled exception traps to prevent modal cascade and protect the UI thread:

1. **Re-entrancy Guard & Signature Throttling**:
```csharp
private static int _isShowingDialog = 0;
private static string _lastErrorSignature = "";
private static DateTime _lastErrorTime = DateTime.MinValue;
```

2. **Asynchronous Dispatch at `ApplicationIdle`**:
```csharp
Current.DispatcherUnhandledException += (s, exArgs) =>
{
    StartupLogger.Log($"[DISPATCHER UNHANDLED UI EXCEPTION] {exArgs.Exception}");
    exArgs.Handled = true;

    string errSig = $"{exArgs.Exception.GetType().Name}:{exArgs.Exception.Message}";
    if (errSig == _lastErrorSignature && (DateTime.Now - _lastErrorTime).TotalSeconds < 3.0)
    {
        StartupLogger.Log("[DISPATCHER UI EXCEPTION THROTTLED] Suppressing duplicate error dialog.");
        TryRecoverMainView();
        return;
    }
    _lastErrorSignature = errSig;
    _lastErrorTime = DateTime.Now;

    if (Interlocked.CompareExchange(ref _isShowingDialog, 1, 0) == 0)
    {
        Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            try
            {
                TryRecoverMainView();
                System.Windows.MessageBox.Show(
                    $"PRISM UI Error:\n{exArgs.Exception.Message}\n\nStack:\n{exArgs.Exception.StackTrace}",
                    "PRISM Interface Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                Interlocked.Exchange(ref _isShowingDialog, 0);
            }
        }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }
};
```

3. **Active View Recovery Routine**:
```csharp
private void TryRecoverMainView()
{
    try
    {
        if (_mainWindow?.DataContext is MainViewModel mainVm)
        {
            if (mainVm.CurrentView != mainVm.HomeVm)
            {
                StartupLogger.Log("[RECOVERY] Active view triggered layout failure. Gracefully recovering to HomeView.");
                mainVm.Navigate("Home");
            }
        }
    }
    catch (Exception ex)
    {
        StartupLogger.Log($"[RECOVERY ERROR] Could not recover to HomeView: {ex.Message}");
    }
}
```

4. **TaskScheduler Unobserved Exception Handling**:
```csharp
TaskScheduler.UnobservedTaskException += (s, exArgs) =>
{
    StartupLogger.Log($"[UNOBSERVED TASK EXCEPTION] {exArgs.Exception}");
    exArgs.SetObserved();
};
```

---

### C. `src/PRISM.App/ViewModels/MainViewModel.cs`
Wrapped the navigation execution in try/catch to ensure ViewModel state remains valid even in the event of an unexpected runtime failure during navigation:
```csharp
public void Navigate(string destination)
{
    try
    {
        switch (destination?.ToLowerInvariant())
        {
            case "home":
                CurrentView = HomeVm;
                IsHomeSelected = true;
                CurrentSectionTitle = "Overview";
                break;
            case "display":
                CurrentView = DisplayVm;
                IsDisplaySelected = true;
                CurrentSectionTitle = "Display Studio";
                break;
            case "audio":
                CurrentView = AudioVm;
                IsAudioSelected = true;
                CurrentSectionTitle = "Audio Studio";
                break;
            case "environments":
                CurrentView = EnvironmentsVm;
                IsEnvironmentsSelected = true;
                CurrentSectionTitle = "Environments";
                break;
            case "settings":
                CurrentView = SettingsVm;
                IsSettingsSelected = true;
                CurrentSectionTitle = "Settings";
                break;
        }
    }
    catch (Exception ex)
    {
        CurrentView = HomeVm;
        IsHomeSelected = true;
        CurrentSectionTitle = "Overview";
    }
}
```

---

### D. `tests/PRISM.Tests/DisplayStudioStabilityTests.cs`
Created automated regression tests to prevent any future resource regression:
- `PrismIcons_ContainsWarningIconGeometry`: Verifies key presence directly in `PrismIcons.xaml`.
- `AllStaticResourceKeysInAllXamlFiles_AreDefinedInResourceDictionaries`: Parses every `.xaml` file in the solution, extracts every `{StaticResource Key}` reference, and validates that each key is declared in a merged dictionary.
- `RepeatedNavigation_DisplayToHomeAndBack_20Times_Succeeds`: Performs rapid automated navigation across Home, Display, Audio, Environments, and Settings 20 consecutive times.
