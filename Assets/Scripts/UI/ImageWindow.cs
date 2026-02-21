using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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
        [SerializeField] private Button _resizeButton;

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

        // Resize mode: when active, controller movement resizes the window
        private bool _isResizeMode;
        public bool IsResizeMode => _isResizeMode;

        public ImageData Data => _imageData;
        public string RelativePath => _imageData?.RelativePath;
        public bool IsHidden { get; private set; }
        public List<ImageData> SiblingImages => _siblingImages;

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

            if (entry.IsHidden)
                Hide();
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
            if (_resizeButton != null)
                _resizeButton.onClick.AddListener(ToggleResizeMode);

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

            _imageAspect = (float)origWidth / Mathf.Max(origHeight, 1);

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

            // Ensure localScale stays at the original prefab scale
            transform.localScale = new Vector3(_canvasScale, _canvasScale, _canvasScale);
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
            // Apply curvature via material property if available.
            // The RawImage material should use a shader that supports a _Curvature parameter.
            if (_imageDisplay != null && _imageDisplay.material != null)
            {
                if (_imageDisplay.material.HasProperty("_Curvature"))
                    _imageDisplay.material.SetFloat("_Curvature", _curvature);
            }
        }

        private void ToggleResizeMode()
        {
            _isResizeMode = !_isResizeMode;

            // Visual feedback: change resize button color when active
            if (_resizeButton != null)
            {
                var img = _resizeButton.GetComponent<Image>();
                if (img != null)
                    img.color = _isResizeMode
                        ? new Color(0.24f, 0.53f, 0.95f, 1f) // accent blue when active
                        : new Color(0.17f, 0.20f, 0.27f, 1f); // normal dark
            }
        }

        /// <summary>
        /// Called by external resize logic when in resize mode.
        /// Applies controller movement delta to window size.
        /// </summary>
        public void ApplyResizeDelta(float deltaWidth, float deltaHeight)
        {
            if (!_isResizeMode) return;
            Resize(deltaWidth, deltaHeight, _maintainAspectRatio);
        }

        #endregion

        #region Visibility

        public void Hide()
        {
            IsHidden = true;
            gameObject.SetActive(false);

            // Notify NormalModeManager if in normal mode
            NormalModeManager.Instance?.UnregisterWindow(this);
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
            if (_resizeButton != null) _resizeButton.onClick.RemoveAllListeners();

            // Legacy
            if (_resetSizeButton != null) _resetSizeButton.onClick.RemoveAllListeners();
            if (_resetPositionButton != null) _resetPositionButton.onClick.RemoveAllListeners();
            if (_zoomInButton != null) _zoomInButton.onClick.RemoveAllListeners();
            if (_zoomOutButton != null) _zoomOutButton.onClick.RemoveAllListeners();
            if (_fitButton != null) _fitButton.onClick.RemoveAllListeners();
            if (_aspectToggleButton != null) _aspectToggleButton.onClick.RemoveAllListeners();
        }
    }
}
