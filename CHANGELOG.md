# Changelog

## Pico 4 Ultra integration fix
- Added runtime XR UI safeguards in `AppBootstrap`:
  - auto-creates `EventSystem + InputSystemUIInputModule`
  - auto-adds `TrackedDeviceGraphicRaycaster` to world-space canvases
  - recenters world-space panels in front of the HMD at startup and on-demand
- Added `Recenter UI` and `Grant Access` actions to folder browser header.
- Reworked Android permission flow:
  - Android 13+ uses `READ_MEDIA_IMAGES`
  - Android 12 and lower uses legacy storage read permission
  - MANAGE_EXTERNAL fallback remains for broad file access paths
  - explicit permission state logging for debugging
- Added toggleable in-app debug overlay (`RuntimeDebugOverlay`) for headset verification.
- Updated Android manifest permissions to align with Android 13+ media policy while preserving Pico fallback access.
