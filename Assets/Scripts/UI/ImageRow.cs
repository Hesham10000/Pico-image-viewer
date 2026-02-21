using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PicoImageViewer.Core;
using PicoImageViewer.Data;

namespace PicoImageViewer.UI
{
    /// <summary>
    /// Container for a row of images in Grid mode. One ImageRow per subfolder.
    /// Provides a shared control bar (close, settings, curvature, resize) that
    /// applies to all child ImageWindow instances in this row.
    /// </summary>
    public class ImageRow : MonoBehaviour
    {
        [Header("Row Control Bar")]
        [SerializeField] private RectTransform _rowControlBar;
        [SerializeField] private Button _closeRowButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Slider _curvatureSlider;
        [SerializeField] private Slider _resizeSlider;
        [SerializeField] private TextMeshProUGUI _rowLabel;

        // Data
        private FolderData _folderData;
        private readonly List<ImageWindow> _childWindows = new List<ImageWindow>();
        private float _curvature;
        private float _resizeScale = 1f;
        private float _defaultWidth;
        private float _defaultHeight;

        public FolderData Folder => _folderData;
        public List<ImageWindow> ChildWindows => _childWindows;

        /// <summary>
        /// Initialize the row with folder data and default window dimensions.
        /// </summary>
        public void Initialize(FolderData folder, float defaultWidth, float defaultHeight)
        {
            _folderData = folder;
            _defaultWidth = defaultWidth;
            _defaultHeight = defaultHeight;

            if (_rowLabel != null)
                _rowLabel.text = folder.FolderName;

            SetupControlBar();
        }

        /// <summary>
        /// Register a child ImageWindow belonging to this row.
        /// </summary>
        public void AddChildWindow(ImageWindow window)
        {
            _childWindows.Add(window);
        }

        private void SetupControlBar()
        {
            if (_closeRowButton != null)
                _closeRowButton.onClick.AddListener(CloseRow);

            if (_settingsButton != null)
                _settingsButton.onClick.AddListener(OnSettingsClicked);

            if (_curvatureSlider != null)
            {
                _curvatureSlider.minValue = 0f;
                _curvatureSlider.maxValue = 1f;
                _curvatureSlider.value = 0f;
                _curvatureSlider.onValueChanged.AddListener(OnCurvatureChanged);
            }

            if (_resizeSlider != null)
            {
                _resizeSlider.minValue = 0.25f;
                _resizeSlider.maxValue = 3f;
                _resizeSlider.value = 1f;
                _resizeSlider.onValueChanged.AddListener(OnResizeChanged);
            }
        }

        private void OnSettingsClicked()
        {
            var settingsPanel = FindAnyObjectByType<SettingsPanel>();
            if (settingsPanel != null)
                settingsPanel.TogglePanel();
        }

        private void OnCurvatureChanged(float value)
        {
            _curvature = value;
            // Apply curvature to all child windows in this row
            foreach (var window in _childWindows)
            {
                if (window != null && !window.IsHidden)
                    window.SetCurvature(_curvature);
            }
        }

        private void OnResizeChanged(float value)
        {
            _resizeScale = value;
            // Apply resize scale to all child windows in this row
            foreach (var window in _childWindows)
            {
                if (window != null && !window.IsHidden)
                    window.SetResizeScale(_resizeScale);
            }
        }

        /// <summary>
        /// Close (destroy) all images in this row.
        /// </summary>
        public void CloseRow()
        {
            foreach (var window in _childWindows)
            {
                if (window != null && window.gameObject != null)
                    Destroy(window.gameObject);
            }
            _childWindows.Clear();

            // Destroy the row itself
            Destroy(gameObject);
        }

        /// <summary>
        /// Called by a child ImageWindow when it hides itself.
        /// If all children are hidden/destroyed, remove the row too.
        /// </summary>
        public void OnChildWindowHidden(ImageWindow window)
        {
            _childWindows.Remove(window);

            // If no more children, destroy the row container
            if (_childWindows.Count == 0)
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (_closeRowButton != null) _closeRowButton.onClick.RemoveAllListeners();
            if (_settingsButton != null) _settingsButton.onClick.RemoveAllListeners();
            if (_curvatureSlider != null) _curvatureSlider.onValueChanged.RemoveAllListeners();
            if (_resizeSlider != null) _resizeSlider.onValueChanged.RemoveAllListeners();
        }
    }
}
