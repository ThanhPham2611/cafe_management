using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Cafe.EditorTools
{
    /// Dựng Resources/Audio/AudioLibrary.asset từ các file AU_Sfx_<Id>[_n] / AU_Bgm_<Id> trong Art/Audio.
    /// Entry mới lấy mặc định ở bảng Defaults; entry đã có (chỉnh tay) giữ nguyên, chỉ cập nhật danh sách clip.
    public static class AudioLibraryBuilder
    {
        public const string LibraryDir = "Assets/_Project/Resources/Audio";
        public const string LibraryPath = LibraryDir + "/AudioLibrary.asset";

        private struct Def
        {
            public float volume, pitchMin, pitchMax, cooldown;
            public int priority;
            public Def(float v, float pMin, float pMax, float cd, int pr) { volume = v; pitchMin = pMin; pitchMax = pMax; cooldown = cd; priority = pr; }
        }

        // Mặc định theo bảng thiết kế: âm lượng (đã trừ dB), pitch ngẫu nhiên, cooldown (giây thực), ưu tiên (0 = chủ đạo).
        private static readonly Dictionary<Sfx, Def> Defaults = new Dictionary<Sfx, Def>
        {
            { Sfx.UiTap, new Def(0.50f, 0.96f, 1.04f, 0.04f, 1) },
            { Sfx.UiBack, new Def(0.40f, 0.96f, 1.04f, 0.04f, 1) },
            { Sfx.DoorChime, new Def(0.40f, 0.97f, 1.03f, 1.50f, 2) },
            { Sfx.OrderNew, new Def(0.35f, 0.97f, 1.03f, 0.25f, 2) },
            { Sfx.OrderSelect, new Def(0.40f, 0.97f, 1.03f, 0.06f, 1) },
            { Sfx.CustomerTap, new Def(0.35f, 0.92f, 1.08f, 0.40f, 2) },
            { Sfx.StepOk, new Def(0.45f, 0.97f, 1.03f, 0.05f, 1) },
            { Sfx.StepWrong, new Def(0.45f, 0.95f, 1.05f, 0.30f, 1) },
            { Sfx.BrewDone, new Def(0.60f, 1.00f, 1.00f, 0.20f, 1) },
            { Sfx.Ruin, new Def(0.50f, 1.00f, 1.00f, 1.00f, 1) },
            { Sfx.Serve, new Def(0.45f, 0.97f, 1.03f, 0.15f, 2) },
            { Sfx.CoinPay, new Def(0.40f, 0.96f, 1.04f, 0.12f, 2) },
            { Sfx.CustomerAngry, new Def(0.50f, 0.97f, 1.03f, 1.50f, 2) },
            { Sfx.CupPick, new Def(0.45f, 0.97f, 1.03f, 0.10f, 1) },
            { Sfx.ScrapCoin, new Def(0.40f, 0.97f, 1.03f, 0.10f, 1) },
            { Sfx.WipeDone, new Def(0.40f, 0.97f, 1.03f, 0.40f, 1) },
            { Sfx.BuildPlace, new Def(0.70f, 0.97f, 1.03f, 0.20f, 1) },
            { Sfx.Sell, new Def(0.55f, 1.00f, 1.00f, 0.30f, 1) },
            { Sfx.Purchase, new Def(0.60f, 1.00f, 1.00f, 0.30f, 1) },
            { Sfx.LevelUp, new Def(1.00f, 1.00f, 1.00f, 1.00f, 0) },
            { Sfx.Claim, new Def(0.55f, 1.00f, 1.00f, 0.30f, 1) },
            { Sfx.Chest, new Def(0.60f, 1.00f, 1.00f, 0.30f, 1) },
            { Sfx.Fanfare, new Def(1.00f, 1.00f, 1.00f, 1.00f, 0) },
            { Sfx.DayOpen, new Def(0.60f, 1.00f, 1.00f, 1.00f, 2) },
            { Sfx.DayClose, new Def(0.55f, 1.00f, 1.00f, 1.00f, 2) },
        };

        private static readonly Regex SfxName = new Regex(@"^AU_Sfx_([A-Za-z0-9]+?)(?:_(\d+))?$");
        private static readonly Regex BgmName = new Regex(@"^AU_Bgm_([A-Za-z0-9]+)$");

        [MenuItem("Cafe/Audio/Ensure Audio Library")]
        public static void Ensure()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Directory.CreateDirectory(LibraryDir);
            var lib = AssetDatabase.LoadAssetAtPath<AudioLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<AudioLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }

            // Gom clip theo id, biến thể xếp theo số thứ tự
            var sfxClips = new Dictionary<Sfx, SortedDictionary<int, AudioClip>>();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioPostprocessor.SfxDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Match m = SfxName.Match(Path.GetFileNameWithoutExtension(path));
                Sfx id;
                if (!m.Success || !Enum.TryParse(m.Groups[1].Value, out id) || !Enum.IsDefined(typeof(Sfx), id))
                {
                    Debug.LogWarning("[Audio] Bỏ qua (tên không khớp AU_Sfx_<Id>[_n] hoặc Id lạ): " + path);
                    continue;
                }
                int n = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 0;
                SortedDictionary<int, AudioClip> bucket;
                if (!sfxClips.TryGetValue(id, out bucket)) sfxClips[id] = bucket = new SortedDictionary<int, AudioClip>();
                bucket[n] = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }

            var sfxList = new List<AudioLibrary.SfxEntry>(lib.sfx ?? new AudioLibrary.SfxEntry[0]);
            foreach (KeyValuePair<Sfx, SortedDictionary<int, AudioClip>> kv in sfxClips)
            {
                AudioLibrary.SfxEntry e = sfxList.Find(x => x != null && x.id == kv.Key);
                if (e == null)
                {
                    Def d;
                    if (!Defaults.TryGetValue(kv.Key, out d)) d = new Def(0.5f, 1f, 1f, 0.05f, 2);
                    e = new AudioLibrary.SfxEntry { id = kv.Key, volume = d.volume, pitchMin = d.pitchMin, pitchMax = d.pitchMax, cooldown = d.cooldown, priority = d.priority };
                    sfxList.Add(e);
                }
                e.clips = new List<AudioClip>(kv.Value.Values).ToArray();
            }
            sfxList.RemoveAll(x => x == null || (!sfxClips.ContainsKey(x.id)));
            sfxList.Sort((a, b) => ((int)a.id).CompareTo((int)b.id));
            lib.sfx = sfxList.ToArray();

            var bgmList = new List<AudioLibrary.BgmEntry>(lib.bgm ?? new AudioLibrary.BgmEntry[0]);
            var bgmFound = new HashSet<Bgm>();
            foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { AudioPostprocessor.BgmDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Match m = BgmName.Match(Path.GetFileNameWithoutExtension(path));
                Bgm id;
                if (!m.Success || !Enum.TryParse(m.Groups[1].Value, out id) || id == Bgm.None || !Enum.IsDefined(typeof(Bgm), id))
                {
                    Debug.LogWarning("[Audio] Bỏ qua (tên không khớp AU_Bgm_<Id> hoặc Id lạ): " + path);
                    continue;
                }
                AudioLibrary.BgmEntry e = bgmList.Find(x => x != null && x.id == id);
                if (e == null)
                {
                    e = new AudioLibrary.BgmEntry { id = id, volume = 0.8f };
                    bgmList.Add(e);
                }
                e.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                bgmFound.Add(id);
            }
            bgmList.RemoveAll(x => x == null || !bgmFound.Contains(x.id));
            bgmList.Sort((a, b) => ((int)a.id).CompareTo((int)b.id));
            lib.bgm = bgmList.ToArray();

            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
            Debug.Log("[Audio] AudioLibrary: " + lib.sfx.Length + " SFX, " + lib.bgm.Length + " nhạc.");
        }

        /// Nghe thử từng SFX lần lượt (cần đang ở Play Mode; host âm thanh có sẵn).
        [MenuItem("Cafe/Audio/Test Sfx (Play Mode)")]
        public static void TestSfx()
        {
            if (!Application.isPlaying || !Audio.Enabled)
            {
                Debug.LogWarning("[Audio] Vào Play Mode trước (host âm thanh chỉ chạy khi chơi).");
                return;
            }
            EditorApplication.CallbackFunction step = null;
            var ids = (Sfx[])Enum.GetValues(typeof(Sfx));
            int i = 0;
            double next = EditorApplication.timeSinceStartup;
            step = () =>
            {
                if (!Application.isPlaying || i >= ids.Length) { EditorApplication.update -= step; return; }
                if (EditorApplication.timeSinceStartup < next) return;
                Debug.Log("[Audio] Test " + ids[i]);
                Audio.Play(ids[i], 1f, false);
                i++;
                next = EditorApplication.timeSinceStartup + 1.6;
            };
            EditorApplication.update += step;
        }
    }
}
