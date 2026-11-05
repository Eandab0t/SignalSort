# FileQuickNamie

A Windows desktop utility for fast, consistent file naming.

## Build

Open `SmartFileClassifier.csproj` in Visual Studio or run the standard .NET build from this directory. The application is Windows-only because it uses WPF.

Reference: [WPF desktop development](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/).

<!-- Existing detailed feature and usage notes follow. -->

A modern, single-file Windows WPF application that scans files, determines their purpose based on metadata, and generates smart new filenames.

## Features

- **Audio Classification**: SoundEffect (<10s), Stinger (10-45s), Song (>45s), VoiceMemo (Mono + low bitrate)
- **Video Classification**: Clip (<15s), Short (15-120s), Recording (>120s), ScreenRec (<720p), CameraFootage (>=1080p)
- **Folder Classification**: AudioCollection, VideoSet, MediaDump, ImagePack, Archive_YYYY-MM-DD
- **Naming Styles**: Category + Number, Category + Hash, Category + Timestamp, Category + AdjectiveNoun
- **Sort Options**: Original Name, Category, Purpose, New Name
- **Options Panel**: Toggle recursive scan, hidden/system files, preview-only (dry run), extension preservation, case sensitivity, auto-close
- **Modern UI**: Light/dark theme toggle, Segoe UI typography, 6px rounded corners, 8px spacing grid, soft shadows, iconography via Segoe MDL2 Assets
- **Live Preview**: Instantly see new names in a sortable DataGrid
- **Safe Rename**: Collision detection, dry-run preview, and detailed logging

## Requirements

- Windows 10 or Windows 11 (64-bit)
- No installation required - runs as a standalone .exe

## Quick Start

1. **Double-click** `FileQuickNamie.exe` to run
2. Click **Browse** and select a folder containing files
3. The app scans and classifies all files, showing original and suggested new names
4. Adjust naming style, sort order, and options in the right panel
5. Toggle **Preview Only** to see what would happen without renaming
6. Click **Rename All** to apply

## Visual Design

| Element | Specification |
|---|---|
| **Font** | Segoe UI (body 14px, captions 12px, headings 16px) |
| **Corner Radius** | 6px panels, 12px window, 20px toggle |
| **Spacing Grid** | 8px base grid |
| **Icons** | Segoe MDL2 Assets font (vector, any resolution) |
| **Shadows** | Soft DropShadowEffect (opacity 0.3-0.4) |
| **Theme** | Toggle between Light and Dark modes |
| **Animations** | 180ms hover transitions, smooth toggles |

## Options Panel

### Scan Options
- **Recursive scan** - Include subfolders
- **Include hidden files** - Scan hidden files
- **Include system files** - Scan system files
- **Case sensitive naming** - Preserve case in generated names

### Rename Options
- **Preserve original extension** - Keep file extensions (always on for safety)
- **Preview only (dry run)** - Show what would be renamed without applying changes
- **Auto-close after rename** - Window closes automatically after successful rename

## Project Structure

```
FileQuickNamie/
├── FileQuickNamie.exe              # Single-file executable (~72 MB, self-contained)
├── SmartFileClassifier.csproj      # Project file (.NET 8, WPF, single-file publish)
├── App.xaml / App.xaml.cs          # Application entry + theme toggle
├── Themes/
│   ├── Light.xaml                  # Light theme color resources
│   └── Dark.xaml                   # Dark theme color resources
├── Views/
│   ├── MainWindow.xaml             # Modern 3-column UI with icons, theme toggle
│   └── MainWindow.xaml.cs          # Window code-behind
├── ViewModels/
│   ├── MainViewModel.cs            # MVVM view model (all logic, 10+ properties)
│   └── RelayCommand.cs             # ICommand implementation
├── Models/
│   ├── FileClassification.cs       # Per-file classification result (bindable)
│   └── SortOrder.cs                # Sort order enum with display names
├── Services/
│   ├── MetadataExtractor.cs        # TagLibSharp wrapper
│   ├── FileClassifier.cs           # Audio/Video/Folder classification
│   ├── PreviewEngine.cs            # Async folder scanning + name generation
│   └── RenameEngine.cs             # Safe file renaming with collision handling
├── Naming/
│   ├── INamingStrategy.cs
│   ├── CategoryNumberStrategy.cs       # Category_001
│   ├── CategoryHashStrategy.cs          # Category_a1b2c3d4
│   ├── CategoryTimestampStrategy.cs     # Category_2024-01-15T143022
│   ├── CategoryAdjectiveNounStrategy.cs # Category_BrightFalcon
│   └── NamingStrategyFactory.cs
├── Utilities/
│   └── AdjectiveNounGenerator.cs       # 70+ adjectives, 70+ nouns
└── Converters/
    ├── BoolToCheckConverter.cs
    ├── BoolToThemeIconConverter.cs
    ├── BoolToThemeTextConverter.cs
    ├── CategoryCounterConverter.cs
    ├── IntGreaterThanZeroToVisibilityConverter.cs
    ├── NamingStyleToDisplayConverter.cs
    └── SortOrderToDisplayConverter.cs
```

## Building from Source

```powershell
cd "E:\Ean server assests\Ean Applications\FileQuickNamie"
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

Output: `bin\Release\net8.0-windows\win-x64\publish\SmartFileClassifier.exe`

## Dependencies

- TagLibSharp (NuGet) - media metadata extraction
- .NET 8.0 runtime (self-contained, bundled in exe)

## Usage Notes

- The app uses WPF's native OpenFileDialog as a folder picker (no Windows Forms dependency)
- All toggle states persist visually within the session
- The summary panel on the left shows real-time file counts by category
- Uncheck the checkbox on any row to exclude it from renaming
- The log area at the bottom shows all operations in timestamped entries
