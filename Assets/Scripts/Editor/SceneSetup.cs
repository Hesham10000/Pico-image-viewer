#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

namespace PicoImageViewer.Editor
{
    /// <summary>
    /// Editor utility to auto-generate the main scene hierarchy and prefabs.
    /// Use via menu: PicoImageViewer > Setup Scene.
    /// </summary>
    public static class SceneSetup
    {
        [MenuItem("PicoImageViewer/Setup Scene")]
        public static void SetupMainScene()
        {
            // Remove any existing default "Main Camera" that's not part of XR Rig
            RemoveDefaultCamera();

            // Create XR Rig placeholder — position at eye level so panels are visible
            var xrRig = FindOrCreate("XR Rig");
            xrRig.transform.position = new Vector3(0f, 1.2f, 0f);
            var cameraOffset = FindOrCreateChild(xrRig, "Camera Offset");
            var mainCamera = FindOrCreateChild(cameraOffset, "Main Camera");
            if (mainCamera.GetComponent<Camera>() == null)
            {
                mainCamera.AddComponent<Camera>().tag = "MainCamera";
                mainCamera.AddComponent<AudioListener>();
            }
            mainCamera.tag = "MainCamera";

            // Add EditorCameraController for desktop testing
            if (mainCamera.GetComponent<Core.EditorCameraController>() == null)
                mainCamera.AddComponent<Core.EditorCameraController>();

            // Add XRSetup to XR Rig
            if (xrRig.GetComponent<Core.XRSetup>() == null)
                xrRig.AddComponent<Core.XRSetup>();

            var leftController = FindOrCreateChild(cameraOffset, "Left Controller");
            var rightController = FindOrCreateChild(cameraOffset, "Right Controller");

            // Create Managers
            var managers = FindOrCreate("[Managers]");

            var windowMgrGO = FindOrCreateChild(managers, "WindowManager");
            if (windowMgrGO.GetComponent<Core.WindowManager>() == null)
                windowMgrGO.AddComponent<Core.WindowManager>();

            var normalMgrGO = FindOrCreateChild(managers, "NormalModeManager");
            if (normalMgrGO.GetComponent<Core.NormalModeManager>() == null)
                normalMgrGO.AddComponent<Core.NormalModeManager>();

            var texLoaderGO = FindOrCreateChild(managers, "TextureLoader");
            if (texLoaderGO.GetComponent<Core.TextureLoader>() == null)
                texLoaderGO.AddComponent<Core.TextureLoader>();

            var permissionsGO = FindOrCreateChild(managers, "AndroidPermissions");
            if (permissionsGO.GetComponent<Android.AndroidPermissions>() == null)
                permissionsGO.AddComponent<Android.AndroidPermissions>();

            // Window Container (shared by both modes)
            FindOrCreateChild(managers, "WindowContainer");

            // Joystick Image Navigator (for normal mode image cycling)
            var joystickNavGO = FindOrCreateChild(managers, "JoystickImageNavigator");
            if (joystickNavGO.GetComponent<Interaction.JoystickImageNavigator>() == null)
                joystickNavGO.AddComponent<Interaction.JoystickImageNavigator>();

            // Create App Bootstrap
            var bootstrapGO = FindOrCreate("[AppBootstrap]");
            if (bootstrapGO.GetComponent<Core.AppBootstrap>() == null)
                bootstrapGO.AddComponent<Core.AppBootstrap>();

            // Settings Panel (world-space canvas)
            if (GameObject.Find("SettingsPanel") == null)
                CreateSettingsPanelPlaceholder();

            // Folder Browser Panel (world-space, for Normal mode)
            if (GameObject.Find("FolderBrowserPanel") == null)
                CreateFolderBrowserPlaceholder();

            // Create ImageWindow prefab
            CreateImageWindowPrefab();

            Debug.Log("[SceneSetup] Scene hierarchy created. All references auto-discover at runtime!");
        }

        /// <summary>
        /// Remove the default standalone Main Camera that Unity creates with new scenes.
        /// The XR Rig has its own camera.
        /// </summary>
        private static void RemoveDefaultCamera()
        {
            // Find all root GameObjects named "Main Camera" that are NOT inside an XR Rig
            var cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (var cam in cameras)
            {
                if (cam.gameObject.name == "Main Camera" && cam.transform.parent == null)
                {
                    Debug.Log("[SceneSetup] Removing default standalone Main Camera");
                    Object.DestroyImmediate(cam.gameObject);
                }
            }
        }

