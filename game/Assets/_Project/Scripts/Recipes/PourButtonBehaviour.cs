using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cafe
{
    /// Bước kiểu RÓT (thay HoldButtonBehaviour, docs/hud_brew_redesign_2026-10-18.md B1): chạm xuống là bắt đầu rót, không cần giữ;
    /// ly tự đầy sau `seconds` = "Đủ". Chạm lại (nút này, ly-thước, hoặc nút nguyên liệu kế - BrewPanel gọi StopPour) để dừng:
    /// trong vạch vàng = "Chuẩn!", sớm = "Hơi ít", quá sớm (< 15%) = bỏ qua, rót tiếp. Rót không bao giờ tính sai bước.
    /// Gắn lên ClayButton (nút không gắn onClick). Kéo cuộn khay ngay khi vừa chạm (< 15%) = hủy lặng lẽ.
    public class PourButtonBehaviour : MonoBehaviour, IPointerDownHandler
    {
        private ClayButton clay;
        private BrewStep step;
        private float seconds = 1f;
        private Func<PourButtonBehaviour, bool> onBegin;          // BrewPanel quyết định có được rót không (bước đúng, có ly...)
        private Action<PourButtonBehaviour, PourGrade> onFinish;  // rót xong (tự đầy hoặc chạm dừng) -> BrewStation.TapStep(step, grade)
        private Image fill;
        private Image band;

        private bool pouring;
        private float elapsed;
        private PointerEventData ptr;
        private Vector2 downPos;
        private float cancelDistPx;

        public BrewStep Step { get { return step; } }
        public bool IsPouring { get { return pouring; } }
        public float Seconds { get { return seconds; } }
        /// Mức đầy hiện tại 0..1 (0 khi không rót).
        public float Fill01 { get { return pouring ? PourRules.Fill(elapsed, seconds) : 0f; } }
        /// Đã qua mốc chạm nhầm: chạm dừng lúc này sẽ được tính.
        public bool PastIgnore { get { return pouring && elapsed >= PourRules.IgnoreSeconds(seconds); } }

        public static PourButtonBehaviour Attach(ClayButton clay, BrewStep step, float seconds,
                                                 Func<PourButtonBehaviour, bool> onBegin, Action<PourButtonBehaviour, PourGrade> onFinish)
        {
            var b = clay.gameObject.AddComponent<PourButtonBehaviour>();
            b.clay = clay;
            b.step = step;
            b.seconds = Mathf.Max(0.2f, seconds);
            b.onBegin = onBegin;
            b.onFinish = onFinish;
            b.cancelDistPx = Mathf.Max(24f, TapInput.DragThresholdPx * 2f);

            // Cung vàng đánh dấu vùng "Chuẩn!" (quạt tròn, xoay tới mép trái vạch), nằm dưới vòng rót
            b.band = StepIconFallback.CreateFillOverlay(clay.Content, Image.FillMethod.Radial360, 88f);
            b.band.name = "PourBand";
            b.band.fillAmount = StepActions.PerfectBandEnd - StepActions.PerfectBandStart;
            b.band.rectTransform.localEulerAngles = new Vector3(0f, 0f, -360f * StepActions.PerfectBandStart);
            b.fill = StepIconFallback.CreateFillOverlay(clay.Content, Image.FillMethod.Radial360, 88f);
            return b;
        }

        /// Đặt lại tâm / cỡ vòng rót cho khớp icon của nút (BrewPanel dựng nút cỡ khác nhau).
        public void PlaceOverlay(Vector2 center, float size)
        {
            foreach (Image img in new[] { band, fill })
            {
                if (img == null) continue;
                img.rectTransform.anchoredPosition = center;
                img.rectTransform.sizeDelta = new Vector2(size, size);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (clay == null || !clay.Interactable) return;
            if (pouring)
            {
                StopPour(); // chạm lại nút đang rót = dừng (quá sớm thì bỏ qua)
                return;
            }
            if (onBegin != null && !onBegin(this)) return;
            pouring = true;
            elapsed = 0f;
            ptr = eventData;
            downPos = eventData.position;
            if (band != null)
            {
                band.enabled = true;
                band.color = new Color(HudBuilder.Accent.r, HudBuilder.Accent.g, HudBuilder.Accent.b, 0.55f);
            }
            SetFill(0f);
        }

        /// Chạm dừng rót: chấm điểm theo thời điểm. Trả false nếu không đang rót hoặc chạm quá sớm (bỏ qua, rót tiếp).
        public bool StopPour()
        {
            if (!pouring) return false;
            PourGrade g;
            if (!PourRules.TryGradeStop(elapsed, seconds, out g)) return false;
            Complete(g);
            return true;
        }

        /// Hủy lặng lẽ (bàn pha đổi trạng thái, cuộn khay...): không tính bước, không phạt.
        public void CancelPour()
        {
            if (!pouring) return;
            ResetPour();
        }

        private void Update()
        {
            if (!pouring) return;
            if (clay == null || !clay.Interactable)
            {
                ResetPour();
                return;
            }
            // Vừa chạm đã kéo xa = đang cuộn khay, không phải rót
            if (ptr != null)
            {
                if (elapsed >= PourRules.IgnoreSeconds(seconds)) ptr = null;
                else if ((ptr.position - downPos).sqrMagnitude > cancelDistPx * cancelDistPx)
                {
                    ResetPour();
                    return;
                }
            }
            elapsed += Time.unscaledDeltaTime;
            if (PourRules.IsFull(elapsed, seconds))
            {
                Complete(PourGrade.Good); // tự đầy = "Đủ"
                return;
            }
            SetFill(PourRules.Fill(elapsed, seconds));
        }

        private void Complete(PourGrade g)
        {
            ResetPour();
            if (onFinish != null) onFinish(this, g);
        }

        private void SetFill(float p)
        {
            float t = p * seconds;
            bool inBand = t >= PourRules.BandStartSeconds(seconds) && t <= PourRules.BandEndSeconds(seconds) + StepActions.PerfectGraceSeconds;
            StepGestureUtil.SetFill(fill, Mathf.Max(0.002f, p), inBand ? HudBuilder.Good : HudBuilder.Cream);
        }

        private void ResetPour()
        {
            pouring = false;
            elapsed = 0f;
            ptr = null;
            StepGestureUtil.SetFill(fill, 0f, HudBuilder.Cream);
            if (band != null) band.enabled = false;
        }

        private void OnDisable()
        {
            pouring = false;
            elapsed = 0f;
            ptr = null;
            if (fill != null) fill.enabled = false;
            if (band != null) band.enabled = false;
        }
    }
}
