using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PicoImageViewer.Core;
using PicoImageViewer.Data;

namespace PicoImageViewer.UI
{
    /// <summary>
    /// World-space folder browser for Normal mode. Shows a navigable directory tree
    /// with image thumbnails. Clicking an image opens it in a new floating window.
    /// Auto-discovers child UI elements by name if not assigned in Inspector.
    /// </summary>
    public class FolderBrowserPanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI _currentPathText;
        [SerializeField] private Button _upButton;
        [SerializeField] private Button _backButton;
        [SerializeField] private Button _homeButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private RectTransform _contentContainer;
        [SerializeField] private GameObject _folderItemPrefab;
        [SerializeField] private GameObject _imageItemPrefab;
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private TextMeshProUGUI _statusText;

        [Header("Layout")]
        [SerializeField] private float _itemHeight = 50f;
        [SerializeField] private float _itemSpacing = 5f;
        [SerializeField] private int _thumbnailSize = 128;

        private string _currentPath;
        private readonly Stack<string> _navigationHistory = new Stack<string>();
        private readonly List<GameObject> _spawnedItems = new List<GameObject>();

        // Cached sibling images for the current folder (used by ImageWindow joystick nav)
        private List<ImageData> _currentFolderImages = new List<ImageData>();

        private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif"
        };

        public List<ImageData> CurrentFolderImages => _currentFolderImages;
        public string CurrentPath => _currentPath;

        public static FolderBrowserPanel Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            // Auto-discover UI elements by searching child hierarchy
            AutoDiscoverUI();

            if (_upButton != null)
                _upButton.onClick.AddListener(NavigateUp);
            if (_backButton != null)
                _backButton.onClick.AddListener(NavigateBack);
            if (_homeButton != null)
                _homeButton.onClick.AddListener(NavigateHome);
            if (_closeButton != null)
                _closeButton.onClick.AddListener(ClosePanel);

            // Start at last browsed folder or root folder
            var settings = AppSettings.Load();
            string startPath = !string.IsNullOrEmpty(settings.LastBrowsedFolder)
                ? settings.LastBrowsedFolder
                : settings.LastRootFolder;

#if UNITY_EDITOR
            // In editor, use a desktop-friendly default if the Android path doesn't exist
            if (!Directory.Exists(startPath))
            {
                // Try common desktop paths
                string desktopPictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                string desktopDocuments = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string userHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

                if (!string.IsNullOrEmpty(desktopPictures) && Directory.Exists(desktopPictures))
                    startPath = desktopPictures;
                else if (!string.IsNullOrEmpty(desktopDocuments) && Directory.Exists(desktopDocuments))
                    startPath = desktopDocuments;
                else if (!string.IsNullOrEmpty(userHome) && Directory.Exists(userHome))
                    startPath = userHome;
                else
                    startPath = Application.dataPath; // fallback to project Assets folder

                Debug.Log($"[FolderBrowser] Editor mode: using desktop path {startPath}");
            }
#endif

            if (Directory.Exists(startPath))
                NavigateTo(startPath);
            else
                NavigateTo(GetDefaultStartPath());
        }

        private string GetDefaultStartPath()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return "/sdcard";
#else
            string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (!string.IsNullOrEmpty(pictures) && Directory.Exists(pictures))
                return pictures;
            return Application.dataPath;
