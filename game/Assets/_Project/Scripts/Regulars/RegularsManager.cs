using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cafe
{
    /// Nội dung, lịch ghé, độ thân và hàng đợi chương truyện của khách quen.
    public sealed class RegularsManager
    {
        private static readonly string[] InitialIds = { "ba_sau", "minh", "chu_tu", "chi_lan", "ken", "mai" };
        public const int ShippedChaptersPerRegular = 4;
        /// Chưa có hệ mũ / trang phục (không có model hay icon): phần thưởng "mũ" quy ra Xu để người chơi vẫn nhận được gì đó.
        public const int HatKeepsakeXu = 300;

        private readonly List<RegularDefinition> definitions = new List<RegularDefinition>(3);
        private readonly Dictionary<string, RegularProgress> progress = new Dictionary<string, RegularProgress>();
        private readonly Queue<StoryChapterView> stories = new Queue<StoryChapterView>();
        private readonly HashSet<string> queued = new HashSet<string>();
        private GameBootstrap game;

        public event Action Changed;
        public event Action StoryQueued;

        public IReadOnlyList<RegularDefinition> Definitions { get { return definitions; } }
        public bool Enabled { get { return definitions.Count > 0; } }

        public void Init(GameBootstrap owner, SaveData save)
        {
            game = owner;
            definitions.Clear();
            progress.Clear();
            stories.Clear();
            queued.Clear();

            if (owner == null || owner.regularsJson == null)
            {
                Debug.LogWarning("[Regulars] Thiếu regulars.json; tính năng khách quen bị tắt.");
                return;
            }

            try
            {
                RegularDataRoot root = JsonUtility.FromJson<RegularDataRoot>(owner.regularsJson.text);
                if (root == null || root.regulars == null) throw new FormatException("Không có mảng regulars");
                var seen = new HashSet<string>();
                for (int n = 0; n < InitialIds.Length; n++)
                {
                    RegularDefinition found = null;
                    for (int i = 0; i < root.regulars.Length; i++)
                        if (root.regulars[i] != null && root.regulars[i].id == InitialIds[n]) { found = root.regulars[i]; break; }
                    if (found == null || string.IsNullOrEmpty(found.id) || !seen.Add(found.id)) continue;
                    if (found.chapters == null) found.chapters = new StoryChapterDefinition[0];
                    definitions.Add(found);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[Regulars] Không đọc được dữ liệu truyện: " + e.Message);
                definitions.Clear();
                return;
            }

            for (int i = 0; i < definitions.Count; i++)
            {
                RegularDefinition def = definitions[i];
                RegularSave saved = new RegularSave { id = def.id };
                if (save != null && save.regulars != null)
                    for (int k = 0; k < save.regulars.Length; k++)
                        if (save.regulars[k].id == def.id) { saved = save.regulars[k]; break; }
                progress[def.id] = RegularProgress.FromSave(saved, ChapterCount(def));
            }
            RaiseChanged();
        }

        public RegularProgress GetProgress(string id)
        {
            RegularProgress item;
            return id != null && progress.TryGetValue(id, out item) ? item : null;
        }

        public bool TryScheduleVisit(int day, int tier, out RegularDefinition regular)
        {
            regular = null;
            if (definitions.Count == 0) return false;
            int start = UnityEngine.Random.Range(0, definitions.Count);
            for (int n = 0; n < definitions.Count; n++)
            {
                RegularDefinition candidate = definitions[(start + n) % definitions.Count];
                RegularProgress item = GetProgress(candidate.id);
                if (candidate.unlockTier > tier || item == null || item.LastVisitDay >= day) continue;
                if (!CanServeFavorite(candidate)) continue;
                if (UnityEngine.Random.value > Mathf.Clamp01(candidate.visitChancePerDay)) continue;
                item.MarkVisited(day);
                regular = candidate;
                RaiseChanged();
                return true;
            }
            return false;
        }

        /// Quán có thể pha món ruột của khách quen không (đã biết công thức, máy đã đặt). Chưa thì khách chưa ghé: ghé mà không có món
        /// ruột thì không cộng thân thiết, mất lượt ghé của ngày. Chỉ xét khi đã có sổ công thức (test thuần logic thì bỏ qua).
        private bool CanServeFavorite(RegularDefinition def)
        {
            if (game == null || game.Recipes == null) return true;
            RecipeDef favorite = game.Recipes.Get(def.favoriteRecipe);
            return favorite != null && game.Recipes.IsKnown(favorite) && game.Recipes.MachineReady(favorite);
        }

        /// Khách quen regularId có đang nhận đúng món ruột recipeId không.
        public bool IsFavorite(string regularId, string recipeId)
        {
            RegularDefinition def = FindDefinition(regularId);
            return def != null && recipeId == def.favoriteRecipe;
        }

        public void RecordService(string regularId, string recipeId, int quality, float waitSeconds, float cleanliness)
        {
            RegularDefinition def = FindDefinition(regularId);
            RegularProgress item = GetProgress(regularId);
            if (def == null || item == null) return;
            item.AddBond(RegularProgressRules.BondGain(recipeId == def.favoriteRecipe, quality, waitSeconds, cleanliness));
            QueueNextEligible(def, item);
            RaiseChanged();
        }

        private void QueueNextEligible(RegularDefinition def, RegularProgress item)
        {
            int index = item.ChaptersDone;
            if (index < 0 || index >= ChapterCount(def)) return;
            StoryChapterDefinition chapter = def.chapters[index];
            if (chapter == null || item.Bond < chapter.requiresBond || queued.Contains(chapter.id)) return;
            queued.Add(chapter.id);
            stories.Enqueue(new StoryChapterView(def, chapter, index));
            if (StoryQueued != null) StoryQueued();
        }

        public bool TryDequeueStory(out StoryChapterView chapter)
        {
            if (stories.Count == 0) { chapter = null; return false; }
            chapter = stories.Dequeue();
            return true;
        }

        public bool CompleteStory(StoryChapterView view)
        {
            if (view == null) return false;
            RegularProgress item = GetProgress(view.Regular.id);
            if (item == null || !item.TryCompleteChapter(view.ChapterIndex)) return false;
            queued.Remove(view.Chapter.id);
            GrantReward(view.Chapter.reward);
            string gift = RewardText(view.Chapter.reward);
            if (gift != null) GameEvents.RaiseToast(gift);
            if (game != null)
            {
                // Truyện khách quen cũng là đường lên sao: RP cố định theo chương (economy §20).
                // Dời mốc Xu đầu ngày theo thưởng Xu của chương, không thì thoát-vào giữa ngày làm mất thưởng
                float rp = game.Reputation.Add(GameBalance.StoryChapterRp(view.ChapterIndex));
                game.NotifyRewardGranted(RewardXu(view.Chapter.reward), rp);
                GameEvents.RaiseToast(string.Format(Strings.StoryRpFmt, Mathf.RoundToInt(rp)));
            }
            RaiseChanged();
            if (game != null) game.SaveSnapshot();
            return true;
        }

        private void GrantReward(StoryRewardDefinition reward)
        {
            if (reward == null || reward.amount <= 0) return;
            if (reward.type == "xu") game.Wallet.Earn(reward.amount);
            else if (reward.type == "furniture" && game.GetDef(reward.id) != null) game.Tier.AddToInventory(reward.id, reward.amount);
            else if (reward.type == "recipe" && game.Recipes.Get(reward.id) != null) game.Recipes.Discover(reward.id);
            else if (reward.type == "hat")
            {
                game.Tier.AddToInventory(reward.id, reward.amount);   // giữ id cho hệ mũ sau này
                game.Wallet.Earn(HatKeepsakeXu * reward.amount);
            }
            else Debug.LogWarning("[Regulars] Phần thưởng không hợp lệ: " + reward.type + "/" + reward.id);
        }

        /// Số Xu mà một phần thưởng chương cộng vào ví ("xu" = amount; "mũ" quy ra HatKeepsakeXu; loại khác = 0).
        public static int RewardXu(StoryRewardDefinition reward)
        {
            if (reward == null || reward.amount <= 0) return 0;
            if (reward.type == "xu") return reward.amount;
            if (reward.type == "hat") return HatKeepsakeXu * reward.amount;
            return 0;
        }

        /// Dòng báo phần thưởng chương ("Quà: +80 Xu"); null nếu không có gì để báo.
        public string RewardText(StoryRewardDefinition reward)
        {
            if (reward == null || reward.amount <= 0 || game == null) return null;
            switch (reward.type)
            {
                case "xu": return string.Format(Strings.StoryGiftXuFmt, Strings.FormatInt(reward.amount));
                case "furniture":
                    FurnitureDef d = game.GetDef(reward.id);
                    return d == null ? null : string.Format(Strings.StoryGiftItemFmt, string.IsNullOrEmpty(d.displayName) ? d.id : d.displayName);
                case "recipe":
                    RecipeDef r = game.Recipes != null ? game.Recipes.Get(reward.id) : null;
                    return r == null ? null : string.Format(Strings.StoryGiftRecipeFmt, string.IsNullOrEmpty(r.displayName) ? r.id : r.displayName);
                case "hat": return string.Format(Strings.StoryGiftHatFmt, Strings.FormatInt(HatKeepsakeXu * reward.amount));
            }
            return null;
        }

        public void WriteToSave(SaveData data)
        {
            if (data == null) return;
            var saved = new List<RegularSave>(definitions.Count);
            for (int i = 0; i < definitions.Count; i++)
            {
                RegularProgress item = GetProgress(definitions[i].id);
                if (item != null) saved.Add(item.ToSave());
            }
            data.regulars = saved.ToArray();
        }

        private RegularDefinition FindDefinition(string id)
        {
            for (int i = 0; i < definitions.Count; i++)
                if (definitions[i].id == id) return definitions[i];
            return null;
        }

        public static int ChapterCount(RegularDefinition definition)
        {
            return definition == null || definition.chapters == null
                ? 0
                : Mathf.Min(ShippedChaptersPerRegular, definition.chapters.Length);
        }

        private void RaiseChanged() { if (Changed != null) Changed(); }
    }
}
