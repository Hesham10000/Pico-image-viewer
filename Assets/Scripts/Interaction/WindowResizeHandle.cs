using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using PicoImageViewer.UI;

namespace PicoImageViewer.Interaction
{
    /// <summary>
    /// Resize handle for image windows. Attach to corner/edge colliders.
    /// Dragging this handle resizes the parent ImageWindow.
    /// </summary>
    public class WindowResizeHandle : XRBaseInteractable
    {
        public enum HandlePosition
        {
            TopLeft, TopRight, BottomLeft, BottomRight,
            Left, Right, Top, Bottom
        }

        [Header("Resize Settings")]
        [SerializeField] private HandlePosition _handlePosition = HandlePosition.BottomRight;
        [SerializeField] private float _resizeSensitivity = 1.0f;

        private ImageWindow _parentWindow;
        private IXRSelectInteractor _currentInteractor;
        private Vector3 _grabStartPos;
        private float _startWidth;
        private float _startHeight;
        private bool _isResizing;

        protected override void Awake()
        {
            base.Awake();
            _parentWindow = GetComponentInParent<ImageWindow>();
        }

        public void SetResizeSensitivity(float sensitivity)
        {
            _resizeSensitivity = sensitivity;
        }

        protected override void OnSelectEntered(SelectEnterEventArgs args)
        {
            base.OnSelectEntered(args);
            _currentInteractor = args.interactorObject;
            _grabStartPos = _currentInteractor.transform.position;
            _startWidth = _parentWindow != null ? _parentWindow.CurrentWidth : 1f;
            _startHeight = _parentWindow != null ? _parentWindow.CurrentHeight : 1f;
            _isResizing = true;
        }

        protected override void OnSelectExited(SelectExitEventArgs args)
        {
            base.OnSelectExited(args);
            _currentInteractor = null;
            _isResizing = false;
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);

            if (!_isResizing || _currentInteractor == null || _parentWindow == null)
                return;

            if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic)
                return;

            Vector3 currentPos = _currentInteractor.transform.position;
            Vector3 delta = (currentPos - _grabStartPos) * _resizeSensitivity;

            // Convert world delta to width/height changes based on handle position
            float dw = 0f, dh = 0f;

            // Project delta onto the window's local axes
            Vector3 localDelta = _parentWindow.transform.InverseTransformDirection(delta);

            switch (_handlePosition)
            {
                case HandlePosition.BottomRight:
                    dw = localDelta.x;
                    dh = -localDelta.y;
                    break;
                case HandlePosition.BottomLeft:
                    dw = -localDelta.x;
                    dh = -localDelta.y;
                    break;
                case HandlePosition.TopRight:
                    dw = localDelta.x;
                    dh = localDelta.y;
                    break;
                case HandlePosition.TopLeft:
                    dw = -localDelta.x;
                    dh = localDelta.y;
                    break;
                case HandlePosition.Right:
                    dw = localDelta.x;
                    break;
                case HandlePosition.Left:
                    dw = -localDelta.x;
                    break;
                case HandlePosition.Top:
                    dh = localDelta.y;
                    break;
                case HandlePosition.Bottom:
                    dh = -localDelta.y;
                    break;
            }

            float newWidth = _startWidth + dw;
            float newHeight = _startHeight + dh;
            _parentWindow.SetSize(newWidth, newHeight);
        }
    }
}
