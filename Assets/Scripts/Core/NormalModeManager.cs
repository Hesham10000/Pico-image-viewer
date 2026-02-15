using System.Collections.Generic;
using UnityEngine;
using PicoImageViewer.Data;
using PicoImageViewer.UI;

namespace PicoImageViewer.Core
{
    /// <summary>
    /// Manages window spawning in Normal mode. When the user opens an image from
    /// the folder browser, a new window is placed relative to the last opened window
    /// (right, left, above, or below) in a natural tiling pattern.
    ///
    /// Each window retains a reference to its sibling images (same folder),
    /// enabling joystick-based prev/next image cycling.
    /// </summary>
    public class NormalModeManager : MonoBehaviour
    {
        public static NormalModeManager Instance { get; private set; }

        [Header("References")]
        [SerializeField] private GameObject _imageWindowPrefab;
        [SerializeField] private Transform _windowContainer;
        [SerializeField] private Transform _headTransform;

        [Header("Placement")]
        [SerializeField] private float _initialForwardDistance = 2.0f;

        private AppSettings _settings;
        private readonly List<ImageWindow> _openWindows = new List<ImageWindow>();
        private ImageWindow _lastOpenedWindow;

        // Track placement direction: cycles right → right → below → right → right → below ...
        private int _windowOpenCount;

        // Events
        public System.Action<int> OnWindowCountChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            _settings = AppSettings.Load();
            AutoDiscoverReferences();
        }

        private void AutoDiscoverReferences()
        {
            if (_imageWindowPrefab == null)
            {
                _imageWindowPrefab = Resources.Load<GameObject>("ImageWindow");
                if (_imageWindowPrefab == null)
                {
                    var prefabs = Resources.FindObjectsOfTypeAll<UI.ImageWindow>();
                    foreach (var p in prefabs)
                    {
                        if (p.gameObject.scene.name == null)
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
                    var container = new GameObject("WindowContainer");
                    container.transform.SetParent(transform.parent, false);
                    _windowContainer = container.transform;
                }
            }

            Debug.Log($"[NormalModeManager] Auto-discovered: Prefab={_imageWindowPrefab != null}, Container={_windowContainer != null}");
        }

        public void SetHeadTransform(Transform head)
        {
            _headTransform = head;
        }

        /// <summary>
        /// Open an image in a new floating window. Places it next to the last window.
        /// </summary>
        /// <param name="imageData">The image to display.</param>
        /// <param name="siblingImages">All images in the same folder, for joystick cycling.</param>
        public void OpenImage(ImageData imageData, List<ImageData> siblingImages)
        {
            if (_imageWindowPrefab == null)
            {
                Debug.LogError("[NormalModeManager] ImageWindow prefab not assigned!");
                return;
            }

            _settings = AppSettings.Load();

            // Compute placement position
            Vector3 position;
            Quaternion rotation;
            ComputeWindowPlacement(out position, out rotation);

            float width = _settings.DefaultWindowWidth * _settings.WindowScaleMultiplier;
            float height = _settings.DefaultWindowHeight * _settings.WindowScaleMultiplier;

            // Spawn window
            GameObject go = Instantiate(_imageWindowPrefab, _windowContainer);
            var window = go.GetComponent<ImageWindow>();
            if (window == null)
            {
                Debug.LogError("[NormalModeManager] Prefab missing ImageWindow component!");
                Destroy(go);
                return;
            }

            window.Initialize(imageData, width, height, position, rotation);

            // Attach sibling data for joystick cycling
            window.SetSiblingImages(siblingImages);

            _openWindows.Add(window);
            _lastOpenedWindow = window;
            _windowOpenCount++;

            OnWindowCountChanged?.Invoke(_openWindows.Count);

            Debug.Log($"[NormalModeManager] Opened window for {imageData.FileName} " +
                      $"(siblings: {siblingImages.Count})");
        }

        /// <summary>
        /// Compute where to place the next window relative to the last one.
        /// Pattern: first window in front of user, then tile right/below.
        /// </summary>
        private void ComputeWindowPlacement(out Vector3 position, out Quaternion rotation)
        {
            if (_headTransform == null)
                _headTransform = Camera.main?.transform;

            float spacing = _settings != null ? _settings.NormalModeSpacing : 0.6f;
            float width = _settings != null
                ? _settings.DefaultWindowWidth * _settings.WindowScaleMultiplier
                : 0.5f;
            float height = _settings != null
                ? _settings.DefaultWindowHeight * _settings.WindowScaleMultiplier
                : 0.4f;

            if (_lastOpenedWindow == null || _openWindows.Count == 0)
            {
                // First window: place in front of user
                Vector3 forward = _headTransform != null ? _headTransform.forward : Vector3.forward;
                forward.y = 0;
                if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
                forward.Normalize();

                float dist = _settings != null ? _settings.GridForwardOffset : _initialForwardDistance;
                Vector3 origin = _headTransform != null ? _headTransform.position : Vector3.zero;
                position = origin + forward * dist;
                rotation = Quaternion.LookRotation(-forward, Vector3.up);
                return;
            }

            // Determine placement direction based on count
            // Pattern: every 3rd window goes below-left (new row), others go right
            PlacementDirection dir = GetNextPlacementDirection();

            Vector3 lastPos = _lastOpenedWindow.transform.position;
            Quaternion lastRot = _lastOpenedWindow.transform.rotation;
            rotation = lastRot;

            Vector3 rightDir = lastRot * Vector3.left;   // window's local right
            Vector3 downDir = lastRot * Vector3.down;     // window's local down

            float stepRight = width + spacing;
            float stepDown = height + spacing;

            switch (dir)
            {
                case PlacementDirection.Right:
                    position = lastPos + rightDir * stepRight;
                    break;
                case PlacementDirection.Left:
                    position = lastPos - rightDir * stepRight;
                    break;
                case PlacementDirection.Below:
                    position = lastPos + downDir * stepDown;
                    break;
                case PlacementDirection.Above:
                    position = lastPos - downDir * stepDown;
                    break;
                default:
                    position = lastPos + rightDir * stepRight;
                    break;
            }
        }

        private PlacementDirection GetNextPlacementDirection()
        {
            // Simple pattern: place right, and start a new row every 3 windows
            int countInRow = _windowOpenCount % 3;
            if (countInRow == 0 && _windowOpenCount > 0)
                return PlacementDirection.Below;
            return PlacementDirection.Right;
        }

        /// <summary>
        /// Close all normal-mode windows.
        /// </summary>
        public void CloseAllWindows()
        {
            foreach (var window in _openWindows)
            {
                if (window != null && window.gameObject != null)
                    Destroy(window.gameObject);
            }
            _openWindows.Clear();
            _lastOpenedWindow = null;
            _windowOpenCount = 0;
            OnWindowCountChanged?.Invoke(0);
        }

        /// <summary>
        /// Remove a specific window from tracking (called when window closes itself).
        /// </summary>
        public void UnregisterWindow(ImageWindow window)
        {
            _openWindows.Remove(window);
            if (_lastOpenedWindow == window)
            {
                _lastOpenedWindow = _openWindows.Count > 0
                    ? _openWindows[_openWindows.Count - 1]
                    : null;
            }
            OnWindowCountChanged?.Invoke(_openWindows.Count);
        }

        public List<ImageWindow> GetOpenWindows() => _openWindows;

        public void ApplySettings(AppSettings settings)
        {
            _settings = settings;
        }

        private enum PlacementDirection
        {
            Right,
            Left,
            Below,
            Above
        }
    }
}
