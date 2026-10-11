using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cafe
{
    /// Nội dung, lịch ghé, độ thân và hàng đợi chương truyện của khách quen.
    public sealed class RegularsManager
    {
        private static readonly string[] InitialIds = { "ba_sau", "minh", "chu_tu" };
        public const int ShippedChaptersPerRegular = 3;

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
                if (UnityEngine.Random.value > Mathf.Clamp01(candidate.visitChancePerDay)) continue;
                item.MarkVisited(day);
                regular = candidate;
                RaiseChanged();
                return true;
            }
            return false;
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
            else if (reward.type == "hat") game.Tier.AddToInventory(reward.id, reward.amount);
            else Debug.LogWarning("[Regulars] Phần thưởng không hợp lệ: " + reward.type + "/" + reward.id);
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
