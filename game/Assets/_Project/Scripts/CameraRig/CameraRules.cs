using UnityEngine;

namespace Cafe
{
    /// Luật camera thuần (không đụng Unity runtime) để kiểm thử được: giới hạn góc nghiêng, độ cao tâm xoay, khoá trục kéo,
    /// ngưỡng ẩn tường / đồ treo khi nhìn ngang tầm mắt.
    public static class CameraRules
    {
        public const float HardMinPitch = 6f;        // thấp hơn thì sàn thành một dải mỏng, đường chân trời chiếm hết khung hình
        public const float MaxPitch = 70f;
        public const float DefaultPitch = 48f;
        public const float BuildMinPitch = 38f;      // chế độ xây: kéo thả theo lưới cần nhìn từ trên
        public const float TutorialMinPitch = 30f;   // hướng dẫn trỏ vào vật thể thế giới: không để bị che
        public const float PivotYTop = 0.35f;        // nhìn vào ngang mặt bàn
        public const float PivotYEye = 0.95f;        // nhìn ngang đầu nhân vật
        public const float PresetTop = 62f, PresetMid = 48f, PresetEye = 8f;
        public const float LowPitchBelow = 20f;      // dưới mức này độ nhạy kéo dọc giảm một nửa

        public static float MinPitch(bool buildFloor, bool tutorialFloor)
        {
            float m = HardMinPitch;
            if (tutorialFloor) m = Mathf.Max(m, TutorialMinPitch);
            if (buildFloor) m = Mathf.Max(m, BuildMinPitch);
            return m;
        }

        public static float ClampPitch(float pitch, bool buildFloor, bool tutorialFloor)
        {
            return Mathf.Clamp(pitch, MinPitch(buildFloor, tutorialFloor), MaxPitch);
        }

        /// Độ cao tâm xoay theo góc nghiêng: từ 30° trở lên là mặt bàn (như cũ); xuống 10° thì nâng lên ngang đầu nhân vật để camera
        /// không chúi xuống sàn.
        public static float PivotY(float pitch)
        {
            return Mathf.Lerp(PivotYEye, PivotYTop, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(10f, 30f, pitch)));
        }

        /// Nhìn ngang thì kéo camera gần lại (tới 70% ở 8°) để nhân vật to hơn và camera hạ xuống gần tầm mắt; từ 30° trở lên giữ nguyên.
        public static float DistanceFactor(float pitch)
        {
            return Mathf.Lerp(0.7f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(8f, 30f, pitch)));
        }

        public static float CameraHeight(float pivotY, float distance, float pitch)
        {
            return pivotY + distance * Mathf.Sin(pitch * Mathf.Deg2Rad);
        }

        /// Hệ số cuốn "tiến / lùi" khi pan: chia cho sin(góc) để bù phối cảnh, nhưng chặn (ở góc thấp sẽ giật mạnh).
        public static float PanForwardScale(float pitch)
        {
            return 1f / Mathf.Max(0.55f, Mathf.Sin(pitch * Mathf.Deg2Rad));
        }

        /// Kéo dọc ở góc thấp nhạy gấp đôi so với cảm giác: giảm một nửa để vùng ngang tầm mắt điều khiển được.
        public static float DragPitchScale(float pitch)
        {
            return pitch < LowPitchBelow ? 0.5f : 1f;
        }

        /// Tường rơi xuống khi camera nằm ngoài mặt phẳng tường (outsideMeters > 0 là phía ngoài). Có trễ để không nhấp nháy.
        public static bool WallDropped(float outsideMeters, bool wasDropped)
        {
            return outsideMeters > (wasDropped ? -0.2f : 0.2f);
        }

        /// Đồ treo (quạt, đèn thả, đèn chùm) chắn tầm nhìn khi nhìn ngang: ẩn dưới 14°, hiện lại trên 18°.
        public static bool HideOverhead(float pitch, bool wasHidden)
        {
            return pitch < (wasHidden ? 18f : 14f);
        }

        /// Nhân vật (khách, chủ quán) đi ngay sát camera khi nhìn ngang tầm mắt che kín màn hình bằng thân / tóc: ẩn khi cách camera dưới
        /// 1,6 m, hiện lại khi xa hơn 2,0 m. Chỉ áp ở góc thấp (dưới 25°); từ trên cao nhân vật không bao giờ sát camera.
        public static bool HideNearCamera(float distance, float pitch, bool wasHidden)
        {
            return pitch < 25f && distance < (wasHidden ? 2.0f : 1.6f);
        }

        public enum DragAxis { Undecided, Yaw, Pitch }

        /// Quyết định trục của một lần kéo từ tổng quãng đường (pixel): ngang trội = chỉ xoay quanh, dọc trội = nghiêng.
        /// Quyết một lần cho cả cử chỉ để vuốt chéo không làm nghiêng nhầm.
        public static DragAxis DecideAxis(Vector2 totalPx, float decidePx)
        {
            if (totalPx.magnitude < decidePx) return DragAxis.Undecided;
            return Mathf.Abs(totalPx.x) > 1.2f * Mathf.Abs(totalPx.y) ? DragAxis.Yaw : DragAxis.Pitch;
        }

        /// Chuyển vòng giữa ba mức nhìn: Trên -> Chéo -> Ngang -> Trên, tính từ mức gần góc hiện tại nhất.
        public static float NextPreset(float pitch)
        {
            float dTop = Mathf.Abs(pitch - PresetTop), dMid = Mathf.Abs(pitch - PresetMid), dEye = Mathf.Abs(pitch - PresetEye);
            if (dTop <= dMid && dTop <= dEye) return PresetMid;
            if (dMid <= dEye) return PresetEye;
            return PresetTop;
        }

        /// 0 = Trên, 1 = Chéo, 2 = Ngang (mức gần nhất với góc hiện tại).
        public static int PresetIndex(float pitch)
        {
            float dTop = Mathf.Abs(pitch - PresetTop), dMid = Mathf.Abs(pitch - PresetMid), dEye = Mathf.Abs(pitch - PresetEye);
            if (dTop <= dMid && dTop <= dEye) return 0;
            return dMid <= dEye ? 1 : 2;
        }
    }
}
