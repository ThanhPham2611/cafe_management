using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cafe
{
    /// Bước kiểu VUỐT (đổ): kéo lên >= 120 px trên nút thì thành công (gọi ngay khi đủ). Kéo ngang = chuyển cho
    /// ScrollRect của khay nguyên liệu (cuộn khay). Vuốt thiếu / chạm đơn thuần chỉ nhắc, không phạt.
    public class SwipeBehaviour : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
                                  IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private ClayButton clay;
        private BrewStep step;
        private Action<BrewStep> onSuccess;
        private Image fill;
        private ScrollRect scroll;

        private bool pressed;
        private bool dragging;
        private bool forward;
        private bool done;
        private float needPx = StepActions.SwipeDistancePx;

        public static SwipeBehaviour Attach(ClayButton clay, BrewStep step, Action<BrewStep> onSuccess)
        {
            var b = clay.gameObject.AddComponent<SwipeBehaviour>();
            b.clay = clay;
            b.step = step;
            b.onSuccess = onSuccess;
            b.fill = StepIconFallback.CreateFillOverlay(clay.Content, Image.FillMethod.Vertical, 88f);
            return b;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pressed = true;
            dragging = false;
            forward = false;
            done = false;
            if (scroll == null) scroll = GetComponentInParent<ScrollRect>();
            needPx = StepActions.SwipeDistancePx * StepGestureUtil.CanvasScale(this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragging = true;
            float dx = eventData.position.x - eventData.pressPosition.x;
            float dy = eventData.position.y - eventData.pressPosition.y;
            // Không chơi được (đang đi xa / chưa chọn đơn) hoặc kéo ngang: nhường cho khay cuộn
            forward = scroll != null && (clay == null || !clay.Interactable || Mathf.Abs(dx) > Mathf.Abs(dy));
            if (forward) scroll.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (forward)
            {
                scroll.OnDrag(eventData);
                return;
            }
            if (clay == null || !clay.Interactable || done) return;
            float dy = eventData.position.y - eventData.pressPosition.y;
            float p = dy / needPx;
            StepGestureUtil.SetFill(fill, p, p >= 1f ? HudBuilder.Good : HudBuilder.Accent);
            if (p >= 1f)
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
            if (!done && clay != null && clay.Interactable && eventData.position.y - eventData.pressPosition.y > 20f)
                StepGestureUtil.Hint(RecipeStrings.SwipeTooShort);
            StepGestureUtil.SetFill(fill, 0f, HudBuilder.Accent);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (pressed && !dragging && clay != null && clay.Interactable)
                StepGestureUtil.Hint(RecipeStrings.ActionHint(step)); // chạm đơn thuần: nhắc cách làm
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
            if (fill != null) fill.enabled = false;
        }
    }
}
