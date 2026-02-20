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
        [SerializeField] private Button _closeButton;
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

        // Window sizing (in world-space meters, mapped to localScale)
        private float _currentWidth;
        private float _currentHeight;
        private const float MinWindowSize = 0.1f;
        private const float MaxWindowSize = 5.0f;
        private const float ZoomStep = 0.1f;

        public ImageData Data => _imageData;
        public string RelativePath => _imageData?.RelativePath;
        public bool IsHidden { get; private set; }
        public List<ImageData> SiblingImages => _siblingImages;

        #region Initialization

        public void Initialize(ImageData imageData, float width, float height,
            Vector3 gridPos, Quaternion gridRot)
        {
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
            if (_closeButton != null)
                _closeButton.onClick.AddListener(Hide);
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
            // Size the window via RectTransform sizeDelta (in canvas units).
            // With localScale 0.001, sizeDelta 500 = 0.5m in world space.
            // So we multiply meters by 1000 to get canvas units.
            if (_windowRect != null)
            {
                _windowRect.sizeDelta = new Vector2(_currentWidth * 1000f, _currentHeight * 1000f);
            }
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
            if (_closeButton != null) _closeButton.onClick.RemoveAllListeners();
            if (_resetSizeButton != null) _resetSizeButton.onClick.RemoveAllListeners();
            if (_resetPositionButton != null) _resetPositionButton.onClick.RemoveAllListeners();
            if (_zoomInButton != null) _zoomInButton.onClick.RemoveAllListeners();
            if (_zoomOutButton != null) _zoomOutButton.onClick.RemoveAllListeners();
            if (_fitButton != null) _fitButton.onClick.RemoveAllListeners();
            if (_aspectToggleButton != null) _aspectToggleButton.onClick.RemoveAllListeners();
        }
    }
}
