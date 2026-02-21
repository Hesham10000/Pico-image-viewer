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
        /// Returns a list of (ImageData, worldPosition) tuples.
        /// </summary>
        public List<GridSlot> ComputeSlots(List<FolderData> folders, Transform headTransform)
        {
            var slots = new List<GridSlot>();

            Vector3 origin = ComputeGridOrigin(headTransform);
            Quaternion rotation = ComputeGridRotation(headTransform);

            // Grid local axes (in world space)
            Vector3 rightDir = rotation * Vector3.right;  // columns go right
            Vector3 downDir = rotation * Vector3.down;    // rows go down

            float windowW = _settings.DefaultWindowWidth * _settings.WindowScaleMultiplier;
            float windowH = _settings.DefaultWindowHeight * _settings.WindowScaleMultiplier;
            float colSpacing = _settings.ColumnSpacing;
            float rowSpacing = _settings.RowSpacing;

            // Total column step = window width + spacing
            float colStep = windowW + colSpacing;
            float rowStep = windowH + rowSpacing;

            for (int r = 0; r < folders.Count; r++)
            {
                var folder = folders[r];
                for (int c = 0; c < folder.Images.Count; c++)
                {
                    // Compute position: origin + column offset to the right + row offset downward
                    Vector3 pos = origin
                        + rightDir * (c * colStep)
                        + downDir * (r * rowStep);

                    slots.Add(new GridSlot
                    {
                        Image = folder.Images[c],
                        Position = pos,
                        Rotation = rotation,
                        Width = windowW,
                        Height = windowH
                    });
                }
            }

            return slots;
        }

        /// <summary>
        /// Computes the grid slot for a single image given its row/col indices.
        /// Used for "reset position" on individual windows.
        /// </summary>
        public GridSlot ComputeSlot(int row, int col, Transform headTransform)
        {
            Vector3 origin = ComputeGridOrigin(headTransform);
            Quaternion rotation = ComputeGridRotation(headTransform);

            Vector3 rightDir = rotation * Vector3.right;
            Vector3 downDir = rotation * Vector3.down;

            float windowW = _settings.DefaultWindowWidth * _settings.WindowScaleMultiplier;
            float windowH = _settings.DefaultWindowHeight * _settings.WindowScaleMultiplier;
            float colStep = windowW + _settings.ColumnSpacing;
            float rowStep = windowH + _settings.RowSpacing;

            return new GridSlot
            {
                Position = origin + rightDir * (col * colStep) + downDir * (row * rowStep),
                Rotation = rotation,
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
