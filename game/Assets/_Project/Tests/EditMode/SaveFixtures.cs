namespace Cafe.Tests
{
    /// JSON mẫu theo từng đời định dạng save (suy từ các trường SaveData, không lấy từ save thật của người chơi).
    public static class SaveFixtures
    {
        public const string V1 = "{\"version\":1,\"xu\":120,\"day\":4,\"rp\":2.5,\"layout\":[{\"furnitureId\":\"Cart_Phin\",\"cell\":{\"x\":2,\"y\":3},\"rotation\":2}]}";

        public const string V2 = "{\"version\":2,\"xu\":950,\"day\":9,\"rp\":14.0,\"layout\":[{\"furnitureId\":\"Cart_Phin\",\"cell\":{\"x\":2,\"y\":3},\"rotation\":2}],"
            + "\"creatorDone\":true,\"ownerName\":\"Thanh\",\"shopName\":\"Quán Cà Phê Việt\","
            + "\"ownerLook\":{\"preset\":1,\"skinSwatch\":53,\"hairSwatch\":62,\"shirtSwatch\":31},"
            + "\"purchases\":[{\"furnitureId\":\"Stool_Basic\",\"count\":3}]}";

        /// Đủ mọi trường public của SaveData (SaveData_AllFieldsAppearInV3Fixture ép cập nhật khi thêm trường).
        public const string V3 = "{\"version\":3,\"xu\":5400,\"day\":21,\"rp\":88.5,\"levelRewarded\":6,\"levelClaimed\":5,"
            + "\"fundTier\":2,\"fundStage\":1,\"weekIndex\":3,\"weekProgress\":7,\"weekDone\":false,\"weekTier\":1,"
            + "\"layout\":[{\"furnitureId\":\"Cart_Phin\",\"cell\":{\"x\":2,\"y\":3},\"rotation\":2}],"
            + "\"creatorDone\":true,\"ownerName\":\"Thanh\",\"shopName\":\"Quán Cà Phê Việt\","
            + "\"ownerLook\":{\"preset\":1,\"skinSwatch\":53,\"hairSwatch\":62,\"shirtSwatch\":31},"
            + "\"purchases\":[{\"furnitureId\":\"Stool_Basic\",\"count\":3}],"
            + "\"tier\":2,\"deposit\":18000,\"rentDebt\":0,\"daysSinceRent\":2,\"gems\":12,"
            + "\"knownRecipes\":[\"tra_da\",\"ca_phe_den\"],\"menuToday\":[\"tra_da\"],"
            + "\"toolLevels\":[{\"furnitureId\":\"phin\",\"count\":2}],\"inventory\":[{\"furnitureId\":\"Stool_Basic\",\"count\":1}],"
            + "\"staff\":[{\"role\":\"serve\",\"preset\":2,\"level\":3,\"name\":\"Lan\"}],"
            + "\"regulars\":[{\"id\":\"co_ba\",\"bond\":4,\"chaptersDone\":1,\"lastVisitDay\":20}],"
            + "\"missions\":[{\"id\":\"serve_n\",\"target\":10,\"progress\":4,\"rewardXu\":200,\"claimed\":false}],"
            + "\"missionDayKey\":20000,\"missionChestClaimed\":false,"
            + "\"zonesOpen\":0,\"tutorialStep\":9,\"tutorialSeen\":[\"tip_a\"],\"gridVersion\":1,"
            + "\"decorOwned\":[\"w_mint\"],\"decorChoices\":[{\"tier\":2,\"wall\":\"w_mint\",\"floor\":null}]}";

        /// Cùng V3 nhưng version cao hơn bản hiện tại.
        public static string Future()
        {
            return V3.Replace("\"version\":3", "\"version\":" + (SaveData.CurrentVersion + 1));
        }
    }
}
