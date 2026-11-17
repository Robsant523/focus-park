using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace FocusPark.Platform
{
    [Serializable] public sealed class BlockingStatus
    {
        public bool available, authorized, configured, active, ready, appsReady, websitesReady;
        public int selectedApps, selectedWebsites, selectedCategories, appCount, domainCount;
        public string sessionId, error, message, coverage, pairingToken;
        public double endUnix;
        public int SelectionCount => Math.Max(selectedApps, appCount) + Math.Max(selectedWebsites, domainCount) + selectedCategories;
    }

    public sealed class BlockingBridge : MonoBehaviour
    {
        public string MacHost = "127.0.0.1";
        public string Token = "";
        public BlockingStatus Phone = new BlockingStatus();
        public BlockingStatus Mac = new BlockingStatus();
        public string NetworkError = "";
        public bool IsPhone {
            get {
#if UNITY_IOS && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void FP_RequestAuthorization();
        [DllImport("__Internal")] static extern void FP_ShowPicker();
        [DllImport("__Internal")] static extern void FP_StartSession(string id, double endUnix);
        [DllImport("__Internal")] static extern void FP_EndSession();
        [DllImport("__Internal")] static extern IntPtr FP_GetStatus();
        [DllImport("__Internal")] static extern void FP_FreeString(IntPtr value);
#endif
        public void Authorize() {
#if UNITY_IOS && !UNITY_EDITOR
            FP_RequestAuthorization();
#endif
        }
        public void Pick() {
#if UNITY_IOS && !UNITY_EDITOR
            FP_ShowPicker();
#endif
        }
        public void ReadPhone() {
#if UNITY_IOS && !UNITY_EDITOR
            var ptr = FP_GetStatus();
            try { Phone = JsonUtility.FromJson<BlockingStatus>(Marshal.PtrToStringAnsi(ptr)) ?? new BlockingStatus(); }
            finally { FP_FreeString(ptr); }
#else
            Phone = new BlockingStatus { error = "Phone protection is available in a signed iPhone build." };
#endif
        }
        public void StartPhone(string id, long endUnix) {
#if UNITY_IOS && !UNITY_EDITOR
            FP_StartSession(id, endUnix);
#endif
        }
        public void StopPhone() {
#if UNITY_IOS && !UNITY_EDITOR
            FP_EndSession();
#endif
        }
        public IEnumerator MacRequest(string action, string sessionId = "", long endUnix = 0)
        {
            // Only local hostnames / numeric LAN addresses; never send a pairing token to an arbitrary URL.
            if (string.IsNullOrWhiteSpace(MacHost) || MacHost.IndexOfAny(new[] {'/', ':', '@', '?', '#'}) >= 0)
            { NetworkError = "Enter only your Mac's local IP address or hostname."; Mac = new BlockingStatus(); yield break; }
            string url = "http://" + MacHost.Trim() + ":43821/" + action;
            using (var request = new UnityWebRequest(url, action == "status" ? "GET" : "POST"))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                if (action == "start" || action == "stop") request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(new StartBody { sessionId = sessionId, endUnix = endUnix })));
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", "Bearer " + Token.Trim());
                request.timeout = 4;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                { NetworkError = "Mac unavailable: " + request.error; Mac = new BlockingStatus(); yield break; }
                try { Mac = JsonUtility.FromJson<BlockingStatus>(request.downloadHandler.text) ?? new BlockingStatus(); NetworkError = ""; }
                catch (Exception) { NetworkError = "The Mac returned an invalid response."; Mac = new BlockingStatus(); }
            }
        }
        [Serializable] sealed class StartBody { public string sessionId; public long endUnix; }
        public void ConfigureMac()
        {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            // The helper is built beside the exported game, or opened separately from Native/macOS/Build.
            string helper = PlayerPrefs.GetString("helperPath", "");
            if (string.IsNullOrEmpty(helper) && Application.isEditor) helper = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../Native/macOS/Build/FocusParkHelper.app/Contents/MacOS/FocusParkHelper"));
            if (System.IO.File.Exists(helper))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(helper, "configure") { UseShellExecute = false });
            else NetworkError = "Open FocusParkHelper.app from Native/macOS/Build to select apps and get your pairing token.";
#else
            NetworkError = "Open FocusParkHelper on your Mac to select distractions and see its pairing token.";
#endif
        }
    }
}
