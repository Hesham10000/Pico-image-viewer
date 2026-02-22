using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEngine;
using PicoImageViewer.Data;

namespace PicoImageViewer.Core
{
    /// <summary>
    /// Async texture loader with LRU cache and max-size downscaling.
    /// Loads image bytes on a background thread, then creates the texture on the main thread.
    /// </summary>
    public class TextureLoader : MonoBehaviour
    {
        public static TextureLoader Instance { get; private set; }

        [Header("Configuration")]
        [SerializeField] private int _maxCacheEntries = 100;

        private readonly Dictionary<string, CacheEntry> _cache = new Dictionary<string, CacheEntry>();
        private readonly LinkedList<string> _lruOrder = new LinkedList<string>();
        private int _maxTextureSize = 2048;

        private class CacheEntry
        {
            public Texture2D Texture;
            public LinkedListNode<string> LruNode;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void SetMaxTextureSize(int size)
        {
            _maxTextureSize = Mathf.Clamp(size, 256, 8192);
        }

        /// <summary>
        /// Load a texture asynchronously. Calls onComplete with the loaded Texture2D
        /// (or null on failure) and the original image dimensions.
        /// </summary>
        public void LoadAsync(string filePath, Action<Texture2D, int, int> onComplete)
        {
            // Check cache first
            if (_cache.TryGetValue(filePath, out var entry))
            {
                // Move to front of LRU
                _lruOrder.Remove(entry.LruNode);
                _lruOrder.AddFirst(entry.LruNode);
                onComplete?.Invoke(entry.Texture,
                    entry.Texture != null ? entry.Texture.width : 0,
                    entry.Texture != null ? entry.Texture.height : 0);
                return;
            }

            StartCoroutine(LoadCoroutine(filePath, onComplete));
        }

        private IEnumerator LoadCoroutine(string filePath, Action<Texture2D, int, int> onComplete)
        {
            byte[] fileBytes = null;
            bool loadDone = false;
            bool loadError = false;

            // Read file bytes on a thread pool thread
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    if (File.Exists(filePath))
                    {
                        fileBytes = File.ReadAllBytes(filePath);
                    }
                    else
                    {
                        loadError = true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[TextureLoader] Failed to read {filePath}: {ex.Message}");
                    loadError = true;
                }
                finally
                {
                    loadDone = true;
                }
            });

            // Wait for background read
            while (!loadDone) yield return null;

            if (loadError || fileBytes == null || fileBytes.Length == 0)
            {
                onComplete?.Invoke(null, 0, 0);
                yield break;
            }

            // Create texture on main thread
            Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
    
            // Use Trilinear + Anisotropic to kill the shiny aliasing in VR!
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 8; 
            tex.wrapMode = TextureWrapMode.Clamp;

            if (!tex.LoadImage(fileBytes))
            {
                Debug.LogWarning($"[TextureLoader] Failed to decode image: {filePath}");
                Destroy(tex);
                onComplete?.Invoke(null, 0, 0);
                yield break;
            }

            int origWidth = tex.width;
            int origHeight = tex.height;

            // Downscale if exceeds max
            if (tex.width > _maxTextureSize || tex.height > _maxTextureSize)
            {
                tex = DownscaleTexture(tex, _maxTextureSize);
            }

            // Cache it
            AddToCache(filePath, tex);

            onComplete?.Invoke(tex, origWidth, origHeight);
        }

        private Texture2D DownscaleTexture(Texture2D source, int maxDimension)
        {
            float scale = Mathf.Min(
                (float)maxDimension / source.width,
                (float)maxDimension / source.height);

            int newWidth = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
            int newHeight = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));

            RenderTexture rt = RenderTexture.GetTemporary(newWidth, newHeight, 0, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;

            Graphics.Blit(source, rt);

            // 1. Create the final resized texture WITH mipmaps (true)
            Texture2D result = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, true);
    
            // 2. Apply the exact same VR filters to the resized version
            result.filterMode = FilterMode.Trilinear;
            result.anisoLevel = 8;
            result.wrapMode = TextureWrapMode.Clamp;

            // 3. Read the pixels and tell Apply to generate the mipmaps (true)
            result.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
            result.Apply(true);

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
            Destroy(source);

            return result;
        }

        private void AddToCache(string key, Texture2D tex)
        {
            // Evict if at capacity
            while (_cache.Count >= _maxCacheEntries && _lruOrder.Last != null)
            {
                string evictKey = _lruOrder.Last.Value;
                _lruOrder.RemoveLast();
                if (_cache.TryGetValue(evictKey, out var evictEntry))
                {
                    if (evictEntry.Texture != null)
                        Destroy(evictEntry.Texture);
                    _cache.Remove(evictKey);
                }
            }

            var node = _lruOrder.AddFirst(key);
            _cache[key] = new CacheEntry { Texture = tex, LruNode = node };
        }

        /// <summary>
        /// Removes a specific texture from the cache and destroys it.
        /// </summary>
        public void Evict(string filePath)
        {
            if (_cache.TryGetValue(filePath, out var entry))
            {
                _lruOrder.Remove(entry.LruNode);
                if (entry.Texture != null) Destroy(entry.Texture);
                _cache.Remove(filePath);
            }
        }

        /// <summary>
        /// Clears entire cache, destroying all textures.
        /// </summary>
        public void ClearCache()
        {
            foreach (var entry in _cache.Values)
            {
                if (entry.Texture != null) Destroy(entry.Texture);
            }
            _cache.Clear();
            _lruOrder.Clear();
        }

        private void OnDestroy()
        {
            ClearCache();
        }
    }
}
