using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using PicoImageViewer.Data;
using PicoImageViewer.UI;

namespace PicoImageViewer.Interaction
{
    /// <summary>
    /// Detects when the user hovers over an ImageWindow and uses the joystick
    /// (thumbstick) to cycle through sibling images in the same folder.
    ///
    /// Input priority (no conflicts):
    ///   1. Thumbstick ALONE (no grip, no trigger) → cycle prev/next image
    ///   2. Grip + thumbstick                      → grab/move (handled by XRGrabInteractable)
    ///   3. Grip + trigger + thumbstick             → resize (handled by ControllerResizeHandler)
    ///
    /// This script only fires for case 1: thumbstick without grip or trigger.
    /// </summary>
    public class JoystickImageNavigator : MonoBehaviour
    {
        [Header("Input (optional - auto-created if not assigned)")]
        [SerializeField] private InputActionReference _leftThumbstickAction;
        [SerializeField] private InputActionReference _rightThumbstickAction;

        [Header("Settings")]
        [SerializeField] private float _deadzone = 0.5f;
        [SerializeField] private float _cooldownSeconds = 0.3f;
        [SerializeField] private float _gripThreshold = 0.3f;
        [SerializeField] private float _triggerThreshold = 0.3f;

        [Header("Hover Detection")]
        [SerializeField] private LayerMask _windowLayerMask = ~0;
        [SerializeField] private Transform _leftRayOrigin;
        [SerializeField] private Transform _rightRayOrigin;
        [SerializeField] private float _rayMaxDistance = 15f;

        private float _leftCooldownTimer;
        private float _rightCooldownTimer;
        private bool _leftWasActive;
        private bool _rightWasActive;

        // Grip and trigger actions (created at runtime to detect modifier keys)
        private InputAction _leftGripAction;
        private InputAction _rightGripAction;
        private InputAction _leftTriggerAction;
        private InputAction _rightTriggerAction;
        // Fallback thumbstick actions if InputActionReference not assigned
        private InputAction _leftThumbstickFallback;
        private InputAction _rightThumbstickFallback;

        private void Start()
        {
            AutoDiscoverRayOrigins();
            CreateModifierActions();
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

        private void CreateModifierActions()
        {
            // Create grip/trigger actions to check if they're held (modifier keys)
            _leftGripAction = new InputAction("LeftGrip",
                InputActionType.Value, "<XRController>{LeftHand}/grip");
            _rightGripAction = new InputAction("RightGrip",
                InputActionType.Value, "<XRController>{RightHand}/grip");
            _leftTriggerAction = new InputAction("LeftTrigger",
                InputActionType.Value, "<XRController>{LeftHand}/trigger");
            _rightTriggerAction = new InputAction("RightTrigger",
                InputActionType.Value, "<XRController>{RightHand}/trigger");

            _leftGripAction.Enable();
            _rightGripAction.Enable();
            _leftTriggerAction.Enable();
            _rightTriggerAction.Enable();

            // Create fallback thumbstick actions if InputActionReference not assigned
            if (_leftThumbstickAction == null)
            {
                _leftThumbstickFallback = new InputAction("LeftThumbstick",
                    InputActionType.Value, "<XRController>{LeftHand}/thumbstick");
                _leftThumbstickFallback.Enable();
            }
            if (_rightThumbstickAction == null)
            {
                _rightThumbstickFallback = new InputAction("RightThumbstick",
                    InputActionType.Value, "<XRController>{RightHand}/thumbstick");
                _rightThumbstickFallback.Enable();
            }
        }

        private void OnEnable()
        {
            EnableAction(_leftThumbstickAction);
            EnableAction(_rightThumbstickAction);
        }

        private void OnDisable()
        {
            DisableAction(_leftThumbstickAction);
            DisableAction(_rightThumbstickAction);
        }

        private void EnableAction(InputActionReference actionRef)
        {
            if (actionRef != null && actionRef.action != null)
                actionRef.action.Enable();
        }

        private void DisableAction(InputActionReference actionRef)
        {
            if (actionRef != null && actionRef.action != null)
                actionRef.action.Disable();
        }

        private void Update()
        {
            // Update cooldowns
            _leftCooldownTimer -= Time.deltaTime;
            _rightCooldownTimer -= Time.deltaTime;

            // Load settings for deadzone/cooldown
            float deadzone = _deadzone;
            float cooldown = _cooldownSeconds;
            var settings = AppSettings.Load();
            if (settings != null)
            {
                deadzone = settings.JoystickDeadzone;
                cooldown = settings.JoystickCooldown;
            }

            // Process left controller
            ProcessController(
                _leftThumbstickAction, _leftThumbstickFallback,
                _leftGripAction, _leftTriggerAction, _leftRayOrigin,
                ref _leftCooldownTimer, ref _leftWasActive, deadzone, cooldown);

            // Process right controller
            ProcessController(
                _rightThumbstickAction, _rightThumbstickFallback,
                _rightGripAction, _rightTriggerAction, _rightRayOrigin,
                ref _rightCooldownTimer, ref _rightWasActive, deadzone, cooldown);
        }

        private void ProcessController(InputActionReference thumbstickRef,
            InputAction thumbstickFallback,
            InputAction gripAction, InputAction triggerAction,
            Transform rayOrigin, ref float cooldownTimer, ref bool wasActive,
            float deadzone, float cooldown)
        {
            if (rayOrigin == null) return;

            // Read thumbstick from either the reference or fallback
            Vector2 thumbstick = Vector2.zero;
            if (thumbstickRef != null && thumbstickRef.action != null)
                thumbstick = thumbstickRef.action.ReadValue<Vector2>();
            else if (thumbstickFallback != null)
                thumbstick = thumbstickFallback.ReadValue<Vector2>();
            else
                return;

            // PRIORITY CHECK: Only cycle images when grip AND trigger are NOT held.
            // If grip is held → user is grabbing/moving (handled by XRGrabInteractable)
            // If grip + trigger held → user is resizing (handled by ControllerResizeHandler)
            float grip = gripAction != null ? gripAction.ReadValue<float>() : 0f;
            float trigger = triggerAction != null ? triggerAction.ReadValue<float>() : 0f;

            if (grip > _gripThreshold || trigger > _triggerThreshold)
            {
                // Another shortcut is active, don't cycle images
                wasActive = false;
                return;
            }

            // Check vertical axis with deadzone
            bool isUp = thumbstick.y > deadzone;
            bool isDown = thumbstick.y < -deadzone;
            bool isActive = isUp || isDown;

            // Require release between activations (edge-triggered) + cooldown
            if (isActive && !wasActive && cooldownTimer <= 0f)
            {
                // Raycast to find hovered window
                ImageWindow hoveredWindow = RaycastForWindow(rayOrigin);
                if (hoveredWindow != null)
                {
                    if (isDown)
                        hoveredWindow.CycleToNextImage();
                    else if (isUp)
                        hoveredWindow.CycleToPreviousImage();

                    cooldownTimer = cooldown;
                }
            }

            wasActive = isActive;
        }

        /// <summary>
        /// Cast a ray from the controller to find an ImageWindow being hovered.
        /// </summary>
        private ImageWindow RaycastForWindow(Transform rayOrigin)
        {
            Ray ray = new Ray(rayOrigin.position, rayOrigin.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, _rayMaxDistance, _windowLayerMask))
            {
                // Walk up the hierarchy to find ImageWindow
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
            _leftThumbstickFallback?.Disable();
            _rightThumbstickFallback?.Disable();
        }
    }
}
