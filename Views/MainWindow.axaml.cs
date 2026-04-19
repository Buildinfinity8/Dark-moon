using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using proj2.Models;

namespace proj2.Views;

public partial class MainWindow : Window
{
    private List<MonitorControl.PHYSICAL_MONITOR> _monitors = new();

    public MainWindow()
    {
        InitializeComponent();
        this.Opened += MainWindow_Opened;
        this.Closed += MainWindow_Closed;
    }

    private void MainWindow_Opened(object? sender, EventArgs e)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var handle = this.TryGetPlatformHandle()?.Handle;
            if (handle.HasValue)
            {
                _monitors = MonitorControl.GetPhysicalMonitors(handle.Value);
                if (_monitors.Count > 0)
                {
                    uint brightness;
                    if (MonitorControl.GetBrightness(_monitors[0].hPhysicalMonitor, out brightness))
                    {
                        BrightnessSlider.Value = brightness;
                        BrightnessValue.Text = $"{brightness} %";
                    }

                    uint contrast;
                    if (MonitorControl.GetContrast(_monitors[0].hPhysicalMonitor, out contrast))
                    {
                        ContrastSlider.Value = contrast;
                        ContrastValue.Text = $"{contrast} %";
                    }
                }
            }
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // On Linux, we use ddcutil which doesn't need the window handle for basic usage
            uint brightness;
            if (MonitorControl.GetBrightness(IntPtr.Zero, out brightness))
            {
                BrightnessSlider.Value = brightness;
                BrightnessValue.Text = $"{brightness} %";
            }

            uint contrast;
            if (MonitorControl.GetContrast(IntPtr.Zero, out contrast))
            {
                ContrastSlider.Value = contrast;
                ContrastValue.Text = $"{contrast} %";
            }
        }
    }

    private void MainWindow_Closed(object? sender, EventArgs e)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            MonitorControl.ReleaseMonitors(_monitors);
        }
    }

    private void BrightnessSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (BrightnessValue != null)
            BrightnessValue.Text = $"{(int)e.NewValue} %";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _monitors.Count > 0)
        {
            MonitorControl.SetBrightness(_monitors[0].hPhysicalMonitor, (uint)e.NewValue);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            MonitorControl.SetBrightness(IntPtr.Zero, (uint)e.NewValue);
        }
    }

    private void ContrastSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (ContrastValue != null)
            ContrastValue.Text = $"{(int)e.NewValue} %";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _monitors.Count > 0)
        {
            MonitorControl.SetContrast(_monitors[0].hPhysicalMonitor, (uint)e.NewValue);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            MonitorControl.SetContrast(IntPtr.Zero, (uint)e.NewValue);
        }
    }

    private void TemperatureSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (TemperatureValue != null)
            TemperatureValue.Text = $"{(int)e.NewValue} %";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _monitors.Count > 0)
        {
            var temp = MapValueToColorTemperature((int)e.NewValue);
            MonitorControl.SetColorTemperature(_monitors[0].hPhysicalMonitor, temp);
        }
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

    private void BlueLightSlider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        if (BlueLightValue != null)
            BlueLightValue.Text = $"{(int)e.NewValue} %";
    }

    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine("Cancelled");
        Close();
    }

    private void Apply_Click(object? sender, RoutedEventArgs e)
    {
        Console.WriteLine($"Apply — Brightness:{(int)BrightnessSlider.Value} Contrast:{(int)ContrastSlider.Value} Temp:{(int)TemperatureSlider.Value} Blue:{(int)BlueLightSlider.Value}");
    }
}