using System.Collections.Generic;
using UnityEngine;
using PicoImageViewer.Data;
using PicoImageViewer.UI;
using PicoImageViewer.Interaction;

namespace PicoImageViewer.Core
{
    /// <summary>
    /// Central manager: orchestrates folder scanning, window spawning/despawning,
    /// layout persistence, and re-scanning.
    /// In Grid mode, images are grouped into ImageRow containers (one per subfolder).
    /// </summary>
    public class WindowManager : MonoBehaviour
    {
        public static WindowManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private GameObject _imageWindowPrefab;
        [SerializeField] private Transform _windowContainer;
        [SerializeField] private Transform _headTransform; // XR camera / head

        [Header("State")]
        private AppSettings _settings;
        private GridLayoutManager _gridLayout;
        private List<FolderData> _currentFolders = new List<FolderData>();
        private List<ImageWindow> _activeWindows = new List<ImageWindow>();
        private List<ImageRow> _activeRows = new List<ImageRow>();
        private string _currentRootFolder;

        // Events
        public System.Action OnScanComplete;
        public System.Action<int> OnWindowCountChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _settings = AppSettings.Load();
            _gridLayout = new GridLayoutManager(_settings);
        }

        private void Start()
        {
            // Auto-discover references if not assigned
            AutoDiscoverReferences();

            // NOTE: Do NOT auto-open here. AppBootstrap.InitializeApp() handles
            // the initial folder open after permissions are granted. Opening here
            // causes a double-open race where the first batch of windows is destroyed
            // while their texture callbacks are still pending.
        }

        private void AutoDiscoverReferences()
        {
            if (_imageWindowPrefab == null)
            {
                _imageWindowPrefab = Resources.Load<GameObject>("ImageWindow");
                if (_imageWindowPrefab == null)
                {
                    // Try loading from Prefabs folder via path
                    var prefabs = Resources.FindObjectsOfTypeAll<UI.ImageWindow>();
                    foreach (var p in prefabs)
                    {
                        if (p.gameObject.scene.name == null) // it's a prefab asset
                        {
                            _imageWindowPrefab = p.gameObject;
                            break;
                        }
                    }
                }
            }

            if (_windowContainer == null)
            {
                var go = GameObject.Find("WindowContainer");
                if (go != null) _windowContainer = go.transform;
                else
                {
                    // Create one under [Managers]
                    var container = new GameObject("WindowContainer");
                    container.transform.SetParent(transform.parent, false);
                    _windowContainer = container.transform;
                }
            }

            Debug.Log($"[WindowManager] Auto-discovered: Prefab={_imageWindowPrefab != null}, Container={_windowContainer != null}");
        }

        /// <summary>
        /// Set the head/camera transform reference (called by XR rig setup).
        /// </summary>
        public void SetHeadTransform(Transform head)
        {
            _headTransform = head;
        }

