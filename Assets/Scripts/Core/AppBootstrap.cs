using UnityEngine;
using PicoImageViewer.Android;
using PicoImageViewer.Data;

namespace PicoImageViewer.Core
{
    /// <summary>
    /// Application entry point. Initializes subsystems, requests permissions,
    /// and sets up XR references.
    /// Attach to a root GameObject in the scene.
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
        [SerializeField] private TextureLoader _textureLoader;
        [SerializeField] private AndroidPermissions _androidPermissions;

        [Header("UI")]
        [SerializeField] private UI.SettingsPanel _settingsPanel;

        private void Awake()
        {
            Application.targetFrameRate = 72; // Pico 4 Ultra default
            QualitySettings.vSyncCount = 0;
        }

        private void Start()
        {
            // Find XR camera if not assigned
            if (_headCamera == null)
            {
                Camera mainCam = Camera.main;
                if (mainCam != null) _headCamera = mainCam.transform;
            }

            // Wire up head transform to window manager
            if (_windowManager != null && _headCamera != null)
            {
                _windowManager.SetHeadTransform(_headCamera);
            }

            // Apply initial settings
            var settings = AppSettings.Load();
            if (_textureLoader != null)
            {
                _textureLoader.SetMaxTextureSize(settings.MaxTextureSize);
            }

            // Request permissions then proceed
            RequestPermissionsAndInit();
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
                    // Still initialize — user can manually enter paths
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

            // Auto-open last folder (WindowManager.Start() also does this,
            // but we ensure permissions are obtained first)
            if (!string.IsNullOrEmpty(settings.LastRootFolder))
            {
                _windowManager?.OpenFolder(settings.LastRootFolder);
            }

            Debug.Log("[AppBootstrap] Initialization complete");
        }
    }
}
