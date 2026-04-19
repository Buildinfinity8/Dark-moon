using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

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

    public static bool GetBrightness(IntPtr handle, out uint current)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GetMonitorBrightness(handle, out _, out current, out _);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return Linux_GetVCPValue(10, out current);
        }
        current = 0;
        return false;
    }

    public static bool SetBrightness(IntPtr handle, uint brightness)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return SetMonitorBrightness(handle, brightness);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return Linux_SetVCPValue(10, brightness);
        }
        return false;
    }

    public static bool GetContrast(IntPtr handle, out uint current)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GetMonitorContrast(handle, out _, out current, out _);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return Linux_GetVCPValue(12, out current);
        }
        current = 0;
        return false;
    }

    public static bool SetContrast(IntPtr handle, uint contrast)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return SetMonitorContrast(handle, contrast);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return Linux_SetVCPValue(12, contrast);
        }
        return false;
    }

    // --- Linux ddcutil Helper Methods ---

    private static bool Linux_SetVCPValue(int vcpCode, uint value)
    {
        try
        {
            // Note: User might need to configure i2c permissions or use sudo
            var startInfo = new ProcessStartInfo
            {
                FileName = "ddcutil",
                Arguments = $"setvcp {vcpCode} {value}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(startInfo);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Linux_SetVCPValue Error: {ex.Message}");
            return false;
        }
    }

    private static bool Linux_GetVCPValue(int vcpCode, out uint value)
    {
        value = 0;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ddcutil",
                Arguments = $"getvcp {vcpCode}",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(startInfo);
            string output = process?.StandardOutput.ReadToEnd() ?? "";
            process?.WaitForExit();

            // Expected output: VCP code 0x10 (Brightness                    ): current value =    25, max value =   100
            var match = Regex.Match(output, @"current value\s*=\s*(\d+)");
            if (match.Success && uint.TryParse(match.Groups[1].Value, out value))
            {
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Linux_GetVCPValue Error: {ex.Message}");
            return false;
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
}
