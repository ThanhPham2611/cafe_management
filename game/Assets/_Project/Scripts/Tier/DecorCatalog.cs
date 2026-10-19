using System.Collections.Generic;
using UnityEngine;

namespace Cafe
{
    /// Mặt trang trí của phòng: sơn tường hoặc kiểu sàn.
    public enum DecorSlot { Wall, Floor }

    /// Họ phong cách (tường + sàn cùng họ = "hợp tông", thêm điểm). None = kiểu mặc định, không cộng điểm.
    public enum DecorFamily { None, Vintage, IndoChine, Modern }

    /// Hoa văn sàn, EnvironmentBuilder dựng theo đây.
    public enum FloorPattern { Encaustic, Parquet, Terracotta, Terrazzo, HueTile }

    /// Một lựa chọn sơn tường / sàn. Giá trả MỘT lần (Xu hoặc Hạt), mua rồi đổi qua lại miễn phí.
    public sealed class DecorOption
    {
        public string id;
        public string name;
        public DecorSlot slot;
        public DecorFamily family;
        public int priceXu;
        public int priceGems;
        public int minTier = 2;
        public int minLevel = 1;
        // Tường: ô palette chân tường / nẹp / thân tường; brick = kẻ mạch vữa ngang trên thân tường
        public int lower, cap, upper;
        public bool brick;
        // Sàn
        public FloorPattern floor;
        /// Màu ô xem trước trong bảng (khớp hex art_bible §3): [0] thân tường / nền sàn, [1] chân tường / hoa văn.
        public Color32 previewMain, previewAccent;

        public bool IsFree { get { return priceXu <= 0 && priceGems <= 0; } }
    }

    /// Danh mục sơn tường + sàn (docs/decor_spec.md). Logic thuần, test được.
    /// Chỉ dùng 64 ô palette sẵn có: đổi màu = dựng lại mesh với ô khác, không thêm vật liệu / draw call.
    public static class DecorCatalog
    {
        public const string DefaultWallId = "w_cream";

        /// Điểm Style cộng vào sảnh cho mỗi mặt (tường / sàn) đang dùng kiểu có họ phong cách, và thêm khi hai mặt cùng họ.
        public const int SlotBonusT2 = 10, SlotBonusT3 = 20;
        public const int MatchBonusT2 = 5, MatchBonusT3 = 10;

        private static Color32 Hex(uint rgb)
        {
            return new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 0xFF);
        }

        public static readonly DecorOption[] Walls =
        {
            new DecorOption { id = DefaultWallId, name = "Vôi ngà", slot = DecorSlot.Wall,
                lower = 36, cap = 19, upper = 11, previewMain = Hex(0xF2DDB8), previewAccent = Hex(0x9CD3B5) },
            new DecorOption { id = "w_mint", name = "Bạc hà Tập thể", slot = DecorSlot.Wall, family = DecorFamily.Vintage,
                priceXu = 1500, minLevel = 7, lower = 23, cap = 22, upper = 36, previewMain = Hex(0x9CD3B5), previewAccent = Hex(0x6E8F7A) },
            new DecorOption { id = "w_hoian", name = "Vàng Hội An", slot = DecorSlot.Wall, family = DecorFamily.IndoChine,
                priceXu = 2500, minLevel = 9, lower = 23, cap = 19, upper = 13, previewMain = Hex(0xF2B93B), previewAccent = Hex(0x6E8F7A) },
            new DecorOption { id = "w_cement", name = "Xi măng Sài Gòn", slot = DecorSlot.Wall, family = DecorFamily.Modern,
                priceXu = 3000, minLevel = 12, lower = 51, cap = 18, upper = 45, previewMain = Hex(0xB9B2A6), previewAccent = Hex(0x2B2D31) },
            new DecorOption { id = "w_jade", name = "Ngọc Đông Dương", slot = DecorSlot.Wall, family = DecorFamily.IndoChine,
                priceXu = 8000, minTier = 3, minLevel = 17, lower = 19, cap = 13, upper = 37, previewMain = Hex(0x3E9E8C), previewAccent = Hex(0x5E3920) },
            new DecorOption { id = "w_brick", name = "Gạch thẻ trần", slot = DecorSlot.Wall, family = DecorFamily.Vintage,
                priceGems = 150, lower = 47, cap = 19, upper = 46, brick = true, previewMain = Hex(0xC4683E), previewAccent = Hex(0x8E877B) },
        };

