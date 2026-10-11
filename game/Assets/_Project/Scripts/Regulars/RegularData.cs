using System;
using UnityEngine;

namespace Cafe
{
    [Serializable]
    public sealed class RegularDataRoot { public RegularDefinition[] regulars; }

    [Serializable]
    public sealed class RegularDefinition
    {
        public string id;
        public string displayName;
        public int age;
        public string role;
        public string favoriteRecipe;
        public int unlockTier = 1;
        public string preferredStyle;
        public float visitChancePerDay;
        public string personality;
        public RegularLook look;
        public StoryChapterDefinition[] chapters;
    }

    [Serializable]
    public sealed class RegularLook
    {
        public string customerPrefab;
        public string hint;
    }

    [Serializable]
    public sealed class StoryChapterDefinition
    {
        public string id;
        public int requiresBond;
        public string title;
        public StoryLineDefinition[] lines;
        public StoryRewardDefinition reward;
    }

    [Serializable]
    public sealed class StoryLineDefinition
    {
        public string speaker;
        [TextArea] public string text;
    }

    [Serializable]
    public sealed class StoryRewardDefinition
    {
        public string type;
        public string id;
        public int amount = 1;
    }

    public sealed class StoryChapterView
    {
        public readonly RegularDefinition Regular;
        public readonly StoryChapterDefinition Chapter;
        public readonly int ChapterIndex;

        public StoryChapterView(RegularDefinition regular, StoryChapterDefinition chapter, int chapterIndex)
        {
            Regular = regular;
            Chapter = chapter;
            ChapterIndex = chapterIndex;
        }
    }

    public sealed class RegularProgress
    {
        public string Id { get; private set; }
        public int Bond { get; private set; }
        public int ChaptersDone { get; private set; }
        public int LastVisitDay { get; private set; }

        public static RegularProgress FromSave(RegularSave save, int chapterCount)
        {
            return new RegularProgress
            {
                Id = save.id ?? "",
                Bond = Mathf.Clamp(save.bond, 0, 10),
                ChaptersDone = Mathf.Clamp(save.chaptersDone, 0, Mathf.Max(0, chapterCount)),
                LastVisitDay = Mathf.Max(0, save.lastVisitDay)
            };
        }

        public void AddBond(int amount) { Bond = Mathf.Clamp(Bond + Mathf.Max(0, amount), 0, 10); }
        public void MarkVisited(int day) { LastVisitDay = Mathf.Max(LastVisitDay, day); }

        public bool TryCompleteChapter(int index)
        {
            if (index != ChaptersDone) return false;
            ChaptersDone++;
            return true;
        }

        public RegularSave ToSave()
        {
            return new RegularSave { id = Id, bond = Bond, chaptersDone = ChaptersDone, lastVisitDay = LastVisitDay };
        }
    }

    public static class RegularProgressRules
    {
        public static int BondGain(bool favorite, int quality, float waitSeconds, float cleanliness)
        {
            return favorite && quality >= 2 && waitSeconds <= 25f && cleanliness >= 70f ? 1 : 0;
        }
    }

    public static class RegularText
    {
        public static string Expand(string source, string owner, string shop, string regularName)
        {
            return (source ?? "")
                .Replace("{owner}", owner ?? "")
                .Replace("{shop}", shop ?? "")
                .Replace("{name}", regularName ?? "");
        }
    }
}
