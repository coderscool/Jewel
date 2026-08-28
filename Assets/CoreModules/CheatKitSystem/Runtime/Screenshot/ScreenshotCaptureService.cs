using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoreModules.CheatKit.Screenshot
{
    /// <summary>
    /// VI: Generic screenshot service — chụp 1 vùng world-space (Bounds) qua dedicated
    /// transient camera, render ARGB32 transparent, lưu PNG vào persistentDataPath.
    ///
    /// ASPECT POLICY:
    ///  • Fit-by-HEIGHT: caller pin height (default 256), width auto-scale theo aspect
    ///    của <c>worldBounds</c> (đã cộng padding 2 phía). Grid 6×6 → 256×256, grid 8×4
    ///    → 512×256, grid 4×6 → ~171×256 — luôn fit chiều cao, không méo, không crop.
    ///
    /// REUSABILITY:
    ///  • Không phụ thuộc BoardManager / BeadsLoop. Caller pass: srcCamera, layerMask,
    ///    worldBounds, fileName.
    ///  • Drop-in module: kéo prefab vào scene + gọi Capture(...) — xong.
    ///
    /// MOBILE OPT:
    ///  • Cached transient Camera GO (1 GameObject suốt lifetime).
    ///  • Cached RenderTexture + Texture2D (reuse khi width/height không đổi).
    ///  • AsyncGPUReadback → 0 main-thread stall khi đọc pixels.
    ///  • Fallback Texture2D.ReadPixels khi platform không support AsyncGPUReadback.
    ///  • EncodeToPNG dùng ImageConversion (faster than legacy).
    ///  • File.WriteAllBytes sync (W×256 PNG ~5–40KB, &lt; 5ms).
    /// </summary>
    public sealed class ScreenshotCaptureService : MonoBehaviour
    {
        [Header("Output")]
        [Tooltip("Subfolder dưới Application.persistentDataPath.\n" +
                 "Final path: persistentDataPath/{subfolder}/{filename}.")]
        [SerializeField] private string _subfolder = "Screenshots";

        [Header("Defaults (caller có thể override)")]
        [Tooltip("Target HEIGHT (pixels) của output PNG. Width auto-scale theo aspect của worldBounds → grid lớn = ảnh rộng hơn, grid vuông = ảnh vuông. Fit theo height (256 = sweet spot editor: nhẹ, sắc nét).")]
        [SerializeField, Range(64, 2048)] private int _defaultResolution = 256;

        [Tooltip("Padding world-units thêm ngoài bounds — tránh outline bị cắt rìa.\n" +
                 "Giữ NHỎ (0.05–0.15) để border/khung tranh KHÔNG lọt vào view.")]
        [SerializeField, Range(0f, 2f)] private float _defaultPaddingWorld = 0.1f;

        // Cached resources — alloc 1 lần, reuse mãi.
        private Camera _captureCam;
        private GameObject _captureCamGO;
        private RenderTexture _rt;
        private Texture2D _readbackTex;
        private int _rtWidth  = -1;
        private int _rtHeight = -1;

        public string LastSavedPath { get; private set; }

        public string SubfolderAbsolute
            => Path.Combine(Application.persistentDataPath, _subfolder);

        /// <summary>
        /// VI: Chụp vùng worldBounds qua camera cấu hình theo srcCameraTemplate.
        /// File ghi async (callback IO-bound nhẹ). Trả path tuyệt đối qua callback +
        /// <see cref="LastSavedPath"/>. <paramref name="onSaved"/> nhận null nếu lỗi.
        /// </summary>
        public void Capture(
            Camera        srcCameraTemplate,
            int           layerMask,
            Bounds        worldBounds,
            string        fileNameNoExt,
            int           targetHeight    = -1,
            float         paddingWorld    = -1f,
            Action<string> onSaved        = null,
            Renderer[]    hideDuringRender = null)
        {
            if (srcCameraTemplate == null)
            {
                Debug.LogError("[ScreenshotCaptureService] srcCameraTemplate null — abort.");
                onSaved?.Invoke(null);
                return;
            }

            int height = targetHeight   > 0  ? targetHeight  : _defaultResolution;
            float pad  = paddingWorld   >= 0 ? paddingWorld  : _defaultPaddingWorld;

            // Width auto-scale theo aspect của worldBounds (đã cộng padding 2 phía).
            // Fit-by-HEIGHT: height pinned, width = round(height * aspect).
            float worldH = Mathf.Max(worldBounds.size.y + 2f * pad, 1e-4f);
            float worldW = Mathf.Max(worldBounds.size.x + 2f * pad, 1e-4f);
            float aspect = worldW / worldH;
            int width    = Mathf.Max(1, Mathf.RoundToInt(height * aspect));

            EnsureCaptureCam();
            EnsureRT(width, height);
            ConfigureCaptureCam(srcCameraTemplate, layerMask, worldBounds, pad, aspect);

            // VI: Tạm ẩn renderers (border, background sprites…) chỉ trong scope render
            //     đồng bộ. State restore ngay sau Render() → user không nhìn thấy nhấp nháy.
            int hideCount = hideDuringRender != null ? hideDuringRender.Length : 0;
            // Cache enabled-state để restore chính xác (đề phòng caller pass renderer đã disabled).
            bool[] prevEnabled = hideCount > 0 ? new bool[hideCount] : null;
            for (int i = 0; i < hideCount; i++)
            {
                var r = hideDuringRender[i];
                if (r == null) continue;
                prevEnabled[i] = r.enabled;
                r.enabled = false;
            }

            // Render đồng bộ — pipeline tự queue GPU command, không stall main.
            _captureCam.targetTexture = _rt;
            _captureCam.Render();
            _captureCam.targetTexture = null;

            // Restore renderers ngay (trước AsyncGPUReadback — readback đã có GPU snapshot).
            for (int i = 0; i < hideCount; i++)
            {
                var r = hideDuringRender[i];
                if (r != null) r.enabled = prevEnabled[i];
            }

            // Mobile: AsyncGPUReadback → CPU buffer, không block main.
            if (SystemInfo.supportsAsyncGPUReadback)
            {
                AsyncGPUReadback.Request(_rt, 0, TextureFormat.RGBA32, request =>
                {
                    if (request.hasError)
                    {
                        Debug.LogError("[ScreenshotCaptureService] AsyncGPUReadback failed.");
                        onSaved?.Invoke(null);
                        return;
                    }
                    EnsureReadbackTex(width, height);
                    var data = request.GetData<byte>();
                    _readbackTex.LoadRawTextureData(data);
                    _readbackTex.Apply(updateMipmaps: false);
                    SaveToFile(_readbackTex, fileNameNoExt, onSaved);
                });
            }
            else
            {
                // Fallback older devices (rare on mobile 2020+).
                StartCoroutine(ReadbackSyncCo(width, height, fileNameNoExt, onSaved));
            }
        }

        private IEnumerator ReadbackSyncCo(int width, int height, string fileNameNoExt, Action<string> onSaved)
        {
            yield return new WaitForEndOfFrame();
            EnsureReadbackTex(width, height);
            var prev = RenderTexture.active;
            RenderTexture.active = _rt;
            _readbackTex.ReadPixels(new Rect(0, 0, width, height), 0, 0, recalculateMipMaps: false);
            _readbackTex.Apply(updateMipmaps: false);
            RenderTexture.active = prev;
            SaveToFile(_readbackTex, fileNameNoExt, onSaved);
        }

        private void SaveToFile(Texture2D tex, string fileNameNoExt, Action<string> onSaved)
        {
            try
            {
                byte[] png = ImageConversion.EncodeToPNG(tex);
                string folder = Path.Combine(Application.persistentDataPath, _subfolder);
                if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, fileNameNoExt + ".png");
                File.WriteAllBytes(path, png);
                LastSavedPath = path;
                Debug.Log($"[ScreenshotCaptureService] ✅ Saved → {path} ({png.Length / 1024} KB)");
                onSaved?.Invoke(path);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ScreenshotCaptureService] Save failed: {ex.Message}");
                onSaved?.Invoke(null);
            }
        }

        private void EnsureCaptureCam()
        {
            if (_captureCam != null) return;
            _captureCamGO = new GameObject("[ScreenshotCaptureCam]");
            _captureCamGO.transform.SetParent(transform, worldPositionStays: false);
            _captureCam = _captureCamGO.AddComponent<Camera>();
            _captureCam.enabled         = false;          // render on-demand only
            _captureCam.clearFlags      = CameraClearFlags.SolidColor;
            _captureCam.backgroundColor = new Color(0, 0, 0, 0);  // TRANSPARENT
            _captureCam.orthographic    = true;
            _captureCam.allowHDR        = false;
            _captureCam.allowMSAA       = false;
            // URP: capture cam phải là BASE type (default mới create) — KHÔNG add vào stack.
        }

        private void ConfigureCaptureCam(Camera _src, int layerMask, Bounds worldBounds, float padding, float aspect)
        {
            _captureCam.cullingMask = layerMask;
            _captureCam.transform.position = new Vector3(worldBounds.center.x, worldBounds.center.y, -10f);
            _captureCam.transform.rotation = Quaternion.identity;

            // Fit-by-HEIGHT: ortho size = vertical half-extent + padding. Aspect đẩy horizontal coverage.
            float halfH = worldBounds.extents.y + padding;
            _captureCam.orthographicSize = Mathf.Max(halfH, 0.01f);
            _captureCam.aspect = Mathf.Max(aspect, 1e-4f);

            _captureCam.nearClipPlane = 0.01f;
            _captureCam.farClipPlane  = 100f;
        }

        private void EnsureRT(int width, int height)
        {
            if (_rt != null && _rtWidth == width && _rtHeight == height) return;
            if (_rt != null) { _rt.Release(); UnityEngine.Object.Destroy(_rt); }

            _rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name             = "[ScreenshotRT]",
                useMipMap        = false,
                autoGenerateMips = false,
                antiAliasing     = 1,
                wrapMode         = TextureWrapMode.Clamp,
                filterMode       = FilterMode.Bilinear,
            };
            _rt.Create();
            _rtWidth  = width;
            _rtHeight = height;
        }

        private void EnsureReadbackTex(int width, int height)
        {
            if (_readbackTex != null && _readbackTex.width == width && _readbackTex.height == height) return;
            if (_readbackTex != null) UnityEngine.Object.Destroy(_readbackTex);
            _readbackTex = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false, linear: false);
        }

        private void OnDestroy()
        {
            if (_rt != null) { _rt.Release(); UnityEngine.Object.Destroy(_rt); _rt = null; }
            if (_readbackTex != null) { UnityEngine.Object.Destroy(_readbackTex); _readbackTex = null; }
            // _captureCamGO destroy theo parent.
        }

#if UNITY_EDITOR
        /// <summary>VI: Editor-only — reveal folder trong Finder/Explorer.</summary>
        public void OpenFolderInEditor()
        {
            string folder = SubfolderAbsolute;
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            UnityEditor.EditorUtility.RevealInFinder(folder);
        }
#endif
    }
}
