using System;
using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// Màn tiêu đề: hiện sau khi scene chính dựng xong, phủ lên quán 3D đang sống (camera tự xoay chậm).
    /// Logo ở trên, nút đỏ lớn "MỞ QUÁN", nút nhỏ Cài đặt (mở lại SettingsPanel). Trong lúc hiện, ngày CHƯA bắt đầu
    /// (DayCycle ở Idle nên CustomerSpawner không thả khách) và HUD bị ẩn. Bấm "MỞ QUÁN" -> đóng màn và gọi onStart
    /// (GameBootstrap.AfterTitle: mở màn tạo chủ quán hoặc StartDay).
    public class TitleScreen : MonoBehaviour
    {
        private const float OrbitDegPerSec = 5f;

        private GameBootstrap game;
        private Action onStart;
        private GameObject root;
        private RectTransform cta;
        private SettingsPanel settings;
        private GameObject settingsButtonHost;
        private bool open;
        private float openedAt;

        public bool IsOpen { get { return open; } }

        /// Chạy tự động (batchmode / bot / QA) thì không hiện màn tiêu đề.
        public static bool SkipForAutomation
        {
            get { return BootRules.ShouldSkipTitle(Environment.GetCommandLineArgs(), Application.isBatchMode); }
        }

        public static TitleScreen Create(GameBootstrap g, Action onStart)
        {
            var go = new GameObject("TitleScreen");
            var t = go.AddComponent<TitleScreen>();
            t.Build(g, onStart);
            return t;
        }

        private void Build(GameBootstrap g, Action startCallback)
        {
            game = g;
            onStart = startCallback;

            Canvas canvas = HudBuilder.CreateCanvas("TitleCanvas", 12, true, transform);
            root = canvas.gameObject;
            RectTransform safe = HudBuilder.NewRect("SafeArea", canvas.transform);
            HudBuilder.Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();

            // Nền mờ dần (tối ở mép) để logo nổi trên quán 3D mà vẫn thấy quán bên dưới
            AddBackdrop(canvas.transform, "TopShade", true, 760f, new Color(0.17f, 0.09f, 0.06f, 0.86f));
            AddBackdrop(canvas.transform, "BottomShade", false, 420f, new Color(0.17f, 0.09f, 0.06f, 0.55f));

            // Logo (hoặc chữ lớn) ở trên, giữa, vừa phải để đọc như tiêu đề
            Sprite logo = HudBuilder.T.logoWide;
            if (logo != null)
            {
                Image li = HudBuilder.Img(safe, "Logo", logo, Color.white, false, false);
                li.preserveAspect = true;
                HudBuilder.SetFixed(li.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(720f, 360f));
            }
            else
            {
                Text lt = HudBuilder.AddText(safe, "LogoText", BootStrings.LogoFallback, 130, TextAnchor.MiddleCenter, HudBuilder.Cream, true, true);
                Outline o = lt.GetComponent<Outline>();
                if (o != null) { o.effectDistance = new Vector2(6f, -6f); o.enabled = true; }
                HudBuilder.SetFixed(lt.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1500f, 200f));
            }

            // Nút lớn "MỞ QUÁN"
            // (host riêng để nhịp thở scale không đụng animation nhấn của ClayButton)
            cta = HudBuilder.NewRect("StartHost", safe);
            HudBuilder.SetFixed(cta, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 90f), new Vector2(680f, 170f));
            Text label;
            ClayButton start = HudBuilder.AddLabelButton(cta, "Start", ClayStyle.Red, BootStrings.TitleStart, 72, OnStartPressed, out label);
            HudBuilder.Stretch((RectTransform)start.transform);

            // Nút nhỏ Cài đặt ở góc phải trên. SettingsPanel tự dựng nút "ĐỒ HỌA" của nó trong một vật chứa ẩn
            // (nút thật của HUD đang bị ẩn); bảng chọn nằm trong canvas của màn này nên hiện được trên màn tiêu đề.
            settingsButtonHost = new GameObject("HiddenSettingsHost", typeof(RectTransform));
            settingsButtonHost.transform.SetParent(transform, false);
            settingsButtonHost.SetActive(false);
            settings = new SettingsPanel(game, settingsButtonHost.transform, canvas.transform);
            Text sl;
            ClayButton sb = HudBuilder.AddLabelButton(safe, "Settings", ClayStyle.Coffee, BootStrings.TitleSettings, 28, OnSettingsPressed, out sl);
            HudBuilder.SetFixed((RectTransform)sb.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -24f), new Vector2(230f, 76f));

            root.SetActive(false);
        }

        private static Sprite gradientSprite;

        /// Dải gradient dọc (đặc ở mép màn hình, trong suốt phía trong), không chặn chạm.
        private static void AddBackdrop(Transform parent, string name, bool top, float height, Color edge)
        {
            Image img = HudBuilder.Img(parent, name, GradientSprite(), edge, false, false);
            img.color = edge;
            img.raycastTarget = false;
            RectTransform rt = img.rectTransform;
            rt.SetAsFirstSibling(); // nằm dưới logo / nút
            rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
            rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, height);
            // Sprite đặc ở đáy ảnh, trong suốt ở đỉnh: dải trên lật dọc để đặc ở mép trên
            rt.localScale = new Vector3(1f, top ? -1f : 1f, 1f);
        }

        /// Gradient alpha 1 (hàng dưới) -> 0 (hàng trên), mượt (smoothstep).
        private static Sprite GradientSprite()
        {
            if (gradientSprite != null) return gradientSprite;
            const int h = 64;
            var tex = new Texture2D(4, h, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < h; y++)
            {
                float t = y / (h - 1f);          // 0 = đáy ảnh
                float a = 1f - t;
                a = a * a * (3f - 2f * a);
                for (int x = 0; x < 4; x++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            gradientSprite = Sprite.Create(tex, new Rect(0, 0, 4, h), new Vector2(0.5f, 0.5f), 100f);
            gradientSprite.name = "TitleGradient";
            return gradientSprite;
        }

        /// Hiện màn tiêu đề: ẩn HUD, camera tự xoay.
        public void Show()
        {
            open = true;
            openedAt = Time.unscaledTime;
            root.SetActive(true);
            if (game.Hud != null) game.Hud.SetHudActive(false);
            if (game.CameraRig != null) game.CameraRig.AutoOrbitDegPerSec = OrbitDegPerSec;
        }

        private void OnSettingsPressed()
        {
            // Bấm nút "ĐỒ HỌA" ẩn của SettingsPanel -> mở bảng (SettingsPanel không có API mở công khai ngoài Editor)
            Transform t = settingsButtonHost.transform.Find("GraphicsButton");
            UnityEngine.UI.Button b = t != null ? t.GetComponent<UnityEngine.UI.Button>() : null;
            if (b != null) b.onClick.Invoke();
        }

        private void OnStartPressed()
        {
            if (!open) return;
            Dismiss();
        }

        /// Đóng màn tiêu đề rồi tiếp tục luồng vào game (cũng dùng cho bot / QA nếu cần).
        public void Dismiss()
        {
            if (!open) return;
            open = false;
            if (game.CameraRig != null) game.CameraRig.AutoOrbitDegPerSec = 0f;
            if (game.Hud != null) game.Hud.SetHudActive(true);
            root.SetActive(false);
            Action cb = onStart;
            onStart = null;
            if (cb != null) cb();
            Destroy(gameObject);
        }

        private void Update()
        {
            if (!open || cta == null) return;
            // Nút nhịp thở nhẹ để mời bấm
            float s = 1f + 0.03f * Mathf.Sin((Time.unscaledTime - openedAt) * 3.2f);
            cta.localScale = new Vector3(s, s, 1f);
        }

        private void OnDestroy()
        {
            if (settings != null) settings.Dispose();
        }
    }
}
