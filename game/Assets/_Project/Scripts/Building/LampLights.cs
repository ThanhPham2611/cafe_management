using System.Collections.Generic;
using UnityEngine;

namespace Cafe
{
    /// Đèn thật cho đồ đèn (đèn thả, đèn cây, đèn bàn quầy, bóng đèn, đèn lồng, đèn chùm) về đêm. Một nhóm Point Light dùng lại,
    /// đặt vào các đèn gần giữa quán nhất (giữ nguyên khi camera di chuyển nên không nhấp nháy). Số đèn / tầm / cường độ theo mức đồ họa
    /// (GraphicsPresets.Lamp*): mức Thấp không tạo đèn nào, chỉ có ô sáng trên mô hình (Toon_Palette). Sáng dần theo DayNightController.Night.
    /// Tạo từ GameBootstrap, nghe Grid.LayoutChanged (đặt / gỡ / dời / đổi cấp quán) và GraphicsQuality.Changed.
    public sealed class LampLights : MonoBehaviour
    {
        private static readonly Color Warm = new Color(1f, 0.72f, 0.42f, 1f);
        private static readonly int SoftId = Shader.PropertyToID("_CafeLampSoft");

        private GameBootstrap game;
        private readonly List<Light> pool = new List<Light>();
        private readonly List<PlacedFurniture> lamps = new List<PlacedFurniture>();
        private int used;
        private float intensity;
        private bool dirty = true;
        private float lastNight = -1f;

        public static bool IsLamp(string id)
        {
            switch (id)
            {
                case "Lamp_Bulb": case "Lamp_Desk": case "Lamp_Floor": case TierConfig.IdLamp: case "Lantern_Hoian": case "Chandelier_Brass":
                    return true;
            }
            return false;
        }

        public static bool AnyPlaced(GridSystem grid)
        {
            if (grid == null) return false;
            for (int i = 0; i < grid.Placed.Count; i++)
                if (grid.Placed[i] != null && grid.Placed[i].Def != null && IsLamp(grid.Placed[i].Def.id)) return true;
            return false;
        }

        public void Init(GameBootstrap g)
        {
            game = g;
            if (g.Grid != null) g.Grid.LayoutChanged += MarkDirty;
            GraphicsQuality.Changed += MarkDirty;
        }

        private void OnDestroy()
        {
            if (game != null && game.Grid != null) game.Grid.LayoutChanged -= MarkDirty;
            GraphicsQuality.Changed -= MarkDirty;
            Shader.SetGlobalFloat(SoftId, 0f);
        }

        private void MarkDirty() { dirty = true; }

        private void Update()
        {
            if (game == null || game.Grid == null) return;
            if (dirty) { dirty = false; Assign(); lastNight = -1f; }
            float night = DayNightController.Night;
            if (night == lastNight) return;
            lastNight = night;
            for (int i = 0; i < used; i++)
            {
                pool[i].enabled = night > 0.01f; // ban ngày tắt hẳn để URP khỏi tính đèn phụ
                pool[i].intensity = intensity * night;
            }
        }

        /// Chọn các đèn gần tâm lưới nhất (tối đa theo mức đồ họa) rồi đặt đèn thật vào đó.
        private void Assign()
        {
            GraphicsLevel level = GraphicsQuality.Current;
            int max = GraphicsPresets.LampLights(level);
            intensity = GraphicsPresets.LampIntensity(level);
            Shader.SetGlobalFloat(SoftId, GraphicsPresets.LampSoftness(level));

            lamps.Clear();
            List<PlacedFurniture> placed = game.Grid.Placed;
            for (int i = 0; i < placed.Count; i++)
                if (placed[i] != null && placed[i].Def != null && IsLamp(placed[i].Def.id)) lamps.Add(placed[i]);
            Vector3 centre = game.Grid.WorldBounds.center;
            centre.y = 0f;
            lamps.Sort((a, b) => FlatSqrDist(a, centre).CompareTo(FlatSqrDist(b, centre)));

            used = Mathf.Min(max, lamps.Count);
            for (int i = 0; i < used; i++)
            {
                if (i >= pool.Count) pool.Add(NewLight(i));
                Light l = pool[i];
                l.transform.position = BulbPosition(lamps[i]);
                l.range = GraphicsPresets.LampRange(level);
            }
            for (int i = used; i < pool.Count; i++) pool[i].enabled = false;
        }

        private static float FlatSqrDist(PlacedFurniture pf, Vector3 centre)
        {
            Vector3 d = pf.WorldBounds.center - centre;
            d.y = 0f;
            return d.sqrMagnitude;
        }

        /// Chỗ bóng đèn: đồ treo ở gần đáy, đèn cao (đèn cây) ở gần đỉnh, còn lại giữa vật.
        private static Vector3 BulbPosition(PlacedFurniture pf)
        {
            Bounds b = pf.WorldBounds;
            float h = b.size.y;
            float y = pf.Overhead ? b.min.y + Mathf.Min(0.2f, h * 0.4f) : (h > 0.8f ? b.max.y - 0.2f : b.center.y);
            return new Vector3(b.center.x, y, b.center.z);
        }

        private Light NewLight(int index)
        {
            var go = new GameObject("LampLight" + index);
            go.transform.SetParent(transform, false);
            Light l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = Warm;
            l.shadows = LightShadows.None;
            l.intensity = 0f;
            l.enabled = false;
            return l;
        }
    }
}
