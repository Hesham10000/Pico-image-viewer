# Pico Image Viewer

A standalone XR application for **Pico 4 Ultra** (Pico OS) that lets you compare many images at once by rendering each image in its own independent floating window/panel in XR space.

## Overview

Point the app at a folder structure like:

```
paradox/
  Folder1/
    img_a.png
    img_b.jpg
  Folder2/
    photo1.jpeg
    photo2.png
    photo3.jpg
```

The app spawns **5 draggable, resizable windows** arranged in a grid:
- **Row 1** (Folder1): 2 windows side by side
- **Row 2** (Folder2): 3 windows side by side

Every window can be grabbed and moved anywhere in 3D space, resized from corners, zoomed, and reset to its grid position.

## Features

- **Folder scanning**: Recursively finds `.png`, `.jpg`, `.jpeg`, `.gif` images in subfolders
- **Grid layout**: Subfolders = rows, images = columns, with configurable spacing
- **Draggable windows**: Grab the title bar to move any window in 6DoF
- **Resizable windows**: Corner handles for resizing, with optional aspect ratio lock
- **Per-window controls**: Close, reset size, reset position, zoom in/out, fit-to-image
- **Settings panel**: World-space UI with grid spacing, window defaults, interaction tuning
- **Layout persistence**: Save/load window positions per folder
- **Android file access**: Storage Access Framework picker + manual path entry
- **Performance**: Async texture loading with LRU cache, configurable max texture size

## Requirements

- **Unity**: 2022.3 LTS or newer
- **Pico XR SDK**: PICO Unity Integration SDK (install via Pico developer portal)
- **XR Interaction Toolkit**: 2.5.x (included in package manifest)
- **TextMeshPro**: 3.0.x (included in package manifest)
- **Target device**: Pico 4 Ultra (Android API 29+)
- **Build target**: Android (ARM64)

## Project Setup

### 1. Clone and open in Unity

```bash
git clone <repo-url>
```

Open the project folder in Unity 2022.3+. Unity will import packages from `Packages/manifest.json`.

### 2. Install Pico XR SDK

