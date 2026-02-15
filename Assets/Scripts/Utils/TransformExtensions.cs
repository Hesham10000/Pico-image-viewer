using UnityEngine;

namespace PicoImageViewer.Utils
{
    /// <summary>
    /// Utility extension methods for Transform operations.
    /// </summary>
    public static class TransformExtensions
    {
        /// <summary>
        /// Makes the transform face toward a target position, keeping Y-axis up.
        /// </summary>
        public static void LookAtFlat(this Transform transform, Vector3 target)
        {
            Vector3 direction = target - transform.position;
            direction.y = 0;
            if (direction.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            }
        }

        /// <summary>
        /// Destroys all children of a transform.
        /// </summary>
        public static void DestroyAllChildren(this Transform transform)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Object.Destroy(transform.GetChild(i).gameObject);
            }
        }
    }
}
