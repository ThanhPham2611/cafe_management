using System;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Cafe
{
    /// Kết quả đọc save (SaveStore.Load).
    public enum SaveStatus { Ok, Missing, RestoredFromBackup, Corrupt, FutureVersion }

    /// Định dạng file save: "CAFESAVE1 <độ dài thân> <sha256>\n<json UTF-8>". Vẫn đọc được JSON trần của bản cũ (bắt đầu bằng '{').
    /// Thuần: không IO, để test được. Chỉ kiểm tra toàn vẹn (cụt / hỏng), không chống gian lận.
    public static class SaveEnvelope
    {
        public const string Magic = "CAFESAVE";
        public const int FormatVersion = 1;

        public static byte[] Wrap(string json)
        {
            byte[] body = new UTF8Encoding(false).GetBytes(json ?? "");
            byte[] header = Encoding.ASCII.GetBytes(Magic + FormatVersion + " " + body.Length + " " + Hash(body) + "\n");
            var all = new byte[header.Length + body.Length];
            Buffer.BlockCopy(header, 0, all, 0, header.Length);
            Buffer.BlockCopy(body, 0, all, header.Length, body.Length);
            return all;
        }

        /// Ok (data != null, chưa Migrate) | Corrupt | FutureVersion (định dạng hoặc version dữ liệu mới hơn bản này).
        public static SaveStatus Parse(byte[] raw, out SaveData data)
        {
            data = null;
            if (raw == null || raw.Length == 0) return SaveStatus.Corrupt;

            int i = 0;
            if (raw.Length >= 3 && raw[0] == 0xEF && raw[1] == 0xBB && raw[2] == 0xBF) i = 3; // BOM
            while (i < raw.Length && (raw[i] == ' ' || raw[i] == '\t' || raw[i] == '\r' || raw[i] == '\n')) i++;
            if (i >= raw.Length) return SaveStatus.Corrupt;

            if (raw[i] == (byte)'{') return ParseJson(Encoding.UTF8.GetString(raw, i, raw.Length - i), out data);
            if (!StartsWith(raw, i, Magic)) return SaveStatus.Corrupt;

            int p = i + Magic.Length;
            long fmt;
            if (!ReadNumber(raw, ref p, out fmt)) return SaveStatus.Corrupt;
            if (fmt > FormatVersion) return SaveStatus.FutureVersion;
            if (fmt < 1 || p >= raw.Length || raw[p] != (byte)' ') return SaveStatus.Corrupt;
            p++;
            long len;
            if (!ReadNumber(raw, ref p, out len)) return SaveStatus.Corrupt;
            if (p >= raw.Length || raw[p] != (byte)' ') return SaveStatus.Corrupt;
            p++;
            if (p + 64 >= raw.Length || raw[p + 64] != (byte)'\n') return SaveStatus.Corrupt;
            string hash = Encoding.ASCII.GetString(raw, p, 64);
            int bodyStart = p + 65;
            if (len != raw.Length - bodyStart) return SaveStatus.Corrupt; // cụt hoặc thừa

            var body = new byte[len];
            Buffer.BlockCopy(raw, bodyStart, body, 0, (int)len);
            if (!string.Equals(Hash(body), hash, StringComparison.Ordinal)) return SaveStatus.Corrupt;
            return ParseJson(Encoding.UTF8.GetString(body), out data);
        }

        private static SaveStatus ParseJson(string text, out SaveData data)
        {
            data = null;
            string t = text.TrimEnd();
            // "{}" hay mảng sẽ được JsonUtility đọc thành save mặc định rỗng: đòi có "version" và đóng ngoặc.
            if (t.Length < 2 || t[t.Length - 1] != '}' || t.IndexOf("\"version\"", StringComparison.Ordinal) < 0) return SaveStatus.Corrupt;
            SaveData d;
            try { d = JsonUtility.FromJson<SaveData>(t); }
            catch (ArgumentException) { return SaveStatus.Corrupt; }
            if (d == null || d.version < 1) return SaveStatus.Corrupt;
            if (d.version > SaveData.CurrentVersion) return SaveStatus.FutureVersion;
            data = d;
            return SaveStatus.Ok;
        }

        private static bool StartsWith(byte[] raw, int at, string s)
        {
            if (at + s.Length > raw.Length) return false;
            for (int k = 0; k < s.Length; k++) if (raw[at + k] != (byte)s[k]) return false;
            return true;
        }

        private static bool ReadNumber(byte[] raw, ref int p, out long value)
        {
            value = 0;
            int start = p;
            while (p < raw.Length && raw[p] >= (byte)'0' && raw[p] <= (byte)'9' && p - start < 12)
            {
                value = value * 10 + (raw[p] - (byte)'0');
                p++;
            }
            return p > start;
        }

        private static string Hash(byte[] body)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] h = sha.ComputeHash(body);
                var sb = new StringBuilder(64);
                for (int k = 0; k < h.Length; k++) sb.Append(h[k].ToString("x2"));
                return sb.ToString();
            }
        }
    }
}
