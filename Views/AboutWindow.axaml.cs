using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace proj2.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to open {url}: {ex.Message}");
        }
    }

    private void Website_Click(object? sender, RoutedEventArgs e) => OpenUrl("https://omnori.github.io");
    private void GitHub_Click(object? sender, RoutedEventArgs e) => OpenUrl("https://github.com/omnori");
    private void Instagram_Click(object? sender, RoutedEventArgs e) => OpenUrl("https://www.instagram.com/omnori.tech");
    private void Discord_Click(object? sender, RoutedEventArgs e) => OpenUrl("https://discord.gg/5ag9gjsDde");

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();
}