        public static readonly DecorOption[] Floors =
        {
            new DecorOption { id = "f_encaustic", name = "Gạch bông Sài Gòn", slot = DecorSlot.Floor, floor = FloorPattern.Encaustic,
                previewMain = Hex(0xE9D9B5), previewAccent = Hex(0xB5472F) },
            new DecorOption { id = "f_parquet", name = "Sàn gỗ ghép", slot = DecorSlot.Floor, floor = FloorPattern.Parquet, minTier = 3,
                previewMain = Hex(0xD8A96B), previewAccent = Hex(0xE8DCC4) },
            new DecorOption { id = "f_terracotta", name = "Gạch tàu đỏ", slot = DecorSlot.Floor, family = DecorFamily.Vintage, floor = FloorPattern.Terracotta,
                priceXu = 2000, minLevel = 8, previewMain = Hex(0xC4683E), previewAccent = Hex(0x8E877B) },
            new DecorOption { id = "f_terrazzo", name = "Đá mài granito", slot = DecorSlot.Floor, family = DecorFamily.Modern, floor = FloorPattern.Terrazzo,
                priceXu = 3500, minLevel = 10, previewMain = Hex(0xF3E9D2), previewAccent = Hex(0x3B6E8F) },
            new DecorOption { id = "f_hue", name = "Gạch men lam Huế", slot = DecorSlot.Floor, family = DecorFamily.IndoChine, floor = FloorPattern.HueTile,
                priceGems = 200, previewMain = Hex(0xF1F0EA), previewAccent = Hex(0x1D4A97) },
        };

        public static DecorOption[] For(DecorSlot slot) { return slot == DecorSlot.Wall ? Walls : Floors; }

