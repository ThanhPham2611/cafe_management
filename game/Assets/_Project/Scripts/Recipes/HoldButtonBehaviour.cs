using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Cafe
{
    /// Bước kiểu GIỮ (rót): giữ nút đủ `seconds` thì thành công (gọi onSuccess ngay lúc đầy), nhả sớm = thất bại.
    /// Gắn lên ClayButton (nút không gắn onClick). Kéo ra khỏi nút / kéo cuộn khay = hủy lặng lẽ, không phạt.
    public class HoldButtonBehaviour : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private ClayButton clay;
        private BrewStep step;
        private float required = 0.6f;
        private Action<BrewStep> onSuccess;
        private Action<BrewStep, bool> onEarlyRelease; // (bước, chạm nhầm quá ngắn?)
        private Image fill;

        private bool holding;
        private bool done;
        private float held;
        private PointerEventData ptr;
        private Vector2 downPos;
        private float cancelDistPx;

        public static HoldButtonBehaviour Attach(ClayButton clay, BrewStep step, float seconds,
                                                 Action<BrewStep> onSuccess, Action<BrewStep, bool> onEarlyRelease)
        {
            var b = clay.gameObject.AddComponent<HoldButtonBehaviour>();
            b.clay = clay;
            b.step = step;
            b.required = Mathf.Max(0.1f, seconds);
            b.onSuccess = onSuccess;
            b.onEarlyRelease = onEarlyRelease;
            b.cancelDistPx = Mathf.Max(24f, TapInput.DragThresholdPx * 2f);
            b.fill = StepIconFallback.CreateFillOverlay(clay.Content, Image.FillMethod.Radial360, 88f);
            return b;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (clay == null || !clay.Interactable) return;
            holding = true;
            done = false;
            held = 0f;
            ptr = eventData;
            downPos = eventData.position;
            SetFill(0f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!holding) return;
            bool wasDone = done;
            float h = held;
            bool moved = (eventData.position - downPos).sqrMagnitude > cancelDistPx * cancelDistPx;
            ResetHold();
            if (!wasDone && !moved && onEarlyRelease != null)
                onEarlyRelease(step, h < StepActions.AccidentalHoldSeconds);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            // Trượt ngón ra khỏi nút: hủy lặng lẽ
            if (holding && !done) ResetHold();
        }

        private void Update()
        {
            if (!holding || done) return;
            if (clay == null || !clay.Interactable)
            {
                ResetHold();
                return;
            }
            if (ptr != null && (ptr.position - downPos).sqrMagnitude > cancelDistPx * cancelDistPx)
            {
                ResetHold(); // đang cuộn khay, không phải giữ
                return;
            }
            held += Time.unscaledDeltaTime;
            float p = held / required;
            if (p >= 1f)
            {
                done = true;
                SetFill(1f);
                if (onSuccess != null) onSuccess(step);
                return;
            }
            SetFill(p);
        }

        private void SetFill(float p)
        {
            StepGestureUtil.SetFill(fill, p, p >= 1f ? HudBuilder.Good : HudBuilder.Accent);
        }

        private void ResetHold()
        {
            holding = false;
            done = false;
            held = 0f;
            ptr = null;
            StepGestureUtil.SetFill(fill, 0f, HudBuilder.Accent);
        }

        private void OnDisable()
        {
            holding = false;
            done = false;
            held = 0f;
            ptr = null;
            if (fill != null) fill.enabled = false;
        }
    }
}