        [MenuItem("PicoImageViewer/Create ImageWindow Prefab")]
        public static void CreateImageWindowPrefab()
        {
            var root = new GameObject("ImageWindow");

            // World-space canvas
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.AddComponent<CanvasScaler>();
            root.AddComponent<GraphicRaycaster>();

            var windowComponent = root.AddComponent<UI.ImageWindow>();
            var rectTransform = root.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(500, 400);

            // Background panel
            var bg = CreateUIChild(root.transform, "Background");
            var bgImage = bg.AddComponent<Image>();
            bgImage.color = new Color(0.15f, 0.15f, 0.18f, 0.95f);
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Title bar
            var titleBar = CreateUIChild(bg.transform, "TitleBar");
            var titleBarImage = titleBar.AddComponent<Image>();
            titleBarImage.color = new Color(0.2f, 0.2f, 0.25f, 1f);
            var titleRect = titleBar.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0, 1);
            titleRect.anchorMax = new Vector2(1, 1);
            titleRect.pivot = new Vector2(0.5f, 1);
            titleRect.sizeDelta = new Vector2(0, 40);
            titleRect.offsetMin = new Vector2(0, -40);
            titleRect.offsetMax = new Vector2(0, 0);

            // Add box collider for dragging
            var dragCollider = titleBar.AddComponent<BoxCollider>();
            dragCollider.size = new Vector3(500, 40, 10);
            var dragComponent = titleBar.AddComponent<Interaction.XRWindowDrag>();

            // Rigidbody needed by XRGrabInteractable
            var rb = titleBar.GetComponent<Rigidbody>();
            if (rb == null) rb = titleBar.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Title text
            var titleTextGO = CreateUIChild(titleBar.transform, "TitleText");
            var titleText = titleTextGO.AddComponent<TextMeshProUGUI>();
            titleText.text = "image.png";
            titleText.fontSize = 18;
            titleText.color = Color.white;
            titleText.alignment = TextAlignmentOptions.MidlineLeft;
            var titleTextRect = titleTextGO.GetComponent<RectTransform>();
            titleTextRect.anchorMin = new Vector2(0, 0);
            titleTextRect.anchorMax = new Vector2(0.7f, 1);
            titleTextRect.offsetMin = new Vector2(10, 0);
            titleTextRect.offsetMax = new Vector2(0, 0);

            // Folder label
            var folderLabelGO = CreateUIChild(titleBar.transform, "FolderLabel");
            var folderLabel = folderLabelGO.AddComponent<TextMeshProUGUI>();
            folderLabel.text = "folder";
            folderLabel.fontSize = 12;
            folderLabel.color = new Color(0.7f, 0.7f, 0.7f);
            folderLabel.alignment = TextAlignmentOptions.MidlineRight;
            var folderRect = folderLabelGO.GetComponent<RectTransform>();
            folderRect.anchorMin = new Vector2(0.5f, 0);
            folderRect.anchorMax = new Vector2(0.85f, 1);
            folderRect.offsetMin = Vector2.zero;
            folderRect.offsetMax = Vector2.zero;

            // Close button
            var closeBtn = CreateButton(titleBar.transform, "CloseButton", "X",
                new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(30, 30));

            // Image container
            var imageContainer = CreateUIChild(bg.transform, "ImageContainer");
            var imgContainerRect = imageContainer.GetComponent<RectTransform>();
            imgContainerRect.anchorMin = new Vector2(0, 0.1f);
            imgContainerRect.anchorMax = new Vector2(1, 0.9f);
            imgContainerRect.offsetMin = new Vector2(5, 0);
            imgContainerRect.offsetMax = new Vector2(-5, -45);

            // RawImage for the photo
            var rawImageGO = CreateUIChild(imageContainer.transform, "ImageDisplay");
            var rawImage = rawImageGO.AddComponent<RawImage>();
            rawImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            var rawImageRect = rawImageGO.GetComponent<RectTransform>();
            rawImageRect.anchorMin = Vector2.zero;
            rawImageRect.anchorMax = Vector2.one;
            rawImageRect.offsetMin = Vector2.zero;
            rawImageRect.offsetMax = Vector2.zero;

            // Loading indicator
            var loadingGO = CreateUIChild(imageContainer.transform, "LoadingIndicator");
            var loadingText = loadingGO.AddComponent<TextMeshProUGUI>();
            loadingText.text = "Loading...";
            loadingText.fontSize = 16;
            loadingText.color = Color.gray;
            loadingText.alignment = TextAlignmentOptions.Center;
            var loadingRect = loadingGO.GetComponent<RectTransform>();
            loadingRect.anchorMin = Vector2.zero;
            loadingRect.anchorMax = Vector2.one;
            loadingRect.offsetMin = Vector2.zero;
            loadingRect.offsetMax = Vector2.zero;

