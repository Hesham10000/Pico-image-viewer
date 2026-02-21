# Pico Image Viewer

A standalone XR application for **Pico 4 Ultra** (Pico OS) that lets you browse and compare images in floating windows in XR space. Supports two viewing modes: **Normal** (file browser) and **Grid** (all-at-once comparison).

## Overview

### Two Modes

**Normal Mode (default)** — A qimgv-style image viewer:
- Browse folders in a world-space file browser panel
- Click any image to open it in a new floating window
- Each window has a **control bar** below the image with: close, settings, curvature slider, and resize
- **Curvature slider** bends the image on a cylinder (0 = flat, 1 = max curve)
- **Resize button**: click to enter resize mode, then move controller to resize the window
- Each new window spawns next to the last (right/below in a tiling pattern)
- **Thumbstick up/down** while hovering a window cycles to previous/next image in the folder
- Every window is draggable and resizable

**Grid Mode** — Batch comparison with row-based controls:
- Select a root folder (e.g., `Paradox/`)
- App scans all subfolders and spawns one window per image
- Windows are arranged in rows: subfolders = rows, images = columns
- **Images in the same row are placed side by side** with near-zero horizontal spacing (configurable)
- Each **row** has a shared **control bar** below it with: close row, settings, curvature slider, and resize row
  - **Close**: closes all images in that row
  - **Settings**: opens the settings panel
  - **Curvature slider**: applies curvature to all images in the row
  - **Resize**: resizes the entire row — all images scale to fill the row's width/height
- Vertical spacing between rows is controlled by the **Row Spacing** slider in Settings
- Horizontal spacing between images within a row is controlled by the **Column Spacing** slider in Settings

### Folder Structure Example

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

Grid mode spawns **5 windows** with row-based control bars:
- **Row 1** (Folder1): 2 images side by side + shared control bar below
- **Row 2** (Folder2): 3 images side by side + shared control bar below
- Each row's control bar can close, resize, or adjust curvature for the entire row

## Features

- **Dual mode**: Normal (file browser + individual windows) and Grid (row-based comparison)
- **Image curvature**: Adjustable cylindrical curve per image via slider (flat to fully curved)
- **Control bar per window** (Normal mode): Close, settings, curvature slider, resize
- **Control bar per row** (Grid mode): Close row, settings, curvature for row, resize row
- **Joystick image cycling**: Hover + thumbstick up/down to flip through folder images in-place
- **Folder browser**: Navigable directory tree with back/up/home buttons (Normal mode)
- **Folder scanning**: Finds `.png`, `.jpg`, `.jpeg`, `.gif` images in subfolders
- **Grid layout**: Subfolders = rows, images = columns, near-zero in-row spacing, configurable row spacing
- **Draggable windows**: Grab the title bar to move any window in 6DoF
- **Resizable windows**: Resize button in control bar + corner handles, with optional aspect ratio lock
- **Settings panel**: World-space polished UI with mode toggle, spacing controls, window defaults, interaction tuning
- **Layout persistence**: Save/load window positions per folder
- **Android file access**: Storage Access Framework picker + manual path entry
- **Performance**: Async texture loading with LRU cache, configurable max texture size

## Requirements

- **Unity**: 6.3 LTS (6000.0.x) or newer
- **Pico XR SDK**: PICO Unity Integration SDK (Unity 6-compatible version from Pico developer portal)
- **XR Interaction Toolkit**: 3.0.x (included in package manifest)
- **TextMeshPro**: 4.0.x (included in package manifest)
- **Input System**: 1.11.x (included in package manifest)
- **Target device**: Pico 4 Ultra (Android API 29+)
- **Build target**: Android (ARM64)

## Project Setup

### 1. Clone and open in Unity

```bash
git clone <repo-url>
```

Open the project folder in Unity 6.3+. Unity will import packages from `Packages/manifest.json`.
Gemini said
Here is the step-by-step guide to setting up Unity on Linux and configuring your project with the Pico XR SDK.

**Part 1: Install Unity on Linux**
The best way to run Unity on Linux (Ubuntu/Debian-based) is to install the Unity Hub via the official repository. This ensures you get updates and can easily manage different Editor versions.

