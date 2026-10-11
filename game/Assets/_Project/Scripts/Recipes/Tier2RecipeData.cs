namespace Cafe
{
    /// Dữ liệu 4 công thức Tier 2 (economy_monetization.md §2.1-2.2): MỘT nguồn cho cả Editor (RecipeImporter tạo asset)
    /// lẫn runtime (RecipeBook tự tạo bản trong bộ nhớ nếu scene dựng trước chưa gán các asset này vào GameBootstrap.recipes).
    public static class Tier2RecipeData
    {
        public struct Entry
        {
            public string id, displayName, hint;
            public int price, cost;
            public float seconds, weight;
            public bool hidden;
            public string[] tools;
            public BrewStep[] steps;
        }

        public static readonly Entry[] All =
        {
            // Trà chanh: mở T2, hiện sẵn, món "rush hour" (Xu/giây pha cao nhất)
            new Entry
            {
                id = "tra_chanh", displayName = "Trà chanh", price = 60, cost = 15, seconds = 3f, weight = 1.4f,
                hidden = false, hint = null, tools = null,
                steps = new[] { BrewStep.Tra, BrewStep.Chanh, BrewStep.Da },
            },
            // Cà phê muối: phin -> sữa đặc -> đá -> phủ kem muối (vuốt đổ)
            new Entry
            {
                id = "ca_phe_muoi", displayName = "Cà phê muối", price = 180, cost = 55, seconds = 9f, weight = 0.8f,
                hidden = true, hint = "Một vị mặn bất ngờ từ Huế…", tools = new[] { "phin" },
                steps = new[] { BrewStep.Phin, BrewStep.SuaDac, BrewStep.Da, BrewStep.KemMuoi },
            },
            // Cà phê cốt dừa: phin -> cốt dừa -> đá -> đánh bông (xoay)
            new Entry
            {
                id = "ca_phe_cot_dua", displayName = "Cà phê cốt dừa", price = 220, cost = 70, seconds = 12f, weight = 0.6f,
                hidden = true, hint = "Béo thơm như trái dừa miền Tây, đánh cho bông lên…", tools = new[] { "phin" },
                steps = new[] { BrewStep.Phin, BrewStep.CotDua, BrewStep.Da, BrewStep.Danh },
            },
            // Cà phê trứng: lòng đỏ -> sữa đặc -> đánh bông (xoay) -> rót lên phin. Cần máy đánh trứng.
            new Entry
            {
                id = "ca_phe_trung", displayName = "Cà phê trứng", price = 250, cost = 80, seconds = 14f, weight = 0.5f,
                hidden = true, hint = "Lớp bọt béo mịn nổi tiếng của phố cổ Hà Nội…", tools = new[] { "phin", "may_danh_trung" },
                steps = new[] { BrewStep.Trung, BrewStep.SuaDac, BrewStep.Danh, BrewStep.Phin },
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
            r.unlockTier = 2;
            r.unlockDay = 0;
            r.orderWeight = e.weight;
            r.hidden = e.hidden;
            r.hint = e.hint;
            r.requiredTools = e.tools;
            r.hintCostGems = 30;
        }
    }
}
