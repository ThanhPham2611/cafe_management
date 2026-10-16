using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Cafe
{
    /// Chạm / click vào thế giới 3D -> ITappable. Bỏ qua chạm trên UI và thao tác kéo (pan camera).
    public class TapInput : MonoBehaviour
    {
        /// Ngưỡng (pixel) phân biệt chạm và kéo. CafeCameraController dùng chung.
        public static float DragThresholdPx
        {
            get
            {
                float dpi = Screen.dpi > 0f ? Screen.dpi : 160f;
                return Mathf.Max(12f, dpi * 0.07f);
            }
        }

        /// Build mode đặt hook này để nhận chạm thay cho khách / ly (null = chạm bình thường).
        public static System.Action<Vector2> TapOverride;

        private static PointerEventData pointerData;
        private static EventSystem pointerDataSystem;
        private static readonly List<RaycastResult> uiHits = new List<RaycastResult>(8);

        /// pointerId < 0: chuột.
        public static bool IsOverUI(Vector2 screenPos, int pointerId)
        {
            EventSystem es = EventSystem.current;
            if (es == null) return false;
            if (pointerId < 0 ? es.IsPointerOverGameObject() : es.IsPointerOverGameObject(pointerId)) return true;

            if (pointerData == null || pointerDataSystem != es)
            {
                pointerData = new PointerEventData(es);
                pointerDataSystem = es;
            }
            pointerData.position = screenPos;
            uiHits.Clear();
            es.RaycastAll(pointerData, uiHits);
            return uiHits.Count > 0;
        }

        /// Như IsOverUI nhưng bỏ qua lớp tối của hướng dẫn (CameraPassThrough): camera vẫn kéo / chụm được khi hướng dẫn đang làm tối,
        /// còn chạm xuống thế giới vẫn bị chặn bởi IsOverUI.
        public static bool IsOverUIForCamera(Vector2 screenPos, int pointerId)
        {
            EventSystem es = EventSystem.current;
            if (es == null) return false;
            if (pointerData == null || pointerDataSystem != es)
            {
                pointerData = new PointerEventData(es);
                pointerDataSystem = es;
            }
            pointerData.position = screenPos;
            uiHits.Clear();
            es.RaycastAll(pointerData, uiHits);
            for (int i = 0; i < uiHits.Count; i++)
            {
                GameObject go = uiHits[i].gameObject;
                if (go != null && go.GetComponent<CameraPassThrough>() == null) return true;
            }
            return false;
        }

        private Camera cam;
        private readonly RaycastHit[] hits = new RaycastHit[24];

        private bool tracking;
        private bool overUI;
        private bool moved;
        private int trackId;
        private Vector2 startPos;
        private float lastTouchTime = -10f;

        private void Update()
        {
            int tc = Input.touchCount;
            if (tc > 0)
            {
                lastTouchTime = Time.unscaledTime;
                HandleTouch(tc);
            }
            else if (Time.unscaledTime - lastTouchTime > 0.3f)
            {
                HandleMouse();
            }
        }

        private void HandleTouch(int tc)
        {
            if (tc >= 2)
            {
                tracking = false; // đa chạm = pinch, không phải tap
                return;
            }

            Touch t = Input.GetTouch(0);
            float thr = DragThresholdPx;
            switch (t.phase)
            {
                case TouchPhase.Began:
                    tracking = true;
                    trackId = t.fingerId;
                    startPos = t.position;
                    moved = false;
                    overUI = IsOverUI(t.position, t.fingerId);
                    break;
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    if (tracking && t.fingerId == trackId && (t.position - startPos).sqrMagnitude > thr * thr)
                        moved = true;
                    break;
                case TouchPhase.Ended:
                    if (tracking && t.fingerId == trackId && !moved && !overUI)
                        Tap(t.position);
                    tracking = false;
                    break;
                case TouchPhase.Canceled:
                    tracking = false;
                    break;
            }
        }

        private void HandleMouse()
        {
            float thr = DragThresholdPx;
            if (Input.GetMouseButtonDown(0))
            {
                tracking = true;
                startPos = Input.mousePosition;
                moved = false;
                overUI = IsOverUI(startPos, -1);
            }
            else if (tracking && Input.GetMouseButton(0))
            {
                if (((Vector2)Input.mousePosition - startPos).sqrMagnitude > thr * thr) moved = true;
            }
            else if (tracking && Input.GetMouseButtonUp(0))
            {
                if (!moved && !overUI) Tap(Input.mousePosition);
                tracking = false;
            }
        }

        private void Tap(Vector2 screenPos)
        {
            if (TapOverride != null)
            {
                TapOverride(screenPos);
                return;
            }
            if (cam == null) cam = Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(screenPos);
            ITappable best = Pick(Physics.RaycastNonAlloc(ray, hits, 200f, ~0, QueryTriggerInteraction.Ignore));
            if (best == null)
            {
                // Dung sai cho ngón tay: quét cầu nhỏ
                best = Pick(Physics.SphereCastNonAlloc(ray, 0.25f, hits, 200f, ~0, QueryTriggerInteraction.Ignore));
            }
            if (best != null) best.OnTap();
        }

        private ITappable Pick(int count)
        {
            ITappable best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = hits[i];
                if (h.collider == null || h.distance >= bestDist) continue;
                ITappable t = h.collider.GetComponentInParent<ITappable>();
                if (t == null) continue;
                best = t;
                bestDist = h.distance;
            }
            return best;
        }
    }
}
