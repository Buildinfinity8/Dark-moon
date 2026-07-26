using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using proj2.Models;

namespace proj2.Views;

public partial class MainWindow : Window
{
    // How long a slider has to sit still before we actually talk to the monitor.
    // Without this, dragging fired a blocking hardware call on every pixel of movement.
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromMilliseconds(180);

    private List<MonitorControl.PHYSICAL_MONITOR> _monitors = new();

    private readonly DispatcherTimer _brightnessTimer;
    private readonly DispatcherTimer _contrastTimer;
    private readonly DispatcherTimer _temperatureTimer;

    public MainWindow()
    {
        InitializeComponent();

        _brightnessTimer = CreateDebounceTimer(() => ApplyBrightnessAsync((uint)BrightnessSlider.Value));
        _contrastTimer = CreateDebounceTimer(() => ApplyContrastAsync((uint)ContrastSlider.Value));
        _temperatureTimer = CreateDebounceTimer(() => ApplyTemperatureAsync((int)TemperatureSlider.Value));

        this.Opened += MainWindow_Opened;
        this.Closed += MainWindow_Closed;
    }

    private DispatcherTimer CreateDebounceTimer(Func<Task> apply)
    {
        var timer = new DispatcherTimer { Interval = DebounceDelay };
        timer.Tick += async (_, _) =>
        {
            timer.Stop();
            await apply();
        };
        return timer;
    }

    private static void Restart(DispatcherTimer timer)
    {
        timer.Stop();
        timer.Start();
    }

    private async void MainWindow_Opened(object? sender, EventArgs e)
    {
        await LoadCurrentValuesAsync();
    }

