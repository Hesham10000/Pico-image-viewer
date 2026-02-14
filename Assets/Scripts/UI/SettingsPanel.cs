using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PicoImageViewer.Core;
using PicoImageViewer.Data;
using PicoImageViewer.Android;

namespace PicoImageViewer.UI
{
    /// <summary>
    /// World-space settings panel. Provides controls for all app settings,
    /// folder picking, and utility actions.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [Header("Folder")]
        [SerializeField] private TMP_InputField _folderPathInput;
        [SerializeField] private Button _browseButton;
        [SerializeField] private Button _openFolderButton;
        [SerializeField] private TextMeshProUGUI _statusText;

        [Header("Grid Origin")]
        [SerializeField] private Slider _forwardOffsetSlider;
        [SerializeField] private TextMeshProUGUI _forwardOffsetLabel;
        [SerializeField] private Slider _upOffsetSlider;
        [SerializeField] private TextMeshProUGUI _upOffsetLabel;
        [SerializeField] private Button _resetOriginButton;

        [Header("Layout")]
        [SerializeField] private Slider _rowSpacingSlider;
        [SerializeField] private TextMeshProUGUI _rowSpacingLabel;
        [SerializeField] private Slider _colSpacingSlider;
        [SerializeField] private TextMeshProUGUI _colSpacingLabel;
        [SerializeField] private Slider _windowWidthSlider;
        [SerializeField] private TextMeshProUGUI _windowWidthLabel;
        [SerializeField] private Slider _windowHeightSlider;
        [SerializeField] private TextMeshProUGUI _windowHeightLabel;
        [SerializeField] private Slider _scaleMultiplierSlider;
        [SerializeField] private TextMeshProUGUI _scaleMultiplierLabel;
        [SerializeField] private Toggle _autoFitAspectToggle;

        [Header("Interaction")]
        [SerializeField] private Slider _dragSensitivitySlider;
        [SerializeField] private TextMeshProUGUI _dragSensitivityLabel;
        [SerializeField] private Slider _resizeSensitivitySlider;
        [SerializeField] private TextMeshProUGUI _resizeSensitivityLabel;
        [SerializeField] private Toggle _snapToGridToggle;

        [Header("Texture")]
        [SerializeField] private TMP_Dropdown _maxTextureSizeDropdown;

        [Header("Utilities")]
        [SerializeField] private Button _rescanButton;
        [SerializeField] private Button _resetAllButton;
        [SerializeField] private Button _closeAllButton;
        [SerializeField] private Button _saveLayoutButton;
        [SerializeField] private Button _loadLayoutButton;

        [Header("Panel Control")]
        [SerializeField] private Button _togglePanelButton; // external button to show/hide
        [SerializeField] private GameObject _panelContent;

        private AppSettings _settings;

        private void Start()
        {
            _settings = AppSettings.Load();
            PopulateUI();
            BindEvents();
        }

