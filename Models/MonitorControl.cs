using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace proj2.Models;

public class MonitorControl
{
    // --- Windows API Definitions ---
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

    public enum MC_COLOR_TEMPERATURE
    {
        MC_COLOR_TEMPERATURE_UNKNOWN,
        MC_COLOR_TEMPERATURE_4000K,
        MC_COLOR_TEMPERATURE_5000K,
        MC_COLOR_TEMPERATURE_6500K,
        MC_COLOR_TEMPERATURE_7500K,
        MC_COLOR_TEMPERATURE_8200K,
        MC_COLOR_TEMPERATURE_9300K,
        MC_COLOR_TEMPERATURE_10000K,
        MC_COLOR_TEMPERATURE_11500K
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint pdwNumberOfPhysicalMonitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool DestroyPhysicalMonitors(uint dwPhysicalMonitorArraySize, PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetMonitorBrightness(IntPtr hMonitor, out uint pdwMinimumBrightness, out uint pdwCurrentBrightness, out uint pdwMaximumBrightness);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool SetMonitorBrightness(IntPtr hMonitor, uint dwNewBrightness);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool GetMonitorContrast(IntPtr hMonitor, out uint pdwMinimumContrast, out uint pdwCurrentContrast, out uint pdwMaximumContrast);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool SetMonitorContrast(IntPtr hMonitor, uint dwNewContrast);

    [DllImport("dxva2.dll", SetLastError = true)]
    private static extern bool SetMonitorColorTemperature(IntPtr hMonitor, MC_COLOR_TEMPERATURE ctCurrentColorTemperature);

    // --- Common Interface ---
    //
    // All of these are async and never block the calling thread: on Linux they await an
    // `ddcutil` subprocess (which talks to the monitor over I2C and can easily take
    // 200ms-2s per call), and on Windows the blocking DXVA2 call is pushed onto a
    // thread-pool thread via Task.Run. Calling the old synchronous versions directly from a
    // UI event handler is what made the sliders feel like they locked up the app.

    public static async Task<(bool Success, uint Value)> GetBrightnessAsync(IntPtr handle)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return await Task.Run(() =>
            {
                bool ok = GetMonitorBrightness(handle, out _, out uint current, out _);
                return (ok, current);
            });
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return await Linux_GetVCPValueAsync(0x10);
        }
        return (false, 0);
    }

    public static async Task<bool> SetBrightnessAsync(IntPtr handle, uint brightness)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return await Task.Run(() => SetMonitorBrightness(handle, brightness));
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return await Linux_SetVCPValueAsync(0x10, brightness);
        }
        return false;
    }

    public static async Task<(bool Success, uint Value)> GetContrastAsync(IntPtr handle)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return await Task.Run(() =>
            {
                bool ok = GetMonitorContrast(handle, out _, out uint current, out _);
                return (ok, current);
            });
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return await Linux_GetVCPValueAsync(0x12);
        }
        return (false, 0);
    }

    public static async Task<bool> SetContrastAsync(IntPtr handle, uint contrast)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return await Task.Run(() => SetMonitorContrast(handle, contrast));
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return await Linux_SetVCPValueAsync(0x12, contrast);
        }
        return false;
    }

    /// VCP 0x14 (Select Color Preset) is the DDC/CI equivalent of the Windows color-temperature
    /// enum below; not every monitor supports it, so a failure here is expected on some hardware.
    public static async Task<bool> SetColorPresetLinuxAsync(byte presetCode)
    {
        return await Linux_SetVCPValueAsync(0x14, presetCode);
    }

    // --- Linux ddcutil Helper Methods ---

    private static async Task<bool> Linux_SetVCPValueAsync(int vcpCode, uint value)
    {
        try
        {
            // Note: User might need to configure i2c permissions or use sudo
            // ddcutil parses the feature-code argument as bare hex digits (no "0x" prefix),
            // e.g. "setvcp 10 50" means feature 0x10 -- so vcpCode must be rendered as hex here,
            // not decimal, even though `value` itself is plain decimal.
            var startInfo = new ProcessStartInfo
            {
                FileName = "ddcutil",
                Arguments = $"setvcp {vcpCode:X2} {value}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(startInfo);
            if (process is null) return false;
            await process.WaitForExitAsync();
            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Linux_SetVCPValue Error: {ex.Message}");
            return false;
        }
    }

    private static async Task<(bool Success, uint Value)> Linux_GetVCPValueAsync(int vcpCode)
    {
        try
        {
            // Same hex-vs-decimal quirk as Linux_SetVCPValueAsync: the feature code is bare hex.
            var startInfo = new ProcessStartInfo
            {
                FileName = "ddcutil",
                Arguments = $"getvcp {vcpCode:X2}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(startInfo);
            if (process is null) return (false, 0);

            // Expected output: VCP code 0x10 (Brightness                    ): current value =    25, max value =   100
            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            var match = Regex.Match(output, @"current value\s*=\s*(\d+)");
            if (match.Success && uint.TryParse(match.Groups[1].Value, out uint value))
            {
                return (true, value);
            }
            return (false, 0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Linux_GetVCPValue Error: {ex.Message}");
            return (false, 0);
        }
    }

    // --- Windows Specific Helpers (Unchanged) ---

    public static List<PHYSICAL_MONITOR> GetPhysicalMonitors(IntPtr windowHandle)
    {
        List<PHYSICAL_MONITOR> monitors = new List<PHYSICAL_MONITOR>();
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return monitors;

        IntPtr hMonitor = MonitorFromWindow(windowHandle, 2);
        if (hMonitor != IntPtr.Zero && GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint count))
        {
            PHYSICAL_MONITOR[] physicalMonitors = new PHYSICAL_MONITOR[count];
            if (GetPhysicalMonitorsFromHMONITOR(hMonitor, count, physicalMonitors))
                monitors.AddRange(physicalMonitors);
        }
        return monitors;
    }

    public static void ReleaseMonitors(List<PHYSICAL_MONITOR> monitors)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && monitors.Count > 0)
            DestroyPhysicalMonitors((uint)monitors.Count, monitors.ToArray());
    }

    public static bool SetColorTemperature(IntPtr hMonitor, MC_COLOR_TEMPERATURE temp)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return SetMonitorColorTemperature(hMonitor, temp);
        return false;
    }

    public static async Task<bool> SetColorTemperatureAsync(IntPtr hMonitor, MC_COLOR_TEMPERATURE temp)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return false;
        return await Task.Run(() => SetMonitorColorTemperature(hMonitor, temp));
    }

    public static async Task<List<PHYSICAL_MONITOR>> GetPhysicalMonitorsAsync(IntPtr windowHandle)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return new List<PHYSICAL_MONITOR>();
        return await Task.Run(() => GetPhysicalMonitors(windowHandle));
    }
}