        public static DecorOption Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < Walls.Length; i++) if (Walls[i].id == id) return Walls[i];
            for (int i = 0; i < Floors.Length; i++) if (Floors[i].id == id) return Floors[i];
            return null;
        }

        /// Sàn mặc định theo cấp: cấp 2 gạch bông, cấp 3 gỗ ghép (giữ nguyên hình cũ).
        public static string DefaultFloorId(int tier) { return tier >= 3 ? "f_parquet" : "f_encaustic"; }

        public static string DefaultId(DecorSlot slot, int tier) { return slot == DecorSlot.Wall ? DefaultWallId : DefaultFloorId(tier); }

        /// Lựa chọn hợp lệ cho cấp (id lạ / chưa tới cấp → mặc định).
        public static DecorOption Resolve(DecorSlot slot, string id, int tier)
        {
            DecorOption o = Find(id);
            if (o == null || o.slot != slot || tier < o.minTier) o = Find(DefaultId(slot, tier));
            return o;
        }

        /// Điểm Style cộng cho sảnh từ tường + sàn đang dùng (cấp 1 không có phòng: 0).
        public static int StyleBonus(int tier, string wallId, string floorId)
        {
            if (tier < 2) return 0;
            DecorOption w = Resolve(DecorSlot.Wall, wallId, tier);
            DecorOption f = Resolve(DecorSlot.Floor, floorId, tier);
            int slot = tier >= 3 ? SlotBonusT3 : SlotBonusT2;
            int bonus = 0;
            if (w.family != DecorFamily.None) bonus += slot;
            if (f.family != DecorFamily.None) bonus += slot;
            if (w.family != DecorFamily.None && w.family == f.family) bonus += tier >= 3 ? MatchBonusT3 : MatchBonusT2;
            return bonus;
        }

        public static string FamilyName(DecorFamily f)
        {
            switch (f)
            {
                case DecorFamily.Vintage: return "Hoài cổ";
                case DecorFamily.IndoChine: return "Đông Dương";
                case DecorFamily.Modern: return "Hiện đại";
                default: return "";
            }
        }
    }

    /// Kết quả thử mua / dùng một lựa chọn.
    public enum DecorCheck { Ok, Owned, Unknown, NeedTier, NeedLevel, NeedXu, NeedGems }

    /// Trạng thái trang trí của người chơi: món đã sở hữu (mọi cấp) + lựa chọn tường / sàn theo từng cấp quán.
    /// Không giữ tiền: bên gọi trừ Xu / Hạt rồi gọi MarkOwned.
    public sealed class DecorState
    {
        private readonly HashSet<string> owned = new HashSet<string>();
        private readonly Dictionary<int, string> walls = new Dictionary<int, string>();
        private readonly Dictionary<int, string> floors = new Dictionary<int, string>();

        public bool Owns(string id)
        {
            DecorOption o = DecorCatalog.Find(id);
            return o != null && (o.IsFree || owned.Contains(id));
        }

        public void MarkOwned(string id)
        {
            if (DecorCatalog.Find(id) != null) owned.Add(id);
        }

        public string WallFor(int tier) { return DecorCatalog.Resolve(DecorSlot.Wall, Get(walls, tier), tier).id; }
        public string FloorFor(int tier) { return DecorCatalog.Resolve(DecorSlot.Floor, Get(floors, tier), tier).id; }
        public string Current(DecorSlot slot, int tier) { return slot == DecorSlot.Wall ? WallFor(tier) : FloorFor(tier); }

        public int StyleBonus(int tier) { return DecorCatalog.StyleBonus(tier, WallFor(tier), FloorFor(tier)); }

        private static string Get(Dictionary<int, string> d, int tier)
        {
            string s;
            return d.TryGetValue(tier, out s) ? s : null;
        }

        /// Có mua được không (chưa trừ tiền). Chưa tới cấp → NeedTier (kể cả món miễn phí / đã mua); đã sở hữu → Owned.
        public DecorCheck CanBuy(string id, int tier, int level, int xu, int gems)
        {
            DecorOption o = DecorCatalog.Find(id);
            if (o == null) return DecorCheck.Unknown;
            if (tier < o.minTier) return DecorCheck.NeedTier;
            if (Owns(id)) return DecorCheck.Owned;
            if (level < o.minLevel) return DecorCheck.NeedLevel;
            if (xu < o.priceXu) return DecorCheck.NeedXu;
            if (gems < o.priceGems) return DecorCheck.NeedGems;
            return DecorCheck.Ok;
        }

        /// Dùng một lựa chọn đã sở hữu cho cấp `tier`. Trả false nếu chưa sở hữu / sai cấp.
        public bool Apply(string id, int tier)
        {
            DecorOption o = DecorCatalog.Find(id);
            if (o == null || !Owns(id) || tier < o.minTier) return false;
            (o.slot == DecorSlot.Wall ? walls : floors)[tier] = id;
            return true;
        }

        // ---- lưu ----

        public void Load(SaveData save)
        {
            owned.Clear();
            walls.Clear();
            floors.Clear();
            if (save == null) return;
            if (save.decorOwned != null)
                for (int i = 0; i < save.decorOwned.Length; i++) MarkOwned(save.decorOwned[i]);
            if (save.decorChoices == null) return;
            for (int i = 0; i < save.decorChoices.Length; i++)
            {
                DecorChoiceSave c = save.decorChoices[i];
                if (!string.IsNullOrEmpty(c.wall) && Owns(c.wall)) walls[c.tier] = c.wall;
                if (!string.IsNullOrEmpty(c.floor) && Owns(c.floor)) floors[c.tier] = c.floor;
            }
        }

        public void WriteToSave(SaveData data)
        {
            if (data == null) return;
            var ids = new List<string>(owned);
            ids.Sort(System.StringComparer.Ordinal);
            data.decorOwned = ids.ToArray();
            var tiers = new SortedSet<int>(walls.Keys);
            tiers.UnionWith(floors.Keys);
            var list = new List<DecorChoiceSave>();
            foreach (int t in tiers) list.Add(new DecorChoiceSave { tier = t, wall = Get(walls, t), floor = Get(floors, t) });
            data.decorChoices = list.ToArray();
        }
    }
}
