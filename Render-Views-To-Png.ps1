Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase
Add-Type -AssemblyName System.Drawing

$script = {
    param($outputPath, $viewType)

    try {
        # Load PRISM assemblies
        $binDir = "$PSScriptRoot\publish\PRISM"
        [System.Reflection.Assembly]::LoadFrom("$binDir\PRISM.Core.dll") | Out-Null
        [System.Reflection.Assembly]::LoadFrom("$binDir\PRISM.Hardware.dll") | Out-Null
        $appAsm = [System.Reflection.Assembly]::LoadFrom("$binDir\PRISM.dll")

        # Initialize WPF Application & Resource Dictionaries
        if ([System.Windows.Application]::Current -eq $null) {
            $app = New-Object System.Windows.Application
            $app.ShutdownMode = [System.Windows.ShutdownMode]::OnExplicitShutdown

            # Load resource dictionaries
            $dictUris = @(
                "pack://application:,,,/PRISM;component/Styles/PrismColors.xaml",
                "pack://application:,,,/PRISM;component/Styles/PrismIcons.xaml",
                "pack://application:,,,/PRISM;component/Styles/PrismTheme.xaml"
            )
            foreach ($uri in $dictUris) {
                $dict = New-Object System.Windows.ResourceDictionary
                $dict.Source = New-Object System.Uri($uri, [System.UriKind]::Absolute)
                $app.Resources.MergedDictionaries.Add($dict)
            }
        }

        # Instantiate Target View & ViewModel
        $view = $null
        $width = 1000
        $height = 1100

        if ($viewType -eq "Display") {
            $view = New-Object PRISM.App.Views.DisplayStudioView
            $resService = New-Object PRISM.Hardware.Display.DisplayResolutionService
            $displayService = New-Object PRISM.Hardware.Display.PhysicalDisplayService
            $profileService = New-Object PRISM.Hardware.Storage.ProfileStorageService("$env:APPDATA\PRISM")
            $vm = New-Object PRISM.App.ViewModels.DisplayStudioViewModel($displayService, $profileService, $resService)
            $view.DataContext = $vm
            $height = 1200
        } elseif ($viewType -eq "Audio") {
            $view = New-Object PRISM.App.Views.AudioStudioView
            $audioService = New-Object PRISM.Hardware.Audio.CoreAudioDeviceService
            $apoBridge = New-Object PRISM.Hardware.Audio.EqualizerApoBridgeService
            $profileService = New-Object PRISM.Hardware.Storage.ProfileStorageService("$env:APPDATA\PRISM")
            $vm = New-Object PRISM.App.ViewModels.AudioStudioViewModel($audioService, $apoBridge, $profileService, $null)
            $view.DataContext = $vm
            $height = 1350
        }

        if ($view -ne $null) {
            $container = New-Object System.Windows.Controls.Border
            $container.Background = [System.Windows.Application]::Current.Resources["PrismCanvasBrush"]
            $container.Child = $view

            $size = New-Object System.Windows.Size($width, $height)
            $container.Measure($size)
            $rect = New-Object System.Windows.Rect(0, 0, $width, $height)
            $container.Arrange($rect)
            $container.UpdateLayout()

            $dpiX = 96
            $dpiY = 96
            $rtb = New-Object System.Windows.Media.Imaging.RenderTargetBitmap($width, $height, $dpiX, $dpiY, [System.Windows.Media.PixelFormats]::Pbgra32)
            $rtb.Render($container)

            $encoder = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
            $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($rtb))

            $fs = [System.IO.File]::Create($outputPath)
            $encoder.Save($fs)
            $fs.Close()

            Write-Host "[+] Successfully rendered $viewType view to $outputPath" -ForegroundColor Green
        }
    } catch {
        Write-Error "Error rendering view: $_"
        Write-Error $_.ScriptStackTrace
    }
}

# Run in an STA thread as required by WPF
$thread = New-Object System.Threading.Thread([System.Threading.ParameterizedThreadStart]{
    param($args)
    & $script $args[0] $args[1]
})
$thread.SetApartmentState([System.Threading.ApartmentState]::STA)
$thread.Start(@("$PSScriptRoot\PRISM_AudioStudio_Rendered.png", "Audio"))
$thread.Join()

$thread2 = New-Object System.Threading.Thread([System.Threading.ParameterizedThreadStart]{
    param($args)
    & $script $args[0] $args[1]
})
$thread2.SetApartmentState([System.Threading.ApartmentState]::STA)
$thread2.Start(@("$PSScriptRoot\PRISM_DisplayStudio_Rendered.png", "Display"))
$thread2.Join()
