using System;

namespace Cafe
{
    /// Ngoại hình chủ quán (GDD §2.1). Swatch = chỉ số ô palette (art_bible §3).
    [Serializable]
    public struct OwnerLook
    {
        public int preset;      // 0..3: dáng nam 1, nam 2, nữ 1, nữ 2 (PF_Char_Owner_*)
        public int skinSwatch;  // 52..55
        public int hairSwatch;  // 62, 7, 1, 0, 61, 59
        public int shirtSwatch; // hàng 3, 4, 7

        public static OwnerLook Default => new OwnerLook { preset = 0, skinSwatch = 53, hairSwatch = 62, shirtSwatch = 31 };
    }

    /// Dữ liệu lưu (JsonUtility). Tăng version khi đổi định dạng.
    ///   v1: xu, day, rp, layout
    ///   v2: + chủ quán (tên, ngoại hình), tên quán, cờ đã tạo nhân vật, số lần đã mua theo id
    ///   v3: + cấp quán (tier), nợ thuê, công thức đã biết + menu hôm nay, mức dụng cụ, nhân viên, khách quen, Hạt, kho đồ
    [Serializable]
    public class SaveData
    {
        public const int CurrentVersion = 3;

        public int version = CurrentVersion;
        public int xu;
        public int day = 1;
        public float rp;
        public int levelRewarded;            // level uy tín cao nhất đã nhận thưởng (0 = save cũ); xem LevelRules
        public LayoutEntry[] layout;

        // v2
        public bool creatorDone;
        public string ownerName = "";
        public string shopName = "";
        public OwnerLook ownerLook = OwnerLook.Default;
        public PurchaseCount[] purchases;

        // v3 (phase2_spec.md)
        public int tier = 1;                 // 1 vỉa hè, 2 thuê mặt bằng, 3 mua nhà hàng, 4 chuỗi
        public int deposit;                  // tiền cọc đang giữ (18.000 ở cấp 2), hoàn khi lên cấp 3
        public int rentDebt;                // nợ tiền thuê/lương (Xu), trừ 30% doanh thu mỗi ngày
        public int daysSinceRent;            // đếm ngày tới kỳ thu tiền thuê kế tiếp (7 ngày/tuần)
        public int gems;                     // Hạt cà phê vàng
        public string[] knownRecipes;        // id công thức đã mở khóa
        public string[] menuToday;           // id món đang bật trong menu
        public PurchaseCount[] toolLevels;   // furnitureId/toolId -> cấp dụng cụ
        public PurchaseCount[] inventory;    // đồ trong kho (đã mua, chưa đặt) theo furnitureId
        public StaffSave[] staff;
        public RegularSave[] regulars;

        // v3 (thêm sau, không đổi version vì chỉ thêm trường: file cũ đọc ra mặc định)
        public MissionSave[] missions;       // nhiệm vụ ngày hiện tại
        public int missionDayKey;            // ngày thật (MissionClock.DayKey) của bộ nhiệm vụ
        public bool missionChestClaimed;

        // Các khu của nhà hàng (cấp 3). Chỉ thêm trường: file cũ đọc ra 0.
        public int zonesOpen;                // mặt nạ khu phụ đã mở (ZoneInfo.Bit): sân vườn 2, lầu 4, VIP 8
        public int tutorialStep;             // hướng dẫn phần lõi: 0 chưa bắt đầu / save cũ, 1..8 đang làm, 9 xong hoặc đã bỏ qua (TutorialStep)
        public string[] tutorialSeen;        // mã gợi ý theo tình huống đã hiện (TutorialTips)
        public int gridVersion;              // 0 = lưới nhà hàng cũ 20 x 24 (chưa có khu), 1 = lưới 44 x 24 (sảnh bắt đầu ở ô x = 12)

        /// Nâng file cũ lên định dạng hiện tại (gọi ngay sau khi load).
        public void Migrate()
        {
            if (version < 2)
            {
                creatorDone = false; // người chơi cũ sẽ được mời tạo nhân vật
                ownerLook = OwnerLook.Default;
            }
            if (version < 3)
            {
                tier = 1;
                knownRecipes = new[] { "tra_da", "ca_phe_den", "ca_phe_sua", "bac_xiu" };
                menuToday = knownRecipes;
            }
            version = CurrentVersion;
        }
    }

    /// Đếm số món đã mua theo furnitureId (giá ghế tăng dần theo số đã có).
    [Serializable]
    public struct PurchaseCount
    {
        public string furnitureId;
        public int count;
    }

    /// Nhân viên đã thuê. role: "clean" | "serve" | "brew" (cashier/manager để sau).
    [Serializable]
    public struct StaffSave
    {
        public string role;
        public int preset;   // 0..3 = PF_Char_Staff_M1, M2, F1, F2
        public int level;    // 1..5
        public string name;
    }

    /// Một nhiệm vụ ngày đã lưu (id tra trong MissionCatalog).
    [Serializable]
    public struct MissionSave
    {
        public string id;
        public int target;
        public int progress;
        public int rewardXu;
        public bool claimed;
    }

    /// Tiến độ một khách quen.
    [Serializable]
    public struct RegularSave
    {
        public string id;
        public int bond;           // 0..10
        public int chaptersDone;   // số chương đã xem
        public int lastVisitDay;
    }
}
