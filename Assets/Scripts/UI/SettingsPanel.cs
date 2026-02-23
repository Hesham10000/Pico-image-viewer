using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using PicoImageViewer.Core;
using PicoImageViewer.Data;
using PicoImageViewer.Android;

namespace PicoImageViewer.UI
{
    /// <summary>
    /// World-space settings panel. Provides controls for all app settings,
    /// folder picking, mode switching, and utility actions.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [Header("Mode")]
        [SerializeField] private Button _normalModeButton;
        [SerializeField] private Button _gridModeButton;
        [SerializeField] private TextMeshProUGUI _currentModeLabel;
        [SerializeField] private GameObject _gridSettingsGroup;    // shown only in Grid mode
        [SerializeField] private GameObject _normalSettingsGroup;  // shown only in Normal mode

        [Header("Normal Mode")]
        [SerializeField] private Slider _normalSpacingSlider;
        [SerializeField] private TextMeshProUGUI _normalSpacingLabel;
        [SerializeField] private Slider _joystickDeadzoneSlider;
        [SerializeField] private TextMeshProUGUI _joystickDeadzoneLabel;
        [SerializeField] private Slider _joystickCooldownSlider;
        [SerializeField] private TextMeshProUGUI _joystickCooldownLabel;
        [SerializeField] private Button _closeAllNormalButton;

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
        private Canvas _canvas;
        private GraphicRaycaster _raycaster;
        private bool _isVisible;
        private InputAction _toggleSettingsAction;

        private void Start()
        {
            _settings = AppSettings.Load();

            // Cache canvas references for show/hide (disabling Canvas hides everything
            // including the background, while keeping the MonoBehaviour findable)
            _canvas = GetComponent<Canvas>();
            _raycaster = GetComponent<GraphicRaycaster>();

            PopulateUI();
            BindEvents();

            // Set up A button (primaryButton) on right controller to toggle settings.
            // This works with Pico controllers, Quest controllers, and gamepads.
            _toggleSettingsAction = new InputAction("ToggleSettings", InputActionType.Button);
            _toggleSettingsAction.AddBinding("<XRController>{RightHand}/primaryButton");
            _toggleSettingsAction.AddBinding("<Gamepad>/buttonSouth");
            _toggleSettingsAction.performed += _ => TogglePanel();
            _toggleSettingsAction.Enable();

            // Hide at start — only shown via settings icon or A button
            SetPanelVisible(false);
        }

        private void PopulateUI()
        {
            // Mode
            UpdateModeUI();

            // Normal mode settings
            SetupSlider(_normalSpacingSlider, _normalSpacingLabel, _settings.NormalModeSpacing,
                0.1f, 3f, "Spacing: {0:F2}m");
            SetupSlider(_joystickDeadzoneSlider, _joystickDeadzoneLabel, _settings.JoystickDeadzone,
                0.1f, 0.9f, "Deadzone: {0:F2}");
            SetupSlider(_joystickCooldownSlider, _joystickCooldownLabel, _settings.JoystickCooldown,
                0.1f, 1.0f, "Cooldown: {0:F2}s");

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
            // Mode buttons
            if (_normalModeButton != null)
                _normalModeButton.onClick.AddListener(() => SwitchMode(ViewMode.Normal));
            if (_gridModeButton != null)
                _gridModeButton.onClick.AddListener(() => SwitchMode(ViewMode.Grid));

            // Normal mode sliders
            BindSlider(_normalSpacingSlider, _normalSpacingLabel, "Spacing: {0:F2}m",
                v => _settings.NormalModeSpacing = v);
            BindSlider(_joystickDeadzoneSlider, _joystickDeadzoneLabel, "Deadzone: {0:F2}",
                v => _settings.JoystickDeadzone = v);
            BindSlider(_joystickCooldownSlider, _joystickCooldownLabel, "Cooldown: {0:F2}s",
                v => _settings.JoystickCooldown = v);

            if (_closeAllNormalButton != null)
                _closeAllNormalButton.onClick.AddListener(() => {
                    NormalModeManager.Instance?.CloseAllWindows();
                    UpdateStatus("Normal mode windows closed");
                });

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

            if (_settings.Mode == ViewMode.Normal)
            {
                // In Normal mode, ensure the folder browser is visible and navigate
                var browser = FolderBrowserPanel.Instance;
                if (browser != null)
                {
                    browser.SetVisible(true);
                    browser.NavigateTo(path);
                }
                UpdateStatus($"Browsing: {path}");
            }
            else
            {
                // In Grid mode, open all images via WindowManager
                WindowManager.Instance?.OpenFolder(path);
                UpdateStatus($"Opened grid: {path}");
            }
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
            SetPanelVisible(!_isVisible);
        }

        public void HidePanel()
        {
            SetPanelVisible(false);
        }

        public void ShowPanel()
        {
            SetPanelVisible(true);
        }

        /// <summary>
        /// Show or hide the entire settings panel by enabling/disabling the Canvas.
        /// The MonoBehaviour stays active so it can still be found via FindAnyObjectByType
        /// and respond to A-button input.
        /// </summary>
        private void SetPanelVisible(bool visible)
        {
            _isVisible = visible;

            // Disable the Canvas component so nothing renders (including background)
            if (_canvas != null)
                _canvas.enabled = visible;

            // Disable raycasting when hidden
            if (_raycaster != null)
                _raycaster.enabled = visible;

            // Also toggle _panelContent if assigned (belt-and-suspenders)
            if (_panelContent != null)
                _panelContent.SetActive(visible);
        }

        public bool IsPanelVisible => _isVisible;

        private void SwitchMode(ViewMode mode)
        {
            _settings.Mode = mode;
            ApplyAndSave();
            WindowManager.Instance?.SetMode(mode);
            UpdateModeUI();
            UpdateStatus($"Switched to {mode} mode");
        }

        private void UpdateModeUI()
        {
            bool isGrid = _settings.Mode == ViewMode.Grid;

            if (_currentModeLabel != null)
                _currentModeLabel.text = isGrid ? "Mode: Grid" : "Mode: Normal";

            // Show/hide mode-specific settings groups
            if (_gridSettingsGroup != null)
                _gridSettingsGroup.SetActive(isGrid);
            if (_normalSettingsGroup != null)
                _normalSettingsGroup.SetActive(!isGrid);

            // Highlight active mode button
            if (_normalModeButton != null)
            {
                var colors = _normalModeButton.colors;
                colors.normalColor = isGrid ? new Color(0.3f, 0.3f, 0.35f) : new Color(0.2f, 0.5f, 0.8f);
                _normalModeButton.colors = colors;
            }
            if (_gridModeButton != null)
            {
                var colors = _gridModeButton.colors;
                colors.normalColor = isGrid ? new Color(0.2f, 0.5f, 0.8f) : new Color(0.3f, 0.3f, 0.35f);
                _gridModeButton.colors = colors;
            }
        }

        private void OnDestroy()
        {
            // Cleanup A-button action
            if (_toggleSettingsAction != null)
            {
                _toggleSettingsAction.Disable();
                _toggleSettingsAction.Dispose();
            }

            // Cleanup listeners
            if (_normalModeButton != null) _normalModeButton.onClick.RemoveAllListeners();
            if (_gridModeButton != null) _gridModeButton.onClick.RemoveAllListeners();
            if (_closeAllNormalButton != null) _closeAllNormalButton.onClick.RemoveAllListeners();
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
