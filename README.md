# Monitor Control (proj2)

A cross-platform desktop application built with **Avalonia UI** and **.NET 10** to control external monitor settings like brightness and contrast directly from your computer.

## Features

- **Cross-Platform Support**: Works on both Windows and Linux.
- **Brightness Control**: Adjust monitor brightness levels.
- **Contrast Control**: Adjust monitor contrast levels.
- **Native Integration**:
  - Uses `dxva2.dll` (High-Level Monitor Configuration API) on Windows.
  - Integrates with `ddcutil` on Linux.

## Prerequisites

### Windows
- Windows 10 or later.
- Monitors must support DDC/CI.

### Linux
- `ddcutil` must be installed.
  ```bash
  sudo apt install ddcutil  # Ubuntu/Debian
  sudo pacman -S ddcutil    # Arch Linux
  ```
- Your user may need permissions to access i2c devices:
  ```bash
  sudo usermod -aG i2c $(whoami)
  ```
  *(Log out and log back in for changes to take effect)*

## Getting Started

### Clone the repository
```bash
git clone <repository-url>
cd proj2
```

### Build and Run
To run the application in development mode:
```bash
dotnet run
```

### Generate Executable
To publish a single-file executable:

**For Linux:**
```bash
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
```

**For Windows:**
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## Project Structure

- `Models/MonitorControl.cs`: Core logic for hardware interaction (Win32 API & Linux ddcutil).
- `Views/`: XAML files for the user interface.
- `ViewModels/`: Application logic and data binding (MVVM).

## Technologies Used

- [Avalonia UI](https://avaloniaui.net/) - Cross-platform UI framework.
- [.NET 10](https://dotnet.microsoft.com/) - Modern development platform.
- [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) - MVVM pattern implementation.
