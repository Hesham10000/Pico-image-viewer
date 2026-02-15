using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PicoImageViewer.Core
{
    /// <summary>
    /// Configures XR Interaction Toolkit 3.x components for Pico 4 Ultra.
    /// Sets up ray interactors on controllers for interacting with world-space UI.
    /// Attach to the XR Rig root.
    /// </summary>
    public class XRSetup : MonoBehaviour
    {
        [Header("Controller References")]
        [SerializeField] private Transform _leftControllerTransform;
        [SerializeField] private Transform _rightControllerTransform;

        [Header("Interaction Settings")]
        [SerializeField] private float _rayMaxDistance = 10f;
        [SerializeField] private float _rayWidth = 0.005f;
        [SerializeField] private LayerMask _interactionLayers = ~0;

        [Header("Visual")]
        [SerializeField] private Material _rayMaterial;
        [SerializeField] private Color _validColor = new Color(0.4f, 0.8f, 1f, 0.8f);
        [SerializeField] private Color _invalidColor = new Color(1f, 1f, 1f, 0.3f);

        private void Start()
        {
            SetupController(_leftControllerTransform, "LeftRayInteractor");
            SetupController(_rightControllerTransform, "RightRayInteractor");

            // Ensure XRInteractionManager exists
            if (FindAnyObjectByType<XRInteractionManager>() == null)
            {
                var managerGO = new GameObject("XRInteractionManager");
                managerGO.transform.SetParent(transform);
                managerGO.AddComponent<XRInteractionManager>();
            }
        }

        private void SetupController(Transform controller, string rayName)
        {
            if (controller == null) return;

            // Create ray interactor child
            var rayGO = new GameObject(rayName);
            rayGO.transform.SetParent(controller, false);
            rayGO.transform.localPosition = Vector3.zero;
            rayGO.transform.localRotation = Quaternion.identity;

            // Add XR Ray Interactor (XRI 3.x)
            var rayInteractor = rayGO.AddComponent<XRRayInteractor>();
            rayInteractor.maxRaycastDistance = _rayMaxDistance;

            // Add line renderer for visual ray
            var lineRenderer = rayGO.AddComponent<LineRenderer>();
            lineRenderer.startWidth = _rayWidth;
            lineRenderer.endWidth = _rayWidth * 0.5f;
            lineRenderer.positionCount = 2;
            lineRenderer.useWorldSpace = true;
            if (_rayMaterial != null) lineRenderer.material = _rayMaterial;

            // Add XR Interactor Line Visual
            var lineVisual = rayGO.AddComponent<XRInteractorLineVisual>();
            lineVisual.lineLength = _rayMaxDistance;
            lineVisual.validColorGradient = CreateGradient(_validColor);
            lineVisual.invalidColorGradient = CreateGradient(_invalidColor);

            Debug.Log($"[XRSetup] Configured {rayName} on {controller.name}");
        }

        private Gradient CreateGradient(Color color)
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(color, 0f),
                    new GradientColorKey(color, 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(color.a, 0f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            return gradient;
        }
    }
}
