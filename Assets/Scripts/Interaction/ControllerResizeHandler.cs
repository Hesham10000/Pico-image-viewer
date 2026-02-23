using UnityEngine;
using UnityEngine.InputSystem;
using PicoImageViewer.UI;

namespace PicoImageViewer.Interaction
{
    /// <summary>
    /// Handles window resizing via controller input:
    ///   Grip (held) + Trigger (held) + Thumbstick Up/Down = resize
    ///
    /// - Thumbstick UP while holding grip+trigger: decrease window size
    /// - Thumbstick DOWN while holding grip+trigger: increase window size
    ///
    /// Works with any window the controller ray is pointing at.
    /// Attach to a manager object (e.g. alongside JoystickImageNavigator).
    /// Creates its own InputActions at runtime - no Inspector setup needed.
    /// </summary>
    public class ControllerResizeHandler : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private float _resizeSpeed = 0.8f;
        [SerializeField] private float _gripThreshold = 0.3f;
        [SerializeField] private float _triggerThreshold = 0.3f;
        [SerializeField] private float _thumbstickDeadzone = 0.3f;

        [Header("Ray Detection")]
        [SerializeField] private Transform _leftRayOrigin;
        [SerializeField] private Transform _rightRayOrigin;
        [SerializeField] private float _rayMaxDistance = 15f;
        [SerializeField] private LayerMask _windowLayerMask = ~0;

        // Input actions created at runtime
        private InputAction _leftGripAction;
        private InputAction _rightGripAction;
        private InputAction _leftTriggerAction;
        private InputAction _rightTriggerAction;
        private InputAction _leftThumbstickAction;
        private InputAction _rightThumbstickAction;

        private void Start()
        {
            AutoDiscoverRayOrigins();
            CreateInputActions();
        }

        private void AutoDiscoverRayOrigins()
        {
            if (_leftRayOrigin == null)
            {
                var go = GameObject.Find("Left Controller");
                if (go != null) _leftRayOrigin = go.transform;
            }
            if (_rightRayOrigin == null)
            {
                var go = GameObject.Find("Right Controller");
                if (go != null) _rightRayOrigin = go.transform;
            }
        }

        private void CreateInputActions()
        {
            _leftGripAction = new InputAction("LeftGrip",
                InputActionType.Value, "<XRController>{LeftHand}/grip");
            _rightGripAction = new InputAction("RightGrip",
                InputActionType.Value, "<XRController>{RightHand}/grip");
            _leftTriggerAction = new InputAction("LeftTrigger",
                InputActionType.Value, "<XRController>{LeftHand}/trigger");
            _rightTriggerAction = new InputAction("RightTrigger",
                InputActionType.Value, "<XRController>{RightHand}/trigger");
            _leftThumbstickAction = new InputAction("LeftThumbstick",
                InputActionType.Value, "<XRController>{LeftHand}/thumbstick");
            _rightThumbstickAction = new InputAction("RightThumbstick",
                InputActionType.Value, "<XRController>{RightHand}/thumbstick");

            _leftGripAction.Enable();
            _rightGripAction.Enable();
            _leftTriggerAction.Enable();
            _rightTriggerAction.Enable();
            _leftThumbstickAction.Enable();
            _rightThumbstickAction.Enable();
        }

        private void Update()
        {
            ProcessController(
                _leftGripAction, _leftTriggerAction, _leftThumbstickAction, _leftRayOrigin);
            ProcessController(
                _rightGripAction, _rightTriggerAction, _rightThumbstickAction, _rightRayOrigin);
        }

        private void ProcessController(InputAction gripAction, InputAction triggerAction,
            InputAction thumbstickAction, Transform rayOrigin)
        {
            if (gripAction == null || triggerAction == null || thumbstickAction == null)
                return;
            if (rayOrigin == null) return;

            // Check grip is held
            float grip = gripAction.ReadValue<float>();
            if (grip < _gripThreshold) return;

            // Check trigger is held
            float trigger = triggerAction.ReadValue<float>();
            if (trigger < _triggerThreshold) return;

            // Check thumbstick vertical deflection
            Vector2 thumbstick = thumbstickAction.ReadValue<Vector2>();
            if (Mathf.Abs(thumbstick.y) < _thumbstickDeadzone) return;

            // Find window being pointed at
            ImageWindow window = RaycastForWindow(rayOrigin);
            if (window == null) return;

            // Thumbstick UP = decrease size, DOWN = increase size
            float delta = -thumbstick.y * _resizeSpeed * Time.deltaTime;
            float newScale = window.ResizeScale + delta;
            window.SetResizeScale(newScale);
        }

        private ImageWindow RaycastForWindow(Transform rayOrigin)
        {
            Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, _rayMaxDistance, _windowLayerMask))
            {
                // Walk up hierarchy to find ImageWindow
                Transform current = hit.transform;
                while (current != null)
                {
                    var window = current.GetComponent<ImageWindow>();
                    if (window != null) return window;
                    current = current.parent;
                }
            }

            return null;
        }

        private void OnDestroy()
        {
            _leftGripAction?.Disable();
            _rightGripAction?.Disable();
            _leftTriggerAction?.Disable();
            _rightTriggerAction?.Disable();
            _leftThumbstickAction?.Disable();
            _rightThumbstickAction?.Disable();
        }
    }
}
