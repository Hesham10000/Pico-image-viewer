using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using PicoImageViewer.Android;
using PicoImageViewer.Data;
using PicoImageViewer.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace PicoImageViewer.Core
{
    /// <summary>
    /// Application entry point. Initializes subsystems, requests permissions,
    /// sets up XR references, and manages mode-dependent UI visibility.
    /// Auto-discovers references at runtime if not assigned in Inspector.
    /// </summary>
    public class AppBootstrap : MonoBehaviour
    {
        public static AppBootstrap Instance { get; private set; }

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

        [Header("Panel Placement")]
        [SerializeField] private float _panelDistance = 1.5f;
        [SerializeField] private float _panelVerticalOffset = -0.05f;

        private bool _initialized;

        private void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 72; // Pico 4 Ultra default
            QualitySettings.vSyncCount = 0;
        }

        private void Start()
        {
            // Auto-discover references if not assigned in Inspector
            AutoDiscoverReferences();

            EnsureXrUiRuntime();
            if (RuntimeDebugOverlay.Instance == null)
            {
                var debugOverlay = new GameObject("RuntimeDebugOverlay");
                debugOverlay.AddComponent<RuntimeDebugOverlay>();
            }

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

            RecenterUI();

            // Request permissions then proceed
            RequestPermissionsAndInit();
        }

        private void EnsureXrUiRuntime()
        {
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var eventSystemGo = new GameObject("EventSystem");
                eventSystemGo.AddComponent<EventSystem>();
                eventSystemGo.AddComponent<InputSystemUIInputModule>();
                Debug.Log("[AppBootstrap] Created EventSystem + InputSystemUIInputModule");
            }

            var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                if (canvas.renderMode == RenderMode.WorldSpace &&
                    canvas.GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                {
                    canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
                }
            }

            Debug.Log($"[AppBootstrap] XR UI setup complete (world canvases: {canvases.Length})");
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
                HandlePermissionResult(granted);
            });
#else
            InitializeApp();
#endif
        }

        public void RequestStoragePermissionFromUI()
        {
            if (_androidPermissions == null)
                _androidPermissions = FindAnyObjectByType<AndroidPermissions>();

            if (_androidPermissions == null)
                return;

            _folderBrowserPanel?.SetPermissionState(false, "Requesting storage permission...");
            _androidPermissions.RequestStoragePermissions(HandlePermissionResult);
        }

        private void HandlePermissionResult(bool granted)
        {
            if (granted)
            {
                Debug.Log("[AppBootstrap] Storage permission granted");
                RuntimeDebugOverlay.Instance?.Log("Permission: granted");
                _folderBrowserPanel?.SetPermissionState(true, "Permission granted");
                RecenterUI();
                InitializeApp();
            }
            else
            {
                Debug.LogWarning("[AppBootstrap] Storage permission denied");
                RuntimeDebugOverlay.Instance?.Log("Permission: denied");
                _folderBrowserPanel?.SetPermissionState(false,
                    "Storage permission needed. Use 'Grant Access' and allow Photos/Files.");
                InitializeApp();
            }
        }

        private void InitializeApp()
        {
            if (_initialized)
                return;
            _initialized = true;

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

        public void RecenterUI()
        {
            if (_headCamera == null)
                _headCamera = Camera.main?.transform;
            if (_headCamera == null)
                return;

            var forward = _headCamera.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f)
                forward = Vector3.forward;
            forward.Normalize();

            PlacePanelInFront(_folderBrowserPanel != null ? _folderBrowserPanel.transform as RectTransform : null, forward, 0f);
            PlacePanelInFront(_settingsPanel != null ? _settingsPanel.transform as RectTransform : null, forward, -0.45f);
            var msg = $"Recenter UI @ {_headCamera.position}";
            Debug.Log($"[AppBootstrap] {msg}");
            RuntimeDebugOverlay.Instance?.Log(msg);
        }

        private void PlacePanelInFront(RectTransform panel, Vector3 forward, float horizontalOffset)
        {
            if (panel == null) return;

            var right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 target = _headCamera.position + forward * _panelDistance + right * horizontalOffset;
            target.y = _headCamera.position.y + _panelVerticalOffset;

            panel.position = target;
            panel.rotation = Quaternion.LookRotation(panel.position - _headCamera.position, Vector3.up);
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

            if (Instance == this)
                Instance = null;
        }
    }
}
