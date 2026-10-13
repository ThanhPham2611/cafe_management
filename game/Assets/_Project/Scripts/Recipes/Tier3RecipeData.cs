namespace Cafe
{
    /// Dữ liệu 3 công thức cấp 3 (economy_monetization.md §2.1 bảng T3). Cùng cách dùng như Tier2RecipeData:
    /// Editor (RecipeImporter) tạo asset từ bảng này; runtime (RecipeBook) tự tạo bản trong bộ nhớ nếu scene chưa có.
    /// Món cấp 3 không ẩn: lên cấp 3 là tự biết. Món cần máy (requiredMachine) chỉ có khách gọi khi máy đã đặt trong quán.
    /// Dùng lại các bước có sẵn (khay nguyên liệu không đổi): Chanh = cam/chanh vàng, Lac = lắc shaker.
    public static class Tier3RecipeData
    {
        public struct Entry
        {
            public string id, displayName, requiredMachine;
            public int price, cost;
            public float seconds, weight;
            public BrewStep[] steps;
        }

        public static readonly Entry[] All =
        {
            // Bạc xỉu kem muối: không cần máy, món "đại trà" của nhà hàng
            new Entry
            {
                id = "bac_xiu_kem_muoi", displayName = "Bạc xỉu kem muối", price = 230, cost = 75, seconds = 10f, weight = 1.0f,
                requiredMachine = null,
                steps = new[] { BrewStep.SuaDac, BrewStep.SuaTuoi, BrewStep.Phin, BrewStep.Da, BrewStep.KemMuoi },
            },
            // Cold brew cam sả: ủ sẵn nên rất nhanh, nhưng cần máy cold brew
            new Entry
            {
                id = "cold_brew_cam_sa", displayName = "Cold brew cam sả", price = 200, cost = 60, seconds = 6f, weight = 1.2f,
                requiredMachine = MachineIds.ColdBrew,
                steps = new[] { BrewStep.Phin, BrewStep.Lac, BrewStep.Da, BrewStep.Chanh },
            },
            // Phin Robusta Cầu Đất: đặc sản đắt nhất, cần máy espresso
            new Entry
            {
                id = "phin_robusta_cau_dat", displayName = "Phin Robusta Cầu Đất", price = 300, cost = 100, seconds = 15f, weight = 0.7f,
                requiredMachine = MachineIds.Espresso,
                steps = new[] { BrewStep.Phin, BrewStep.Duong, BrewStep.Lac, BrewStep.Da },
            },
        };

        public static void Apply(RecipeDef r, Entry e)
        {
            r.id = e.id;
            r.displayName = e.displayName;
            r.price = e.price;
            r.ingredientCost = e.cost;
            r.brewSeconds = e.seconds;
            r.steps = e.steps;
            r.unlockTier = 3;
            r.unlockDay = 0;
            r.orderWeight = e.weight;
            r.hidden = false;
            r.hint = null;
            r.requiredTools = null;
            r.requiredMachine = e.requiredMachine;
        }
    }
}
