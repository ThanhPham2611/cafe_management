using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Cafe
{
    /// Scene Boot (index 0): nền kem, logo bật ra + vài sợi khói, thanh tải "ly cà phê đầy dần" và mẹo ngẫu nhiên.
    /// Nạp scene chính bằng LoadSceneAsync; canvas sống qua scene (DontDestroyOnLoad) và chỉ mờ đi khi
    /// GameBootstrap.IsBuilt (Build() xong, màn tiêu đề đã dựng) và đã hiện đủ BootRules.MinDisplaySeconds.
    /// Mở thẳng Prototype_Tier1 trong Editor (không qua Boot) thì không có màn tải; màn tiêu đề vẫn hiện.
    public class BootLoader : MonoBehaviour
    {
        public const string MainSceneName = "Prototype_Tier1";

        [Header("Editor gán (BootSceneBuilder)")]
        public UiTheme theme;
        [Tooltip("Logo ngang (Art/UI/Logo/logo_wide.png). Null thì dùng UiTheme.logoWide, rồi chữ lớn BootStrings.LogoFallback.")]
        public Sprite logo;

        private const float LogoPopSeconds = 0.4f;
        private const float FadeSeconds = 0.35f;
        private const float BarSpeed = 0.9f; // phần thanh / giây

        private CanvasGroup group;
        private RectTransform logoRect;
        private BarView bar;
        private Text tipText;
        private Text loadingText;
        private Wisp[] wisps;
        private float shownProgress;
        private float startTime;
        private bool dismissing;

        private sealed class Wisp
        {
            public RectTransform rt;
            public Image img;
            public float phase;
            public float period;
            public float x;
            public float sway;
        }

        private static Sprite softCircle;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            if (theme != null) HudBuilder.SetTheme(theme);
            Build();
        }

        private void Start()
        {
            startTime = Time.unscaledTime;
            StartCoroutine(Run());
        }

        // ------------------------------------------------------------------ dựng UI

        private void Build()
        {
            Canvas canvas = HudBuilder.CreateCanvas("BootCanvas", 5000, true, transform);
            group = canvas.gameObject.AddComponent<CanvasGroup>();

            Image bg = HudBuilder.Img(canvas.transform, "Background", null, HudBuilder.Cream, false, true);
            HudBuilder.Stretch(bg.rectTransform);
            bg.color = HudBuilder.Cream;

            Sprite logoSprite = logo != null ? logo : HudBuilder.T.logoWide;

            // Khói: sau logo
            wisps = new Wisp[3];
            for (int i = 0; i < wisps.Length; i++)
            {
                var w = new Wisp();
                Image img = HudBuilder.Img(canvas.transform, "Wisp" + i, SoftCircle(), Color.white, false, false);
                img.color = new Color(HudBuilder.CreamDark.r, HudBuilder.CreamDark.g, HudBuilder.CreamDark.b, 0f);
                float size = 150f + i * 40f;
                HudBuilder.SetFixed(img.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
                w.rt = img.rectTransform;
                w.img = img;
                w.x = (i - 1) * 110f;
                w.sway = 28f + i * 8f;
                w.period = 2.6f + i * 0.5f;
                w.phase = i / (float)wisps.Length;
                wisps[i] = w;
            }

            // Logo (hoặc chữ lớn)
            if (logoSprite != null)
            {
                Image li = HudBuilder.Img(canvas.transform, "Logo", logoSprite, Color.white, false, false);
                li.preserveAspect = true;
                logoRect = li.rectTransform;
                HudBuilder.SetFixed(logoRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1000f, 500f));
            }
            else
            {
                Text lt = HudBuilder.AddText(canvas.transform, "LogoText", BootStrings.LogoFallback, 140, TextAnchor.MiddleCenter, HudBuilder.Cream, true, true);
                Outline o = lt.GetComponent<Outline>();
                if (o != null) { o.effectDistance = new Vector2(6f, -6f); o.enabled = true; }
                logoRect = lt.rectTransform;
                HudBuilder.SetFixed(logoRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(1500f, 220f));
            }
            logoRect.localScale = Vector3.one * 0.8f;

            // Thanh tải: ly cà phê ở đầu trái, thanh clay đầy dần
            bar = HudBuilder.CreateBar(canvas.transform, "LoadingBar");
            HudBuilder.SetFixed(bar.root, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(40f, 250f), new Vector2(720f, 46f));
            bar.Set(0f, HudBuilder.Good);

            Image cup = HudBuilder.Icon(canvas.transform, "Cup", HudBuilder.T.iconCup, 96f);
            HudBuilder.SetFixed(cup.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-380f, 250f), new Vector2(96f, 96f));

            loadingText = HudBuilder.AddText(canvas.transform, "Loading", BootStrings.Loading, 34, TextAnchor.MiddleCenter, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(loadingText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 320f), new Vector2(900f, 50f));

            tipText = HudBuilder.AddText(canvas.transform, "Tip", BootStrings.TipPrefix + BootRules.PickTip(BootStrings.Tips, Random.Range(0, 1000)),
                28, TextAnchor.MiddleCenter, HudBuilder.InkSoft, false, false);
            HudBuilder.SetFixed(tipText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(1500f, 50f));
            HudBuilder.Fit(tipText, 20);
        }

        /// Hình tròn mờ dần ra rìa, tạo lúc chạy (không cần asset) cho sợi khói.
        private static Sprite SoftCircle()
        {
            if (softCircle != null) return softCircle;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    byte a = (byte)(Mathf.SmoothStep(0f, 1f, d) * 255f);
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            softCircle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            return softCircle;
        }

        // ------------------------------------------------------------------ chạy

        private IEnumerator Run()
        {
            AsyncOperation op = null;
            if (Application.CanStreamedLevelBeLoaded(MainSceneName)) op = SceneManager.LoadSceneAsync(MainSceneName);
            else Debug.LogError("[Boot] Không có scene " + MainSceneName + " trong Build Settings.");

            while (true)
            {
                float elapsed = Time.unscaledTime - startTime;
                bool sceneLoaded = op == null || op.isDone;
                bool built = op == null || (sceneLoaded && GameBootstrap.IsBuilt);
                float target = BootRules.DisplayProgress(op != null ? op.progress : 1f, sceneLoaded, built);
                shownProgress = BootRules.Smooth(shownProgress, target, Time.unscaledDeltaTime, BarSpeed);
                bar.Set(shownProgress, HudBuilder.Good);

                Animate(elapsed);
                bool timedOut = elapsed >= BootRules.MaxWaitSeconds;
                if (BootRules.CanDismiss(elapsed, built) && (shownProgress >= 0.999f || timedOut)) break;
                yield return null;
            }

            dismissing = true;
            group.blocksRaycasts = false;
            float t = 0f;
            while (t < FadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = 1f - Mathf.Clamp01(t / FadeSeconds);
                yield return null;
            }
            Destroy(gameObject);
        }

        private void Animate(float elapsed)
        {
            if (logoRect != null)
                logoRect.localScale = Vector3.one * BootRules.LogoPopScale(elapsed / LogoPopSeconds);

            float logoIn = Mathf.Clamp01((elapsed - 0.25f) / 0.4f);
            for (int i = 0; i < wisps.Length; i++)
            {
                Wisp w = wisps[i];
                float u = Mathf.Repeat(elapsed / w.period + w.phase, 1f);
                float y = 40f + u * 260f;
                float x = w.x + Mathf.Sin(u * 6.28f + i) * w.sway;
                w.rt.anchoredPosition = new Vector2(x, y + 120f);
                w.rt.localScale = Vector3.one * (0.7f + u * 0.8f);
                Color c = w.img.color;
                c.a = Mathf.Sin(u * Mathf.PI) * 0.45f * logoIn;
                w.img.color = c;
            }

            if (!dismissing && loadingText != null)
            {
                int dots = 1 + (int)(elapsed * 2f) % 3;
                loadingText.text = BootStrings.Loading.TrimEnd('.') + new string('.', dots);
            }
        }
    }
}
