using UnityEngine;

namespace Cafe
{
    /// Màu của các ô palette dùng cho chủ quán (nguồn: docs/art_bible.md §3). Chỉ để vẽ ô chọn màu trên UI.
    public static class OwnerPalette
    {
        public static readonly int[] SkinSwatches = { 52, 53, 54, 55 };
        public static readonly int[] HairSwatches = { 62, 7, 1, 0, 61, 59 };
        public static readonly int[] ShirtSwatches = { 24, 26, 28, 30, 31, 58, 60 };

        public static Color Color(int swatch)
        {
            switch (swatch)
            {
                case 0: return new Color32(0x2A, 0x1A, 0x12, 0xFF);
                case 1: return new Color32(0x4A, 0x2C, 0x1E, 0xFF);
                case 7: return new Color32(0x3B, 0x2A, 0x22, 0xFF);
                case 24: return new Color32(0xD9, 0x36, 0x2B, 0xFF);
                case 26: return new Color32(0x2E, 0x6F, 0xD1, 0xFF);
                case 28: return new Color32(0x2F, 0xA0, 0x5A, 0xFF);
                case 30: return new Color32(0xF2, 0xC2, 0x30, 0xFF);
                case 31: return new Color32(0xF4, 0xF1, 0xEA, 0xFF);
                case 52: return new Color32(0xF6, 0xD7, 0xBE, 0xFF);
                case 53: return new Color32(0xE3, 0xB4, 0x8E, 0xFF);
                case 54: return new Color32(0xC4, 0x8B, 0x63, 0xFF);
                case 55: return new Color32(0x8D, 0x5A, 0x3B, 0xFF);
                case 58: return new Color32(0xF2, 0x8D, 0xB2, 0xFF);
                case 59: return new Color32(0xF4, 0x80, 0x1F, 0xFF);
                case 60: return new Color32(0x2A, 0x9D, 0x9B, 0xFF);
                case 61: return new Color32(0x7B, 0x5E, 0xA7, 0xFF);
                case 62: return new Color32(0x1A, 0x15, 0x14, 0xFF);
            }
            return UnityEngine.Color.gray;
        }
    }
}
