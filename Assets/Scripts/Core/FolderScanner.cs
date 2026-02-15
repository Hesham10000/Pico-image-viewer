using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using PicoImageViewer.Data;

namespace PicoImageViewer.Core
{
    /// <summary>
    /// Scans a root folder for subfolders (rows) and images (columns).
    /// Returns structured FolderData sorted by folder name and filename.
    /// </summary>
    public static class FolderScanner
    {
        private static readonly HashSet<string> SupportedExtensions = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif"
        };

        /// <summary>
        /// Scans the root folder and returns a list of FolderData (rows),
        /// each containing sorted ImageData entries (columns).
        /// Images directly under root are placed in a "(root)" row at index 0.
        /// </summary>
        public static List<FolderData> Scan(string rootPath)
        {
            var results = new List<FolderData>();

            if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
            {
                Debug.LogWarning($"[FolderScanner] Root path does not exist: {rootPath}");
                return results;
            }

            // Normalize path
            rootPath = Path.GetFullPath(rootPath).TrimEnd(Path.DirectorySeparatorChar);
            int rowIndex = 0;

            // Check for images directly under root → "(root)" row
            var rootImages = GetImagesInDirectory(rootPath, rootPath, "(root)", rowIndex);
            if (rootImages.Count > 0)
            {
                var rootFolder = new FolderData("(root)", rootPath, rowIndex);
                rootFolder.Images = rootImages;
                results.Add(rootFolder);
                rowIndex++;
            }

            // Scan subfolders sorted by name (case-insensitive)
            string[] subDirs;
            try
            {
                subDirs = Directory.GetDirectories(rootPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FolderScanner] Cannot read subfolders of {rootPath}: {ex.Message}");
                return results;
            }

            var sortedDirs = subDirs
                .OrderBy(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (var dir in sortedDirs)
            {
                string folderName = Path.GetFileName(dir);
                var images = GetImagesInDirectory(dir, rootPath, folderName, rowIndex);
                if (images.Count > 0)
                {
                    var folderData = new FolderData(folderName, dir, rowIndex);
                    folderData.Images = images;
                    results.Add(folderData);
                    rowIndex++;
                }
            }

            Debug.Log($"[FolderScanner] Scanned {rootPath}: {results.Count} rows, " +
                      $"{results.Sum(f => f.Images.Count)} total images");
            return results;
        }

        private static List<ImageData> GetImagesInDirectory(
            string dirPath, string rootPath, string folderName, int rowIndex)
        {
            var images = new List<ImageData>();

            string[] files;
            try
            {
                files = Directory.GetFiles(dirPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[FolderScanner] Cannot read files in {dirPath}: {ex.Message}");
                return images;
            }

            int colIndex = 0;
            var sortedFiles = files
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f)))
                .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
                .ToArray();

            foreach (var file in sortedFiles)
            {
                images.Add(new ImageData(file, rootPath, folderName, rowIndex, colIndex));
                colIndex++;
            }

            return images;
        }
    }
}
