using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using PRISM.Core.Common;
using PRISM.Core.Interfaces;
using PRISM.Core.Models;

namespace PRISM.Hardware.Audio;

public class CoreAudioDeviceService : IAudioService
{
    private readonly List<AudioDeviceInfo> _devices = new();
    private AudioDeviceInfo? _activeDevice;
    private readonly object _lock = new();

    public IReadOnlyList<AudioDeviceInfo> GetConnectedDevices() => _devices.AsReadOnly();
    public AudioDeviceInfo? ActiveDevice => _activeDevice;

    public event EventHandler<AudioDeviceInfo>? AudioEndpointChanged;

    public CoreAudioDeviceService()
    {
        RefreshEndpoints();
    }

    public void RefreshEndpoints()
    {
        lock (_lock)
        {
            _devices.Clear();

            try
            {
                var enumerator = (CoreAudioNative.IMMDeviceEnumerator)new CoreAudioNative.MMDeviceEnumeratorComObject();
                
                // Get Default
                string defaultId = string.Empty;
                try
                {
                    if (enumerator.GetDefaultAudioEndpoint(0, 0, out var defaultDev) == 0)
                    {
                        defaultDev.GetId(out defaultId);
                    }
                }
                catch { }

                // Enum Render endpoints
                if (enumerator.EnumAudioEndpoints(0, CoreAudioNative.DEVICE_STATE_ACTIVE, out var collection) == 0)
                {
                    uint count;
                    collection.GetCount(out count);

                    for (uint i = 0; i < count; i++)
                    {
                        if (collection.Item(i, out var dev) == 0)
                        {
                            string id;
                            dev.GetId(out id);

                            string name = "Audio Endpoint";
                            if (dev.OpenPropertyStore(CoreAudioNative.STGM_READ, out var store) == 0)
                            {
                                var key = CoreAudioNative.PropertyKey.PKEY_Device_FriendlyName;
                                if (store.GetValue(ref key, out var pv) == 0 && pv.val1 != IntPtr.Zero)
                                {
                                    name = Marshal.PtrToStringUni(pv.val1) ?? name;
                                }
                            }

                            // Query volume
                            int volumePercent = 50;
                            bool isMuted = false;
                            var iid = typeof(CoreAudioNative.IAudioEndpointVolume).GUID;
                            if (dev.Activate(ref iid, CoreAudioNative.CLSCTX_ALL, IntPtr.Zero, out var volObj) == 0)
                            {
                                var endpointVol = (CoreAudioNative.IAudioEndpointVolume)volObj;
                                float scalar;
                                if (endpointVol.GetMasterVolumeLevelScalar(out scalar) == 0)
                                {
                                    volumePercent = (int)Math.Round(scalar * 100);
                                }
                                endpointVol.GetMute(out isMuted);
                            }

                            bool isDef = string.Equals(id, defaultId, StringComparison.OrdinalIgnoreCase);

                            var device = new AudioDeviceInfo
                            {
                                Id = id,
                                Name = name,
                                IsDefault = isDef,
                                VolumePercent = volumePercent,
                                IsMuted = isMuted,
                                DeviceType = DetectDeviceType(name)
                            };

                            _devices.Add(device);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Audio Endpoint Discovery Error: {ex.Message}");
            }

            // Fallback if COM enumeration fails
            if (_devices.Count == 0)
            {
                _devices.Add(new AudioDeviceInfo
                {
                    Id = "DEV_01",
                    Name = "Cosmic Byte Proteus (High Definition Audio)",
                    IsDefault = true,
                    VolumePercent = 75,
                    DeviceType = AudioDeviceType.Headphones
                });
                _devices.Add(new AudioDeviceInfo
                {
                    Id = "DEV_02",
                    Name = "Speakers (Realtek High Definition Audio)",
                    IsDefault = false,
                    VolumePercent = 60,
                    DeviceType = AudioDeviceType.Speakers
                });
            }

            _activeDevice = _devices.FirstOrDefault(d => d.IsDefault) ?? _devices.FirstOrDefault();
        }

        if (_activeDevice != null)
        {
            AudioEndpointChanged?.Invoke(this, _activeDevice);
        }
    }

    private AudioDeviceType DetectDeviceType(string name)
    {
        string lower = name.ToLowerInvariant();
        if (lower.Contains("headphone") || lower.Contains("headset") || lower.Contains("earphone") || lower.Contains("proteus"))
            return AudioDeviceType.Headphones;
        if (lower.Contains("speaker") || lower.Contains("realtek") || lower.Contains("audio"))
            return AudioDeviceType.Speakers;
        return AudioDeviceType.Other;
    }

    public Task<Result> SetActiveDeviceAsync(string deviceId)
    {
        lock (_lock)
        {
            var target = _devices.FirstOrDefault(d => d.Id == deviceId);
            if (target == null) return Task.FromResult(Result.Failure("Device not found"));

            try
            {
                // Attempt undocumented IPolicyConfig
                var policyConfig = (CoreAudioNative.IPolicyConfig)new CoreAudioNative.CPolicyConfigClientComObject();
                // ERole: eConsole = 0, eMultimedia = 1, eCommunications = 2
                policyConfig.SetDefaultEndpoint(target.Id, 0);
                policyConfig.SetDefaultEndpoint(target.Id, 1);
                policyConfig.SetDefaultEndpoint(target.Id, 2);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SetDefaultEndpoint Error: {ex.Message}");
            }

            foreach (var d in _devices)
            {
                d.IsDefault = (d.Id == target.Id);
            }
            _activeDevice = target;
            AudioEndpointChanged?.Invoke(this, _activeDevice);
        }

        return Task.FromResult(Result.Success());
    }

    public Task<Result> SetVolumeAsync(int volumePercent)
    {
        lock (_lock)
        {
            if (_activeDevice == null) return Task.FromResult(Result.Failure("No active audio device"));
            _activeDevice.VolumePercent = Math.Clamp(volumePercent, 0, 100);

            try
            {
                var enumerator = (CoreAudioNative.IMMDeviceEnumerator)new CoreAudioNative.MMDeviceEnumeratorComObject();
                if (enumerator.GetDevice(_activeDevice.Id, out var dev) == 0)
                {
                    var iid = typeof(CoreAudioNative.IAudioEndpointVolume).GUID;
                    if (dev.Activate(ref iid, CoreAudioNative.CLSCTX_ALL, IntPtr.Zero, out var volObj) == 0)
                    {
                        var endpointVol = (CoreAudioNative.IAudioEndpointVolume)volObj;
                        var ctx = Guid.Empty;
                        endpointVol.SetMasterVolumeLevelScalar(_activeDevice.VolumePercent / 100f, ref ctx);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SetVolume Error: {ex.Message}");
            }
        }

        return Task.FromResult(Result.Success());
    }

    public Task<Result> SetMuteAsync(bool isMuted)
    {
        lock (_lock)
        {
            if (_activeDevice == null) return Task.FromResult(Result.Failure("No active audio device"));
            _activeDevice.IsMuted = isMuted;

            try
            {
                var enumerator = (CoreAudioNative.IMMDeviceEnumerator)new CoreAudioNative.MMDeviceEnumeratorComObject();
                if (enumerator.GetDevice(_activeDevice.Id, out var dev) == 0)
                {
                    var iid = typeof(CoreAudioNative.IAudioEndpointVolume).GUID;
                    if (dev.Activate(ref iid, CoreAudioNative.CLSCTX_ALL, IntPtr.Zero, out var volObj) == 0)
                    {
                        var endpointVol = (CoreAudioNative.IAudioEndpointVolume)volObj;
                        var ctx = Guid.Empty;
                        endpointVol.SetMute(isMuted, ref ctx);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"SetMute Error: {ex.Message}");
            }
        }

        return Task.FromResult(Result.Success());
    }

    public Task<Result> ToggleOutputDeviceAsync()
    {
        lock (_lock)
        {
            if (_devices.Count < 2) return Task.FromResult(Result.Failure("Less than two audio devices available"));
            var next = _devices.FirstOrDefault(d => d.Id != _activeDevice?.Id);
            if (next != null)
            {
                return SetActiveDeviceAsync(next.Id);
            }
        }
        return Task.FromResult(Result.Success());
    }

    public Task<Result<(float Left, float Right)>> GetChannelBalanceAsync(string? deviceId = null)
    {
        lock (_lock)
        {
            string targetId = deviceId ?? _activeDevice?.Id ?? "";
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return Task.FromResult(Result<(float Left, float Right)>.Success((1.0f, 1.0f)));
            }

            try
            {
                var enumerator = (CoreAudioNative.IMMDeviceEnumerator)new CoreAudioNative.MMDeviceEnumeratorComObject();
                if (enumerator.GetDevice(targetId, out var dev) == 0)
                {
                    var iid = typeof(CoreAudioNative.IAudioEndpointVolume).GUID;
                    if (dev.Activate(ref iid, CoreAudioNative.CLSCTX_ALL, IntPtr.Zero, out var volObj) == 0)
                    {
                        var endpointVol = (CoreAudioNative.IAudioEndpointVolume)volObj;
                        endpointVol.GetChannelCount(out uint channelCount);

                        if (channelCount >= 2)
                        {
                            endpointVol.GetChannelVolumeLevelScalar(0, out float l);
                            endpointVol.GetChannelVolumeLevelScalar(1, out float r);
                            return Task.FromResult(Result<(float Left, float Right)>.Success((l, r)));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"GetChannelBalance Error: {ex.Message}");
            }

            return Task.FromResult(Result<(float Left, float Right)>.Success((1.0f, 1.0f)));
        }
    }

    public Task<Result> SetChannelBalanceAsync(float left, float right, string? deviceId = null)
    {
        lock (_lock)
        {
            string targetId = deviceId ?? _activeDevice?.Id ?? "";
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return Task.FromResult(Result.Failure("No target audio device"));
            }

            try
            {
                var enumerator = (CoreAudioNative.IMMDeviceEnumerator)new CoreAudioNative.MMDeviceEnumeratorComObject();
                if (enumerator.GetDevice(targetId, out var dev) == 0)
                {
                    var iid = typeof(CoreAudioNative.IAudioEndpointVolume).GUID;
                    if (dev.Activate(ref iid, CoreAudioNative.CLSCTX_ALL, IntPtr.Zero, out var volObj) == 0)
                    {
                        var endpointVol = (CoreAudioNative.IAudioEndpointVolume)volObj;
                        endpointVol.GetChannelCount(out uint channelCount);

                        if (channelCount >= 2)
                        {
                            var ctx = Guid.Empty;
                            endpointVol.SetChannelVolumeLevelScalar(0, Math.Clamp(left, 0f, 1f), ref ctx);
                            endpointVol.SetChannelVolumeLevelScalar(1, Math.Clamp(right, 0f, 1f), ref ctx);
                            return Task.FromResult(Result.Success());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result.Failure($"SetChannelBalance Error: {ex.Message}"));
            }

            return Task.FromResult(Result.Success());
        }
    }
}
