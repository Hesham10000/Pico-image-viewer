using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.UI;
using TMPro;
using PicoImageViewer.Core;
using PicoImageViewer.Data;
using PicoImageViewer.Interaction;

namespace PicoImageViewer.UI
{
    /// <summary>
    /// Controller for a single image window panel in world space.
    /// Manages the window's visual state, texture display, and user controls.
    /// Attach to the root of the ImageWindow prefab.
    /// </summary>
    public class ImageWindow : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private RawImage _imageDisplay;
        [SerializeField] private TextMeshProUGUI _titleText;
        [SerializeField] private TextMeshProUGUI _folderLabel;
        [SerializeField] private RectTransform _windowRect;
        [SerializeField] private RectTransform _titleBar;
        [SerializeField] private RectTransform _imageContainer;
        [SerializeField] private GameObject _loadingIndicator;

        [Header("Control Bar")]
        [SerializeField] private RectTransform _controlBar;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Slider _curvatureSlider;
        [SerializeField] private Slider _resizeSlider;

        [Header("Legacy Buttons (optional)")]
        [SerializeField] private Button _resetSizeButton;
        [SerializeField] private Button _resetPositionButton;
        [SerializeField] private Button _zoomInButton;
        [SerializeField] private Button _zoomOutButton;
        [SerializeField] private Button _fitButton;
        [SerializeField] private Button _aspectToggleButton;

        [Header("Resize Handles")]
        [SerializeField] private WindowResizeHandle[] _resizeHandles;

        // Data
        private ImageData _imageData;
        private Vector3 _gridPosition;
        private Quaternion _gridRotation;
        private float _defaultWidth;
        private float _defaultHeight;
        private bool _maintainAspectRatio = true;
        private float _imageAspect = 1f;
        private bool _isTextureLoaded;

        // Normal mode: sibling images in the same folder for joystick cycling
        private List<ImageData> _siblingImages;
        private int _currentSiblingIndex = -1;

        // Window sizing (in world-space meters, mapped to Canvas sizeDelta)
        private float _currentWidth;
        private float _currentHeight;
        private float _canvasScale = 0.001f; // prefab's original localScale (preserved)
        private const float MinWindowSize = 0.1f;
        private const float MaxWindowSize = 5.0f;
        private const float ZoomStep = 0.1f;

        // Curvature (0 = flat, 1 = max cylindrical curve)
        private float _curvature;
        public float Curvature => _curvature;

        // 3D curved mesh objects (created at runtime for real VR curvature)
        private GameObject _curvedMeshGO;
        private MeshFilter _curvedMeshFilter;
        private MeshRenderer _curvedMeshRenderer;
        private Material _curvedMeshMaterial;
        private const int CurveMeshSegments = 32;

        // Original control bar Z position (for restoring when curvature is removed)
        private float _controlBarOriginalZ;

        // Resize scale multiplier driven by the resize slider (0.5 = half, 1 = default, 2 = double)
        private float _resizeScale = 1f;
        public float ResizeScale => _resizeScale;

        // Reference to parent row in grid mode (set by WindowManager)
        private ImageRow _parentRow;

        public ImageData Data => _imageData;
        public string RelativePath => _imageData?.RelativePath;
        public bool IsHidden { get; private set; }
        public List<ImageData> SiblingImages => _siblingImages;

        /// <summary>
        /// Set the parent ImageRow (called by WindowManager in grid mode).
        /// </summary>
        public void SetParentRow(ImageRow row)
        {
            _parentRow = row;
        }

        #region Initialization