        private void PopulateUI()
        {
            // Folder
            if (_folderPathInput != null)
                _folderPathInput.text = _settings.LastRootFolder;

            // Grid origin
            SetupSlider(_forwardOffsetSlider, _forwardOffsetLabel, _settings.GridForwardOffset,
                0.5f, 10f, "Forward: {0:F1}m");
            SetupSlider(_upOffsetSlider, _upOffsetLabel, _settings.GridUpOffset,
                -3f, 3f, "Up: {0:F1}m");

            // Layout
            SetupSlider(_rowSpacingSlider, _rowSpacingLabel, _settings.RowSpacing,
                0.0f, 3f, "Row Gap: {0:F2}m");
            SetupSlider(_colSpacingSlider, _colSpacingLabel, _settings.ColumnSpacing,
                0.0f, 3f, "Col Gap: {0:F2}m");
            SetupSlider(_windowWidthSlider, _windowWidthLabel, _settings.DefaultWindowWidth,
                0.1f, 2f, "Width: {0:F2}m");
            SetupSlider(_windowHeightSlider, _windowHeightLabel, _settings.DefaultWindowHeight,
                0.1f, 2f, "Height: {0:F2}m");
            SetupSlider(_scaleMultiplierSlider, _scaleMultiplierLabel, _settings.WindowScaleMultiplier,
                0.1f, 5f, "Scale: {0:F1}x");

            if (_autoFitAspectToggle != null)
                _autoFitAspectToggle.isOn = _settings.AutoFitAspect;

            // Interaction
            SetupSlider(_dragSensitivitySlider, _dragSensitivityLabel, _settings.DragSensitivity,
                0.1f, 5f, "Drag: {0:F1}x");
            SetupSlider(_resizeSensitivitySlider, _resizeSensitivityLabel, _settings.ResizeSensitivity,
                0.1f, 5f, "Resize: {0:F1}x");

            if (_snapToGridToggle != null)
                _snapToGridToggle.isOn = _settings.SnapToGrid;

            // Texture size dropdown
            if (_maxTextureSizeDropdown != null)
            {
                _maxTextureSizeDropdown.ClearOptions();
                _maxTextureSizeDropdown.AddOptions(new System.Collections.Generic.List<string>
                    { "512", "1024", "2048", "4096" });
                int idx = 2; // default 2048
                if (_settings.MaxTextureSize <= 512) idx = 0;
                else if (_settings.MaxTextureSize <= 1024) idx = 1;
                else if (_settings.MaxTextureSize <= 2048) idx = 2;
                else idx = 3;
                _maxTextureSizeDropdown.value = idx;
            }
        }

        private void SetupSlider(Slider slider, TextMeshProUGUI label, float value,
            float min, float max, string format)
        {
            if (slider == null) return;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            UpdateLabel(label, format, value);
        }

        private void UpdateLabel(TextMeshProUGUI label, string format, float value)
        {
            if (label != null)
                label.text = string.Format(format, value);
        }

        private void BindEvents()
        {
            // Folder
            if (_browseButton != null)
                _browseButton.onClick.AddListener(OnBrowseFolder);
            if (_openFolderButton != null)
                _openFolderButton.onClick.AddListener(OnOpenFolder);

            // Grid origin sliders
            BindSlider(_forwardOffsetSlider, _forwardOffsetLabel, "Forward: {0:F1}m",
                v => _settings.GridForwardOffset = v);
            BindSlider(_upOffsetSlider, _upOffsetLabel, "Up: {0:F1}m",
                v => _settings.GridUpOffset = v);

            // Layout sliders
            BindSlider(_rowSpacingSlider, _rowSpacingLabel, "Row Gap: {0:F2}m",
                v => _settings.RowSpacing = v);
            BindSlider(_colSpacingSlider, _colSpacingLabel, "Col Gap: {0:F2}m",
                v => _settings.ColumnSpacing = v);
            BindSlider(_windowWidthSlider, _windowWidthLabel, "Width: {0:F2}m",
                v => _settings.DefaultWindowWidth = v);
            BindSlider(_windowHeightSlider, _windowHeightLabel, "Height: {0:F2}m",
                v => _settings.DefaultWindowHeight = v);
            BindSlider(_scaleMultiplierSlider, _scaleMultiplierLabel, "Scale: {0:F1}x",
                v => _settings.WindowScaleMultiplier = v);

            if (_autoFitAspectToggle != null)
                _autoFitAspectToggle.onValueChanged.AddListener(v => _settings.AutoFitAspect = v);

            // Interaction sliders
            BindSlider(_dragSensitivitySlider, _dragSensitivityLabel, "Drag: {0:F1}x",
                v => _settings.DragSensitivity = v);
            BindSlider(_resizeSensitivitySlider, _resizeSensitivityLabel, "Resize: {0:F1}x",
                v => _settings.ResizeSensitivity = v);

            if (_snapToGridToggle != null)
                _snapToGridToggle.onValueChanged.AddListener(v => _settings.SnapToGrid = v);

            // Texture size
            if (_maxTextureSizeDropdown != null)
            {
                _maxTextureSizeDropdown.onValueChanged.AddListener(idx =>
                {
                    int[] sizes = { 512, 1024, 2048, 4096 };
                    _settings.MaxTextureSize = sizes[Mathf.Clamp(idx, 0, sizes.Length - 1)];
                });
            }

            // Utilities
            if (_rescanButton != null)
                _rescanButton.onClick.AddListener(() => {
                    ApplyAndSave();
                    WindowManager.Instance?.RescanFolder();
                    UpdateStatus("Re-scanned folder");
                });
            if (_resetAllButton != null)
                _resetAllButton.onClick.AddListener(() => {
                    WindowManager.Instance?.ResetAllToGrid();
                    UpdateStatus("Reset all to grid");
                });
            if (_closeAllButton != null)
                _closeAllButton.onClick.AddListener(() => {
                    WindowManager.Instance?.CloseAllWindows();
                    UpdateStatus("All windows closed");
                });
            if (_saveLayoutButton != null)
                _saveLayoutButton.onClick.AddListener(() => {
                    WindowManager.Instance?.SaveLayout();
                    UpdateStatus("Layout saved");
                });
            if (_loadLayoutButton != null)
                _loadLayoutButton.onClick.AddListener(() => {
                    WindowManager.Instance?.LoadLayout();
                    UpdateStatus("Layout loaded");
                });

            // Reset origin
            if (_resetOriginButton != null)
                _resetOriginButton.onClick.AddListener(() => {
                    _settings.GridForwardOffset = 2.0f;
                    _settings.GridUpOffset = 0f;
                    _settings.GridLeftOffset = 0f;
                    PopulateUI();
                    ApplyAndSave();
                    UpdateStatus("Origin reset");
                });

            // Panel toggle
            if (_togglePanelButton != null)
                _togglePanelButton.onClick.AddListener(TogglePanel);
        }