            // Bottom controls bar
            var controlsBar = CreateUIChild(bg.transform, "ControlsBar");
            var controlsBg = controlsBar.AddComponent<Image>();
            controlsBg.color = new Color(0.18f, 0.18f, 0.22f, 1f);
            var controlsRect = controlsBar.GetComponent<RectTransform>();
            controlsRect.anchorMin = new Vector2(0, 0);
            controlsRect.anchorMax = new Vector2(1, 0);
            controlsRect.pivot = new Vector2(0.5f, 0);
            controlsRect.sizeDelta = new Vector2(0, 35);
            controlsRect.offsetMin = new Vector2(0, 0);
            controlsRect.offsetMax = new Vector2(0, 35);

            // Control buttons
            float btnX = 5f;
            float btnW = 55f;
            float btnSpacing = 5f;

            CreateControlButton(controlsBar.transform, "ResetSizeBtn", "Size", btnX, btnW);
            btnX += btnW + btnSpacing;
            CreateControlButton(controlsBar.transform, "ResetPosBtn", "Pos", btnX, btnW);
            btnX += btnW + btnSpacing;
            CreateControlButton(controlsBar.transform, "ZoomInBtn", "+", btnX, 30);
            btnX += 30 + btnSpacing;
            CreateControlButton(controlsBar.transform, "ZoomOutBtn", "-", btnX, 30);
            btnX += 30 + btnSpacing;
            CreateControlButton(controlsBar.transform, "FitBtn", "Fit", btnX, 45);
            btnX += 45 + btnSpacing;
            CreateControlButton(controlsBar.transform, "AspectBtn", "AR: On", btnX, 60);

            // Resize handles (corners)
            CreateResizeHandle(bg.transform, "ResizeHandle_BR",
                Interaction.WindowResizeHandle.HandlePosition.BottomRight,
                new Vector2(1, 0), new Vector2(1, 0));
            CreateResizeHandle(bg.transform, "ResizeHandle_BL",
                Interaction.WindowResizeHandle.HandlePosition.BottomLeft,
                new Vector2(0, 0), new Vector2(0, 0));
            CreateResizeHandle(bg.transform, "ResizeHandle_TR",
                Interaction.WindowResizeHandle.HandlePosition.TopRight,
                new Vector2(1, 1), new Vector2(1, 1));
            CreateResizeHandle(bg.transform, "ResizeHandle_TL",
                Interaction.WindowResizeHandle.HandlePosition.TopLeft,
                new Vector2(0, 1), new Vector2(0, 1));

            // Save as prefab in both locations
            string prefabPath = "Assets/Prefabs/ImageWindow.prefab";
            EnsureDirectory("Assets/Prefabs");
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

            // Also save to Resources so it can be loaded at runtime
            string resourcesPath = "Assets/Resources/ImageWindow.prefab";
            EnsureDirectory("Assets/Resources");
            PrefabUtility.SaveAsPrefabAsset(root, resourcesPath);

            Object.DestroyImmediate(root);
            Debug.Log($"[SceneSetup] ImageWindow prefab saved to {prefabPath} and {resourcesPath}");
        }

        private static void CreateSettingsPanelPlaceholder()
        {
            var settingsRoot = CreateGameObject("SettingsPanel");
            var canvas = settingsRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            settingsRoot.AddComponent<CanvasScaler>();
            settingsRoot.AddComponent<GraphicRaycaster>();

            var settingsComponent = settingsRoot.AddComponent<UI.SettingsPanel>();
            var settingsRect = settingsRoot.GetComponent<RectTransform>();
            settingsRect.sizeDelta = new Vector2(600, 800);
            settingsRect.localScale = Vector3.one * 0.001f; // 1mm per unit for world space

            // Position to the left of the user
            settingsRoot.transform.position = new Vector3(-1.5f, 1.2f, 1.5f);
            settingsRoot.transform.rotation = Quaternion.Euler(0, 30, 0);

            Debug.Log("[SceneSetup] Settings panel placeholder created.");
        }

        private static void CreateFolderBrowserPlaceholder()
        {
            var browserRoot = CreateGameObject("FolderBrowserPanel");
            var canvas = browserRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            browserRoot.AddComponent<CanvasScaler>();
            browserRoot.AddComponent<GraphicRaycaster>();

            var browserComponent = browserRoot.AddComponent<UI.FolderBrowserPanel>();
            var browserRect = browserRoot.GetComponent<RectTransform>();
            browserRect.sizeDelta = new Vector2(500, 700);
            browserRect.localScale = Vector3.one * 0.001f;

            // Position to the right of the user (opposite side from settings)
            browserRoot.transform.position = new Vector3(1.0f, 1.2f, 1.5f);
            browserRoot.transform.rotation = Quaternion.Euler(0, -20, 0);

            // Create basic structure: path bar, navigation buttons, scroll content
            var bg = CreateUIChild(browserRoot.transform, "Background");
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.12f, 0.12f, 0.15f, 0.95f);
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            // Header with path and nav buttons
            var header = CreateUIChild(bg.transform, "Header");
            var headerImg = header.AddComponent<Image>();
            headerImg.color = new Color(0.18f, 0.18f, 0.22f, 1f);
            var headerRect = header.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = new Vector2(0, 80);
            headerRect.offsetMin = new Vector2(0, -80);
            headerRect.offsetMax = Vector2.zero;

