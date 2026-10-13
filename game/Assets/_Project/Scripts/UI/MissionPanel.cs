using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// Nhiệm vụ ngày + rương Hạt: nút cột phải (chấm đỏ khi có gì để nhận) và bảng 3 nhiệm vụ.
    public sealed class MissionPanel
    {
        private const float CardW = 920f;
        private const float CardH = 820f;
        private const float RowW = 780f;
        private const float RowH = 132f;
        private const float RowGap = 12f;

        private sealed class RowView
        {
            public GameObject root;
            public Text title;
            public BarView bar;
            public Text progress;
            public Text reward;
            public ClayButton claim;
            public Text claimLabel;
        }

        private readonly GameBootstrap game;
        private readonly ClayButton button;
        private readonly Text buttonLabel;
        private readonly GameObject dot;
        private readonly GameObject root;
        private readonly Text gemsText;
        private readonly Text resetText;
        private readonly RowView[] rows = new RowView[MissionBook.DailyCount];
        private readonly Text chestHint;
        private readonly ClayButton chestButton;
        private bool open;

        public MissionPanel(GameBootstrap game, Transform buttonParent, Transform overlayParent)
        {
            this.game = game;

            button = HudBuilder.AddLabelButton(buttonParent, "MissionButton", ClayStyle.Gold,
                MissionStrings.Button, 24, () => SetOpen(true), out buttonLabel);
            HudBuilder.SetFixed((RectTransform)button.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-24f, -558f), new Vector2(230f, 86f)); // cột phải, dưới nút Nhân viên

            Image dotImg = HudBuilder.Img(button.transform, "Dot", HudBuilder.T.disc, HudBuilder.Red, false, false);
            HudBuilder.SetFixed(dotImg.rectTransform, new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-6f, -6f), new Vector2(32f, 32f));
            dot = dotImg.gameObject;

            Image dim = HudBuilder.Dim(overlayParent, "MissionBook", 0.62f);
            HudBuilder.Stretch(dim.rectTransform);
            root = dim.gameObject;
            Image card = HudBuilder.Panel(dim.transform, "Card", true);
            HudBuilder.SetFixed(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(CardW, CardH));

            Text title = HudBuilder.AddText(card.transform, "Title", MissionStrings.Title, 46, TextAnchor.MiddleCenter, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(760f, 60f));

            gemsText = HudBuilder.AddText(card.transform, "Gems", "", 28, TextAnchor.MiddleLeft, HudBuilder.InkSoft, true, false);
            HudBuilder.SetFixed(gemsText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(70f, -88f), new Vector2(300f, 40f));
            resetText = HudBuilder.AddText(card.transform, "Reset", "", 26, TextAnchor.MiddleRight, HudBuilder.InkSoft, false, false);
            HudBuilder.SetFixed(resetText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-70f, -88f), new Vector2(420f, 40f));

            for (int i = 0; i < rows.Length; i++) rows[i] = BuildRow(card.transform, i);

            // --- rương
            float chestY = 140f + rows.Length * (RowH + RowGap);
            Image chest = HudBuilder.Chip(card.transform, "Chest", false);
            HudBuilder.SetFixed(chest.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -chestY), new Vector2(RowW, RowH));
            Text chestTitle = HudBuilder.AddText(chest.transform, "Title", MissionStrings.ChestTitle + "   " +
                string.Format(MissionStrings.ChestRewardFmt, MissionBook.ChestGems, Mathf.RoundToInt(MissionBook.ChestRp)),
                32, TextAnchor.MiddleLeft, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(chestTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -14f), new Vector2(480f, 46f));
            HudBuilder.Fit(chestTitle, 20);
            chestHint = HudBuilder.AddText(chest.transform, "Hint", "", 26, TextAnchor.MiddleLeft, HudBuilder.InkSoft, false, false);
            HudBuilder.SetFixed(chestHint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -66f), new Vector2(480f, 40f));
            HudBuilder.Fit(chestHint, 18);
            Text chestLabel;
            chestButton = HudBuilder.AddLabelButton(chest.transform, "Open", ClayStyle.Green, MissionStrings.OpenChest, 28, OnChest, out chestLabel);
            HudBuilder.SetFixed((RectTransform)chestButton.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f, 0f), new Vector2(220f, 84f));

            Text closeText;
            ClayButton close = HudBuilder.AddLabelButton(card.transform, "Close", ClayStyle.Red, MissionStrings.Close, 32, () => SetOpen(false), out closeText);
            HudBuilder.SetFixed((RectTransform)close.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(300f, 88f));

            game.Missions.Changed += Refresh;
            game.Missions.Completed += OnCompleted;
            game.Tier.TierChanged += Refresh;
            if (game.Gems != null) game.Gems.Changed += OnGems;
            GameEvents.DayStarted += OnDayStarted;
            SetOpen(false);
            Refresh();
        }

        private RowView BuildRow(Transform card, int index)
        {
            var v = new RowView();
            Image row = HudBuilder.Chip(card, "Mission" + index, false);
            HudBuilder.SetFixed(row.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -(140f + index * (RowH + RowGap))), new Vector2(RowW, RowH));
            v.root = row.gameObject;

            v.title = HudBuilder.AddText(row.transform, "Title", "", 32, TextAnchor.MiddleLeft, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(v.title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -12f), new Vector2(470f, 46f));
            HudBuilder.Fit(v.title, 20);

            v.bar = HudBuilder.CreateBar(row.transform, "Bar");
            HudBuilder.SetFixed(v.bar.root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -72f), new Vector2(360f, 30f));
            v.progress = HudBuilder.AddText(row.transform, "Progress", "", 24, TextAnchor.MiddleLeft, HudBuilder.InkSoft, true, false);
            HudBuilder.SetFixed(v.progress.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(402f, -68f), new Vector2(120f, 36f));

            v.reward = HudBuilder.AddText(row.transform, "Reward", "", 28, TextAnchor.MiddleCenter, HudBuilder.GoodDark, true, false);
            HudBuilder.SetFixed(v.reward.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, -12f), new Vector2(210f, 40f));
            HudBuilder.Fit(v.reward, 18);

            int idx = index;
            v.claim = HudBuilder.AddLabelButton(row.transform, "Claim", ClayStyle.Green, MissionStrings.Claim, 26, () => OnClaim(idx), out v.claimLabel);
            HudBuilder.SetFixed((RectTransform)v.claim.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 14f), new Vector2(210f, 66f));
            return v;
        }

        private void SetOpen(bool value)
        {
            open = value;
            root.SetActive(value);
            if (value) Refresh();
        }

        private void OnClaim(int index)
        {
            if (index >= game.Missions.Items.Count) return;
            int reward = game.Missions.Items[index].rewardXu;
            if (game.Missions.Claim(index)) UiFx.FloatTextTop(string.Format(MissionStrings.RewardXuFmt, reward), HudBuilder.Accent);
        }

        private void OnChest()
        {
            if (game.Missions.ClaimChest())
                UiFx.FloatTextTop(string.Format(MissionStrings.ChestOpenedFmt, MissionBook.ChestGems, Mathf.RoundToInt(MissionBook.ChestRp)), HudBuilder.Accent);
        }

        private void OnCompleted(int index)
        {
            if (index < 0 || index >= game.Missions.Items.Count) return;
            GameEvents.RaiseToast(string.Format(MissionStrings.CompletedFmt, MissionStrings.Describe(game.Missions.Items[index])));
        }

        private void OnDayStarted(int day)
        {
            Refresh();
        }

        private void OnGems(int gems)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (game == null || game.Missions == null) return;
            MissionBook book = game.Missions;

            int claimed = 0;
            for (int i = 0; i < book.Items.Count; i++)
                if (book.Items[i].claimed) claimed++;
            buttonLabel.text = string.Format(MissionStrings.ButtonProgressFmt, claimed, MissionBook.DailyCount);
            dot.SetActive(book.HasClaimable);

            if (!open) return;
            gemsText.text = string.Format(MissionStrings.GemsFmt, game.Gems != null ? game.Gems.Gems : 0);
            resetText.text = string.Format(MissionStrings.ResetFmt,
                MissionStrings.Duration(MissionClock.SecondsUntilReset(System.DateTime.UtcNow)));

            for (int i = 0; i < rows.Length; i++)
            {
                RowView v = rows[i];
                bool has = i < book.Items.Count;
                v.root.SetActive(has);
                if (!has) continue;
                MissionState m = book.Items[i];
                v.title.text = MissionStrings.Describe(m);
                v.bar.Set(m.target > 0 ? (float)m.progress / m.target : 0f, m.IsDone ? HudBuilder.Good : HudBuilder.Accent);
                v.progress.text = m.progress + "/" + m.target;
                v.reward.text = string.Format(MissionStrings.RewardXuFmt, m.rewardXu);
                v.claimLabel.text = m.claimed ? MissionStrings.Claimed : (m.IsDone ? MissionStrings.Claim : MissionStrings.InProgress);
                v.claim.SetInteractable(m.IsDone && !m.claimed);
            }

            chestHint.text = book.ChestClaimed ? MissionStrings.ChestOpenedHint
                : (book.ChestReady ? MissionStrings.ChestReadyHint : MissionStrings.ChestLockedHint);
            chestButton.SetInteractable(book.ChestReady);
        }

        public void Dispose()
        {
            if (game != null)
            {
                if (game.Missions != null)
                {
                    game.Missions.Changed -= Refresh;
                    game.Missions.Completed -= OnCompleted;
                }
                if (game.Tier != null) game.Tier.TierChanged -= Refresh;
                if (game.Gems != null) game.Gems.Changed -= OnGems;
            }
            GameEvents.DayStarted -= OnDayStarted;
        }
    }
}
