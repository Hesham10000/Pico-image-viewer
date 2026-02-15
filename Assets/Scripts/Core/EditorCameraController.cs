using UnityEngine;
using UnityEngine.EventSystems;

namespace PicoImageViewer.Core
{
    /// <summary>
    /// Simple camera controller for testing in the Unity Editor without a VR headset.
    /// WASD to move, right-click + mouse to look around, scroll to zoom.
    /// Only active in the editor (disabled on device builds).
    /// Also provides mouse-click interaction with world-space UI canvases.
    /// </summary>
    public class EditorCameraController : MonoBehaviour
    {
#if UNITY_EDITOR
        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 3f;
        [SerializeField] private float _lookSpeed = 2f;
        [SerializeField] private float _scrollSpeed = 5f;

        private float _rotationX;
        private float _rotationY;
        private Transform _moveTarget; // the transform we actually move

        private void Start()
        {
            // Move the XR Rig root (or Camera Offset parent) so the whole rig is positioned correctly.
            // This avoids conflicts with XR Rig hierarchy expectations.
            // Walk up to find the XR Rig root.
            _moveTarget = transform;
            var xrRig = transform.parent;
            while (xrRig != null)
            {
                if (xrRig.name == "XR Rig")
                {
                    _moveTarget = xrRig;
                    break;
                }
                xrRig = xrRig.parent;
            }

            // Position at a good starting point to see the UI panels
            // Panels are at: SettingsPanel (-1.5, 1.2, 1.5) and FolderBrowser (1.0, 1.2, 1.5)
            _moveTarget.position = new Vector3(0f, 1.2f, 0f);
            transform.localRotation = Quaternion.Euler(0, 0, 0);

            _rotationX = 0;
            _rotationY = 0;

            // Ensure EventSystem exists for UI interaction
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                Debug.Log("[EditorCamera] Created EventSystem for UI interaction");
            }

            Debug.Log("[EditorCamera] Editor camera active. Controls:\n" +
                       "  WASD = move, Q/E = down/up\n" +
                       "  Right-click + Mouse = look around\n" +
                       "  Shift = move faster\n" +
                       "  Scroll = zoom\n" +
                       "  Left-click = interact with UI panels");
        }

        private void Update()
        {
            HandleLook();
            HandleMovement();
        }

        private void HandleLook()
        {
            // Right mouse button to look around
            if (Input.GetMouseButton(1))
            {
                _rotationX -= Input.GetAxis("Mouse Y") * _lookSpeed;
                _rotationY += Input.GetAxis("Mouse X") * _lookSpeed;
                _rotationX = Mathf.Clamp(_rotationX, -90f, 90f);
                transform.rotation = Quaternion.Euler(_rotationX, _rotationY, 0);
            }
        }

        private void HandleMovement()
        {
            float speed = _moveSpeed * Time.deltaTime;

            // Hold shift to move faster
            if (Input.GetKey(KeyCode.LeftShift)) speed *= 3f;

            Vector3 move = Vector3.zero;

            if (Input.GetKey(KeyCode.W)) move += transform.forward;
            if (Input.GetKey(KeyCode.S)) move -= transform.forward;
            if (Input.GetKey(KeyCode.A)) move -= transform.right;
            if (Input.GetKey(KeyCode.D)) move += transform.right;
            if (Input.GetKey(KeyCode.E)) move += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) move -= Vector3.up;

            // Scroll wheel for forward/back zoom
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                move += transform.forward * scroll * _scrollSpeed;
            }

            _moveTarget.position += move * speed;
        }
#endif
    }
}
