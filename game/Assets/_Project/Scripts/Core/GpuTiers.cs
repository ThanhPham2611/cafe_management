namespace Cafe
{
    /// Trần mức đồ họa theo tên GPU (SystemInfo.graphicsDeviceName). Thuần logic để kiểm thử được.
    /// RAM + số nhân (GraphicsPresets.Recommend) không biết GPU yếu hay mạnh: máy 6-8 GB chip tầm thấp vẫn bị xếp Cao.
    /// Quy tắc: chỉ GPU nhận diện CHẮC CHẮN là mạnh mới được Cao; không nhận ra tên trên điện thoại thì Vừa.
    /// Các ngưỡng là kiến thức chung về dòng chip, chưa kiểm bằng máy thật: chỉnh bảng này sau đợt thử máy.
    public static class GpuTiers
    {
        /// Mức cao nhất được tự gợi ý cho GPU này. Máy tính không giới hạn.
        public static GraphicsLevel MaxLevel(string gpuName, bool mobile)
        {
            if (!mobile) return GraphicsLevel.High;
            if (string.IsNullOrEmpty(gpuName)) return GraphicsLevel.Medium;
            string n = gpuName.ToLowerInvariant();

            if (n.Contains("adreno"))
            {
                int model = NumberAfter(n, "adreno");
                if (model < 0) return GraphicsLevel.Medium;
                if (model < 610) return GraphicsLevel.Low;
                return model == 650 || model == 660 || model >= 730 ? GraphicsLevel.High : GraphicsLevel.Medium;
            }
            if (n.Contains("immortalis") || n.Contains("xclipse")) return GraphicsLevel.High;
            if (n.Contains("mali-g")) return NumberAfter(n, "mali-g") >= 710 ? GraphicsLevel.High : GraphicsLevel.Medium;
            if (n.Contains("mali") || n.Contains("powervr")) return GraphicsLevel.Low; // Mali-T / Mali-4xx, PowerVR
            if (n.StartsWith("apple")) return GraphicsLevel.High; // iPhone bị chặn Cao sẵn bởi luật nhân (6 nhân)
            return GraphicsLevel.Medium;
        }

        /// Cap theo GPU lên mức gợi ý theo RAM / nhân.
        public static GraphicsLevel Cap(GraphicsLevel level, string gpuName, bool mobile)
        {
            GraphicsLevel max = MaxLevel(gpuName, mobile);
            return level < max ? level : max;
        }

        /// Dãy số nguyên đầu tiên sau `key` (vd. "adreno (tm) 740" -> 740); -1 nếu không có.
        private static int NumberAfter(string s, string key)
        {
            int i = s.IndexOf(key, System.StringComparison.Ordinal) + key.Length;
            while (i < s.Length && !char.IsDigit(s[i])) i++;
            int value = -1;
            for (; i < s.Length && char.IsDigit(s[i]); i++) value = (value < 0 ? 0 : value * 10) + (s[i] - '0');
            return value;
        }
    }
}