        public void Initialize(ImageData imageData, float width, float height,
            Vector3 gridPos, Quaternion gridRot)
        {
            // Preserve the prefab's canvas scale (typically 0.001 for world-space UI)
            _canvasScale = transform.localScale.x;
            if (_canvasScale <= 0f) _canvasScale = 0.001f;

            _imageData = imageData;
            _defaultWidth = width;
            _defaultHeight = height;
            _gridPosition = gridPos;
            _gridRotation = gridRot;

            _currentWidth = width;
            _currentHeight = height;

            // Set title
            if (_titleText != null)
                _titleText.text = imageData.FileName;
            if (_folderLabel != null)
                _folderLabel.text = imageData.FolderName;

            // Position at grid slot
            transform.position = gridPos;
            transform.rotation = gridRot;
            ApplySize();

            Debug.Log($"[ImageWindow] Initialized '{imageData.FileName}' at " +
                      $"pos={transform.position}, rot={transform.rotation.eulerAngles}, " +
                      $"scale={transform.localScale}, sizeDelta={GetComponent<RectTransform>()?.sizeDelta}, " +
                      $"canvasScale={_canvasScale}, size={_currentWidth}x{_currentHeight}m, " +
                      $"canvas={GetComponent<Canvas>()?.enabled}, " +
                      $"imageDisplay={(_imageDisplay != null ? "OK" : "NULL")}");

            // Auto-discover control bar if not assigned
            AutoDiscoverControlBar();

            // Store original control bar position for curvature reset
            if (_controlBar != null)
                _controlBarOriginalZ = _controlBar.localPosition.z;

            // Ensure Canvas has TrackedDeviceGraphicRaycaster for XR interaction
            EnsureXRGraphicRaycaster();

            // Wire up buttons
            SetupButtons();

            // Show loading state
            SetLoadingState(true);

            // Start async texture load
            if (TextureLoader.Instance != null)
            {
                TextureLoader.Instance.LoadAsync(imageData.FullPath, OnTextureLoaded);
            }
        }

        /// <summary>
        /// Apply a saved layout override (position, rotation, scale, visibility).
        /// </summary>
        public void ApplyLayoutOverride(WindowLayoutEntry entry)
        {
            if (entry == null) return;

            transform.position = entry.Position;
            transform.rotation = entry.Rotation;
            _currentWidth = entry.Width;
            _currentHeight = entry.Height;

            // Restore canvas scale if the saved entry had the old localScale behaviour
            if (entry.Scale.x > 0.01f)
                _canvasScale = 0.001f; // reset to standard VR canvas scale
            ApplySize();

            // NOTE: We intentionally do NOT restore IsHidden here.
            // Freshly spawned windows should always start visible.
            // The user can close them manually if desired.
        }

        /// <summary>
        /// Creates a layout entry snapshot of this window's current state.
        /// </summary>
        public WindowLayoutEntry CreateLayoutEntry()
        {
            return new WindowLayoutEntry
            {
                RelativePath = _imageData.RelativePath,
                Position = transform.position,
                Rotation = transform.rotation,
                Scale = transform.localScale,
                Width = _currentWidth,
                Height = _currentHeight,
                IsHidden = IsHidden
            };
        }

        #endregion

        #region Buttons

        private void SetupButtons()
        {
            // Control bar buttons
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Hide);
            if (_settingsButton != null)
                _settingsButton.onClick.AddListener(OnSettingsClicked);
            if (_curvatureSlider != null)
            {
                _curvatureSlider.minValue = 0f;
                _curvatureSlider.maxValue = 1f;
                _curvatureSlider.value = _curvature;
                _curvatureSlider.onValueChanged.AddListener(OnCurvatureChanged);
            }
            // Resize slider removed - resize is now via controller input
            // (grip + trigger + thumbstick). Hide the slider if present.
            if (_resizeSlider != null)
                _resizeSlider.gameObject.SetActive(false);

