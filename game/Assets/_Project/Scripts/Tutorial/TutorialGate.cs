using UnityEngine;

namespace Cafe
{
    /// Cờ toàn cục mà gameplay đọc khi đang hướng dẫn (móc nhỏ trong CustomerSpawner, Customer, BrewStation, DayCycle).
    /// TutorialDirector là nơi duy nhất ghi các cờ này; hết hướng dẫn thì Reset() trả mọi thứ về bình thường.
    public static class TutorialGate
    {
        /// Phần lõi ngày 1 còn chạy (chưa xong / chưa bỏ qua).
        public static bool Active;
        /// Khách đứng chờ món không mất kiên nhẫn.
        public static bool PatiencePaused;
        /// Sai bước khi pha chỉ nhắc, không tính lỗi.
        public static bool NoPenalty;
        /// Đồng hồ ngày đứng yên (lịch sinh khách cũng đứng theo).
        public static bool ClockHold;
        /// Số khách tối đa được sinh ra từ đầu ngày.
        public static int AllowedSpawns = int.MaxValue;
        /// Chỉ số đơn kế tiếp sẽ nhận món ép (xem TutorialRules.ForcedRecipes).
        public static int NextForcedOrder;
        /// Số khách đã sinh (Director cập nhật để biết khi nào thả xong).
        public static int Spawned;
        /// Bước hiện tại (để đọc nhanh ở những chỗ khác, vd. bot).
        public static int Step;

        private static float lastForcedSpawn = -100f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            Active = false;
            PatiencePaused = false;
            NoPenalty = false;
            ClockHold = false;
            AllowedSpawns = int.MaxValue;
            NextForcedOrder = 0;
            Spawned = 0;
            Step = 0;
            lastForcedSpawn = -100f;
        }

        /// CustomerSpawner: khách thứ index đã tới giờ chưa? Khi đồng hồ đứng thì thả lần lượt, cách nhau ~2 giây.
        public static bool SpawnIsDue(int index, float scheduleTime, float elapsed)
        {
            if (!Active) return elapsed >= scheduleTime;
            if (index >= AllowedSpawns) return false;
            if (!ClockHold) return elapsed >= scheduleTime;
            if (Time.time - lastForcedSpawn < 2f) return false;
            lastForcedSpawn = Time.time;
            return true;
        }

        /// Customer.PlaceOrder: món ép cho đơn này (null = chọn ngẫu nhiên). Mỗi lần gọi dùng hết một lượt ép.
        public static RecipeDef TakeForcedRecipe(GameBootstrap game)
        {
            if (!Active || game == null || game.Recipes == null) return null;
            string id = TutorialRules.ForcedRecipeFor(NextForcedOrder);
            if (id == null) return null;
            NextForcedOrder++;
            return game.Recipes.Get(id);
        }
    }
}
