using UnityEngine;

namespace Cafe
{
    /// Lưu/đọc save tại persistentDataPath/save_v1.json (ghi an toàn + bản sao lưu: xem SaveStore / SaveEnvelope).
    public static class SaveSystem
    {
        public const int CurrentVersion = SaveData.CurrentVersion;

        private static SaveStore store;
        private static SaveStore Store { get { return store ?? (store = new SaveStore(Application.persistentDataPath)); } }

        /// Kết quả lần đọc gần nhất (GameBootstrap dùng để báo người chơi).
        public static SaveLoadResult LastLoad { get; private set; }
        /// Save của phiên bản mới hơn / lỗi đọc: không ghi để khỏi ghi đè.
        public static bool IsLocked { get { return Store.Locked; } }

        public static SaveLoadResult Load()
        {
            LastLoad = Store.Load();
            return LastLoad;
        }

        public static bool TryLoad(out SaveData data)
        {
            data = Load().data;
            return data != null;
        }

        public static bool Save(SaveData data) { return Store.Save(data); }

        public static void Delete() { Store.Delete(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            store = null;
            LastLoad = default(SaveLoadResult);
        }
    }
}
