using System.Collections.Generic;
using UnityEngine;

namespace PicoImageViewer.Core
{
    /// <summary>
    /// Lightweight on-device debug overlay. Toggle with F1 in editor/device keyboard.
    /// </summary>
    public class RuntimeDebugOverlay : MonoBehaviour
    {
        [SerializeField] private KeyCode _toggleKey = KeyCode.F1;
        [SerializeField] private bool _visible = true;

        private readonly Queue<string> _lines = new Queue<string>();
        private const int MaxLines = 12;

        public static RuntimeDebugOverlay Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
                _visible = !_visible;
        }

        public void Log(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            _lines.Enqueue(message);
            while (_lines.Count > MaxLines)
                _lines.Dequeue();
        }

        private void OnGUI()
        {
            if (!_visible) return;

            GUILayout.BeginArea(new Rect(12, 12, 900, 420), GUI.skin.box);
            GUILayout.Label("Pico Image Viewer Debug Overlay (F1)");
            foreach (var line in _lines)
                GUILayout.Label(line);
            GUILayout.EndArea();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
