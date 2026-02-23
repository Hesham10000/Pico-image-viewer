using UnityEngine;
using UnityEngine.EventSystems;
using PicoImageViewer.Android;
using PicoImageViewer.Data;
using PicoImageViewer.Interaction;
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

            // Ensure interaction components exist
            EnsureInteractionComponents();

            // Ensure XR UI Input Module exists (required for XR ray → Canvas UI interaction)
            EnsureXRUIInputModule();

            Debug.Log($"[AppBootstrap] Auto-discovered references: " +
                      $"Camera={_headCamera != null}, WindowMgr={_windowManager != null}, " +
                      $"NormalMgr={_normalModeManager != null}, TextureLoader={_textureLoader != null}, " +
                      $"Settings={_settingsPanel != null}, FolderBrowser={_folderBrowserPanel != null}");
        }

        /// <summary>
        /// Ensure interaction handler components exist in the scene.
        /// Creates them at runtime if they weren't placed in the scene via SceneSetup.
        /// </summary>
        private void EnsureInteractionComponents()
        {
            // ControllerResizeHandler: grip + trigger + thumbstick to resize windows
            if (FindAnyObjectByType<ControllerResizeHandler>() == null)
            {
                var go = new GameObject("ControllerResizeHandler");
                go.transform.SetParent(transform.parent, false);
                go.AddComponent<ControllerResizeHandler>();
                Debug.Log("[AppBootstrap] Auto-created ControllerResizeHandler");
            }

            // JoystickImageNavigator: thumbstick to cycle images
            if (FindAnyObjectByType<JoystickImageNavigator>() == null)
            {
                var go = new GameObject("JoystickImageNavigator");
                go.transform.SetParent(transform.parent, false);
                go.AddComponent<JoystickImageNavigator>();
                Debug.Log("[AppBootstrap] Auto-created JoystickImageNavigator");
            }
        }

        /// <summary>
        /// Ensure an XR UI Input Module exists so that XR rays can interact with
        /// Canvas UI elements (buttons, sliders). Without this, the
        /// TrackedDeviceGraphicRaycaster on canvases won't receive input events.
        /// </summary>
        private void EnsureXRUIInputModule()
        {
            // Check for any existing EventSystem
            var eventSystem = FindAnyObjectByType<EventSystem>();
            if (eventSystem == null)
            {
                var go = new GameObject("EventSystem");
                go.transform.SetParent(transform.parent, false);
                eventSystem = go.AddComponent<EventSystem>();
            }

            // Replace standard InputModule with XR UI Input Module if needed
            var xrInputModule = eventSystem.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>();
            if (xrInputModule == null)
            {
                // Remove standard input modules that conflict
                var standaloneInput = eventSystem.GetComponent<StandaloneInputModule>();
                if (standaloneInput != null) Destroy(standaloneInput);

                eventSystem.gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule>();
                Debug.Log("[AppBootstrap] Added XRUIInputModule to EventSystem");
            }
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
            bool hasContent = false;
            if (settings.Mode == ViewMode.Grid && !string.IsNullOrEmpty(settings.LastRootFolder))
            {
                _windowManager?.OpenFolder(settings.LastRootFolder);
                // Check if any windows were actually created
                hasContent = _windowManager != null && _windowManager.GetActiveWindows().Count > 0;
            }
            else if (settings.Mode == ViewMode.Normal)
            {
                // In Normal mode, the FolderBrowserPanel handles its own initialization
                // Consider it "has content" if folder browser is active
                hasContent = _folderBrowserPanel != null;
            }

            // If no content to display, auto-show settings so user can pick a folder
            if (!hasContent && _settingsPanel != null)
            {
                _settingsPanel.ShowPanel();
            }

            Debug.Log($"[AppBootstrap] Initialization complete (mode: {settings.Mode}, hasContent: {hasContent})");
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
