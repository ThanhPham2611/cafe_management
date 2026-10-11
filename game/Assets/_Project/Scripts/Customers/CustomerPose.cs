namespace Cafe
{
    /// Tư thế hoạt họa của khách. Idle/Walk/Sit tự suy ra từ Tick(speed, sitting); Drink bật cử chỉ uống;
    /// Happy/Angry là hiệu ứng ngắn (nhảy mừng ~0.6 s / lắc đầu giận ~1.2 s) chồng lên tư thế nền.
    public enum CustomerPose { Idle, Walk, Sit, Drink, Happy, Angry }
}