        private void BindSlider(Slider slider, TextMeshProUGUI label, string format,
            System.Action<float> setter)
        {
            if (slider == null) return;
            slider.onValueChanged.AddListener(v =>
            {
                UpdateLabel(label, format, v);
                setter(v);
            });
        }

        private void OnBrowseFolder()
        {
            AndroidFilePicker.PickFolder(path =>
            {
                if (!string.IsNullOrEmpty(path))
                {
                    if (_folderPathInput != null)
                        _folderPathInput.text = path;
                    _settings.LastRootFolder = path;
                    ApplyAndSave();
                    OnOpenFolder();
                }
            });
        }

        private void OnOpenFolder()
        {
            string path = _folderPathInput != null ? _folderPathInput.text : _settings.LastRootFolder;
            if (string.IsNullOrEmpty(path))
            {
                UpdateStatus("No folder path specified");
                return;
            }

            _settings.LastRootFolder = path;
            ApplyAndSave();
            WindowManager.Instance?.OpenFolder(path);
            UpdateStatus($"Opened: {path}");
        }

        private void ApplyAndSave()
        {
            _settings.Save();
            WindowManager.Instance?.ApplySettings(_settings);
        }

        private void UpdateStatus(string msg)
        {
            if (_statusText != null)
                _statusText.text = msg;
        }

        public void TogglePanel()
        {
            if (_panelContent != null)
                _panelContent.SetActive(!_panelContent.activeSelf);
        }

        private void OnDestroy()
        {
            // Cleanup listeners
            if (_browseButton != null) _browseButton.onClick.RemoveAllListeners();
            if (_openFolderButton != null) _openFolderButton.onClick.RemoveAllListeners();
            if (_rescanButton != null) _rescanButton.onClick.RemoveAllListeners();
            if (_resetAllButton != null) _resetAllButton.onClick.RemoveAllListeners();
            if (_closeAllButton != null) _closeAllButton.onClick.RemoveAllListeners();
            if (_saveLayoutButton != null) _saveLayoutButton.onClick.RemoveAllListeners();
            if (_loadLayoutButton != null) _loadLayoutButton.onClick.RemoveAllListeners();
            if (_resetOriginButton != null) _resetOriginButton.onClick.RemoveAllListeners();
            if (_togglePanelButton != null) _togglePanelButton.onClick.RemoveAllListeners();
        }
    }
}
