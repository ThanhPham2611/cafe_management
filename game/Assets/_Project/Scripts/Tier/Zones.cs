using UnityEngine;

namespace Cafe
{
    /// Các khu của nhà hàng (cấp 3). Sảnh chính luôn mở; ba khu phụ mua bằng Xu (economy §3.5, phase3_spec 3.2).
    public enum ZoneId { Hall = 0, Garden = 1, Terrace = 2, Vip = 3 }

    /// Thông tin tĩnh của từng khu: giá, bit lưu, tên.
    public static class ZoneInfo
    {
        public const int Count = 4;

        /// Ba khu phụ theo thứ tự hiện trong bảng Mở rộng.
        public static readonly ZoneId[] Buyable = { ZoneId.Garden, ZoneId.Terrace, ZoneId.Vip };

        /// Cờ bit trong mặt nạ khu đã mở (SaveData.zonesOpen). Sảnh chính không lưu (luôn mở).
        public static int Bit(ZoneId z)
        {
            return 1 << (int)z;
        }

        public static int Cost(ZoneId z)
        {
            switch (z)
            {
                case ZoneId.Garden: return GameBalance.ZoneGardenCost;
                case ZoneId.Terrace: return GameBalance.ZoneTerraceCost;
                case ZoneId.Vip: return GameBalance.ZoneVipCost;
                default: return 0;
            }
        }

        /// Số khu phụ đã mở trong mặt nạ (không đếm sảnh chính).
        public static int OpenCount(int mask)
        {
            int n = 0;
            for (int i = 0; i < Buyable.Length; i++)
                if ((mask & Bit(Buyable[i])) != 0) n++;
            return n;
        }

        /// F_ref của khu: sảnh chính theo cấp quán, khu phụ dùng FRefZone.
        public static float FRef(ZoneId z, float hallFRef)
        {
            return z == ZoneId.Hall ? hallFRef : GameBalance.FRefZone;
        }

        /// Hệ số giá của khu: khách ngồi VIP trả 1,5x.
        public static float PriceMult(ZoneId z)
        {
            return z == ZoneId.Vip ? GameBalance.VipPriceMult : 1f;
        }

        public static string Name(ZoneId z)
        {
            switch (z)
            {
                case ZoneId.Garden: return Strings.ZoneGarden;
                case ZoneId.Terrace: return Strings.ZoneTerrace;
                case ZoneId.Vip: return Strings.ZoneVip;
                default: return Strings.ZoneHall;
            }
        }

        public static string Description(ZoneId z)
        {
            switch (z)
            {
                case ZoneId.Garden: return Strings.ZoneGardenDesc;
                case ZoneId.Terrace: return Strings.ZoneTerraceDesc;
                case ZoneId.Vip: return Strings.ZoneVipDesc;
                default: return "";
            }
        }
    }

    /// Chia lưới nhà hàng thành các khu hình chữ nhật (đơn vị ô 0,5 m). Mọi ô đều thuộc đúng một khu; khu chưa mở
    /// thì không đặt được đồ. Bố cục (nhìn từ trên, mặt tiền ở dưới):
    ///   [ Sân vườn 6x12 ][ Sảnh chính 10x12 ][ Lầu 6x6   ]
    ///                                         [ VIP  6x6   ]
    /// Mặt nạ OpenMask luôn có bit của sảnh chính.
    public sealed class ZoneMap
    {
        public readonly RectInt[] Rects = new RectInt[ZoneInfo.Count];
        public int OpenMask { get; private set; }

        public ZoneMap(int annexCells, int hallCellsX, int depthCells)
        {
            int half = depthCells / 2;
            Rects[(int)ZoneId.Garden] = new RectInt(0, 0, annexCells, depthCells);
            Rects[(int)ZoneId.Hall] = new RectInt(annexCells, 0, hallCellsX, depthCells);
            int rightX = annexCells + hallCellsX;
            Rects[(int)ZoneId.Terrace] = new RectInt(rightX, 0, annexCells, half);
            Rects[(int)ZoneId.Vip] = new RectInt(rightX, half, annexCells, depthCells - half);
            OpenMask = ZoneInfo.Bit(ZoneId.Hall);
        }

        public RectInt Hall { get { return Rects[(int)ZoneId.Hall]; } }

        /// Đặt mặt nạ khu phụ đã mở (sảnh chính luôn mở).
        public void SetOpen(int buyableMask)
        {
            OpenMask = ZoneInfo.Bit(ZoneId.Hall) | (buyableMask & ~ZoneInfo.Bit(ZoneId.Hall));
        }

        public bool IsOpen(ZoneId z)
        {
            return (OpenMask & ZoneInfo.Bit(z)) != 0;
        }

        /// Khu chứa ô (kể cả ô nằm ngoài lưới => sảnh chính, để không phát sinh lỗi ở chỗ gọi).
        public ZoneId ZoneAt(Vector2Int cell)
        {
            for (int i = 0; i < Rects.Length; i++)
            {
                RectInt r = Rects[i];
                if (cell.x >= r.xMin && cell.x < r.xMax && cell.y >= r.yMin && cell.y < r.yMax) return (ZoneId)i;
            }
            return ZoneId.Hall;
        }

        /// Nửa rộng lỗ mở trên tường sảnh sang khu phụ, tính bằng ô (3 ô = 1,5 m: lỗ mở rộng 3 m, khớp EnvironmentBuilder.AnnexOpeningHalf).
        public const int OpeningHalfCells = 3;

        /// Lối sang khu còn thông: ở phía sảnh của lỗ mở (hai cột ô sát tường, đúng dải y của lỗ) phải có một lối rộng 2 ô (1 m)
        /// mà cả 4 ô đều trống. isFree(ô) = ô không bị đồ chiếm. Sảnh chính luôn thông.
        public bool OpeningClear(ZoneId zone, System.Func<Vector2Int, bool> isFree)
        {
            if (zone == ZoneId.Hall) return true;
            RectInt hall = Hall, r = Rects[(int)zone];
            int x0 = zone == ZoneId.Garden ? hall.xMin : hall.xMax - 1; // cột ô sát tường
            int dx = zone == ZoneId.Garden ? 1 : -1;                    // cột thứ hai nằm sâu hơn vào sảnh
            int centerY = r.yMin + r.height / 2;
            for (int y = centerY - OpeningHalfCells; y + 1 < centerY + OpeningHalfCells; y++)
            {
                if (isFree(new Vector2Int(x0, y)) && isFree(new Vector2Int(x0 + dx, y))
                    && isFree(new Vector2Int(x0, y + 1)) && isFree(new Vector2Int(x0 + dx, y + 1))) return true;
            }
            return false;
        }

        /// Món (footprint đã xoay) đặt ở ô cell nằm gọn trong MỘT khu đã mở.
        public bool Fits(Vector2Int cell, Vector2Int footprint)
        {
            ZoneId z = ZoneAt(cell);
            if (!IsOpen(z)) return false;
            RectInt r = Rects[(int)z];
            return cell.x >= r.xMin && cell.y >= r.yMin && cell.x + footprint.x <= r.xMax && cell.y + footprint.y <= r.yMax;
        }
    }
}