**1. Install Unity Hub**
   Open your terminal and run the following commands to add the Unity repository and install the Hub:

```shell

# 1. Add the public signing key
wget -qO - https://hub.unity3d.com/linux/keys/public | gpg --dearmor | sudo tee /usr/share/keyrings/Unity_Technologies_ApS.gpg > /dev/null

# 2. Add the Unity Hub repository to your sources
sudo sh -c 'echo "deb [signed-by=/usr/share/keyrings/Unity_Technologies_ApS.gpg] https://hub.unity3d.com/linux/repos/deb stable main" > /etc/apt/sources.list.d/unityhub.list'

# 3. Update package cache and install
sudo apt update
sudo apt install unityhub
```
**2. Install Unity Editor (6.3+)**
   
1. Open **Unity Hub.**
2. Go to the Installs tab.

3. Click **Install Editor.**

4. Select the **6.3 LTS** version (or the specific version your project requires).

5. **Important:** Under "Modules," ensure you check **Android Build Support**, **OpenJDK**, and **Android SDK & NDK Tools**. These are required for building to Pico headsets.



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
2. This creates the full scene hierarchy, the ImageWindow prefab, and the FolderBrowser panel
3. Wire up serialized references in the Inspector:
   - `AppBootstrap`: assign XR Rig, Head Camera, WindowManager, NormalModeManager, TextureLoader, FolderBrowserPanel
   - `WindowManager`: assign the ImageWindow prefab and WindowContainer transform
   - `NormalModeManager`: assign the ImageWindow prefab and WindowContainer transform
   - `XRSetup`: assign left/right controller transforms
   - `JoystickImageNavigator`: assign thumbstick InputActionReferences and ray origins

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
[AppBootstrap]              - Entry point, permissions, mode management
[Managers]
  ├── WindowManager         - Grid mode: spawns/manages windows
  ├── NormalModeManager     - Normal mode: spawns windows on demand
  ├── TextureLoader         - Async image loading with LRU cache
  ├── AndroidPermissions    - Runtime permission handling
  └── JoystickImageNavigator - Joystick hover detection for image cycling
XR Rig
  └── Camera Offset
      ├── Main Camera
      ├── Left Controller     (XRSetup adds ray interactors)
      └── Right Controller
