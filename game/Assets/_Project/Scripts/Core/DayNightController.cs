using UnityEngine;
using UnityEngine.Rendering;

namespace Cafe
{
    /// Đổi nắng / ambient / nền camera theo đồng hồ ngày (DayNightRules) và bật độ sáng đèn (global shader _CafeNight).
    /// Tạo từ GameBootstrap, không cần sửa scene. Pha Chuẩn bị luôn sáng ban ngày để người chơi trang trí cho dễ nhìn.
    public class DayNightController : MonoBehaviour
    {
        /// >= 0: ép giờ (bot / chụp ảnh: -dayhour N).
        public static float ForcedHour = -1f;
        public const float PrepHour = 9.5f;
        private const float Smooth = 3f;
        /// Độ tối hiện tại 0..1 (đã làm mượt, trùng _CafeNight): LampLights nhân vào cường độ đèn, TutorialDirector dùng để nhắc đèn.
        public static float Night { get; private set; }

        private static readonly int NightId = Shader.PropertyToID("_CafeNight");

        private GameBootstrap game;
        private Camera cam;
        private Light sun;
        private DayNightLook cur;
        private bool started;
        private float accum;

        public static DayNightController Create(GameBootstrap g, Camera c)
        {
            var go = new GameObject("DayNight");
            go.transform.SetParent(g.transform, false);
            var d = go.AddComponent<DayNightController>();
            d.game = g;
            d.cam = c;
            d.sun = RenderSettings.sun;
            if (d.sun == null)
            {
                foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { d.sun = l; break; }
            }
            return d;
        }

        private void OnDestroy()
        {
            Night = 0f;
            Shader.SetGlobalFloat(NightId, 0f);
        }

        private float TargetHour()
        {
            if (ForcedHour >= 0f) return ForcedHour;
            if (game.Day == null) return GameBalance.OpenHour;
            if (game.Day.CurrentPhase == DayCycle.Phase.Prep) return PrepHour;
            return game.Day.ClockHours;
        }

        private void Update()
        {
            float step = 1f / Mathf.Max(1, GraphicsQuality.DayNightHz);
            accum += Time.unscaledDeltaTime;
            if (started && accum < step) return;
            float dt = accum;
            accum = 0f;

            DayNightLook target = DayNightRules.Sample(TargetHour());
            cur = !started || ForcedHour >= 0f ? target : DayNightLook.Lerp(cur, target, 1f - Mathf.Exp(-dt * Smooth));
            started = true;
            Apply(cur);
        }

        private void Apply(DayNightLook l)
        {
            if (sun != null)
            {
                sun.color = l.sun;
                sun.intensity = l.sunIntensity;
                sun.transform.rotation = Quaternion.Euler(l.pitch, l.yaw, 0f);
                if (GraphicsQuality.Current != GraphicsLevel.Low) sun.shadowStrength = l.shadowStrength;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = l.sky;
            RenderSettings.ambientEquatorColor = l.equator;
            RenderSettings.ambientGroundColor = l.ground;
            if (cam != null) cam.backgroundColor = l.background;
            Night = l.night;
            Shader.SetGlobalFloat(NightId, l.night);
        }
    }
}
