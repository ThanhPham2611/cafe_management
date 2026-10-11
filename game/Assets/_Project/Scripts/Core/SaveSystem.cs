using System;
using System.IO;
using UnityEngine;

namespace Cafe
{
    /// Lưu/đọc JSON tại persistentDataPath/save_v1.json.
    public static class SaveSystem
    {
        public const int CurrentVersion = SaveData.CurrentVersion;
        private const string FileName = "save_v1.json";

        private static string FilePath
        {
            get { return Path.Combine(Application.persistentDataPath, FileName); }
        }

        public static bool Save(SaveData data)
        {
            if (data == null) return false;
            try
            {
                data.version = CurrentVersion;
                File.WriteAllText(FilePath, JsonUtility.ToJson(data));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveSystem] Không lưu được: " + e.Message);
                return false;
            }
        }

        public static bool TryLoad(out SaveData data)
        {
            data = null;
            try
            {
                string p = FilePath;
                if (!File.Exists(p)) return false;
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(p));
                if (data == null || data.version < 1 || data.version > SaveData.CurrentVersion)
                {
                    data = null;
                    return false;
                }
                data.Migrate(); // v1 -> v2: giữ tiền/ngày/bố cục, mời tạo nhân vật
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveSystem] Không đọc được: " + e.Message);
                data = null;
                return false;
            }
        }

        public static void Delete()
        {
            try { if (File.Exists(FilePath)) File.Delete(FilePath); }
            catch (Exception) { }
        }
    }
}
