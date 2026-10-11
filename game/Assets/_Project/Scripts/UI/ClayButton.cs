using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cafe
{
    /// Cảm giác nhấn "đất sét": đổi sprite sang bản nhấn, scale 0.96, nội dung lún 6 px,
    /// thả ra thì nảy về trong ~160 ms (lò xo, dùng unscaledDeltaTime).
    public class ClayButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private const float PressScale = 0.96f;
        private const float PressOffset = 6f;
        private const float Stiffness = 1400f;
        private const float DampPress = 80f;
        private const float DampRelease = 36f;
        private const float MaxStep = 0.0175f; // ~1 khung ở 60 FPS: giữ nguyên cảm giác cũ, chỉ chia nhỏ khi khung dài hơn

        private Image face;
        private Sprite normal;
        private Sprite pressed;
        private RectTransform content;
        private CanvasGroup contentGroup;
        private Button button;
        private Color baseColor = Color.white;
        private Vector2 contentBase;

        private bool isDown;
        private float p;       // 0 = thả, 1 = nhấn hết cỡ
        private float v;       // vận tốc lò xo
        private bool animating;

        public Button Button { get { return button; } }
        public RectTransform Content { get { return content; } }
        public Image Face { get { return face; } }

        public bool Interactable
        {
            get { return button == null || button.interactable; }
        }

        public void Init(Image faceImage, Sprite normalSprite, Sprite pressedSprite, RectTransform contentRect, Button btn, Color tint)
        {
            face = faceImage;
            normal = normalSprite;
            pressed = pressedSprite;
            content = contentRect;
            button = btn;
            baseColor = tint;
            contentBase = content != null ? content.anchoredPosition : Vector2.zero;
            if (content != null)
            {
                contentGroup = content.GetComponent<CanvasGroup>();
                if (contentGroup == null) contentGroup = content.gameObject.AddComponent<CanvasGroup>();
                contentGroup.blocksRaycasts = false;
                contentGroup.interactable = false;
            }
            ApplyVisual();
            ApplyColor();
        }

        /// Đổi màu nền (vd. thẻ order đang chọn).
        public void SetTint(Color c)
        {
            baseColor = c;
            ApplyColor();
        }

        public void SetInteractable(bool value)
        {
            if (button == null || button.interactable == value) return;
            button.interactable = value;
            if (!value) Release();
            ApplyColor();
        }

        private void ApplyColor()
        {
            if (face == null) return;
            face.color = Interactable ? baseColor : new Color(baseColor.r * 0.62f, baseColor.g * 0.62f, baseColor.b * 0.62f, baseColor.a);
            if (contentGroup != null) contentGroup.alpha = Interactable ? 1f : 0.6f;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!Interactable) return;
            isDown = true;
            animating = true;
            if (pressed != null && face != null) face.sprite = pressed;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Release();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Release();
        }

        private void Release()
        {
            if (!isDown) return;
            isDown = false;
            animating = true;
            if (normal != null && face != null) face.sprite = normal;
        }

        private void OnDisable()
        {
            isDown = false;
            p = 0f;
            v = 0f;
            animating = false;
            ApplyVisual();
        }

        private void Update()
        {
            if (!animating) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            float target = isDown ? 1f : 0f;
            float damp = isDown ? DampPress : DampRelease;
            // Euler nửa ẩn chỉ ổn định khi damp*dt < 2 (DampPress 80 => dt < 25 ms, tức > 40 FPS).
            // Máy thật hay rớt 30 FPS: bước dài làm lò xo nổ tung (nút phóng to, nháy, che hết màn hình),
            // nên chia nhỏ mỗi khung thành các bước <= MaxStep.
            int steps = Mathf.Max(1, Mathf.CeilToInt(dt / MaxStep));
            float h = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                v += (-Stiffness * (p - target) - damp * v) * h;
                p += v * h;
            }
            if (float.IsNaN(p) || float.IsNaN(v))
            {
                p = 0f;
                v = 0f;
            }
            if (!isDown && Mathf.Abs(p) < 0.002f && Mathf.Abs(v) < 0.02f)
            {
                p = 0f;
                v = 0f;
                animating = false;
            }
            ApplyVisual();
        }

        private void ApplyVisual()
        {
            float s = 1f - (1f - PressScale) * p;
            transform.localScale = new Vector3(s, s, 1f);
            if (content != null) content.anchoredPosition = contentBase + new Vector2(0f, -PressOffset * p);
        }
    }
}