1. Download the **PICO Unity Integration SDK** from the [Pico Developer Portal](https://developer-global.pico-interactive.com/)
2. Import the `.unitypackage` into the project
3. Or add via scoped registry in `Packages/manifest.json`:
   ```json
   "com.unity.xr.picoxr": "2.x.x"
   ```

### 3. Configure XR settings

1. Go to **Edit > Project Settings > XR Plug-in Management**
2. Enable **PICO** under the Android tab
3. Under **PICO** settings, enable:
   - 6DoF tracking
   - Controller tracking
   - Hand tracking (optional)

### 4. Configure build settings

1. **File > Build Settings** → Switch platform to **Android**
2. Set:
   - **Minimum API Level**: 29 (Android 10)
   - **Target API Level**: 32+
   - **Scripting Backend**: IL2CPP
   - **Target Architecture**: ARM64
   - **Color Space**: Linear

### 5. Setup the scene

1. In Unity Editor, go to **PicoImageViewer > Setup Scene** (menu bar)
2. This creates the full scene hierarchy and the ImageWindow prefab
3. Wire up serialized references in the Inspector:
   - `AppBootstrap`: assign XR Rig, Head Camera, WindowManager, TextureLoader
   - `WindowManager`: assign the ImageWindow prefab and WindowContainer transform
   - `XRSetup`: assign left/right controller transforms

### 6. Build and deploy

```bash
# Build APK via Unity Build Settings, or:
# Use Unity command line:
Unity -batchmode -buildTarget Android -executeMethod BuildScript.Build
```

Install to Pico 4 Ultra via:
```bash
adb install -r PicoImageViewer.apk
```

## Architecture

### Scene Hierarchy

```
[AppBootstrap]          - Entry point, permission requests, initialization
[Managers]
  ├── WindowManager     - Spawns/manages all image windows
  ├── TextureLoader     - Async image loading with LRU cache
  └── AndroidPermissions - Runtime permission handling
XR Rig
  └── Camera Offset
      ├── Main Camera
      ├── Left Controller   (XRSetup adds ray interactors)
      └── Right Controller
SettingsPanel           - World-space settings UI
WindowContainer         - Parent for all spawned ImageWindow instances
```

### Key Scripts

| Script | Path | Purpose |
|--------|------|---------|
| `AppBootstrap` | `Scripts/Core/AppBootstrap.cs` | App entry point and initialization |
| `WindowManager` | `Scripts/Core/WindowManager.cs` | Central orchestrator for windows |
| `FolderScanner` | `Scripts/Core/FolderScanner.cs` | Scans folders, builds data model |
| `GridLayoutManager` | `Scripts/Core/GridLayoutManager.cs` | Computes grid positions |
| `TextureLoader` | `Scripts/Core/TextureLoader.cs` | Async texture loading + LRU cache |
| `XRSetup` | `Scripts/Core/XRSetup.cs` | Configures XR ray interactors |
| `ImageWindow` | `Scripts/UI/ImageWindow.cs` | Single window controller |
| `SettingsPanel` | `Scripts/UI/SettingsPanel.cs` | World-space settings UI |
| `XRWindowDrag` | `Scripts/Interaction/XRWindowDrag.cs` | 6DoF drag via XR Grab |
| `WindowResizeHandle` | `Scripts/Interaction/WindowResizeHandle.cs` | Corner resize handles |
| `AndroidFilePicker` | `Scripts/Android/AndroidFilePicker.cs` | SAF folder picker |
| `AndroidPermissions` | `Scripts/Android/AndroidPermissions.cs` | Runtime permissions |
| `SceneSetup` | `Scripts/Editor/SceneSetup.cs` | Editor tool to build scene + prefab |

### Data Model

```
AppSettings         - Persisted user preferences (grid spacing, window defaults, etc.)
FolderData          - One per subfolder (row), contains list of ImageData
ImageData           - One per image file (column), stores path and grid indices
FolderLayoutData    - Per-folder saved window positions/sizes
WindowLayoutEntry   - Per-window position, rotation, scale override
```

### Flow

1. `AppBootstrap.Start()` → requests Android storage permissions
2. `WindowManager.OpenFolder(path)` → calls `FolderScanner.Scan()`
3. `FolderScanner` returns `List<FolderData>` sorted by folder name, each containing `List<ImageData>` sorted by filename
4. `GridLayoutManager.ComputeSlots()` calculates world positions for each image
5. `WindowManager` instantiates `ImageWindow` prefab for each slot
6. Each `ImageWindow` calls `TextureLoader.LoadAsync()` to load its image
7. User interacts via XR ray interactors → `XRWindowDrag` for moving, `WindowResizeHandle` for resizing

## Settings

The in-app settings panel provides:

| Setting | Default | Description |
|---------|---------|-------------|
| Root folder | `/sdcard/Download/paradox` | Path to scan |
| Forward offset | 2.0m | Grid distance from user |
| Up offset | 0.0m | Grid vertical offset |
| Row spacing | 0.8m | Gap between rows |
| Column spacing | 0.6m | Gap between columns |
| Window width | 0.5m | Default window width |
| Window height | 0.4m | Default window height |
| Scale multiplier | 1.0x | Global window scale |
| Auto-fit aspect | On | Match window to image aspect ratio |
| Drag sensitivity | 1.0x | Drag speed multiplier |
| Resize sensitivity | 1.0x | Resize speed multiplier |
| Snap to grid | Off | Snap windows back to grid on release |
| Max texture size | 2048 | Downscale limit for large images |

## Pico-Specific Notes

### Interaction Toolkit
- Uses **XR Interaction Toolkit** which Pico XR SDK supports natively
- Ray interactors work with both Pico controllers and hand tracking
- `XRGrabInteractable` is used for window dragging (title bar grab)
- `XRBaseInteractable` is used for resize handles

### File Access
- Pico OS runs Android 12+ (API 31+), which restricts file access
- The app requests `MANAGE_EXTERNAL_STORAGE` for broad file access
- Alternatively, users can use the SAF folder picker to grant scoped access
- Default path `/sdcard/Download/` is typically accessible
- The `AndroidManifest.xml` includes `requestLegacyExternalStorage="true"` as fallback

### Performance Considerations
- Pico 4 Ultra targets 72Hz refresh rate
- Textures are downscaled to configurable max size (default 2048px)
- LRU cache limits to 100 textures to prevent OOM
- Images are loaded asynchronously (file I/O on thread pool, texture creation on main thread)
- World-space Canvas rendering is used for each window panel

### Known Limitations
- SAF folder picker returns content URIs that must be converted to file paths; this works for primary storage but may fail for external SD cards
- Very large image sets (100+) may require reducing max texture size
- Hand tracking precision for resize handles may need larger grab colliders

## Testing Checklist

### Setup
- [ ] Project opens in Unity 2022.3+ without errors
- [ ] Pico XR SDK is properly configured
- [ ] Build produces a valid APK
- [ ] APK installs and launches on Pico 4 Ultra

### Core Functionality
- [ ] Selecting a folder with subfolders produces one window per image
- [ ] Windows appear in correct row/column grouping
- [ ] Rows are sorted by folder name (case-insensitive, ascending)
- [ ] Columns are sorted by filename (ascending)
- [ ] Images directly under root appear in a "(root)" row
- [ ] Empty subfolders are skipped (no empty rows)
- [ ] Supported formats load correctly: .png, .jpg, .jpeg, .gif

### Window Interaction
- [ ] Each window displays image preview (contain fit, not cropped)
- [ ] Title bar shows filename
- [ ] Grabbing title bar moves window in 3D space (6DoF)
- [ ] Corner handles resize the window
- [ ] Aspect ratio lock toggle works
- [ ] Close button hides the window
- [ ] Reset Size restores default dimensions
- [ ] Reset Position returns window to grid slot
- [ ] Zoom in/out changes window size
- [ ] Fit button adjusts window to match image aspect

### Settings
- [ ] Settings panel is visible and interactable in XR
- [ ] Folder path input accepts manual entry
- [ ] Browse button opens Android folder picker
- [ ] Grid spacing sliders affect window layout
- [ ] Window size defaults are applied to new windows
- [ ] Drag/resize sensitivity settings are effective
- [ ] Snap to grid toggle works on window release

### Persistence
- [ ] Settings are saved and restored across app launches
- [ ] Last opened folder is remembered
- [ ] Save Layout persists window positions
- [ ] Load Layout restores saved positions
- [ ] Re-scan keeps existing layout for matched images
- [ ] New images from re-scan get default grid positions

### Performance
- [ ] App maintains 72Hz with 10+ windows
- [ ] Large images (4000x4000) don't cause OOM
- [ ] Texture loading doesn't freeze the UI
- [ ] Closing windows frees texture memory

## License

MIT
