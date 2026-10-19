using System.Collections.Generic;
using UnityEngine;

namespace Cafe
{
    /// Luật của Quầy pha chế phụ và Quầy line (cấp 3, docs/stations_spec.md). Thuần logic (không đụng thế giới) để kiểm thử được:
    /// điểm đứng của chỗ làm, gán Pha chế vào chỗ theo thứ tự thuê, số quầy được tính, sức chứa kệ giao, hệ số pha.
    public static class StationRules
    {
        /// Khoảng cách (m) từ mép quầy tới điểm đứng của Pha chế (cùng phía chủ quán đứng ở quầy chính: transform.forward, +Z cục bộ).
        public const float StandOffset = 0.4f;
        /// Phía nhân viên đứng của quầy pha phụ theo trục Z cục bộ (Unity): +1 = transform.forward = phía -Y trong Blender (cùng phía
        /// chủ quán đứng ở quầy bar chính, OwnerController.ComputeCart); -1 = phía ngược lại. Model xuất ngược phía thì chỉ đổi số này.
        public const float StaffSide = 1f;

        /// Bật/tắt "+1 chỗ thuê Pha chế mỗi quầy" (bot -nostationslots tắt để tách hiệu ứng tốc độ khỏi hiệu ứng thêm người).
        public static bool HireSlotsEnabled = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            HireSlotsEnabled = true;
        }

        /// Số quầy pha phụ được tính cho trần thuê: chỉ ở cấp 3, chặn ở GameBalance.BrewStationMaxCount, 0 khi bị tắt.
        public static int StationsCounted(int tier, int placedStations, bool hireSlotsEnabled = true)
        {
            if (tier < 3 || !hireSlotsEnabled) return 0;
            return Mathf.Clamp(placedStations, 0, GameBalance.BrewStationMaxCount);
        }

        /// Sức chứa kệ giao: gốc 6 + tổng shelfSlots của các quầy line đang đặt (âm coi như 0).
        public static int ShelfCapacity(int shelfSlotsTotal)
        {
            return GameBalance.ReadyShelfBaseCapacity + Mathf.Max(0, shelfSlotsTotal);
        }

        /// Hệ số thời gian pha của Pha chế: có chỗ ở quầy pha phụ thì × BrewSpotFactor, không có thì giữ nguyên.
        public static float BrewFactor(bool hasSpot)
        {
            return hasSpot ? GameBalance.BrewSpotFactor : 1f;
        }

        /// Chỗ làm (chỉ số trong danh sách điểm đứng) của Pha chế thứ `brewerOrdinal` (từ 0, theo thứ tự thuê); -1 = không có chỗ, đứng hàng cũ.
        public static int SpotIndexFor(int brewerOrdinal, int spotCount)
        {
            return brewerOrdinal >= 0 && brewerOrdinal < spotCount ? brewerOrdinal : -1;
        }

        /// Gán cả đội: phần tử k là chỗ của Pha chế thứ k (theo thứ tự thuê), -1 nếu không có chỗ. Mỗi chỗ tối đa 1 người.
        public static int[] Assign(int brewerCount, int spotCount)
        {
            var result = new int[Mathf.Max(0, brewerCount)];
            for (int k = 0; k < result.Length; k++) result[k] = SpotIndexFor(k, spotCount);
            return result;
        }

        /// Điểm đứng cục bộ (m, gốc ở tâm footprint mặt đất, chưa xoay) của chỗ thứ `index` trong `spots` chỗ: chia đều theo bề ngang
        /// (trục X cục bộ), lùi ra khỏi mép quầy phía nhân viên (StaffSide, mặc định +Z cục bộ) StandOffset m.
        public static Vector3 SpotLocal(Vector2Int footprint, int spots, int index, float cell = GameBalance.CellSize)
        {
            int n = Mathf.Max(1, spots);
            float width = Mathf.Max(1, footprint.x) * cell;
            float depth = Mathf.Max(1, footprint.y) * cell;
            float x = -width * 0.5f + width * (Mathf.Clamp(index, 0, n - 1) + 0.5f) / n;
            return new Vector3(x, 0f, StaffSide * (depth * 0.5f + StandOffset));
        }

        /// Thêm điểm đứng (thế giới) của một quầy đã đặt: `position` là tâm footprint mặt đất, `rotation` là góc xoay của quầy.
        public static void AppendSpots(List<Vector3> into, Vector3 position, Quaternion rotation, Vector2Int footprint, int spots)
        {
            if (into == null) return;
            for (int i = 0; i < spots; i++) into.Add(position + rotation * SpotLocal(footprint, spots, i));
        }

        /// Dòng hiệu ứng trên thẻ cửa hàng ("" = không phải quầy pha phụ / quầy line).
        public static string Effect(int brewSpots, int shelfSlots)
        {
            if (brewSpots > 0) return "Pha nhanh −15%, +1 Pha chế";
            if (shelfSlots > 0) return "Kệ giao +" + shelfSlots + " ly";
            return "";
        }
    }

    /// Bộ đếm cho bot đo quầy (DebugAutoPilot in mỗi ngày rồi đặt lại): số lần Pha chế không để được ly lên kệ giao vì kệ đầy,
    /// và số ly Pha chế tự mang ra khách.
    public static class StationStats
    {
        public static int ShelfFull;
        public static int SelfDelivered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Reset();
        }

        public static void Reset()
        {
            ShelfFull = 0;
            SelfDelivered = 0;
        }
    }
}
