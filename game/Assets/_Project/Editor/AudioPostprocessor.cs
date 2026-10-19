using UnityEditor;
using UnityEngine;

namespace Cafe.EditorTools
{
    /// Cài đặt import âm thanh theo thư mục (art_bible §11): SFX mono, nén Vorbis, nạp sẵn trong bộ nhớ; nhạc stereo, phát dòng.
    /// File đặt trong Assets/_Project/Art/Audio/Sfx hoặc /Bgm (tên AU_Sfx_<Id>[_n] / AU_Bgm_<Id>).
    public class AudioPostprocessor : AssetPostprocessor
    {
        public const string SfxDir = "Assets/_Project/Art/Audio/Sfx";
        public const string BgmDir = "Assets/_Project/Art/Audio/Bgm";

        private void OnPreprocessAudio()
        {
            var imp = (AudioImporter)assetImporter;
            bool sfx = assetPath.StartsWith(SfxDir + "/");
            bool bgm = assetPath.StartsWith(BgmDir + "/");
            if (!sfx && !bgm) return;

            imp.forceToMono = sfx;
            imp.loadInBackground = bgm;
            imp.ambisonic = false;
            var s = new AudioImporterSampleSettings
            {
                loadType = sfx ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.Streaming,
                compressionFormat = AudioCompressionFormat.Vorbis,
                quality = 0.4f,
                preloadAudioData = sfx, // SFX nạp sẵn: lần phát đầu không bị khựng
                sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate,
            };
            imp.defaultSampleSettings = s;
        }
    }
}
