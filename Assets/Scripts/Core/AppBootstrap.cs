using UnityEngine;
using PicoImageViewer.Android;
using PicoImageViewer.Data;
using PicoImageViewer.UI;

namespace PicoImageViewer.Core
{
    /// <summary>
    /// Application entry point. Initializes subsystems, requests permissions,
    /// sets up XR references, and manages mode-dependent UI visibility.
    /// Auto-discovers references at runtime if not assigned in Inspector.
    /// </summary>
    public class AppBootstrap : MonoBehaviour
    {
        [Header("XR References")]
        [SerializeField] private Transform _xrRig;
        [SerializeField] private Transform _headCamera;
        [SerializeField] private Transform _leftController;
        [SerializeField] private Transform _rightController;

        [Header("Managers")]
        [SerializeField] private WindowManager _windowManager;
        [SerializeField] private NormalModeManager _normalModeManager;
        [SerializeField] private TextureLoader _textureLoader;
        [SerializeField] private AndroidPermissions _androidPermissions;

        [Header("UI")]
        [SerializeField] private SettingsPanel _settingsPanel;
        [SerializeField] private FolderBrowserPanel _folderBrowserPanel;

        private void Awake()
        {
            Application.targetFrameRate = 72; // Pico 4 Ultra default
            QualitySettings.vSyncCount = 0;
        }

        private void Start()
        {
            // Auto-discover references if not assigned in Inspector
            AutoDiscoverReferences();

            // Wire up head transform to managers
            if (_headCamera != null)
            {
                _windowManager?.SetHeadTransform(_headCamera);
                _normalModeManager?.SetHeadTransform(_headCamera);
            }

            // Apply initial settings
            var settings = AppSettings.Load();
            if (_textureLoader != null)
            {
                _textureLoader.SetMaxTextureSize(settings.MaxTextureSize);
            }

            // Listen for mode changes to toggle UI
            if (_windowManager != null)
            {
                _windowManager.OnModeChanged += OnModeChanged;
            }

            // Request permissions then proceed
            RequestPermissionsAndInit();
        }

        /// <summary>
        /// Finds all references by searching the scene hierarchy.
        /// This eliminates the need to manually wire up Inspector references.
        /// </summary>
        private void AutoDiscoverReferences()
        {
            // Find XR Rig
            if (_xrRig == null)
            {
                var rigGO = GameObject.Find("XR Rig");
                if (rigGO != null) _xrRig = rigGO.transform;
            }

            // Find head camera
            if (_headCamera == null)
            {
                Camera mainCam = Camera.main;
                if (mainCam != null) _headCamera = mainCam.transform;
            }

            // Find controllers
            if (_leftController == null)
            {
                var go = GameObject.Find("Left Controller");
                if (go != null) _leftController = go.transform;
            }
            if (_rightController == null)
            {
                var go = GameObject.Find("Right Controller");
                if (go != null) _rightController = go.transform;
            }

            // Find managers
            if (_windowManager == null)
                _windowManager = FindAnyObjectByType<WindowManager>();
            if (_normalModeManager == null)
                _normalModeManager = FindAnyObjectByType<NormalModeManager>();
            if (_textureLoader == null)
                _textureLoader = FindAnyObjectByType<TextureLoader>();
            if (_androidPermissions == null)
                _androidPermissions = FindAnyObjectByType<AndroidPermissions>();

            // Find UI panels
            if (_settingsPanel == null)
                _settingsPanel = FindAnyObjectByType<SettingsPanel>();
            if (_folderBrowserPanel == null)
                _folderBrowserPanel = FindAnyObjectByType<FolderBrowserPanel>();

            Debug.Log($"[AppBootstrap] Auto-discovered references: " +
                      $"Camera={_headCamera != null}, WindowMgr={_windowManager != null}, " +
                      $"NormalMgr={_normalModeManager != null}, TextureLoader={_textureLoader != null}, " +
                      $"Settings={_settingsPanel != null}, FolderBrowser={_folderBrowserPanel != null}");
        }

        private void RequestPermissionsAndInit()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_androidPermissions == null)
            {
                _androidPermissions = gameObject.AddComponent<AndroidPermissions>();
            }

            _androidPermissions.RequestStoragePermissions(granted =>
            {
                if (granted)
                {
                    Debug.Log("[AppBootstrap] Storage permission granted");
                    InitializeApp();
                }
                else
                {
                    Debug.LogWarning("[AppBootstrap] Storage permission denied. " +
                                     "User must grant permission to browse files.");
                    InitializeApp();
                }
            });
#else
            InitializeApp();
#endif
        }

        private void InitializeApp()
        {
            var settings = AppSettings.Load();

            // Apply initial mode UI state
            OnModeChanged(settings.Mode);

            // In Grid mode, auto-open last folder
            if (settings.Mode == ViewMode.Grid && !string.IsNullOrEmpty(settings.LastRootFolder))
            {
                _windowManager?.OpenFolder(settings.LastRootFolder);
            }
            // In Normal mode, the FolderBrowserPanel handles its own initialization

            Debug.Log($"[AppBootstrap] Initialization complete (mode: {settings.Mode})");
        }

        /// <summary>
        /// Called when the view mode changes. Shows/hides the folder browser panel
        /// which is only needed in Normal mode.
        /// </summary>
        private void OnModeChanged(ViewMode mode)
        {
            bool isNormal = mode == ViewMode.Normal;

            // Folder browser is only visible in Normal mode
            if (_folderBrowserPanel != null)
                _folderBrowserPanel.SetVisible(isNormal);
        }

        private void OnDestroy()
        {
            if (_windowManager != null)
                _windowManager.OnModeChanged -= OnModeChanged;
        }
    }
}