            // Path text
            var pathGO = CreateUIChild(header.transform, "PathText");
            var pathText = pathGO.AddComponent<TextMeshProUGUI>();
            pathText.text = "/sdcard";
            pathText.fontSize = 12;
            pathText.color = Color.white;
            pathText.alignment = TextAlignmentOptions.MidlineLeft;
            pathText.enableWordWrapping = false;
            pathText.overflowMode = TextOverflowModes.Ellipsis;
            var pathRect = pathGO.GetComponent<RectTransform>();
            pathRect.anchorMin = new Vector2(0, 0.5f);
            pathRect.anchorMax = new Vector2(1, 1);
            pathRect.offsetMin = new Vector2(10, 0);
            pathRect.offsetMax = new Vector2(-10, -5);

            // Scroll content area
            var scrollArea = CreateUIChild(bg.transform, "ScrollArea");
            var scrollRect = scrollArea.AddComponent<ScrollRect>();
            var scrollAreaRect = scrollArea.GetComponent<RectTransform>();
            scrollAreaRect.anchorMin = new Vector2(0, 0);
            scrollAreaRect.anchorMax = new Vector2(1, 1);
            scrollAreaRect.offsetMin = new Vector2(5, 30);
            scrollAreaRect.offsetMax = new Vector2(-5, -85);

            var content = CreateUIChild(scrollArea.transform, "Content");
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 0);
            scrollRect.content = contentRect;
            scrollRect.vertical = true;
            scrollRect.horizontal = false;

            // Status bar at bottom
            var statusGO = CreateUIChild(bg.transform, "StatusText");
            var statusText = statusGO.AddComponent<TextMeshProUGUI>();
            statusText.text = "Ready";
            statusText.fontSize = 11;
            statusText.color = new Color(0.6f, 0.6f, 0.6f);
            statusText.alignment = TextAlignmentOptions.MidlineLeft;
            var statusRect = statusGO.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0, 0);
            statusRect.anchorMax = new Vector2(1, 0);
            statusRect.pivot = new Vector2(0.5f, 0);
            statusRect.sizeDelta = new Vector2(0, 25);
            statusRect.offsetMin = new Vector2(10, 0);
            statusRect.offsetMax = new Vector2(-10, 25);

            Debug.Log("[SceneSetup] Folder browser panel placeholder created.");
        }

        private static void CreateResizeHandle(Transform parent, string name,
            Interaction.WindowResizeHandle.HandlePosition handlePos,
            Vector2 anchor, Vector2 pivot)
        {
            var go = CreateUIChild(parent, name);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = new Vector2(20, 20);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.4f, 0.6f, 1f, 0.6f);

            var collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(20, 20, 10);

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            go.AddComponent<Interaction.WindowResizeHandle>();
        }

        private static void CreateControlButton(Transform parent, string name,
            string label, float xPos, float width)
        {
            var go = CreateUIChild(parent, name);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(xPos, 0);
            rect.sizeDelta = new Vector2(width, 0);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.3f, 0.3f, 0.35f, 1f);
            go.AddComponent<Button>();

            var textGO = CreateUIChild(go.transform, "Text");
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 12;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        private static GameObject CreateButton(Transform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
        {
            var go = CreateUIChild(parent, name);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMax;
            rect.sizeDelta = size;
            rect.anchoredPosition = new Vector2(-5, 0);

            var img = go.AddComponent<Image>();
            img.color = new Color(0.8f, 0.2f, 0.2f, 0.9f);
            go.AddComponent<Button>();

            var textGO = CreateUIChild(go.transform, "Text");
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 16;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            var textRect = textGO.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return go;
        }

        private static GameObject FindOrCreate(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null) return existing;
            return new GameObject(name);
        }

        private static GameObject FindOrCreateChild(GameObject parent, string name)
        {
            var t = parent.transform.Find(name);
            if (t != null) return t.gameObject;

            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        private static GameObject CreateGameObject(string name)
        {
            return new GameObject(name);
        }

        private static GameObject CreateUIChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void EnsureDirectory(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                string parent = System.IO.Path.GetDirectoryName(path);
                string folder = System.IO.Path.GetFileName(path);
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }
}
#endif
