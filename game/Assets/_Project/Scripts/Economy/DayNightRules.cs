using UnityEngine;

namespace Cafe
{
    /// Ánh sáng tại một giờ trong ngày (DayNightRules.Sample).
    public struct DayNightLook
    {
        public Color sun, sky, equator, ground, background;
        public float sunIntensity, pitch, yaw, shadowStrength;
        public float night; // 0 = ban ngày, 1 = đêm (đèn phát sáng)

        public static DayNightLook Lerp(DayNightLook a, DayNightLook b, float t)
        {
            return new DayNightLook
            {
                sun = Color.Lerp(a.sun, b.sun, t),
                sky = Color.Lerp(a.sky, b.sky, t),
                equator = Color.Lerp(a.equator, b.equator, t),
                ground = Color.Lerp(a.ground, b.ground, t),
                background = Color.Lerp(a.background, b.background, t),
                sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t),
                pitch = Mathf.Lerp(a.pitch, b.pitch, t),
                yaw = Mathf.Lerp(a.yaw, b.yaw, t),
                shadowStrength = Mathf.Lerp(a.shadowStrength, b.shadowStrength, t),
                night = Mathf.Lerp(a.night, b.night, t),
            };
        }
    }

    /// Bảng ánh sáng theo giờ 06:00 -> 22:00 (art_bible §6.1). Chỉ hình ảnh, không ảnh hưởng gameplay.
    /// Nắng ban ngày (09:30-15:00) trùng giá trị dựng sẵn trong PrototypeSceneBuilder.SetupLighting.
    /// Góc nắng kẹp trong [MinPitch, MaxPitch] để toon band không vỡ; đêm sáng xanh ánh trăng, vẫn đủ sáng để đọc
    /// (nắng ~0,9 và ambient sáng hơn ngày khoảng một nửa). `night` cũng là độ sáng của đèn (LampLights): 0 từ 17:30, đầy lúc 19:00.
    public static class DayNightRules
    {
        public const float MinPitch = 12f;
        public const float MaxPitch = 45f;
        public const float MinSunIntensity = 0.4f;

        private struct Key
        {
            public float hour;
            public DayNightLook look;
        }

        private static Color C(int rgb)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
        }

        private static Key K(float hour, int sun, float intensity, float pitch, float yaw, float shadow,
                             int sky, int equator, int ground, int background, float night)
        {
            return new Key
            {
                hour = hour,
                look = new DayNightLook
                {
                    sun = C(sun), sunIntensity = intensity, pitch = pitch, yaw = yaw, shadowStrength = shadow,
                    sky = C(sky), equator = C(equator), ground = C(ground), background = C(background), night = night,
                }
            };
        }

        private static readonly Key[] Keys =
        {
            //     giờ   nắng      cường độ góc  hướng bóng   trời     đường chân trời  đất      nền camera  đèn
            K( 6.0f, 0xFFB48A, 0.80f, 18f, -80f, 0.50f, 0xC7B6D9, 0xF2C4A8, 0x6E5460, 0xE3B7A8, 0.4f),
            K( 8.5f, 0xFFE6C8, 1.05f, 32f, -55f, 0.60f, 0xC4D6E8, 0xEBCFA8, 0x7A5A4A, 0xC4D6E8, 0.0f),
            K( 9.5f, 0xFFEBD2, 1.15f, 38f, -35f, 0.60f, 0xBFD3E6, 0xE8C9A0, 0x7A5A4A, 0xBFD3E6, 0.0f),
            K(15.0f, 0xFFEBD2, 1.15f, 38f, -35f, 0.60f, 0xBFD3E6, 0xE8C9A0, 0x7A5A4A, 0xBFD3E6, 0.0f),
            K(17.5f, 0xFFC98A, 1.00f, 24f, -10f, 0.55f, 0xD9A6A0, 0xF0A878, 0x6A4A4A, 0xE3B8A0, 0.0f),
            K(19.0f, 0xE88A6A, 0.70f, 12f,   5f, 0.40f, 0x5A6A9C, 0xB07A7A, 0x4A3A48, 0x3A3F6A, 1.0f),
            K(20.5f, 0x8FA8E8, 0.90f, 45f,  10f, 0.30f, 0x6676AA, 0x7A82B4, 0x40446A, 0x1B2440, 1.0f),
            K(22.0f, 0x8FA8E8, 0.90f, 45f,  10f, 0.30f, 0x6676AA, 0x7A82B4, 0x40446A, 0x1B2440, 1.0f),
        };

        public static DayNightLook Sample(float hour)
        {
            if (hour <= Keys[0].hour) return Keys[0].look;
            for (int i = 1; i < Keys.Length; i++)
            {
                if (hour > Keys[i].hour) continue;
                float t = (hour - Keys[i - 1].hour) / Mathf.Max(1e-4f, Keys[i].hour - Keys[i - 1].hour);
                return DayNightLook.Lerp(Keys[i - 1].look, Keys[i].look, t);
            }
            return Keys[Keys.Length - 1].look;
        }
    }
}