        /// <summary>
        /// Open a folder: scan, spawn windows, apply saved layouts.
        /// In grid mode, groups images into ImageRow containers per subfolder.
        /// </summary>
        public void OpenFolder(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath))
            {
                Debug.LogWarning("[WindowManager] Empty root path");
                return;
            }

            _currentRootFolder = rootPath;
            _settings.LastRootFolder = rootPath;
            _settings.Save();

            // Clear existing windows
            CloseAllWindows();

            // Scan
            _currentFolders = FolderScanner.Scan(rootPath);
            if (_currentFolders.Count == 0)
            {
                Debug.LogWarning("[WindowManager] No images found in: " + rootPath);
                OnScanComplete?.Invoke();
                return;
            }

            // Load saved layout for this folder
            var savedLayout = FolderLayoutData.Load(rootPath);

            // Compute grid slots
            if (_headTransform == null)
            {
                _headTransform = Camera.main?.transform;
            }

            var slots = _gridLayout.ComputeSlots(_currentFolders, _headTransform);

            // Build a lookup: rowIndex -> list of slots
            var slotsByRow = new Dictionary<int, List<GridSlot>>();
            foreach (var slot in slots)
            {
                int row = slot.Image.RowIndex;
                if (!slotsByRow.ContainsKey(row))
                    slotsByRow[row] = new List<GridSlot>();
                slotsByRow[row].Add(slot);
            }

            // Spawn: one ImageRow per folder/row, ImageWindows inside each row
            foreach (var folder in _currentFolders)
            {
                if (!slotsByRow.ContainsKey(folder.RowIndex)) continue;
                var rowSlots = slotsByRow[folder.RowIndex];
                if (rowSlots.Count == 0) continue;

                // Create the row container GameObject
                var rowGO = new GameObject($"Row_{folder.FolderName}");
                rowGO.transform.SetParent(_windowContainer, false);
                // Position row at the first slot's position (row-level)
                rowGO.transform.position = rowSlots[0].Position;
                rowGO.transform.rotation = rowSlots[0].Rotation;

                var imageRow = rowGO.AddComponent<ImageRow>();
                float windowW = _settings.DefaultWindowWidth * _settings.WindowScaleMultiplier;
                float windowH = _settings.DefaultWindowHeight * _settings.WindowScaleMultiplier;
                imageRow.Initialize(folder, windowW, windowH);
                _activeRows.Add(imageRow);

                // Spawn individual image windows inside the row
                foreach (var slot in rowSlots)
                {
                    SpawnWindowInRow(slot, savedLayout, imageRow);
                }
            }

            OnScanComplete?.Invoke();
            OnWindowCountChanged?.Invoke(_activeWindows.Count);

            Debug.Log($"[WindowManager] Opened {rootPath}: {_activeWindows.Count} windows in {_activeRows.Count} rows");
        }

        private void SpawnWindowInRow(GridSlot slot, FolderLayoutData savedLayout, ImageRow row)
        {
            if (_imageWindowPrefab == null)
            {
                Debug.LogError("[WindowManager] ImageWindow prefab not assigned!");
                return;
            }

            // Spawn under the window container first (not directly under row,
            // because the ImageWindow Canvas needs its own world-space transform).
            // We register with the row logically but keep the window at scene root level.
            GameObject go = Instantiate(_imageWindowPrefab, _windowContainer);
            var window = go.GetComponent<ImageWindow>();
            if (window == null)
            {
                Debug.LogError("[WindowManager] Prefab missing ImageWindow component!");
                Destroy(go);
                return;
            }

            window.Initialize(slot.Image, slot.Width, slot.Height, slot.Position, slot.Rotation);

            // Apply saved layout override if available
            var savedEntry = savedLayout?.FindEntry(slot.Image.RelativePath);
            if (savedEntry != null)
            {
                window.ApplyLayoutOverride(savedEntry);
            }

            window.SetParentRow(row);
            row.AddChildWindow(window);
            _activeWindows.Add(window);
        }

        /// <summary>
        /// Re-scan the current folder. Preserves layouts for images that still exist.
        /// </summary>
        public void RescanFolder()
        {
            if (string.IsNullOrEmpty(_currentRootFolder)) return;

            // Save current layout before re-scanning
            SaveLayout();

            // Re-open (which clears + re-creates, applying saved layout)
            OpenFolder(_currentRootFolder);
        }

        /// <summary>
        /// Close all windows and destroy their GameObjects.
        /// </summary>
        public void CloseAllWindows()
        {
            // Destroy row containers (which also destroys child windows)
            foreach (var row in _activeRows)
            {
                if (row != null && row.gameObject != null)
                    Destroy(row.gameObject);
            }
            _activeRows.Clear();

            // Also destroy any orphaned windows not in a row
            foreach (var window in _activeWindows)
            {
                if (window != null && window.gameObject != null)
                    Destroy(window.gameObject);
            }
            _activeWindows.Clear();
            OnWindowCountChanged?.Invoke(0);
        }

        /// <summary>
        /// Reset all windows to their grid slot positions.
        /// </summary>
        public void ResetAllToGrid()
        {
            if (_headTransform == null) return;

            var slots = _gridLayout.ComputeSlots(_currentFolders, _headTransform);
            var slotMap = new Dictionary<string, GridSlot>();
            foreach (var slot in slots)
            {
                if (slot.Image != null)
                    slotMap[slot.Image.RelativePath] = slot;
            }

            foreach (var window in _activeWindows)
            {
                if (window == null) continue;
                string key = window.RelativePath;
                if (key != null && slotMap.TryGetValue(key, out var slot))
                {
                    window.UpdateGridSlot(slot.Position, slot.Rotation);
                    window.ResetPosition();
                    window.SetSize(slot.Width, slot.Height);
                    window.Show();
                }
            }
        }

        /// <summary>
        /// Save all current window layouts for the active folder.
        /// </summary>
        public void SaveLayout()
        {
            if (string.IsNullOrEmpty(_currentRootFolder)) return;

            var layoutData = new FolderLayoutData { RootFolder = _currentRootFolder };
            foreach (var window in _activeWindows)
            {
                if (window != null)
                    layoutData.Entries.Add(window.CreateLayoutEntry());
            }
            layoutData.Save();
            Debug.Log($"[WindowManager] Layout saved for {_currentRootFolder}");
        }

        /// <summary>
        /// Load and apply saved layout for the active folder.
        /// </summary>
        public void LoadLayout()
        {
            if (string.IsNullOrEmpty(_currentRootFolder)) return;

            var layoutData = FolderLayoutData.Load(_currentRootFolder);
            foreach (var window in _activeWindows)
            {
                if (window == null) continue;
                var entry = layoutData.FindEntry(window.RelativePath);
                if (entry != null)
                    window.ApplyLayoutOverride(entry);
            }
            Debug.Log($"[WindowManager] Layout loaded for {_currentRootFolder}");
        }

        /// <summary>
        /// Update settings and recompute grid if needed.
        /// </summary>
        public void ApplySettings(AppSettings newSettings)
        {
            _settings = newSettings;
            _settings.Save();
            _gridLayout.UpdateSettings(_settings);

            if (TextureLoader.Instance != null)
                TextureLoader.Instance.SetMaxTextureSize(_settings.MaxTextureSize);
        }

        public AppSettings GetSettings() => _settings;
        public List<FolderData> GetCurrentFolders() => _currentFolders;
        public List<ImageWindow> GetActiveWindows() => _activeWindows;
        public List<ImageRow> GetActiveRows() => _activeRows;
        public string GetCurrentRootFolder() => _currentRootFolder;
        public ViewMode CurrentMode => _settings.Mode;

        /// <summary>
        /// Switch between Normal and Grid mode. Cleans up the current mode's
        /// windows and activates the appropriate UI.
        /// </summary>
        public void SetMode(ViewMode mode)
        {
            if (_settings.Mode == mode) return;

            // Clean up current mode
            CloseAllWindows();
            NormalModeManager.Instance?.CloseAllWindows();

            _settings.Mode = mode;
            _settings.Save();

            // Activate mode-specific UI
            OnModeChanged?.Invoke(mode);

            if (mode == ViewMode.Grid && !string.IsNullOrEmpty(_settings.LastRootFolder))
            {
                OpenFolder(_settings.LastRootFolder);
            }

            Debug.Log($"[WindowManager] Switched to {mode} mode");
        }

        public System.Action<ViewMode> OnModeChanged;

        private void OnApplicationPause(bool paused)
        {
            if (paused) SaveLayout();
        }

        private void OnApplicationQuit()
        {
            SaveLayout();
        }
    }
}