SettingsPanel               - World-space settings UI (both modes)
FolderBrowserPanel          - World-space file browser (Normal mode only)
WindowContainer             - Parent for all spawned ImageWindow instances
```

### Key Scripts

| Script | Path | Purpose |
|--------|------|---------|
| `AppBootstrap` | `Scripts/Core/AppBootstrap.cs` | App entry point, mode-dependent init |
| `WindowManager` | `Scripts/Core/WindowManager.cs` | Grid mode orchestrator |
| `NormalModeManager` | `Scripts/Core/NormalModeManager.cs` | Normal mode window spawning + placement |
| `FolderScanner` | `Scripts/Core/FolderScanner.cs` | Scans folders, builds data model |
| `GridLayoutManager` | `Scripts/Core/GridLayoutManager.cs` | Computes grid positions |
| `TextureLoader` | `Scripts/Core/TextureLoader.cs` | Async texture loading + LRU cache |
| `XRSetup` | `Scripts/Core/XRSetup.cs` | Configures XR ray interactors |
| `ImageWindow` | `Scripts/UI/ImageWindow.cs` | Window controller + joystick image cycling |
| `FolderBrowserPanel` | `Scripts/UI/FolderBrowserPanel.cs` | Folder/file browser for Normal mode |
| `SettingsPanel` | `Scripts/UI/SettingsPanel.cs` | World-space settings UI with mode toggle |
| `XRWindowDrag` | `Scripts/Interaction/XRWindowDrag.cs` | 6DoF drag via XR Grab |
| `WindowResizeHandle` | `Scripts/Interaction/WindowResizeHandle.cs` | Corner resize handles |
| `JoystickImageNavigator` | `Scripts/Interaction/JoystickImageNavigator.cs` | Joystick hover → prev/next image |
| `AndroidFilePicker` | `Scripts/Android/AndroidFilePicker.cs` | SAF folder picker |
| `AndroidPermissions` | `Scripts/Android/AndroidPermissions.cs` | Runtime permissions |
| `SceneSetup` | `Scripts/Editor/SceneSetup.cs` | Editor tool to build scene + prefabs |

### Data Model

```
ViewMode            - Enum: Normal or Grid
AppSettings         - Persisted preferences (mode, grid spacing, joystick settings, etc.)
FolderData          - One per subfolder (row), contains list of ImageData
ImageData           - One per image file (column), stores path and grid indices
FolderLayoutData    - Per-folder saved window positions/sizes
WindowLayoutEntry   - Per-window position, rotation, scale override
```

### Flow: Normal Mode

1. `AppBootstrap` starts in Normal mode (default) → shows `FolderBrowserPanel`
2. User navigates folders in the browser, clicks an image
3. `FolderBrowserPanel.OnImageClicked()` → `NormalModeManager.OpenImage()`
4. `NormalModeManager` computes placement (first window: in front; subsequent: tiled right/below)
5. Spawns `ImageWindow` with sibling image list attached
6. `JoystickImageNavigator` raycasts each frame; when hovering a window + joystick Y input:
   - Down → `ImageWindow.CycleToNextImage()` (loads next image in folder, wraps around)
   - Up → `ImageWindow.CycleToPreviousImage()` (loads previous, wraps around)
7. Image swap is in-place: same window, new texture, updated title

### Flow: Grid Mode

1. User switches to Grid mode in Settings → `WindowManager.SetMode(Grid)`
2. `WindowManager.OpenFolder(path)` → `FolderScanner.Scan()`
3. `GridLayoutManager.ComputeSlots()` calculates world positions
4. `WindowManager` instantiates `ImageWindow` prefab for each slot
5. Each window loads its texture async via `TextureLoader`
6. `FolderBrowserPanel` is hidden in Grid mode

## Settings

The in-app settings panel provides:

### Mode Selection
| Setting | Default | Description |
|---------|---------|-------------|
| Mode | Normal | Toggle between Normal and Grid mode |

### Normal Mode Settings
| Setting | Default | Description |
|---------|---------|-------------|
| Window spacing | 0.6m | Gap between spawned windows |
| Joystick deadzone | 0.5 | Thumbstick threshold for image cycling |
| Joystick cooldown | 0.3s | Minimum interval between image changes |

### Grid Mode Settings
| Setting | Default           | Description |
|---------|-------------------|-------------|
| Root folder | `/sdcard/Paradox` | Path to scan |
| Forward offset | 2.0m              | Grid distance from user |
| Up offset | 0.0m              | Grid vertical offset |
| Row spacing | 0.8m              | Gap between rows |
| Column spacing | 0.6m              | Gap between columns |

### Shared Settings
| Setting | Default | Description |
|---------|---------|-------------|
| Window width | 0.5m | Default window width |
| Window height | 0.4m | Default window height |
| Scale multiplier | 1.0x | Global window scale |
| Auto-fit aspect | On | Match window to image aspect ratio |
| Drag sensitivity | 1.0x | Drag speed multiplier |
| Resize sensitivity | 1.0x | Resize speed multiplier |
| Snap to grid | Off | Snap windows back to grid on release |
| Max texture size | 2048 | Downscale limit for large images |

## Normal Mode — Joystick Image Cycling

When in Normal mode, each window knows its sibling images (all images in the same folder, sorted by filename). To cycle:

1. Point your controller ray at an open image window (hover)
2. Push the **thumbstick down** → the window loads the **next** image in the folder
3. Push the **thumbstick up** → the window loads the **previous** image
4. The image wraps around (last → first, first → last)
5. Title bar updates to show the new filename
6. Works with both left and right controllers independently

This is edge-triggered with a configurable cooldown, so holding the joystick won't rapid-fire through images.

## Pico-Specific Notes

### Interaction Toolkit
- Uses **XR Interaction Toolkit** which Pico XR SDK supports natively
- Ray interactors work with both Pico controllers and hand tracking
- `XRGrabInteractable` is used for window dragging (title bar grab)
- `XRBaseInteractable` is used for resize handles
- **Unity Input System** is used for thumbstick reading (joystick image cycling)

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
- Joystick cycling reuses the existing window — no new GameObjects created
- World-space Canvas rendering is used for each window panel

### Known Limitations
- SAF folder picker returns content URIs that must be converted to file paths; this works for primary storage but may fail for external SD cards
- Very large image sets (100+) may require reducing max texture size
- Hand tracking precision for resize handles may need larger grab colliders
- Joystick cycling requires controller input; hand-tracking gesture equivalent not yet implemented

## Testing Checklist

### Setup
- [ ] Project opens in Unity 6.3+ without errors
- [ ] Pico XR SDK is properly configured
- [ ] Build produces a valid APK
- [ ] APK installs and launches on Pico 4 Ultra

### Normal Mode
- [ ] App starts in Normal mode by default
- [ ] Folder browser panel is visible and navigable
- [ ] Clicking a folder navigates into it
- [ ] Up/Back/Home navigation buttons work
- [ ] Clicking an image opens a new floating window
- [ ] First window appears in front of user
- [ ] Subsequent windows tile to the right, then start new rows
- [ ] Hovering window + joystick down shows next image in folder
- [ ] Hovering window + joystick up shows previous image
- [ ] Image cycling wraps around at boundaries
- [ ] Title bar updates when cycling images
- [ ] Joystick deadzone and cooldown settings are respected
- [ ] "Close All" button in Normal settings closes all open windows

### Grid Mode
- [ ] Switching to Grid mode hides folder browser
- [ ] Selecting a folder produces one window per image
- [ ] Windows appear in correct row/column grouping
- [ ] Rows sorted by folder name (case-insensitive, ascending)
- [ ] Columns sorted by filename (ascending)
- [ ] Images directly under root appear in a "(root)" row
- [ ] Empty subfolders are skipped
- [ ] Supported formats load correctly: .png, .jpg, .jpeg, .gif

### Window Interaction — Normal Mode
- [ ] Each window displays image preview (contain fit, not cropped)
- [ ] Control bar appears below each image window
- [ ] Close button in control bar hides the window
- [ ] Settings button opens the settings panel
- [ ] Curvature slider bends the image (0 = flat, 1 = max curve)
- [ ] Resize button toggles resize mode; controller movement resizes window
- [ ] Thumbstick up/down cycles to previous/next image in folder
- [ ] Grabbing title bar moves window in 3D space (6DoF)

### Window Interaction — Grid Mode
- [ ] Images in same row are side by side with near-zero spacing
- [ ] Each row has a shared control bar below all images
- [ ] Row close button closes all images in that row
- [ ] Row settings button opens settings panel
- [ ] Row curvature slider applies curvature to all images in the row
- [ ] Row resize button resizes all images to fill row width/height
- [ ] Vertical row spacing matches the Row Spacing setting
- [ ] Horizontal image spacing matches the Column Spacing setting

### Mode Switching
- [ ] Mode toggle in settings switches between Normal and Grid
- [ ] Switching modes clears windows from the previous mode
- [ ] Folder browser appears/disappears based on mode
- [ ] Mode preference is persisted across app restarts

### Settings
- [ ] Settings panel is visible and interactable in XR
- [ ] Normal mode settings group shows/hides based on mode
- [ ] Grid mode settings group shows/hides based on mode
- [ ] All sliders update their labels in real time

### Persistence
- [ ] Settings are saved and restored across app launches
- [ ] Default mode (Normal) is restored on fresh install
- [ ] Last opened folder is remembered
- [ ] Save Layout persists window positions
- [ ] Load Layout restores saved positions

### Performance
- [ ] App maintains 72Hz with 10+ windows
- [ ] Large images (4000x4000) don't cause OOM
- [ ] Texture loading doesn't freeze the UI
- [ ] Joystick cycling texture swap is smooth
- [ ] Closing windows frees texture memory

## License

MIT