#endif
        }

        /// <summary>
        /// Auto-discover UI child elements by name from the hierarchy.
        /// </summary>
        private void AutoDiscoverUI()
        {
            if (_currentPathText == null)
            {
                var go = FindChildRecursive(transform, "PathText");
                if (go != null) _currentPathText = go.GetComponent<TextMeshProUGUI>();
            }

            if (_statusText == null)
            {
                var go = FindChildRecursive(transform, "StatusText");
                if (go != null) _statusText = go.GetComponent<TextMeshProUGUI>();
            }

            if (_scrollRect == null)
            {
                var go = FindChildRecursive(transform, "ScrollArea");
                if (go != null) _scrollRect = go.GetComponent<ScrollRect>();
            }

            if (_contentContainer == null)
            {
                var go = FindChildRecursive(transform, "Content");
                if (go != null) _contentContainer = go.GetComponent<RectTransform>();
            }

            // Auto-create nav buttons if they don't exist in hierarchy
            EnsureNavigationButtons();

            Debug.Log($"[FolderBrowser] Auto-discovered: Path={_currentPathText != null}, " +
                      $"Status={_statusText != null}, Scroll={_scrollRect != null}, " +
                      $"Content={_contentContainer != null}");
        }

        private void EnsureNavigationButtons()
        {
            // Find or create navigation buttons in the Header area
            var header = FindChildRecursive(transform, "Header");
            if (header == null) return;

            if (_upButton == null)
            {
                var go = FindChildRecursive(header, "UpButton");
                if (go != null) _upButton = go.GetComponent<Button>();
                else _upButton = CreateNavButton(header, "UpButton", "Up", 0);
            }

            if (_backButton == null)
            {
                var go = FindChildRecursive(header, "BackButton");
                if (go != null) _backButton = go.GetComponent<Button>();
                else _backButton = CreateNavButton(header, "BackButton", "Back", 1);
            }

            if (_homeButton == null)
            {
                var go = FindChildRecursive(header, "HomeButton");
                if (go != null) _homeButton = go.GetComponent<Button>();
                else _homeButton = CreateNavButton(header, "HomeButton", "Home", 2);
            }

            if (_closeButton == null)
            {
                var go = FindChildRecursive(header, "CloseButton");
                if (go != null) _closeButton = go.GetComponent<Button>();
                else _closeButton = CreateNavButton(header, "CloseButton", "X", 3);
            }
        }

        private Button CreateNavButton(Transform parent, string name, string label, int index)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            // Anchor to center-left of Header so buttons stay inside the panel
            rect.anchorMin = new Vector2(0, 0.5f);
            rect.anchorMax = new Vector2(0, 0.5f);
            rect.pivot = new Vector2(0, 0.5f);
            float btnWidth = 80f;
            float spacing = 5f;
            rect.anchoredPosition = new Vector2(10 + index * (btnWidth + spacing), 0);
            rect.sizeDelta = new Vector2(btnWidth, 30);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.25f, 0.3f, 0.4f, 1f);
            var btn = go.AddComponent<Button>();

            var textGO = new GameObject("Text", typeof(RectTransform));
            textGO.transform.SetParent(go.transform, false);
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 13;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return btn;
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

        /// <summary>
        /// Navigate to a specific folder path and display its contents.
        /// </summary>
        public void NavigateTo(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
            {
                UpdateStatus($"Folder not found: {folderPath}");
                return;
            }

            // Push current path to history for back navigation
            if (!string.IsNullOrEmpty(_currentPath))
                _navigationHistory.Push(_currentPath);

            _currentPath = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar);

            // Save as last browsed
            var settings = AppSettings.Load();
            settings.LastBrowsedFolder = _currentPath;
            settings.Save();

            // Update UI
            if (_currentPathText != null)
                _currentPathText.text = _currentPath;

            // Clear existing items
            ClearItems();

            // Build image list for this folder (used by joystick cycling)
            BuildCurrentFolderImageList();

            // Populate entries
            PopulateFolder();

            // Scroll to top
            if (_scrollRect != null)
                _scrollRect.normalizedPosition = new Vector2(0, 1);
        }

        private void PopulateFolder()
        {
            int itemIndex = 0;

            // List subfolders first
            try
            {
                var dirs = Directory.GetDirectories(_currentPath)
                    .OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase);

                foreach (var dir in dirs)
                {
                    string dirName = Path.GetFileName(dir);
                    CreateFolderItem(dirName, dir, itemIndex);
                    itemIndex++;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FolderBrowser] Cannot list directories: {ex.Message}");
            }

            // List images
            try
            {
                var files = Directory.GetFiles(_currentPath)
                    .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)))
                    .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase);

                foreach (var file in files)
                {
                    string fileName = Path.GetFileName(file);
                    CreateImageItem(fileName, file, itemIndex);
                    itemIndex++;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FolderBrowser] Cannot list files: {ex.Message}");
            }

            // Resize content area
            if (_contentContainer != null)
            {
                float totalHeight = itemIndex * (_itemHeight + _itemSpacing);
                _contentContainer.sizeDelta = new Vector2(_contentContainer.sizeDelta.x, totalHeight);
            }

            UpdateStatus($"{_spawnedItems.Count} items");
        }

        private void BuildCurrentFolderImageList()
        {
            _currentFolderImages.Clear();

            try
            {
                var files = Directory.GetFiles(_currentPath)
                    .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)))
                    .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                for (int i = 0; i < files.Length; i++)
                {
                    string folderName = Path.GetFileName(_currentPath);
                    _currentFolderImages.Add(new ImageData(files[i], _currentPath, folderName, 0, i));
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FolderBrowser] Cannot build image list: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the sorted image list for a specific folder path.
        /// Used by joystick navigation when the window was opened from a different folder.
        /// </summary>
        public List<ImageData> GetImagesInFolder(string folderPath)
        {
            var images = new List<ImageData>();
            if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath)) return images;

            try
            {
                var files = Directory.GetFiles(folderPath)
                    .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)))
                    .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                string folderName = Path.GetFileName(folderPath);
                for (int i = 0; i < files.Length; i++)
                {
                    images.Add(new ImageData(files[i], folderPath, folderName, 0, i));
                }
            }
            catch (Exception) { }

            return images;
        }

        private void CreateFolderItem(string name, string fullPath, int index)
        {
            var go = CreateListItem(index);
            if (go == null) return;

            // Folder icon placeholder + name
            var text = go.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
                text.text = $"[DIR]  {name}";

            // Background color for folders
            var bg = go.GetComponent<Image>();
            if (bg != null)
                bg.color = new Color(0.2f, 0.25f, 0.35f, 0.9f);

            // Click to navigate
            var button = go.GetComponent<Button>();
            if (button != null)
            {
                string path = fullPath; // capture
                button.onClick.AddListener(() => NavigateTo(path));
            }

            _spawnedItems.Add(go);
        }

        private void CreateImageItem(string name, string fullPath, int index)
        {
            var go = CreateListItem(index);
            if (go == null) return;

            // Image name
            var text = go.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
                text.text = $"[IMG]  {name}";

            // Background color for images
            var bg = go.GetComponent<Image>();
            if (bg != null)
                bg.color = new Color(0.18f, 0.2f, 0.22f, 0.9f);

            // Click to open image in a new window
            var button = go.GetComponent<Button>();
            if (button != null)
            {
                string path = fullPath;
                string folder = _currentPath;
                button.onClick.AddListener(() => OnImageClicked(path, folder));
            }

            _spawnedItems.Add(go);
        }

        private GameObject CreateListItem(int index)
        {
            // Create item dynamically (works with or without a prefab)
            GameObject go;

            if (_folderItemPrefab != null)
            {
                go = Instantiate(_folderItemPrefab, _contentContainer);
            }
            else
            {
                // Create a basic list item
                go = new GameObject("Item_" + index, typeof(RectTransform));
                go.transform.SetParent(_contentContainer, false);

                var img = go.AddComponent<Image>();
                img.color = new Color(0.2f, 0.2f, 0.25f, 0.9f);
                go.AddComponent<Button>();

                // Add text child
                var textGO = new GameObject("Text", typeof(RectTransform));
                textGO.transform.SetParent(go.transform, false);
                var tmp = textGO.AddComponent<TextMeshProUGUI>();
                tmp.fontSize = 14;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                var textRect = textGO.GetComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(10, 0);
                textRect.offsetMax = new Vector2(-10, 0);
            }

            // Position
            var rect = go.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0.5f, 1);
                float yPos = -index * (_itemHeight + _itemSpacing);
                rect.anchoredPosition = new Vector2(0, yPos);
                rect.sizeDelta = new Vector2(0, _itemHeight);
            }

            return go;
        }

        private void OnImageClicked(string imagePath, string folderPath)
        {
            // Find the ImageData for this image from our cached list,
            // or build it from the folder
            List<ImageData> folderImages;
            if (folderPath == _currentPath)
            {
                folderImages = _currentFolderImages;
            }
            else
            {
                folderImages = GetImagesInFolder(folderPath);
            }

            ImageData imageData = folderImages.Find(img => img.FullPath == imagePath);
            if (imageData == null)
            {
                string folderName = Path.GetFileName(folderPath);
                imageData = new ImageData(imagePath, folderPath, folderName, 0, 0);
            }

            // Find NormalModeManager - try Instance first, then scene search
            var nmm = NormalModeManager.Instance;
            if (nmm == null)
            {
                nmm = FindAnyObjectByType<NormalModeManager>();
            }

            if (nmm != null)
            {
                nmm.OpenImage(imageData, folderImages);
                UpdateStatus($"Opened: {imageData.FileName}");
            }
            else
            {
                Debug.LogError("[FolderBrowser] NormalModeManager not found in scene! " +
                               "Add a GameObject with NormalModeManager component.");
                UpdateStatus("Error: NormalModeManager missing from scene");
            }
        }

        private void NavigateUp()
        {
            if (string.IsNullOrEmpty(_currentPath)) return;

            string parent = Path.GetDirectoryName(_currentPath);
            if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                NavigateTo(parent);
        }

        private void NavigateBack()
        {
            if (_navigationHistory.Count > 0)
            {
                string prev = _navigationHistory.Pop();
                // Don't push to history when going back
                _currentPath = null;
                NavigateTo(prev);
            }
        }

        private void NavigateHome()
        {
            var settings = AppSettings.Load();
            string home = settings.LastRootFolder;

            // If stored home path doesn't exist, try common fallbacks
            if (string.IsNullOrEmpty(home) || !Directory.Exists(home))
            {
                // Try common Pico paths
                string[] fallbacks = { "/sdcard/Paradox", "/sdcard/Download", "/sdcard/Downloads", "/sdcard" };
                home = null;
                foreach (var path in fallbacks)
                {
                    if (Directory.Exists(path)) { home = path; break; }
                }
            }

            if (!string.IsNullOrEmpty(home))
                NavigateTo(home);
            else
                NavigateTo(GetDefaultStartPath());
        }

        private void ClearItems()
        {
            foreach (var item in _spawnedItems)
            {
                if (item != null) Destroy(item);
            }
            _spawnedItems.Clear();
        }

        private void UpdateStatus(string msg)
        {
            if (_statusText != null)
                _statusText.text = msg;
        }

        /// <summary>
        /// Show or hide the browser panel.
        /// When showing, repositions the panel in front of the user if it was hidden.
        /// </summary>
        public void SetVisible(bool visible)
        {
            bool wasHidden = !gameObject.activeSelf;
            gameObject.SetActive(visible);

            // Reposition in front of user when transitioning from hidden to visible
            if (visible && wasHidden)
            {
                RepositionInFrontOfUser();
            }
        }

        /// <summary>
        /// Close (hide) the folder browser panel.
        /// </summary>
        public void ClosePanel()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Position the panel in front of the user's current view direction.
        /// Called when the panel is shown or when the user can't see it.
        /// </summary>
        public void RepositionInFrontOfUser()
        {
            Camera mainCam = Camera.main;
            if (mainCam == null) return;

            Vector3 forward = mainCam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();

            float distance = 1.5f;
            // Position slightly to the left so it doesn't overlap image windows
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 targetPos = mainCam.transform.position
                + forward * distance
                - right * 0.3f; // slight left offset

            transform.position = targetPos;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            Debug.Log($"[FolderBrowser] Repositioned in front of user at {targetPos}");
        }

        private void OnDestroy()
        {
            if (_upButton != null) _upButton.onClick.RemoveAllListeners();
            if (_backButton != null) _backButton.onClick.RemoveAllListeners();
            if (_homeButton != null) _homeButton.onClick.RemoveAllListeners();
            if (_closeButton != null) _closeButton.onClick.RemoveAllListeners();
            ClearItems();

            if (Instance == this) Instance = null;
        }
    }
}
