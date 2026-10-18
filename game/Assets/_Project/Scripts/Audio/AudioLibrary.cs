using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cafe
{
    /// Bảng id -> clip. Asset ở Resources/Audio/AudioLibrary.asset (menu Cafe/Audio/Ensure Audio Library dựng từ thư mục Art/Audio).
    [CreateAssetMenu(menuName = "Cafe/Audio Library", fileName = "AudioLibrary")]
    public sealed class AudioLibrary : ScriptableObject
    {
        [Serializable]
        public class SfxEntry
        {
            public Sfx id;
            public AudioClip[] clips;
            [Range(0f, 1f)] public float volume = 1f;
            public float pitchMin = 1f;
            public float pitchMax = 1f;
            public float cooldown = 0.05f;
            [Tooltip("0 = âm chủ đạo (không bị cắt), 1 = thao tác người chơi, 2 = sự kiện trong quán, 3 = nền")]
            public int priority = 2;
        }

        [Serializable]
        public class BgmEntry
        {
            public Bgm id;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
        }

        public SfxEntry[] sfx = new SfxEntry[0];
        public BgmEntry[] bgm = new BgmEntry[0];

        private Dictionary<Sfx, SfxEntry> sfxMap;
        private Dictionary<Bgm, BgmEntry> bgmMap;

        public bool TryGet(Sfx id, out SfxEntry entry)
        {
            if (sfxMap == null)
            {
                sfxMap = new Dictionary<Sfx, SfxEntry>();
                if (sfx != null)
                    foreach (SfxEntry e in sfx)
                        if (e != null && !sfxMap.ContainsKey(e.id)) sfxMap.Add(e.id, e); // trùng id: giữ cái đầu
            }
            return sfxMap.TryGetValue(id, out entry) && entry.clips != null && entry.clips.Length > 0;
        }

        public bool TryGet(Bgm id, out BgmEntry entry)
        {
            if (bgmMap == null)
            {
                bgmMap = new Dictionary<Bgm, BgmEntry>();
                if (bgm != null)
                    foreach (BgmEntry e in bgm)
                        if (e != null && !bgmMap.ContainsKey(e.id)) bgmMap.Add(e.id, e);
            }
            return bgmMap.TryGetValue(id, out entry) && entry.clip != null;
        }

        private void OnValidate()
        {
            sfxMap = null;
            bgmMap = null;
        }
    }
}
