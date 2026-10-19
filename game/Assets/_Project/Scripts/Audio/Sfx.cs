namespace Cafe
{
    /// Id âm thanh hiệu ứng. CHỈ THÊM Ở CUỐI, giá trị tường minh: AudioLibrary lưu số nguyên, đổi thứ tự sẽ lệch clip.
    public enum Sfx
    {
        UiTap = 1,
        UiBack = 2,
        DoorChime = 3,
        OrderNew = 4,
        OrderSelect = 5,
        CustomerTap = 6,
        StepOk = 7,
        StepWrong = 8,
        BrewDone = 9,
        Ruin = 10,
        Serve = 11,
        CoinPay = 12,
        CustomerAngry = 13,
        CupPick = 14,
        ScrapCoin = 15,
        WipeDone = 16,
        BuildPlace = 17,
        Sell = 18,
        Purchase = 19,
        LevelUp = 20,
        Claim = 21,
        Chest = 22,
        Fanfare = 23,
        DayOpen = 24,
        DayClose = 25,
    }

    /// Nhạc nền. CHỈ THÊM Ở CUỐI.
    public enum Bgm
    {
        None = 0,
        Title = 1,
        Day = 2,
        Calm = 3,
    }

    public enum HapticKind { None = 0, Light = 1, Medium = 2, Success = 3 }
}
