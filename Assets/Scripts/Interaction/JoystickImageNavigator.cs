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
    /// - Joystick DOWN: load next image in folder into the hovered window
    /// - Joystick UP: load previous image in folder into the hovered window
    ///
    /// Uses Unity Input System for joystick input and XR ray hover detection.
    /// Attach to a controller or a central manager object.
    /// </summary>
    public class JoystickImageNavigator : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionReference _leftThumbstickAction;
        [SerializeField] private InputActionReference _rightThumbstickAction;

        [Header("Settings")]
        [SerializeField] private float _deadzone = 0.5f;
        [SerializeField] private float _cooldownSeconds = 0.3f;

        [Header("Hover Detection")]
        [SerializeField] private LayerMask _windowLayerMask = ~0;
        [SerializeField] private Transform _leftRayOrigin;
        [SerializeField] private Transform _rightRayOrigin;
        [SerializeField] private float _rayMaxDistance = 15f;

        private float _leftCooldownTimer;
        private float _rightCooldownTimer;
        private bool _leftWasActive;
        private bool _rightWasActive;

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
            ProcessController(_leftThumbstickAction, _leftRayOrigin,
                ref _leftCooldownTimer, ref _leftWasActive, deadzone, cooldown);

            // Process right controller
            ProcessController(_rightThumbstickAction, _rightRayOrigin,
                ref _rightCooldownTimer, ref _rightWasActive, deadzone, cooldown);
        }

        private void ProcessController(InputActionReference thumbstickAction,
            Transform rayOrigin, ref float cooldownTimer, ref bool wasActive,
            float deadzone, float cooldown)
        {
            if (thumbstickAction == null || thumbstickAction.action == null) return;
            if (rayOrigin == null) return;

            Vector2 thumbstick = thumbstickAction.action.ReadValue<Vector2>();

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
    }
}
