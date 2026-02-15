using System;
using System.Collections;
using UnityEngine;

namespace PicoImageViewer.Android
{
    /// <summary>
    /// Handles Android runtime permission requests for image/file access on Pico devices.
    /// Uses READ_MEDIA_IMAGES on Android 13+ and falls back to legacy storage permissions.
    /// </summary>
    public class AndroidPermissions : MonoBehaviour
    {
        public static AndroidPermissions Instance { get; private set; }

        public Action<bool> OnPermissionResult;

        public enum PermissionState
        {
            Unknown,
            Checking,
            Requesting,
            Granted,
            Denied
        }

        public PermissionState CurrentState { get; private set; } = PermissionState.Unknown;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void RequestStoragePermissions(Action<bool> onResult)
        {
            OnPermissionResult = onResult;
#if UNITY_ANDROID && !UNITY_EDITOR
            StartCoroutine(RequestPermissionsCoroutine());
#else
            SetState(PermissionState.Granted);
            onResult?.Invoke(true);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private IEnumerator RequestPermissionsCoroutine()
        {
            SetState(PermissionState.Checking);

            if (HasStoragePermission())
            {
                SetState(PermissionState.Granted);
                OnPermissionResult?.Invoke(true);
                yield break;
            }

            SetState(PermissionState.Requesting);
            int apiLevel = GetApiLevel();
            bool granted = false;

            if (apiLevel >= 33)
            {
                yield return RequestRuntimePermission("android.permission.READ_MEDIA_IMAGES", v => granted = v);
            }
            else
            {
                yield return RequestRuntimePermission("android.permission.READ_EXTERNAL_STORAGE", v => granted = v);
            }

            if (!granted && apiLevel >= 30)
            {
                RequestManageExternalStorage();
                yield return WaitForSettingsResult();
                granted = HasStoragePermission();
            }

            SetState(granted ? PermissionState.Granted : PermissionState.Denied);
            OnPermissionResult?.Invoke(granted);
        }

        private IEnumerator RequestRuntimePermission(string permission, Action<bool> onComplete)
        {
            if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission))
            {
                onComplete?.Invoke(true);
                yield break;
            }

            UnityEngine.Android.Permission.RequestUserPermission(permission);
            float timeout = 8f;
            while (timeout > 0f)
            {
                if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission))
                {
                    onComplete?.Invoke(true);
                    yield break;
                }

                timeout -= 0.25f;
                yield return new WaitForSeconds(0.25f);
            }

            onComplete?.Invoke(UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission));
        }

        private IEnumerator WaitForSettingsResult()
        {
            float timeout = 30f;
            while (timeout > 0f)
            {
                if (HasStoragePermission())
                    yield break;

                timeout -= 1f;
                yield return new WaitForSeconds(1f);
            }
        }

        public bool HasStoragePermission()
        {
            int apiLevel = GetApiLevel();
            if (apiLevel >= 33)
            {
                return UnityEngine.Android.Permission.HasUserAuthorizedPermission("android.permission.READ_MEDIA_IMAGES")
                       || IsExternalStorageManager();
            }

            if (apiLevel >= 30)
            {
                return IsExternalStorageManager()
                       || UnityEngine.Android.Permission.HasUserAuthorizedPermission("android.permission.READ_EXTERNAL_STORAGE");
            }

            return UnityEngine.Android.Permission.HasUserAuthorizedPermission("android.permission.READ_EXTERNAL_STORAGE");
        }

        private bool IsExternalStorageManager()
        {
            using (var env = new AndroidJavaClass("android.os.Environment"))
            {
                return env.CallStatic<bool>("isExternalStorageManager");
            }
        }

        private void RequestManageExternalStorage()
        {
            try
            {
                using (var activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer").GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    string action = "android.settings.MANAGE_ALL_FILES_ACCESS_PERMISSION";
                    using (var intent = new AndroidJavaObject("android.content.Intent", action))
                    {
                        activity.Call("startActivity", intent);
                    }
                }
                Debug.Log("[AndroidPermissions] Opened MANAGE_ALL_FILES_ACCESS settings");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AndroidPermissions] Failed to request MANAGE_EXTERNAL_STORAGE: {ex.Message}");
            }
        }

        private int GetApiLevel()
        {
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                return version.GetStatic<int>("SDK_INT");
            }
        }
#endif

        private void SetState(PermissionState state)
        {
            CurrentState = state;
            Debug.Log($"[AndroidPermissions] State -> {state}");
        }
    }
}
