using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// Tên quán trên biển xe đẩy: Canvas world-space + Text (font heading, chữ kem viền cà phê).
    /// Neo vào child "SignAnchor" của prefab xe đẩy nếu có (vị trí/xoay/kích thước theo anchor, +Z local = hướng ra ngoài);
    /// không có thì dùng vị trí mặc định theo mô hình gen_furniture.py (biển giấy 1.04 x 0.34 m, mặt trước z = +0.37).
    /// Tự gắn lại khi xe đẩy bị đặt lại (build mode xóa + tạo mới).
    public class ShopSign : MonoBehaviour
    {
        // Mặc định theo cart local space (m)
        private static readonly Vector3 DefaultLocalPos = new Vector3(0f, 0.62f, 0.385f); // giữa biển hiệu (đã bỏ logo sao khỏi xe đẩy)
        private static readonly Vector2 DefaultSize = new Vector2(1.0f, 0.30f);
        private const float OffsetFromSurface = 0.015f;
        private const float PixelsPerMeter = 1000f;

        private GameBootstrap game;
        private Canvas canvas;
        private Text label;
        private PlacedFurniture attachedTo;
        private string text = "";

        public static ShopSign Create(GameBootstrap g)
        {
            var go = new GameObject("ShopSign");
            var s = go.AddComponent<ShopSign>();
            s.Init(g);
            return s;
        }

        private void Init(GameBootstrap g)
        {
            game = g;

            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 0;
            var rt = (RectTransform)transform;
            rt.sizeDelta = DefaultSize * PixelsPerMeter;
            rt.pivot = new Vector2(0.5f, 0.5f);

            // Biển hiệu màu giấy (kem): chữ phải tối (coffee), không viền, nếu không sẽ chìm vào nền
            label = HudBuilder.AddText(transform, "Name", "", 110, TextAnchor.MiddleCenter, HudBuilder.Ink, true, false);
            HudBuilder.SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 8f), new Vector2(-16f, -8f));
            HudBuilder.Fit(label, 24);
            label.resizeTextMaxSize = 110;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;

            SetText(g.ShopName);
            Attach();
        }

        public void SetText(string shopName)
        {
            text = string.IsNullOrEmpty(shopName) ? Strings.DefaultShopName : shopName;
            if (label != null) label.text = text;
        }

        private void LateUpdate()
        {
            PlacedFurniture pf = game != null && game.Grid != null ? game.Grid.BrewStationFurniture : null;
            if (pf != attachedTo) Attach();
        }

        private void Attach()
        {
            PlacedFurniture pf = game.Grid != null ? game.Grid.BrewStationFurniture : null;
            attachedTo = pf;
            if (pf == null)
            {
                canvas.enabled = false;
                return;
            }
            canvas.enabled = true;

            Transform cart = pf.transform;
            transform.SetParent(cart, false);

            Vector3 localPos = DefaultLocalPos;
            Vector2 size = DefaultSize;
            Transform anchor = FindAnchor(cart);
            if (anchor != null)
            {
                // Anchor: +Z hướng ra ngoài mặt biển; scale XY của anchor = kích thước (m) nếu khác 1
                Vector3 ls = anchor.lossyScale;
                transform.position = anchor.position + anchor.forward * OffsetFromSurface;
                transform.rotation = anchor.rotation * Quaternion.Euler(0f, 180f, 0f);
                if (ls.x > 0.05f && ls.y > 0.05f && (Mathf.Abs(ls.x - 1f) > 0.01f || Mathf.Abs(ls.y - 1f) > 0.01f))
                    size = new Vector2(ls.x, ls.y);
            }
            else
            {
                // Canvas world-space nhìn được từ phía -Z của nó, nên xoay 180 độ để chữ hướng ra +Z của xe
                transform.localPosition = localPos;
                transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }

            var rt = (RectTransform)transform;
            rt.sizeDelta = size * PixelsPerMeter;
            // Bù scale của cha để 1000 px = 1 m
            Vector3 p = cart.lossyScale;
            float inv = 1f / PixelsPerMeter;
            rt.localScale = new Vector3(inv / Mathf.Max(0.0001f, p.x), inv / Mathf.Max(0.0001f, p.y), inv / Mathf.Max(0.0001f, p.z));
        }

        private static Transform FindAnchor(Transform root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t != root && t.name.StartsWith("SignAnchor")) return t;
            }
            return null;
        }
    }
}