            // Legacy buttons (still wired if present in prefab)
            if (_resetSizeButton != null)
                _resetSizeButton.onClick.AddListener(ResetSize);
            if (_resetPositionButton != null)
                _resetPositionButton.onClick.AddListener(ResetPosition);
            if (_zoomInButton != null)
                _zoomInButton.onClick.AddListener(ZoomIn);
            if (_zoomOutButton != null)
                _zoomOutButton.onClick.AddListener(ZoomOut);
            if (_fitButton != null)
                _fitButton.onClick.AddListener(FitToImage);
            if (_aspectToggleButton != null)
                _aspectToggleButton.onClick.AddListener(ToggleAspectLock);
        }

        #endregion

        #region Texture Loading

        private void OnTextureLoaded(Texture2D texture, int origWidth, int origHeight)
        {
            // Guard: this callback may fire after the window has been destroyed
            // (e.g. if the folder was re-opened while textures were still loading).
            if (this == null) return;

            SetLoadingState(false);
            _isTextureLoaded = texture != null;

            if (texture == null)
            {
                Debug.LogWarning($"[ImageWindow] Failed to load: {_imageData.FullPath}");
                if (_titleText != null)
                    _titleText.text = _imageData.FileName + " (error)";
                return;
            }

            if (_imageDisplay != null)
            {
                _imageDisplay.texture = texture;
                _imageDisplay.color = Color.white;
            }

            // Also update curved mesh material if it exists
            if (_curvedMeshMaterial != null)
                _curvedMeshMaterial.mainTexture = texture;

            _imageAspect = (float)origWidth / Mathf.Max(origHeight, 1);

            Debug.Log($"[ImageWindow] Texture loaded '{_imageData.FileName}' " +
                      $"{origWidth}x{origHeight}, pos={transform.position}, " +
                      $"active={gameObject.activeSelf}, display={_imageDisplay?.gameObject.activeSelf}");

            // Auto-fit aspect if enabled
            var settings = AppSettings.Load();
            if (settings.AutoFitAspect)
            {
                FitToImage();
            }
        }

        private void SetLoadingState(bool loading)
        {
            if (_loadingIndicator != null)
                _loadingIndicator.SetActive(loading);
            if (_imageDisplay != null)
                _imageDisplay.gameObject.SetActive(!loading);
        }

        #endregion

        #region Size and Position

        private void ApplySize()
        {
            // Guard against being called on a destroyed window
            if (this == null) return;

            // Convert meters to canvas units (e.g. 0.5m / 0.001 = 500 units).
            // This preserves the prefab's original localScale (0.001) so child UI
            // elements remain correctly sized.
            var canvasRect = GetComponent<RectTransform>();
            if (canvasRect != null)
            {
                canvasRect.sizeDelta = new Vector2(
                    _currentWidth / _canvasScale,
                    _currentHeight / _canvasScale
                );
            }

            // Apply uniform scaling: base canvas scale * resize multiplier.
            // This scales the ENTIRE window (image + control bar + text) uniformly.
            float effectiveScale = _canvasScale * _resizeScale;
            transform.localScale = new Vector3(effectiveScale, effectiveScale, effectiveScale);
        }


        public void SetSize(float width, float height)
        {
            _currentWidth = Mathf.Clamp(width, MinWindowSize, MaxWindowSize);
            _currentHeight = Mathf.Clamp(height, MinWindowSize, MaxWindowSize);
            ApplySize();
        }

        public void Resize(float deltaWidth, float deltaHeight, bool maintainAspect)
        {
            float newW = _currentWidth + deltaWidth;
            float newH = _currentHeight + deltaHeight;

            if (maintainAspect && _imageAspect > 0)
            {
                // Use the larger delta to drive both axes
                if (Mathf.Abs(deltaWidth) > Mathf.Abs(deltaHeight))
                    newH = newW / _imageAspect;
                else
                    newW = newH * _imageAspect;
            }

            SetSize(newW, newH);
        }

        public float CurrentWidth => _currentWidth;
        public float CurrentHeight => _currentHeight;
        public bool MaintainAspectRatio => _maintainAspectRatio;
        public float ImageAspect => _imageAspect;

        private void ZoomIn()
        {
            Resize(ZoomStep, ZoomStep / _imageAspect, _maintainAspectRatio);
        }

        private void ZoomOut()
        {
            Resize(-ZoomStep, -ZoomStep / _imageAspect, _maintainAspectRatio);
        }

        private void FitToImage()
        {
            if (_imageAspect >= 1f)
            {
                // Landscape: width stays, height adjusts
                SetSize(_currentWidth, _currentWidth / _imageAspect);
            }
            else
            {
                // Portrait: height stays, width adjusts
                SetSize(_currentHeight * _imageAspect, _currentHeight);
            }
        }

        private void ResetSize()
        {
            _currentWidth = _defaultWidth;
            _currentHeight = _defaultHeight;
            ApplySize();
        }

        public void ResetPosition()
        {
            transform.position = _gridPosition;
            transform.rotation = _gridRotation;
        }

        public void UpdateGridSlot(Vector3 pos, Quaternion rot)
        {
            _gridPosition = pos;
            _gridRotation = rot;
        }

        private void ToggleAspectLock()
        {
            _maintainAspectRatio = !_maintainAspectRatio;
            if (_aspectToggleButton != null)
            {
                var txt = _aspectToggleButton.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null)
                    txt.text = _maintainAspectRatio ? "AR: On" : "AR: Off";
            }
        }

        #endregion

        #region Normal Mode - Sibling Image Cycling

        /// <summary>
        /// Set the list of sibling images (same folder) for joystick cycling.
        /// Called by NormalModeManager when spawning windows in Normal mode.
        /// </summary>
        public void SetSiblingImages(List<ImageData> siblings)
        {
            _siblingImages = siblings;
            // Find current index
            _currentSiblingIndex = -1;
            if (_siblingImages != null && _imageData != null)
            {
                for (int i = 0; i < _siblingImages.Count; i++)
                {
                    if (_siblingImages[i].FullPath == _imageData.FullPath)
                    {
                        _currentSiblingIndex = i;
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Cycle to the next image in the folder (joystick down).
        /// Replaces the current image in this window.
        /// </summary>
        public void CycleToNextImage()
        {
            if (_siblingImages == null || _siblingImages.Count <= 1) return;

            int nextIndex = _currentSiblingIndex + 1;
            if (nextIndex >= _siblingImages.Count) nextIndex = 0; // wrap around
            CycleToIndex(nextIndex);
        }

        /// <summary>
        /// Cycle to the previous image in the folder (joystick up).
        /// Replaces the current image in this window.
        /// </summary>
        public void CycleToPreviousImage()
        {
            if (_siblingImages == null || _siblingImages.Count <= 1) return;

            int prevIndex = _currentSiblingIndex - 1;
            if (prevIndex < 0) prevIndex = _siblingImages.Count - 1; // wrap around
            CycleToIndex(prevIndex);
        }

        private void CycleToIndex(int index)
        {
            if (_siblingImages == null || index < 0 || index >= _siblingImages.Count) return;

            _currentSiblingIndex = index;
            var newImage = _siblingImages[index];

            // Update data reference
            _imageData = newImage;

            // Update title
            if (_titleText != null)
                _titleText.text = newImage.FileName;
            if (_folderLabel != null)
                _folderLabel.text = newImage.FolderName;

            // Show loading and load new texture
            SetLoadingState(true);
            _isTextureLoaded = false;

            if (TextureLoader.Instance != null)
            {
                TextureLoader.Instance.LoadAsync(newImage.FullPath, OnTextureLoaded);
            }
        }

        #endregion

        #region Control Bar

        private void OnSettingsClicked()
        {
            // Find and toggle the SettingsPanel
            var settingsPanel = FindAnyObjectByType<SettingsPanel>();
            if (settingsPanel != null)
                settingsPanel.TogglePanel();
        }

        private void OnCurvatureChanged(float value)
        {
            _curvature = value;
            ApplyCurvature();
        }

        /// <summary>
        /// Set curvature externally (e.g. from a row control bar in grid mode).
        /// </summary>
        public void SetCurvature(float value)
        {
            _curvature = Mathf.Clamp01(value);
            if (_curvatureSlider != null)
                _curvatureSlider.SetValueWithoutNotify(_curvature);
            ApplyCurvature();
        }

        private void ApplyCurvature()
        {
            if (_curvature < 0.01f)
            {
                // Flat: show RawImage, hide curved mesh, restore control bar position
                if (_imageDisplay != null)
                    _imageDisplay.gameObject.SetActive(true);
                if (_curvedMeshGO != null)
                    _curvedMeshGO.SetActive(false);

                // Restore control bar to original position
                if (_controlBar != null)
                {
                    Vector3 cbPos = _controlBar.localPosition;
                    cbPos.z = _controlBarOriginalZ;
                    _controlBar.localPosition = cbPos;
                }
                return;
            }

            // Curved: hide RawImage, show 3D curved mesh
            if (_imageDisplay != null)
                _imageDisplay.gameObject.SetActive(false);

            EnsureCurvedMeshObjects();
            GenerateCurvedMesh();

            if (_curvedMeshGO != null)
                _curvedMeshGO.SetActive(true);

            // Move control bar forward so it's not hidden behind the curved mesh.
            // The curve extends toward the viewer at its edges; the control bar
            // needs to be in front of the maximum forward extent of the curve.
            if (_controlBar != null)
            {
                float canvasW = _currentWidth / _canvasScale;
                float arcAngleRad = _curvature * Mathf.PI;
                float radius = canvasW / arcAngleRad;
                // Maximum forward extension at arc edges (in canvas units)
                float maxForward = radius * (1f - Mathf.Cos(arcAngleRad * 0.5f));
                // Push control bar in front of curve + margin
                Vector3 cbPos = _controlBar.localPosition;
                cbPos.z = -(maxForward + 30f);
                _controlBar.localPosition = cbPos;
            }
        }

        /// <summary>
        /// Create the child GameObject for the curved 3D mesh if it doesn't exist.
        /// </summary>
        private void EnsureCurvedMeshObjects()
        {
            if (_curvedMeshGO != null) return;

            _curvedMeshGO = new GameObject("CurvedMesh");
            _curvedMeshGO.transform.SetParent(transform, false);
            _curvedMeshGO.transform.localPosition = Vector3.zero;
            _curvedMeshGO.transform.localRotation = Quaternion.identity;
            _curvedMeshGO.transform.localScale = Vector3.one;

            _curvedMeshFilter = _curvedMeshGO.AddComponent<MeshFilter>();
            _curvedMeshRenderer = _curvedMeshGO.AddComponent<MeshRenderer>();

            // Create an Unlit material for the curved mesh
            var shader = Shader.Find("Unlit/Texture");
            if (shader == null) shader = Shader.Find("UI/Default");
            _curvedMeshMaterial = new Material(shader);
            _curvedMeshMaterial.renderQueue = (int)RenderQueue.Transparent;
            _curvedMeshRenderer.material = _curvedMeshMaterial;
            _curvedMeshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _curvedMeshRenderer.receiveShadows = false;

            // Copy texture from RawImage if already loaded
            if (_imageDisplay != null && _imageDisplay.texture != null)
                _curvedMeshMaterial.mainTexture = _imageDisplay.texture;
        }

        /// <summary>
        /// Generate (or regenerate) the cylindrical curved mesh covering the full
        /// window area (image + control bar). The mesh is in canvas-unit local space.
        /// Curvature 0→1 maps to arc angle 0→180°.
        /// The mesh curves TOWARD the viewer (edges bend toward -Z).
        /// </summary>
        private void GenerateCurvedMesh()
        {
            if (_curvedMeshFilter == null) return;

            float canvasW = _currentWidth / _canvasScale;
            float canvasH = _currentHeight / _canvasScale;

            // Include control bar height in the curved surface
            float controlBarH = 0f;
            if (_controlBar != null)
                controlBarH = _controlBar.rect.height;
            float totalH = canvasH + controlBarH;

            float arcAngleRad = _curvature * Mathf.PI; // 0 to π
            if (arcAngleRad < 0.001f) return;

            float radius = canvasW / arcAngleRad;

            // Image UV occupies the top portion, control bar area at bottom
            float imageUVRatio = canvasH / totalH;

            var vertices = new Vector3[(CurveMeshSegments + 1) * 2];
            var uvs = new Vector2[(CurveMeshSegments + 1) * 2];
            var triangles = new int[CurveMeshSegments * 6];

            for (int x = 0; x <= CurveMeshSegments; x++)
            {
                float u = (float)x / CurveMeshSegments;
                float angle = (u - 0.5f) * arcAngleRad;

                float px = radius * Mathf.Sin(angle);
                float pz = radius * (Mathf.Cos(angle) - 1f);

                int vi = x * 2;
                // Bottom vertex (includes control bar area)
                vertices[vi] = new Vector3(px, -totalH * 0.5f, pz);
                uvs[vi] = new Vector2(u, 0f);
                // Top vertex
                vertices[vi + 1] = new Vector3(px, totalH * 0.5f, pz);
                uvs[vi + 1] = new Vector2(u, 1f);
            }

            for (int x = 0; x < CurveMeshSegments; x++)
            {
                int vi = x * 2;
                int ti = x * 6;
                triangles[ti + 0] = vi;
                triangles[ti + 1] = vi + 1;
                triangles[ti + 2] = vi + 2;
                triangles[ti + 3] = vi + 2;
                triangles[ti + 4] = vi + 1;
                triangles[ti + 5] = vi + 3;
            }

            var mesh = _curvedMeshFilter.mesh;
            if (mesh == null)
                mesh = new Mesh();
            mesh.Clear();
            mesh.name = "CurvedImage";
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            _curvedMeshFilter.mesh = mesh;

            // Update texture on curved mesh material
            if (_curvedMeshMaterial != null && _imageDisplay != null && _imageDisplay.texture != null)
                _curvedMeshMaterial.mainTexture = _imageDisplay.texture;
        }

        /// <summary>
        /// Auto-discover the control bar RectTransform from child hierarchy.
        /// </summary>
        private void AutoDiscoverControlBar()
        {
            if (_controlBar != null) return;

            // Search for common control bar names
            string[] names = { "ControlBar", "ControllerBar", "Controls", "BottomBar" };
            foreach (var name in names)
            {
                var found = FindChildRecursive(transform, name);
                if (found != null)
                {
                    _controlBar = found.GetComponent<RectTransform>();
                    if (_controlBar != null) break;
                }
            }
        }

        /// <summary>
        /// Ensure the Canvas has a TrackedDeviceGraphicRaycaster for XR ray interaction.
        /// Without this, XR controller rays cannot interact with Canvas UI buttons.
        /// </summary>
        private void EnsureXRGraphicRaycaster()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null) return;

            // Check for TrackedDeviceGraphicRaycaster (XRI UI support)
            var existingRaycaster = GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
            if (existingRaycaster == null)
            {
                // Remove any existing GraphicRaycaster that might conflict
                var graphicRaycaster = GetComponent<GraphicRaycaster>();
                if (graphicRaycaster != null)
                    Destroy(graphicRaycaster);

                gameObject.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
            }
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == name) return child;
                var found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private void OnResizeSliderChanged(float value)
        {
            _resizeScale = value;
            // Uniform scale: resize the entire window (image + control bar + text)
            // by changing the transform scale, not the canvas dimensions.
            ApplySize();
        }

        /// <summary>
        /// Set resize scale externally (e.g. from a row control bar in grid mode).
        /// </summary>
        public void SetResizeScale(float value)
        {
            _resizeScale = Mathf.Clamp(value, 0.25f, 3f);
            if (_resizeSlider != null)
                _resizeSlider.SetValueWithoutNotify(_resizeScale);
            OnResizeSliderChanged(_resizeScale);
        }

        #endregion

        #region Visibility

        public void Hide()
        {
            IsHidden = true;
            gameObject.SetActive(false);

            // Close the settings panel if it was opened from this window
            var settingsPanel = FindAnyObjectByType<SettingsPanel>();
            if (settingsPanel != null)
                settingsPanel.HidePanel();

            // Notify NormalModeManager if in normal mode
            NormalModeManager.Instance?.UnregisterWindow(this);

            // Notify parent ImageRow if in grid mode
            if (_parentRow != null)
                _parentRow.OnChildWindowHidden(this);
        }

        public void Show()
        {
            IsHidden = false;
            gameObject.SetActive(true);
        }

        #endregion

        private void OnDestroy()
        {
            // Control bar
            if (_closeButton != null) _closeButton.onClick.RemoveAllListeners();
            if (_settingsButton != null) _settingsButton.onClick.RemoveAllListeners();
            if (_curvatureSlider != null) _curvatureSlider.onValueChanged.RemoveAllListeners();
            if (_resizeSlider != null) _resizeSlider.onValueChanged.RemoveAllListeners();

            // Legacy
            if (_resetSizeButton != null) _resetSizeButton.onClick.RemoveAllListeners();
            if (_resetPositionButton != null) _resetPositionButton.onClick.RemoveAllListeners();
            if (_zoomInButton != null) _zoomInButton.onClick.RemoveAllListeners();
            if (_zoomOutButton != null) _zoomOutButton.onClick.RemoveAllListeners();
            if (_fitButton != null) _fitButton.onClick.RemoveAllListeners();
            if (_aspectToggleButton != null) _aspectToggleButton.onClick.RemoveAllListeners();

            // Curved mesh cleanup
            if (_curvedMeshMaterial != null)
                Destroy(_curvedMeshMaterial);
        }
    }
}
