using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace PicoImageViewer.Interaction
{
    /// <summary>
    /// Enables dragging of an ImageWindow in 3D space via XR Interaction Toolkit 3.x.
    /// Attach to the title bar collider or the window body collider.
    /// Uses XRGrabInteractable for controller/hand-based grabbing.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class XRWindowDrag : XRGrabInteractable
    {
        [Header("Drag Settings")]
        [SerializeField] private float _dragSensitivity = 1.0f;
        [SerializeField] private bool _useParentTransform = true;

        private Transform _windowRoot;
        private Vector3 _grabOffset;
        private Quaternion _grabRotationOffset;
        private bool _isDragging;

        protected override void Awake()
        {
            base.Awake();

            // Find the window root (parent with ImageWindow component)
            _windowRoot = _useParentTransform ? FindWindowRoot() : transform;

            // Configure grab behavior for XRI 3.x
            movementType = MovementType.Instantaneous;
            throwOnDetach = false;
            trackPosition = true;
            trackRotation = true;
            useDynamicAttach = true;

            // Ensure rigidbody is kinematic (no physics)
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        private Transform FindWindowRoot()
        {
            Transform current = transform;
            while (current.parent != null)
            {
                if (current.parent.GetComponent<UI.ImageWindow>() != null)
                    return current.parent;
                current = current.parent;
            }
            return transform.parent != null ? transform.parent : transform;
        }

        public void SetDragSensitivity(float sensitivity)
        {
            _dragSensitivity = sensitivity;
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            _isDragging = true;

            // Store offset between interactor and window root
            if (args.interactorObject != null)
            {
                Transform interactorTransform = args.interactorObject.transform;
                _grabOffset = _windowRoot.position - interactorTransform.position;
                _grabRotationOffset = Quaternion.Inverse(interactorTransform.rotation) * _windowRoot.rotation;
            }
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            _isDragging = false;

            // Snap to grid if enabled
            var settings = Data.AppSettings.Load();
            if (settings.SnapToGrid)
            {
                var window = _windowRoot.GetComponent<UI.ImageWindow>();
                if (window != null)
                {
                    // Snap is handled by WindowManager checking proximity to grid slot
                    window.ResetPosition();
                }
            }
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);

            if (_isDragging && updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic)
            {
                // The XRGrabInteractable base handles movement,
                // but we apply it to the window root instead of this object
                if (_useParentTransform && _windowRoot != transform)
                {
                    _windowRoot.position = transform.position;
                    _windowRoot.rotation = transform.rotation;
                }
            }
        }
    }
}
