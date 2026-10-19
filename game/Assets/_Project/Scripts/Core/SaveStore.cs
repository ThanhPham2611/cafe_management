using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Cafe
{
    public struct SaveLoadResult
    {
        public SaveStatus status;
        public SaveData data;   // khác null khi Ok / RestoredFromBackup (đã Migrate)
        public string detail;
    }

    /// Toàn bộ IO của save trong một thư mục: ghi an toàn (tmp -> bak -> main), đọc main rồi bak, cách ly file hỏng.
    /// Không bao giờ ghi đè file không đọc được: bản hỏng được đổi tên "*.corrupt-<giờ>", bản của phiên bản mới hơn khóa việc ghi.
    public sealed class SaveStore
    {
        public const string MainName = "save_v1.json";
        private const int KeepQuarantined = 3;

        private readonly string dir;
        private readonly string main, tmp, bak;
        private byte[] lastFile;
        private bool warnedLocked;

        /// Đã gặp save của phiên bản mới hơn (hoặc lỗi IO khi đọc): mọi Save bị chặn để không ghi đè.
        public bool Locked { get; private set; }

        public SaveStore(string directory)
        {
            dir = directory;
            main = Path.Combine(dir, MainName);
            tmp = main + ".tmp";
            bak = main + ".bak";
        }

        public SaveLoadResult Load()
        {
            try
            {
                DeleteQuiet(tmp); // dở dang của lần ghi bị ngắt
                bool hasMain = File.Exists(main), hasBak = File.Exists(bak);
                if (!hasMain && !hasBak) return Result(SaveStatus.Missing, null);

                SaveData d;
                if (hasMain)
                {
                    SaveStatus st = SaveEnvelope.Parse(File.ReadAllBytes(main), out d);
                    if (st == SaveStatus.Ok) return Done(SaveStatus.Ok, d);
                    if (st == SaveStatus.FutureVersion) return Lock("save của phiên bản mới hơn");
                }
                if (hasBak)
                {
                    SaveStatus st = SaveEnvelope.Parse(File.ReadAllBytes(bak), out d);
                    if (st == SaveStatus.FutureVersion) return Lock("bản sao lưu của phiên bản mới hơn");
                    if (st == SaveStatus.Ok)
                    {
                        if (hasMain) Quarantine(main, "");
                        return Done(SaveStatus.RestoredFromBackup, d);
                    }
                }
                if (hasMain) Quarantine(main, "");
                if (hasBak) Quarantine(bak, "-bak");
                Debug.LogWarning("[SaveSystem] Save hỏng và không có bản sao lưu dùng được; đã cách ly tệp cũ.");
                return Result(SaveStatus.Corrupt, null);
            }
            catch (Exception e)
            {
                // Lỗi IO tạm thời: không cách ly, không ghi đè.
                Locked = true;
                Debug.LogWarning("[SaveSystem] Không đọc được: " + e.Message);
                var r = Result(SaveStatus.Corrupt, null);
                r.detail = e.Message;
                return r;
            }
        }

        public bool Save(SaveData data)
        {
            if (data == null) return false;
            if (Locked)
            {
                if (!warnedLocked) Debug.LogWarning("[SaveSystem] Save đang bị khóa (dữ liệu của phiên bản mới hơn hoặc lỗi đọc): không ghi.");
                warnedLocked = true;
                return false;
            }
            try
            {
                data.version = SaveData.CurrentVersion;
                byte[] file = SaveEnvelope.Wrap(JsonUtility.ToJson(data));
                if (lastFile != null && File.Exists(main) && file.SequenceEqual(lastFile)) return true; // pause rồi quit: không ghi hai lần

                Directory.CreateDirectory(dir);
                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    fs.Write(file, 0, file.Length);
                    fs.Flush(true); // fsync
                }
                SaveData check;
                if (SaveEnvelope.Parse(File.ReadAllBytes(tmp), out check) != SaveStatus.Ok)
                {
                    DeleteQuiet(tmp);
                    return false; // main giữ nguyên
                }
                if (File.Exists(main))
                {
                    SaveData cur;
                    SaveStatus m = SaveEnvelope.Parse(File.ReadAllBytes(main), out cur);
                    if (m == SaveStatus.Ok) File.Copy(main, bak, true); // chỉ bản hợp lệ mới thành .bak
                    else if (m == SaveStatus.FutureVersion)
                    {
                        Locked = true;
                        DeleteQuiet(tmp);
                        return false;
                    }
                    else Quarantine(main, ""); // main hỏng không bao giờ đè lên .bak tốt
                }
                try
                {
                    if (File.Exists(main)) File.Delete(main);
                    File.Move(tmp, main);
                }
                catch (Exception)
                {
                    File.Copy(tmp, main, true); // dự phòng (.NET Standard 2.1 không có File.Move ghi đè)
                    DeleteQuiet(tmp);
                }
                lastFile = file;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SaveSystem] Không lưu được: " + e.Message);
                return false;
            }
        }

        /// Xóa mọi tệp save (chỉ dùng cho bot -freshsave) và mở khóa.
        public void Delete()
        {
            DeleteQuiet(main);
            DeleteQuiet(tmp);
            DeleteQuiet(bak);
            foreach (string q in Quarantined()) DeleteQuiet(q);
            Locked = false;
            warnedLocked = false;
            lastFile = null;
        }

        private SaveLoadResult Done(SaveStatus status, SaveData d)
        {
            d.Migrate();
            lastFile = null;
            return Result(status, d);
        }

        private SaveLoadResult Lock(string why)
        {
            Locked = true;
            Debug.LogWarning("[SaveSystem] " + why + ": không ghi đè.");
            var r = Result(SaveStatus.FutureVersion, null);
            r.detail = why;
            return r;
        }

        private static SaveLoadResult Result(SaveStatus status, SaveData d)
        {
            return new SaveLoadResult { status = status, data = d };
        }

        private string[] Quarantined()
        {
            if (!Directory.Exists(dir)) return new string[0];
            return Directory.GetFiles(dir, MainName + ".corrupt-*");
        }

        private void Quarantine(string path, string tag)
        {
            string stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            string target = main + ".corrupt-" + stamp + tag;
            for (int n = 1; File.Exists(target); n++) target = main + ".corrupt-" + stamp + tag + "-" + n;
            File.Move(path, target);
            string[] all = Quarantined().OrderByDescending(p => Path.GetFileName(p), StringComparer.Ordinal).ToArray();
            for (int k = KeepQuarantined; k < all.Length; k++) DeleteQuiet(all[k]);
        }

        private static void DeleteQuiet(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception) { }
        }
    }
}
