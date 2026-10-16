using System;
using UnityEngine;

namespace Cafe
{
    [Serializable]
    public sealed class BarkData { public BarkGroups barks; }

    /// Các nhóm câu thoại nền trong barks.json (tên trường khớp tên nhóm để JsonUtility đọc được).
    [Serializable]
    public sealed class BarkGroups
    {
        public string[] order;
        public string[] waiting;
        public string[] served_happy;
        public string[] served_meh;
        public string[] leaving_angry;
        public string[] tip;
        public string[] dirty_table;
        public string[] regular_arrives;
    }

    /// Lời thoại nền của khách ("Ngon dữ thần luôn!", "Thôi, tui đi chỗ khác nha!"...) hiện trên đầu khách lúc gọi món, được phục vụ,
    /// bỏ về. Hiếm và có thời gian nghỉ chung để màn hình không rối: mỗi câu chỉ phát với xác suất cho trước và tối đa một câu mỗi
    /// MinGapSeconds giây thực.
    public sealed class BarkBook
    {
        public static readonly string[] Groups = { "order", "waiting", "served_happy", "served_meh", "leaving_angry", "tip", "dirty_table", "regular_arrives" };
        public const float MinGapSeconds = 2.2f;

        private BarkData data;
        private float lastBarkTime = -100f;

        public static string[] LinesOf(BarkData d, string group)
        {
            if (d == null || d.barks == null) return new string[0];
            BarkGroups b = d.barks;
            string[] lines;
            switch (group)
            {
                case "order": lines = b.order; break;
                case "waiting": lines = b.waiting; break;
                case "served_happy": lines = b.served_happy; break;
                case "served_meh": lines = b.served_meh; break;
                case "leaving_angry": lines = b.leaving_angry; break;
                case "tip": lines = b.tip; break;
                case "dirty_table": lines = b.dirty_table; break;
                case "regular_arrives": lines = b.regular_arrives; break;
                default: lines = null; break;
            }
            return lines ?? new string[0];
        }

        public void Load(TextAsset asset)
        {
            data = null;
            if (asset == null) return;
            try { data = JsonUtility.FromJson<BarkData>(asset.text); }
            catch (Exception e) { Debug.LogWarning("[Barks] Không đọc được barks.json: " + e.Message); }
        }

        public bool Enabled { get { return data != null && data.barks != null; } }

        /// Chọn ngẫu nhiên một câu của nhóm; `{name}` thay bằng tên khách. Null nếu nhóm rỗng.
        public string Pick(string group, string name = null)
        {
            string[] lines = LinesOf(data, group);
            if (lines.Length == 0) return null;
            string line = lines[UnityEngine.Random.Range(0, lines.Length)];
            return string.IsNullOrEmpty(name) ? line.Replace("{name}", "Khách") : line.Replace("{name}", name);
        }

        /// Cho phép phát một câu lúc này không: đủ thời gian nghỉ chung và qua xác suất `chance`. Nếu được thì tính là vừa phát.
        public bool Allow(float chance, float now)
        {
            if (!Enabled || now - lastBarkTime < MinGapSeconds) return false;
            if (UnityEngine.Random.value > chance) return false;
            lastBarkTime = now;
            return true;
        }
    }
}