    private async Task LoadCurrentValuesAsync()
    {
        SetStatus("Reading monitor…", StatusKind.Busy);
        SetConnected(false);

        bool foundMonitor = false;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var handle = this.TryGetPlatformHandle()?.Handle;
            if (handle.HasValue)
            {
                _monitors = await MonitorControl.GetPhysicalMonitorsAsync(handle.Value);
                if (_monitors.Count > 0)
                {
                    var handleMonitor = _monitors[0].hPhysicalMonitor;
                    var (bOk, brightness) = await MonitorControl.GetBrightnessAsync(handleMonitor);
                    if (bOk)
                    {
                        BrightnessSlider.Value = brightness;
                        BrightnessValue.Text = $"{brightness} %";
                        foundMonitor = true;
                    }

                    var (cOk, contrast) = await MonitorControl.GetContrastAsync(handleMonitor);
                    if (cOk)
                    {
                        ContrastSlider.Value = contrast;
                        ContrastValue.Text = $"{contrast} %";
                        foundMonitor = true;
                    }
                }
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // On Linux we use ddcutil, which doesn't need a window handle for basic usage.
            var (bOk, brightness) = await MonitorControl.GetBrightnessAsync(IntPtr.Zero);
            if (bOk)
            {
                BrightnessSlider.Value = brightness;
                BrightnessValue.Text = $"{brightness} %";
                foundMonitor = true;
            }

            var (cOk, contrast) = await MonitorControl.GetContrastAsync(IntPtr.Zero);
            if (cOk)
            {
                ContrastSlider.Value = contrast;
                ContrastValue.Text = $"{contrast} %";
                foundMonitor = true;
            }
        }

        SetConnected(foundMonitor);
        SetStatus(
            foundMonitor ? "Monitor connected" : "No DDC/CI monitor found — check ddcutil / permissions",
            foundMonitor ? StatusKind.Idle : StatusKind.Error);
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        _brightnessTimer.Stop();
        _contrastTimer.Stop();
        _temperatureTimer.Stop();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            MonitorControl.ReleaseMonitors(_monitors);
        }
    }

    private void BrightnessSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        BrightnessValue.Text = $"{(int)e.NewValue} %";
        Restart(_brightnessTimer);
    }

    private void ContrastSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        ContrastValue.Text = $"{(int)e.NewValue} %";
        Restart(_contrastTimer);
    }

    private void TemperatureSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        TemperatureValue.Text = $"{(int)e.NewValue} %";
        Restart(_temperatureTimer);
    }

    private async Task ApplyBrightnessAsync(uint value)
    {
        SetStatus("Applying brightness…", StatusKind.Busy);
        bool ok;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _monitors.Count > 0)
            ok = await MonitorControl.SetBrightnessAsync(_monitors[0].hPhysicalMonitor, value);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            ok = await MonitorControl.SetBrightnessAsync(IntPtr.Zero, value);
        else
            ok = false;

        ReportApplyResult(ok, "Brightness");
    }

    private async Task ApplyContrastAsync(uint value)
    {
        SetStatus("Applying contrast…", StatusKind.Busy);
        bool ok;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _monitors.Count > 0)
            ok = await MonitorControl.SetContrastAsync(_monitors[0].hPhysicalMonitor, value);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            ok = await MonitorControl.SetContrastAsync(IntPtr.Zero, value);
        else
            ok = false;

        ReportApplyResult(ok, "Contrast");
    }

    private async Task ApplyTemperatureAsync(int value)
    {
        SetStatus("Applying temperature…", StatusKind.Busy);
        bool ok;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _monitors.Count > 0)
        {
            var temp = MapValueToColorTemperature(value);
            ok = await MonitorControl.SetColorTemperatureAsync(_monitors[0].hPhysicalMonitor, temp);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var preset = MapValueToColorPreset(value);
            ok = await MonitorControl.SetColorPresetLinuxAsync(preset);
        }
        else
        {
            ok = false;
        }

        ReportApplyResult(ok, "Temperature");
    }

    private void ReportApplyResult(bool ok, string label)
    {
        SetStatus(
            ok ? $"{label} applied" : $"Couldn't apply {label.ToLowerInvariant()} — is ddcutil installed and permitted?",
            ok ? StatusKind.Idle : StatusKind.Error);
    }

    private MonitorControl.MC_COLOR_TEMPERATURE MapValueToColorTemperature(int value)
    {
        if (value < 10) return MonitorControl.MC_COLOR_TEMPERATURE.MC_COLOR_TEMPERATURE_4000K;
        if (value < 20) return MonitorControl.MC_COLOR_TEMPERATURE.MC_COLOR_TEMPERATURE_5000K;
        if (value < 35) return MonitorControl.MC_COLOR_TEMPERATURE.MC_COLOR_TEMPERATURE_6500K;
        if (value < 50) return MonitorControl.MC_COLOR_TEMPERATURE.MC_COLOR_TEMPERATURE_7500K;
        if (value < 65) return MonitorControl.MC_COLOR_TEMPERATURE.MC_COLOR_TEMPERATURE_8200K;
        if (value < 80) return MonitorControl.MC_COLOR_TEMPERATURE.MC_COLOR_TEMPERATURE_9300K;
        if (value < 90) return MonitorControl.MC_COLOR_TEMPERATURE.MC_COLOR_TEMPERATURE_10000K;
        return MonitorControl.MC_COLOR_TEMPERATURE.MC_COLOR_TEMPERATURE_11500K;
    }

    // DDC/CI VCP 0x14 "Select Color Preset" codes, mirroring the Windows breakpoints above.
    private byte MapValueToColorPreset(int value)
    {
        if (value < 10) return 0x03; // 4000K
        if (value < 20) return 0x04; // 5000K
        if (value < 35) return 0x05; // 6500K
        if (value < 50) return 0x06; // 7500K
        if (value < 65) return 0x07; // 8200K
        if (value < 80) return 0x08; // 9300K
        if (value < 90) return 0x09; // 10000K
        return 0x0A; // 11500K
    }

    private async void Refresh_Click(object? sender, RoutedEventArgs e)
    {
        await LoadCurrentValuesAsync();
    }

    private async void About_Click(object? sender, RoutedEventArgs e)
    {
        await new AboutWindow().ShowDialog(this);
    }

    private void Close_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private enum StatusKind { Idle, Busy, Error }

    private void SetStatus(string text, StatusKind kind)
    {
        StatusText.Text = text;
        StatusText.Foreground = kind switch
        {
            StatusKind.Error => Avalonia.Media.Brushes.IndianRed,
            StatusKind.Busy => Avalonia.Media.Brush.Parse("#8ab4f8"),
            _ => Avalonia.Media.Brush.Parse("#888"),
        };
    }

    private void SetConnected(bool connected)
    {
        StatusDot.Fill = connected
            ? Avalonia.Media.Brush.Parse("#4caf50")
            : Avalonia.Media.Brush.Parse("#666");
    }
}
