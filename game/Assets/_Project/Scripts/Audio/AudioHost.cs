using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cafe
{
    /// Vật chủ âm thanh (DontDestroyOnLoad): pool AudioSource cho SFX, hai nguồn nhạc crossfade, AudioListener duy nhất.
    /// Tự tạo trước mọi scene (kể cả khi mở thẳng Prototype_Tier1, bỏ qua Boot). Mọi fade / cooldown dùng thời gian thực vì
    /// StoryDialog đặt timeScale = 0.
    public sealed class AudioHost : MonoBehaviour
    {
        public static AudioHost Instance { get; private set; }

        private const int VoiceCount = 10;
        private const float MusicFadeSeconds = 1.2f;
        private const float DuckFadeSeconds = 0.25f;

        private AudioLibrary library;
        private AudioSource[] voices;
        private int[] voicePriority;
        private float[] voiceStart;
        private bool[] voiceBusy;
        private readonly Dictionary<Sfx, float> lastPlayed = new Dictionary<Sfx, float>();
        private readonly Dictionary<Sfx, int> lastVariant = new Dictionary<Sfx, int>();
        private readonly HashSet<Sfx> warned = new HashSet<Sfx>();

        private AudioSource[] music;
        private float[] musicGain;
        private float[] musicBase;      // volume gốc của clip (entry.volume)
        private int activeMusic;
        private Bgm currentBgm;
        private bool duck;
        private float duckGain = 1f;
        private AudioBridge bridge;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            AudioPrefs.Init();
            if (Instance != null) return;
            if (!AudioRules.EnabledForRun(Environment.GetCommandLineArgs(), Application.isBatchMode)) return;
            var go = new GameObject("[Audio]");
            DontDestroyOnLoad(go);
            go.AddComponent<AudioHost>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            library = Resources.Load<AudioLibrary>("Audio/AudioLibrary");
            if (library == null) Debug.LogWarning("[Audio] Chưa có Resources/Audio/AudioLibrary (menu Cafe/Audio/Ensure Audio Library): game chạy im lặng.");

            voices = new AudioSource[VoiceCount];
            voicePriority = new int[VoiceCount];
            voiceStart = new float[VoiceCount];
            voiceBusy = new bool[VoiceCount];
            for (int i = 0; i < VoiceCount; i++) voices[i] = NewSource("Sfx" + i, false);

            music = new[] { NewSource("MusicA", true), NewSource("MusicB", true) };
            musicGain = new float[2];
            musicBase = new[] { 1f, 1f };

            EnsureSingleListener();
            SceneManager.sceneLoaded += OnSceneLoaded;
            AudioPrefs.Changed += OnPrefsChanged;
            bridge = new AudioBridge();
            bridge.Attach();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            AudioPrefs.Changed -= OnPrefsChanged;
            if (bridge != null) bridge.Detach();
            Instance = null;
        }

        private AudioSource NewSource(string n, bool loop)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0f;
            return s;
        }

        // ---- Listener: host giữ AudioListener duy nhất, tắt cái của camera trong scene ----

        private AudioListener ownListener;

        private void EnsureSingleListener()
        {
            if (ownListener == null) ownListener = gameObject.AddComponent<AudioListener>();
            foreach (AudioListener l in FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l != ownListener && l.enabled) l.enabled = false;
        }

        private void OnSceneLoaded(Scene s, LoadSceneMode mode) { EnsureSingleListener(); }

        private void OnApplicationPause(bool paused)
        {
            AudioListener.pause = paused;
        }

        // ---- SFX ----

        public void PlaySfx(Sfx id, float volumeScale)
        {
            if (library == null) return;
            float gain = AudioPrefs.SfxGain;
            if (gain <= 0f) return;

            AudioLibrary.SfxEntry e;
            if (!library.TryGet(id, out e))
            {
                if (warned.Add(id)) Debug.LogWarning("[Audio] Thiếu clip cho " + id + " (đặt file AU_Sfx_" + id + ".wav rồi chạy Cafe/Audio/Ensure Audio Library).");
                return;
            }

            float now = Time.realtimeSinceStartup;
            float last;
            if (lastPlayed.TryGetValue(id, out last) && !AudioRules.CooldownReady(now, last, e.cooldown)) return;

            for (int i = 0; i < VoiceCount; i++) voiceBusy[i] = voices[i].isPlaying;
            int v = AudioRules.PickVoice(voiceBusy, voicePriority, voiceStart, e.priority);
            if (v < 0) return;

            int lv;
            if (!lastVariant.TryGetValue(id, out lv)) lv = -1;
            int variant = AudioRules.NextVariant(e.clips.Length, lv, UnityEngine.Random.value);
            if (variant < 0 || e.clips[variant] == null) return;
            lastVariant[id] = variant;
            lastPlayed[id] = now;

            AudioSource s = voices[v];
            s.Stop();
            s.clip = e.clips[variant];
            s.pitch = AudioRules.Pitch(e.pitchMin, e.pitchMax, UnityEngine.Random.value);
            s.volume = Mathf.Clamp01(e.volume * gain * volumeScale);
            voicePriority[v] = e.priority;
            voiceStart[v] = now;
            s.Play();
        }

        // ---- Nhạc ----

        public void PlayMusic(Bgm id)
        {
            if (id == currentBgm) return;
            currentBgm = id;
            int next = 1 - activeMusic;
            AudioLibrary.BgmEntry e = null;
            if (id != Bgm.None && library != null && library.TryGet(id, out e))
            {
                AudioSource s = music[next];
                s.clip = e.clip;
                musicBase[next] = e.volume;
                musicGain[next] = 0f;
                s.volume = 0f;
                s.Play();
                activeMusic = next; // nguồn cũ tự giảm về 0 rồi dừng trong Update
            }
            else
            {
                activeMusic = next; // không có nhạc mới: cả hai nguồn giảm về 0
                if (music[next].isPlaying) music[next].Stop();
                musicGain[next] = 0f;
            }
        }

        public void SetDuck(bool on) { duck = on; }

        private void OnPrefsChanged()
        {
            ApplyMusicVolumes();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            duckGain = AudioRules.Fade(duckGain, AudioRules.DuckFactor(duck, Time.timeScale <= 0f), dt, DuckFadeSeconds);
            for (int i = 0; i < 2; i++)
            {
                bool isActive = i == activeMusic && currentBgm != Bgm.None && music[i].clip != null;
                musicGain[i] = AudioRules.Fade(musicGain[i], isActive ? 1f : 0f, dt, MusicFadeSeconds);
                if (!isActive && musicGain[i] <= 0f && music[i].isPlaying) music[i].Stop();
            }
            ApplyMusicVolumes();
        }

        private void ApplyMusicVolumes()
        {
            float master = AudioPrefs.MusicGain * duckGain;
            for (int i = 0; i < 2; i++) music[i].volume = Mathf.Clamp01(musicBase[i] * master * musicGain[i]);
        }
    }
}
