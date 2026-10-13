using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// Hộp thoại chương truyện; chỉ mở ở thời điểm an toàn và tạm dừng thế giới phía sau.
    public sealed class StoryDialog
    {
        private readonly GameBootstrap game;
        private readonly GameObject root;
        private readonly Text title;
        private readonly Text speaker;
        private readonly Text body;
        private readonly Text nextLabel;
        private StoryChapterView current;
        private int lineIndex;
        private float previousTimeScale = 1f;

        public bool IsOpen { get { return root.activeSelf; } }

        public StoryDialog(GameBootstrap game, Transform parent)
        {
            this.game = game;
            Image dim = HudBuilder.Dim(parent, "StoryDialog", 0.72f);
            HudBuilder.Stretch(dim.rectTransform);
            root = dim.gameObject;

            Image card = HudBuilder.Panel(dim.transform, "Card", true);
            HudBuilder.SetFixed(card.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(1120f, 590f));
            title = HudBuilder.AddText(card.transform, "Title", "", 42, TextAnchor.MiddleCenter, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -40f), new Vector2(950f, 58f));
            speaker = HudBuilder.AddText(card.transform, "Speaker", "", 32, TextAnchor.MiddleLeft, HudBuilder.GoodDark, true, false);
            HudBuilder.SetFixed(speaker.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(78f, -128f), new Vector2(900f, 48f));
            body = HudBuilder.AddText(card.transform, "Body", "", 34, TextAnchor.UpperLeft, HudBuilder.Ink, false, false);
            HudBuilder.SetFixed(body.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(78f, -188f), new Vector2(960f, 230f));
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Truncate;

            ClayButton next = HudBuilder.AddLabelButton(card.transform, "Next", ClayStyle.Red, "TIẾP", 36,
                Advance, out nextLabel);
            HudBuilder.SetFixed((RectTransform)next.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-70f, 42f), new Vector2(260f, 100f));
            root.SetActive(false);
        }

        public void Tick()
        {
            if (IsOpen || game.Regulars == null || game.Day == null || game.Day.CurrentPhase != DayCycle.Phase.Open) return;
            if (game.Tier.Transitioning || (game.Builder != null && game.Builder.IsOpen) ||
                (game.Creator != null && game.Creator.IsOpen)) return;
            StoryChapterView view;
            if (game.Regulars.TryDequeueStory(out view)) Open(view);
        }

        private void Open(StoryChapterView view)
        {
            current = view;
            lineIndex = 0;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            root.SetActive(true);
            ShowLine();
        }

        private void ShowLine()
        {
            if (current == null || current.Chapter.lines == null || current.Chapter.lines.Length == 0)
            {
                Finish();
                return;
            }
            StoryLineDefinition line = current.Chapter.lines[lineIndex];
            title.text = current.Chapter.title;
            speaker.text = SpeakerName(line.speaker);
            body.text = RegularText.Expand(line.text, game.OwnerName, game.ShopName, current.Regular.displayName);
            nextLabel.text = lineIndex >= current.Chapter.lines.Length - 1 ? "NHẬN QUÀ" : "TIẾP";
        }

        private string SpeakerName(string id)
        {
            if (id == "narrator") return "CHUYỆN Ở QUÁN";
            if (id == "owner") return string.IsNullOrEmpty(game.OwnerName) ? "CHỦ QUÁN" : game.OwnerName.ToUpperInvariant();
            return current.Regular.displayName.ToUpperInvariant();
        }

        /// Chỉ cho DebugAutoPilot: bấm TIẾP.
        public void DebugAdvance() { Advance(); }

        private void Advance()
        {
            if (current == null) return;
            lineIndex++;
            if (current.Chapter.lines == null || lineIndex >= current.Chapter.lines.Length) Finish();
            else ShowLine();
        }

        private void Finish()
        {
            StoryChapterView done = current;
            current = null;
            root.SetActive(false);
            Time.timeScale = previousTimeScale;
            if (done != null && game.Regulars.CompleteStory(done))
                game.Hud.ShowToast("Hoàn thành: " + done.Chapter.title);
        }

        public void Dispose()
        {
            if (IsOpen) Time.timeScale = previousTimeScale;
            current = null;
        }
    }
}
