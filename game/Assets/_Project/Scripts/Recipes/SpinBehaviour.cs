using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cafe
{
    /// Bước kiểu XOAY VÒNG (đánh/lắc): kéo ngón tay theo vòng tròn >= 1 vòng. Đo bằng tổng góc rẽ của đường kéo
    /// (không phụ thuộc tâm/kích thước vòng) nên xoay ở đâu quanh nút cũng được; rẽ gấp > 100 độ (zích zắc) thì tính lại.
    /// Kéo ngang ngay từ đầu (khi nút chưa chơi được) = cuộn khay.
    public class SpinBehaviour : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
                                 IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const float SampleDistPx = 8f;
        private const float MaxCornerDeg = 100f;

        private ClayButton clay;
        private BrewStep step;
        private Action<BrewStep> onSuccess;
        private Image fill;
        private ScrollRect scroll;

        private bool pressed;
        private bool dragging;
        private bool forward;
        private bool done;
        private bool haveDir;
        private Vector2 lastSample;
        private Vector2 lastDir;
        private float sum;
        private float sampleDist = SampleDistPx;

        public static SpinBehaviour Attach(ClayButton clay, BrewStep step, Action<BrewStep> onSuccess)
        {
            var b = clay.gameObject.AddComponent<SpinBehaviour>();
            b.clay = clay;
            b.step = step;
            b.onSuccess = onSuccess;
            b.fill = StepIconFallback.CreateFillOverlay(clay.Content, Image.FillMethod.Radial360, 88f);
            return b;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pressed = true;
            dragging = false;
            forward = false;
            done = false;
            if (scroll == null) scroll = GetComponentInParent<ScrollRect>();
            sampleDist = SampleDistPx * StepGestureUtil.CanvasScale(this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragging = true;
            forward = scroll != null && (clay == null || !clay.Interactable);
            if (forward)
            {
                scroll.OnBeginDrag(eventData);
                return;
            }
            sum = 0f;
            haveDir = false;
            lastSample = eventData.pressPosition;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (forward)
            {
                scroll.OnDrag(eventData);
                return;
            }
            if (clay == null || !clay.Interactable || done) return;

            Vector2 p = eventData.position;
            Vector2 d = p - lastSample;
            if (d.sqrMagnitude < sampleDist * sampleDist) return;
            Vector2 dir = d.normalized;
            if (haveDir)
            {
                float turn = Vector2.SignedAngle(lastDir, dir);
                if (Mathf.Abs(turn) > MaxCornerDeg) sum = 0f; // rẽ gấp: không phải đường cong
                else sum += turn;
            }
            lastDir = dir;
            haveDir = true;
            lastSample = p;

            float prog = Mathf.Abs(sum) / StepActions.SpinRequiredDegrees;
            StepGestureUtil.SetFill(fill, prog, prog >= 1f ? HudBuilder.Good : HudBuilder.Accent);
            if (prog >= 1f)
            {
                done = true;
                if (onSuccess != null) onSuccess(step);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (forward && scroll != null)
            {
                scroll.OnEndDrag(eventData);
                forward = false;
                return;
            }
            if (!done && clay != null && clay.Interactable && Mathf.Abs(sum) > 60f)
                StepGestureUtil.Hint(RecipeStrings.SpinTooShort);
            sum = 0f;
            haveDir = false;
            StepGestureUtil.SetFill(fill, 0f, HudBuilder.Accent);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (pressed && !dragging && clay != null && clay.Interactable)
                StepGestureUtil.Hint(RecipeStrings.ActionHint(step));
            pressed = false;
            dragging = false;
            forward = false;
            StepGestureUtil.SetFill(fill, 0f, HudBuilder.Accent);
        }

        private void OnDisable()
        {
            pressed = false;
            dragging = false;
            forward = false;
            done = false;
            haveDir = false;
            sum = 0f;
            if (fill != null) fill.enabled = false;
        }
    }
}
