using System.Collections.Generic;
using UnityEngine;
using PicoImageViewer.Data;

namespace PicoImageViewer.Core
{
    /// <summary>
    /// Computes world-space positions for image windows arranged in a grid.
    /// Rows correspond to subfolders; columns to images within each subfolder.
    /// The grid is anchored relative to a user-defined origin transform.
    /// </summary>
    public class GridLayoutManager
    {
        private AppSettings _settings;

        public GridLayoutManager(AppSettings settings)
        {
            _settings = settings;
        }

        public void UpdateSettings(AppSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// Computes the grid origin in world space, positioned in front of the given
        /// camera/head transform based on settings offsets.
        /// </summary>
        public Vector3 ComputeGridOrigin(Transform headTransform)
        {
            Vector3 forward = headTransform.forward;
            forward.y = 0; // keep grid level
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            Vector3 origin = headTransform.position
                + forward * _settings.GridForwardOffset
                + Vector3.up * _settings.GridUpOffset
                + right * _settings.GridLeftOffset;

            return origin;
        }

        /// <summary>
        /// Computes the rotation for all windows to face the head transform.
        /// </summary>
        public Quaternion ComputeGridRotation(Transform headTransform)
        {
            Vector3 forward = headTransform.forward;
            forward.y = 0;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();

            // Canvas content is visible from its -Z side, so canvas +Z must
            // point AWAY from the user (same as the user's forward direction).
            return Quaternion.LookRotation(forward, Vector3.up);
        }

        /// <summary>
        /// Computes grid slot positions for all images across all folders.
        /// Images in each row are arranged in a curved arc around the user,
        /// creating a panoramic cylinder layout natural for VR viewing.
        /// </summary>
        public List<GridSlot> ComputeSlots(List<FolderData> folders, Transform headTransform)
        {
            var slots = new List<GridSlot>();

            float windowW = _settings.DefaultWindowWidth * _settings.WindowScaleMultiplier;
            float windowH = _settings.DefaultWindowHeight * _settings.WindowScaleMultiplier;
            float colSpacing = _settings.ColumnSpacing;
            float rowSpacing = _settings.RowSpacing;
            float radius = _settings.GridForwardOffset;

            // Head position and forward direction (flattened to horizontal)
            Vector3 headPos = headTransform.position;
            Vector3 forward = headTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();

            // Angular step per image: how many radians each image+spacing occupies on the arc
            float arcPerImage = 2f * Mathf.Atan2((windowW + colSpacing) * 0.5f, radius);

            Debug.Log($"[GridLayout] Curved arc: radius={radius}, arcPerImage={arcPerImage * Mathf.Rad2Deg}°, " +
                      $"windowSize={windowW}x{windowH}, head={headPos}");

            for (int r = 0; r < folders.Count; r++)
            {
                var folder = folders[r];
                int imageCount = folder.Images.Count;

                // Center the row: total arc angle for this row
                float totalArcAngle = (imageCount - 1) * arcPerImage;
                float startAngle = -totalArcAngle * 0.5f;

                // Row Y position: eye level + up offset, then step down per row
                float yPos = headPos.y + _settings.GridUpOffset - r * (windowH + rowSpacing);

                for (int c = 0; c < imageCount; c++)
                {
                    float angle = startAngle + c * arcPerImage;

                    // Rotate the forward direction by this angle around world up
                    Quaternion arcRot = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, Vector3.up);
                    Vector3 dir = arcRot * forward;

                    // Position on the arc at the given radius
                    Vector3 pos = new Vector3(headPos.x, yPos, headPos.z)
                                  + dir * radius;

                    // Window faces the user (look direction = dir, so canvas front faces -dir)
                    Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

                    slots.Add(new GridSlot
                    {
                        Image = folder.Images[c],
                        Position = pos,
                        Rotation = rot,
                        Width = windowW,
                        Height = windowH
                    });
                }
            }

            return slots;
        }

        /// <summary>
        /// Computes the grid slot for a single image given its row/col indices
        /// and the total number of images in that row (needed for arc centering).
        /// </summary>
        public GridSlot ComputeSlot(int row, int col, int imagesInRow, Transform headTransform)
        {
            float windowW = _settings.DefaultWindowWidth * _settings.WindowScaleMultiplier;
            float windowH = _settings.DefaultWindowHeight * _settings.WindowScaleMultiplier;
            float radius = _settings.GridForwardOffset;

            Vector3 headPos = headTransform.position;
            Vector3 forward = headTransform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            forward.Normalize();

            float arcPerImage = 2f * Mathf.Atan2((windowW + _settings.ColumnSpacing) * 0.5f, radius);
            float totalArcAngle = (imagesInRow - 1) * arcPerImage;
            float startAngle = -totalArcAngle * 0.5f;
            float angle = startAngle + col * arcPerImage;

            float yPos = headPos.y + _settings.GridUpOffset - row * (windowH + _settings.RowSpacing);

            Quaternion arcRot = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, Vector3.up);
            Vector3 dir = arcRot * forward;
            Vector3 pos = new Vector3(headPos.x, yPos, headPos.z) + dir * radius;
            Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);

            return new GridSlot
            {
                Position = pos,
                Rotation = rot,
                Width = windowW,
                Height = windowH
            };
        }
    }

    public struct GridSlot
    {
        public ImageData Image;
        public Vector3 Position;
        public Quaternion Rotation;
        public float Width;
        public float Height;
    }
}
