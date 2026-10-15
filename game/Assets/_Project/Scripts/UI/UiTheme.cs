using UnityEngine;

namespace Cafe
{
    public enum ClayStyle { Red, Green, Gold, Cream, Coffee }

    /// Sprite + font + màu của UI "Clay Café" (docs/ui_style.md). Không dùng Resources:
    /// asset do Editor tạo (UiSpriteImporter.EnsureTheme) rồi gán vào GameBootstrap.uiTheme.
    /// Mọi field có thể null -> UI tự dùng font mặc định / Image trơn.
    [CreateAssetMenu(menuName = "Cafe/UI Theme", fileName = "SO_UiTheme")]
    public class UiTheme : ScriptableObject
    {
        [Header("Fonts")]
        public Font heading;
        public Font body;

        [Header("Khối")]
        public Sprite panel;
        public Sprite chip;
        public Sprite card;
        public Sprite barTrack;
        public Sprite barFill;
        public Sprite ringBg;
        public Sprite ringFill;
        public Sprite disc;
        public Sprite bubble;
        public Sprite bubbleTail;
        public Sprite dim;

        [Header("Nút (bình thường / nhấn)")]
        public Sprite btnRed;
        public Sprite btnRedPressed;
        public Sprite btnGreen;
        public Sprite btnGreenPressed;
        public Sprite btnGold;
        public Sprite btnGoldPressed;
        public Sprite btnCream;
        public Sprite btnCreamPressed;
        public Sprite btnCoffee;
        public Sprite btnCoffeePressed;

        [Header("Icon")]
        public Sprite iconCoin;
        public Sprite iconStar;
        public Sprite iconClock;
        public Sprite iconCup;
        public Sprite iconPhin;
        public Sprite iconSuaDac;
        public Sprite iconSuaTuoi;
        public Sprite iconDa;
        public Sprite iconTra;
        public Sprite iconCheck;
        public Sprite iconCross;
        public Sprite iconHeart;
        [Tooltip("Cây chổi (độ sạch / lau sàn). Chưa có sprite thì dùng iconCup - xem CleaningRules.BroomIcon")]
        public Sprite iconBroom;

        [Header("Logo")]
        [Tooltip("Logo ngang (Art/UI/Logo/logo_wide.png) cho màn tải / màn tiêu đề. Null thì dùng chữ lớn.")]
        public Sprite logoWide;

        [Header("Hướng dẫn")]
        [Tooltip("Bàn tay chỉ (Art/UI/Logo/ui_hand_pointer.png, đầu ngón ở pivot 0.109, 0.93). Null thì dùng chấm tròn tự vẽ.")]
        public Sprite tutorialHand;

        [Header("Màu (ui_style.md §1)")]
        public Color cream = new Color32(0xFF, 0xF4, 0xDC, 0xFF);
        public Color creamDark = new Color32(0xEF, 0xE3, 0xC8, 0xFF);
        public Color coffee = new Color32(0x4A, 0x2C, 0x1E, 0xFF);
        public Color coffeeMid = new Color32(0x6B, 0x40, 0x26, 0xFF);
        public Color red = new Color32(0xD9, 0x36, 0x2B, 0xFF);
        public Color green = new Color32(0x2F, 0xA0, 0x5A, 0xFF);
        public Color gold = new Color32(0xF2, 0xB9, 0x3B, 0xFF);
        public Color mint = new Color32(0x9C, 0xD3, 0xB5, 0xFF);
        public Color danger = new Color32(0xD6, 0x28, 0x28, 0xFF);

        public Sprite Button(ClayStyle s, bool pressed)
        {
            switch (s)
            {
                case ClayStyle.Red: return pressed ? btnRedPressed : btnRed;
                case ClayStyle.Green: return pressed ? btnGreenPressed : btnGreen;
                case ClayStyle.Gold: return pressed ? btnGoldPressed : btnGold;
                case ClayStyle.Cream: return pressed ? btnCreamPressed : btnCream;
                default: return pressed ? btnCoffeePressed : btnCoffee;
            }
        }

        public Sprite IconFor(BrewStep step)
        {
            switch (step)
            {
                case BrewStep.Phin: return iconPhin;
                case BrewStep.SuaDac: return iconSuaDac;
                case BrewStep.SuaTuoi: return iconSuaTuoi;
                case BrewStep.Da: return iconDa;
                case BrewStep.Tra: return iconTra;
                default: return iconCoin;
            }
        }

        /// Icon đại diện cho món: nguyên liệu "đặc trưng" nhất (Trà > Sữa tươi > Sữa đặc > Phin), mặc định là ly.
        public Sprite IconFor(RecipeDef r)
        {
            if (r == null || r.steps == null) return iconCup;
            bool tra = false, tuoi = false, dac = false, phin = false;
            for (int i = 0; i < r.steps.Length; i++)
            {
                switch (r.steps[i])
                {
                    case BrewStep.Tra: tra = true; break;
                    case BrewStep.SuaTuoi: tuoi = true; break;
                    case BrewStep.SuaDac: dac = true; break;
                    case BrewStep.Phin: phin = true; break;
                }
            }
            if (tra) return iconTra;
            if (tuoi) return iconSuaTuoi;
            if (dac) return iconSuaDac;
            if (phin) return iconPhin;
            return iconCup;
        }
    }
}
