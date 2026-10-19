using System;
using NUnit.Framework;

namespace Cafe.Tests
{
    public class AudioRulesTests
    {
        [Test]
        public void StepGain_OffLowMedHigh()
        {
            Assert.That(AudioRules.StepGain(0), Is.EqualTo(0f));
            Assert.That(AudioRules.StepGain(1), Is.EqualTo(AudioRules.DbToGain(-14f)).Within(1e-4f));
            Assert.That(AudioRules.StepGain(2), Is.EqualTo(AudioRules.DbToGain(-7f)).Within(1e-4f));
            Assert.That(AudioRules.StepGain(3), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(AudioRules.StepGain(99), Is.EqualTo(1f).Within(1e-4f), "kẹp ở bậc cao nhất");
            Assert.That(AudioRules.StepGain(-5), Is.EqualTo(0f), "kẹp ở bậc thấp nhất");
        }

        [Test]
        public void DbToGain_KnownValues()
        {
            Assert.That(AudioRules.DbToGain(0f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(AudioRules.DbToGain(-6f), Is.EqualTo(0.501f).Within(1e-3f));
            Assert.That(AudioRules.DbToGain(-4f), Is.EqualTo(0.631f).Within(1e-3f));
        }

        [Test]
        public void DuckFactor_OnlyWhenDuckedOrTimeFrozen()
        {
            Assert.That(AudioRules.DuckFactor(false, false), Is.EqualTo(1f));
            Assert.That(AudioRules.DuckFactor(true, false), Is.EqualTo(0.631f).Within(1e-3f));
            Assert.That(AudioRules.DuckFactor(false, true), Is.EqualTo(0.631f).Within(1e-3f));
            Assert.That(AudioRules.DuckFactor(true, true), Is.EqualTo(0.631f).Within(1e-3f), "không cộng dồn");
        }

        [Test]
        public void CooldownReady_Boundary()
        {
            Assert.That(AudioRules.CooldownReady(1.0f, 0.9f, 0.2f), Is.False);
            Assert.That(AudioRules.CooldownReady(1.1f, 0.9f, 0.2f), Is.True);
            Assert.That(AudioRules.CooldownReady(5f, 5f, 0f), Is.True, "cooldown 0 luôn sẵn sàng");
        }

        [Test]
        public void Pitch_ClampsRandom()
        {
            Assert.That(AudioRules.Pitch(0.9f, 1.1f, -3f), Is.EqualTo(0.9f).Within(1e-5f));
            Assert.That(AudioRules.Pitch(0.9f, 1.1f, 7f), Is.EqualTo(1.1f).Within(1e-5f));
            Assert.That(AudioRules.Pitch(0.9f, 1.1f, 0.5f), Is.EqualTo(1f).Within(1e-5f));
        }

        [TestCase(2)]
        [TestCase(3)]
        [TestCase(5)]
        public void NextVariant_NeverRepeatsLast_AndReachesAll(int count)
        {
            for (int last = 0; last < count; last++)
            {
                var seen = new bool[count];
                for (int k = 0; k <= 20; k++)
                {
                    int v = AudioRules.NextVariant(count, last, k / 20f);
                    Assert.That(v, Is.InRange(0, count - 1));
                    Assert.That(v, Is.Not.EqualTo(last));
                    seen[v] = true;
                }
                for (int j = 0; j < count; j++) if (j != last) Assert.That(seen[j], Is.True, "last=" + last + " thiếu " + j);
            }
        }

        [Test]
        public void NextVariant_EdgeCases()
        {
            Assert.That(AudioRules.NextVariant(0, -1, 0.5f), Is.EqualTo(-1));
            Assert.That(AudioRules.NextVariant(1, 0, 0.5f), Is.EqualTo(0));
            Assert.That(AudioRules.NextVariant(3, -1, 0f), Is.EqualTo(0));
            Assert.That(AudioRules.NextVariant(3, -1, 1f), Is.EqualTo(2), "chưa có clip vừa phát: đủ mọi biến thể");
        }

        [Test]
        public void Fade_MovesTowardsTargetAtRate()
        {
            Assert.That(AudioRules.Fade(0f, 1f, 0.1f, 1f), Is.EqualTo(0.1f).Within(1e-5f));
            Assert.That(AudioRules.Fade(0.95f, 1f, 0.1f, 1f), Is.EqualTo(1f));
            Assert.That(AudioRules.Fade(1f, 0f, 0.5f, 1f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(AudioRules.Fade(0.3f, 0.9f, 0.01f, 0f), Is.EqualTo(0.9f), "0 giây = nhảy thẳng");
        }

        [Test]
        public void PickVoice_FreeSlotFirst_ThenStealsLeastImportantOldest()
        {
            var playing = new[] { true, true, false };
            var prio = new[] { 2, 3, 0 };
            var start = new[] { 1f, 2f, 0f };
            Assert.That(AudioRules.PickVoice(playing, prio, start, 2), Is.EqualTo(2));

            playing = new[] { true, true, true };
            prio = new[] { 1, 3, 3 };
            start = new[] { 5f, 4f, 2f };
            Assert.That(AudioRules.PickVoice(playing, prio, start, 2), Is.EqualTo(2), "priority lớn nhất, hòa thì cũ nhất");
            Assert.That(AudioRules.PickVoice(playing, prio, start, 0), Is.EqualTo(2), "âm chủ đạo cướp được voice kém quan trọng");
        }

        [Test]
        public void PickVoice_NeverStealsMoreImportant()
        {
            var playing = new[] { true, true };
            var prio = new[] { 0, 1 };
            var start = new[] { 1f, 1f };
            Assert.That(AudioRules.PickVoice(playing, prio, start, 2), Is.EqualTo(-1));
        }

        [Test]
        public void HapticFor_CoversEverySfxWithoutThrowing()
        {
            foreach (Sfx s in Enum.GetValues(typeof(Sfx))) Assert.DoesNotThrow(() => AudioRules.HapticFor(s));
            Assert.That(AudioRules.HapticFor(Sfx.UiTap), Is.EqualTo(HapticKind.Light));
            Assert.That(AudioRules.HapticFor(Sfx.StepWrong), Is.EqualTo(HapticKind.Medium));
            Assert.That(AudioRules.HapticFor(Sfx.Fanfare), Is.EqualTo(HapticKind.Success));
            Assert.That(AudioRules.HapticFor(Sfx.Serve), Is.EqualTo(HapticKind.None), "nhân viên phục vụ thì không rung");
            Assert.That(AudioRules.HapticFor(Sfx.CoinPay), Is.EqualTo(HapticKind.None));
        }

        [Test]
        public void EnabledForRun_OffForAutomation_OnOtherwise()
        {
            Assert.That(AudioRules.EnabledForRun(new[] { "game" }, false), Is.True);
            Assert.That(AudioRules.EnabledForRun(new[] { "game", "-autoplay" }, false), Is.False);
            Assert.That(AudioRules.EnabledForRun(new[] { "game", "-shotsettings" }, false), Is.False);
            Assert.That(AudioRules.EnabledForRun(new[] { "game" }, true), Is.False, "batchmode");
            Assert.That(AudioRules.EnabledForRun(new[] { "game", "-autoplay", "-audio" }, false), Is.True, "-audio ép bật");
            Assert.That(AudioRules.EnabledForRun(new[] { "game", "-autoplay", "-title" }, false), Is.True, "-title ép hiện màn tiêu đề thì cũng có tiếng");
        }

        [Test]
        public void SfxEnum_ValuesUniqueAndPositive()
        {
            var seen = new System.Collections.Generic.HashSet<int>();
            foreach (Sfx s in Enum.GetValues(typeof(Sfx)))
            {
                Assert.That((int)s, Is.GreaterThan(0));
                Assert.That(seen.Add((int)s), Is.True, s.ToString());
            }
        }
    }

    public class AudioPrefsTests
    {
        [SetUp]
        public void SetUp()
        {
            UnityEngine.PlayerPrefs.DeleteKey("cafe.music");
            UnityEngine.PlayerPrefs.DeleteKey("cafe.sfx");
            UnityEngine.PlayerPrefs.DeleteKey("cafe.haptics");
            AudioPrefs.Init();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.PlayerPrefs.DeleteKey("cafe.music");
            UnityEngine.PlayerPrefs.DeleteKey("cafe.sfx");
            UnityEngine.PlayerPrefs.DeleteKey("cafe.haptics");
            AudioPrefs.Init();
        }

        [Test]
        public void Defaults_MusicMedium_SfxHigh_HapticsOn()
        {
            Assert.That(AudioPrefs.MusicStep, Is.EqualTo(2));
            Assert.That(AudioPrefs.SfxStep, Is.EqualTo(3));
            Assert.That(AudioPrefs.Haptics, Is.True);
        }

        [Test]
        public void Setters_ClampAndRaiseChanged()
        {
            int changed = 0;
            System.Action h = () => changed++;
            AudioPrefs.Changed += h;
            try
            {
                AudioPrefs.SetMusic(99);
                AudioPrefs.SetSfx(-4);
                AudioPrefs.SetHaptics(false);
            }
            finally { AudioPrefs.Changed -= h; }
            Assert.That(AudioPrefs.MusicStep, Is.EqualTo(3));
            Assert.That(AudioPrefs.SfxStep, Is.EqualTo(0));
            Assert.That(AudioPrefs.Haptics, Is.False);
            Assert.That(changed, Is.EqualTo(3));
        }

        [Test]
        public void Persist_AcrossInit()
        {
            AudioPrefs.SetMusic(1);
            AudioPrefs.SetHaptics(false);
            AudioPrefs.Init();
            Assert.That(AudioPrefs.MusicStep, Is.EqualTo(1));
            Assert.That(AudioPrefs.Haptics, Is.False);
            Assert.That(AudioPrefs.MusicGain, Is.EqualTo(AudioRules.StepGain(1)).Within(1e-5f));
        }
    }

    public class AudioLibraryTests
    {
        private static AudioLibrary Make()
        {
            var lib = UnityEngine.ScriptableObject.CreateInstance<AudioLibrary>();
            lib.sfx = new[]
            {
                new AudioLibrary.SfxEntry { id = Sfx.UiTap, clips = new[] { UnityEngine.AudioClip.Create("a", 100, 1, 44100, false) } },
                new AudioLibrary.SfxEntry { id = Sfx.UiTap, clips = new[] { UnityEngine.AudioClip.Create("b", 100, 1, 44100, false) }, volume = 0.1f },
                new AudioLibrary.SfxEntry { id = Sfx.CoinPay, clips = null },
                null,
            };
            lib.bgm = new[] { new AudioLibrary.BgmEntry { id = Bgm.Day, clip = null } };
            return lib;
        }

        [Test]
        public void TryGet_UnknownOrEmptyOrNull_False_NoThrow()
        {
            AudioLibrary lib = Make();
            AudioLibrary.SfxEntry e;
            Assert.That(lib.TryGet(Sfx.Sell, out e), Is.False, "id không có trong thư viện");
            Assert.That(lib.TryGet(Sfx.CoinPay, out e), Is.False, "entry không có clip");
            AudioLibrary.BgmEntry b;
            Assert.That(lib.TryGet(Bgm.Day, out b), Is.False, "nhạc không có clip");
            Assert.That(lib.TryGet(Bgm.Title, out b), Is.False);
        }

        [Test]
        public void TryGet_DuplicateId_KeepsFirst()
        {
            AudioLibrary lib = Make();
            AudioLibrary.SfxEntry e;
            Assert.That(lib.TryGet(Sfx.UiTap, out e), Is.True);
            Assert.That(e.volume, Is.EqualTo(1f));
        }

        [Test]
        public void EmptyLibrary_IsSafe()
        {
            var lib = UnityEngine.ScriptableObject.CreateInstance<AudioLibrary>();
            lib.sfx = null;
            lib.bgm = null;
            AudioLibrary.SfxEntry e;
            Assert.That(lib.TryGet(Sfx.UiTap, out e), Is.False);
        }
    }
}
