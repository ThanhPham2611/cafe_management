using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// Màn tổng kết cuối ngày: nền tối mờ + panel giữa màn hình, icon trên từng dòng, nút đỏ "Ngày mới".
    public class SummaryPanel
    {
        private class Row
        {
            public Text label;
            public Text value;
        }

        private const float PopTime = 0.2f;
        private const float AdviceExtra = 80f;

        private readonly GameBootstrap game;
        private readonly GameObject root;
        private readonly Image dim;
        private readonly RectTransform card;
        private readonly Text title;
        private readonly Row rowRevenue, rowTips, rowScrap, rowIngredients, rowFee, rowStaffWages;
        private readonly Row rowServed, rowLost, rowRp;
        private readonly Row rowMissions;    // tiến độ nhiệm vụ ngày
        private readonly Row rowWeekly;      // dự án tuần (WeeklyProject)
        private const float WeeklyExtra = 64f; // cấp 1: thẻ cao thêm cho dòng dự án tuần (các cấp khác còn chỗ trống)
        private readonly GameObject adviceChip;  // gợi ý đầu ngày mới (DayAdvisor), chỉ hiện khi có
        private readonly Text adviceText;
        private readonly Row rowClean;       // độ sạch TB trong ngày (chỉ hiện khi CleaningRules.Enabled)
        private readonly GameObject rowCleanRoot;
        private readonly Row rowRent, rowDebt;   // thuê nhà + nợ (chỉ cấp 2 trở lên)
        private readonly GameObject rowRentRoot, rowDebtRoot;
        private readonly Text profitValue;
        private readonly Image[] starIcons;
        private float popTimer = -1f;

        public bool IsVisible { get { return root.activeSelf; } }

        public SummaryPanel(GameBootstrap game, Transform parent)
        {
            this.game = game;

            dim = HudBuilder.Img(parent, "SummaryPanel", HudBuilder.T.dim, Color.white, false, true);
            dim.color = new Color(0f, 0f, 0f, 0.55f);
            HudBuilder.Stretch(dim.rectTransform);
            root = dim.gameObject;

            Image panel = HudBuilder.Panel(dim.transform, "Card", true);
            card = panel.rectTransform;
            card.gameObject.AddComponent<FitInCanvas>();
            HudBuilder.SetFixed(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 800f));

            title = HudBuilder.AddText(card, "Title", "", 58, TextAnchor.MiddleCenter, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(900f, 84f));

            // cột trái: tiền
            rowRevenue = MakeRow(card, 52f, -134f, 450f, HudBuilder.T.iconCoin, Strings.SumRevenue);
            rowTips = MakeRow(card, 52f, -198f, 450f, HudBuilder.T.iconHeart, Strings.SumTips);
            rowScrap = MakeRow(card, 52f, -262f, 450f, HudBuilder.T.iconCup, Strings.SumScrap);
            rowIngredients = MakeRow(card, 52f, -326f, 450f, HudBuilder.T.iconPhin, Strings.SumIngredients);
            rowFee = MakeRow(card, 52f, -390f, 450f, HudBuilder.T.iconClock, Strings.SumFee);
            rowStaffWages = MakeRow(card, 52f, -454f, 450f, HudBuilder.T.iconCoin, Strings.SumStaffWages);
            HudBuilder.Fit(rowStaffWages.label, 22); // "Lương nhân viên" sát giá trị ở độ phân giải thấp
            HudBuilder.Fit(rowFee.label, 22);

            // lãi ròng: chip vàng
            Image chip = HudBuilder.Chip(card, "ProfitChip", false);
            chip.color = new Color(1f, 0.88f, 0.52f, 1f);
            HudBuilder.SetFixed(chip.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(86f, -528f), new Vector2(430f, 100f));
            Image coin = HudBuilder.Icon(chip.transform, "Coin", HudBuilder.T.iconCoin, 96f);
            HudBuilder.SetFixed(coin.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(2f, 4f), new Vector2(96f, 96f));
            Text pl = HudBuilder.AddText(chip.transform, "Label", Strings.SumProfit, 32, TextAnchor.MiddleLeft, HudBuilder.Ink, true, false);
            HudBuilder.SetRect(pl.rectTransform, new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(60f, 14f), new Vector2(0f, -4f));
            profitValue = HudBuilder.AddText(chip.transform, "Value", "", 44, TextAnchor.MiddleRight, HudBuilder.GoodDark, true, false);
            HudBuilder.SetRect(profitValue.rectTransform, new Vector2(0.4f, 0f), new Vector2(1f, 1f), new Vector2(0f, 14f), new Vector2(-30f, -4f));

            // cấp 2: thuê nhà + nợ, dưới chip lãi ròng (panel cao thêm khi hiện, xem Show)
            rowRent = MakeRow(card, 52f, -644f, 450f, HudBuilder.T.iconCoin, Strings.SumRent);
            rowRentRoot = rowRent.value.transform.parent.gameObject;
            rowDebt = MakeRow(card, 52f, -708f, 450f, HudBuilder.T.iconCoin, Strings.SumDebt);
            rowDebtRoot = rowDebt.value.transform.parent.gameObject;
            HudBuilder.Fit(rowDebt.label, 18);
            rowRentRoot.SetActive(false);
            rowDebtRoot.SetActive(false);

            // cột phải: thống kê
            rowServed = MakeRow(card, 540f, -134f, 410f, HudBuilder.T.iconCheck, Strings.SumServed);
            rowLost = MakeRow(card, 540f, -198f, 410f, HudBuilder.T.iconCross, Strings.SumLost);
            rowRp = MakeRow(card, 540f, -262f, 410f, HudBuilder.T.iconStar, Strings.SumRp);
            rowClean = MakeRow(card, 540f, -470f, 410f, CleaningRules.BroomIcon(HudBuilder.T), Strings.SumCleanliness);
            rowCleanRoot = rowClean.value.transform.parent.gameObject;
            rowCleanRoot.SetActive(false);
            rowMissions = MakeRow(card, 540f, -534f, 410f, HudBuilder.T.iconHeart, MissionStrings.SummaryRow);
            rowWeekly = MakeRow(card, 540f, -598f, 410f, HudBuilder.T.iconCup, "");
            HudBuilder.Fit(rowWeekly.label, 18);
            HudBuilder.Fit(rowWeekly.value, 22);

            // dải gợi ý nằm sát đáy, trên hàng nút (thẻ cao thêm AdviceExtra khi hiện)
            Image advice = HudBuilder.Chip(card, "AdviceChip", false);
            advice.color = new Color(1f, 0.93f, 0.7f, 1f);
            HudBuilder.SetFixed(advice.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 184f), new Vector2(900f, 64f));
            adviceChip = advice.gameObject;
            adviceText = HudBuilder.AddText(advice.transform, "Text", "", 26, TextAnchor.MiddleLeft, HudBuilder.Ink, false, false);
            adviceText.rectTransform.offsetMin = new Vector2(24f, 4f);
            adviceText.rectTransform.offsetMax = new Vector2(-24f, -4f);
            HudBuilder.Fit(adviceText, 18);
            adviceChip.SetActive(false);

            Text starLabel = HudBuilder.AddText(card, "StarsLabel", Strings.SumStars, 30, TextAnchor.MiddleLeft, HudBuilder.Ink, false, false);
            HudBuilder.SetFixed(starLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(548f, -340f), new Vector2(400f, 44f));
            int starCount = GameBalance.StarThresholds.Length;
            starIcons = new Image[starCount];
            for (int i = 0; i < starCount; i++)
            {
                Image s = HudBuilder.Icon(card, "Star" + i, HudBuilder.T.iconStar, 64f);
                HudBuilder.SetFixed(s.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(544f + i * 76f, -388f), new Vector2(64f, 64f));
                starIcons[i] = s;
            }

            Text nextLabel;
            ClayButton next = HudBuilder.AddLabelButton(card, "NextDay", ClayStyle.Red, Strings.NewDay, 54, OnNextDay, out nextLabel);
            HudBuilder.SetFixed((RectTransform)next.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(480f, 136f));

            // Sửa nhân vật chủ quán (không reset tiến trình)
            Text editLabel;
            ClayButton edit = HudBuilder.AddLabelButton(card, "EditLook", ClayStyle.Cream, Strings.EditLook, 30, OnEditLook, out editLabel);
            HudBuilder.SetFixed((RectTransform)edit.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(34f, 40f), new Vector2(210f, 110f));

            // Trang trí / mua đồ / nâng cấp quán trước khi mở cửa (đồng hồ đứng, chưa có khách)
            Text prepLabel;
            ClayButton prep = HudBuilder.AddLabelButton(card, "PrepShop", ClayStyle.Gold, Strings.PrepShop, 30, OnPrep, out prepLabel);
            HudBuilder.SetFixed((RectTransform)prep.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-34f, 40f), new Vector2(210f, 110f));

            root.SetActive(false);
        }

        private static Row MakeRow(RectTransform parent, float x, float y, float w, Sprite icon, string label)
        {
            var row = new Row();
            RectTransform r = HudBuilder.NewRect("Row_" + label, parent);
            HudBuilder.SetFixed(r, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, y), new Vector2(w, 58f));
            Image ic = HudBuilder.Icon(r, "Icon", icon, 52f);
            HudBuilder.SetFixed(ic.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(52f, 52f));
            Text l = HudBuilder.AddText(r, "Label", label, 30, TextAnchor.MiddleLeft, HudBuilder.Ink, false, false);
            HudBuilder.SetRect(l.rectTransform, Vector2.zero, new Vector2(0.62f, 1f), new Vector2(64f, 0f), Vector2.zero);
            row.label = l;
            row.value =HudBuilder.AddText(r, "Value", "", 36, TextAnchor.MiddleRight, HudBuilder.GoodDark, true, false);
            HudBuilder.SetRect(row.value.rectTransform, new Vector2(0.4f, 0f), Vector2.one, Vector2.zero, Vector2.zero);
            return row;
        }

        private void OnEditLook()
        {
            game.OpenCreator(true);
        }

        private void OnPrep()
        {
            game.EnterPrep();
        }

        private void OnNextDay()
        {
            Hide();
            game.StartNextDay();
        }

        public void Hide()
        {
            root.SetActive(false);
            popTimer = -1f;
        }

        public void Show(DaySummary s)
        {
            title.text = string.Format(Strings.SummaryTitleFmt, s.day) + "  ·  " + Strings.TierShortName(s.tier);
            SetMoney(rowRevenue, s.revenue, true);
            SetMoney(rowTips, s.tips, true);
            SetMoney(rowScrap, s.scrap, true);
            SetMoney(rowIngredients, -s.ingredientCost, true);
            SetMoney(rowFee, -s.fee, true);
            SetMoney(rowStaffWages, -s.staffWages, true);

            // Cấp 2+: "Chi phí vận hành" thay "Phí vỉa hè", thêm dòng nợ; cấp 2 còn dòng thuê nhà (cấp 3 đã mua nhà)
            bool rented = s.tier >= 2;
            bool paysRent = s.tier == 2;
            rowFee.label.text = rented ? Strings.SumFixedCost : Strings.SumFee;
            rowRentRoot.SetActive(paysRent);
            rowDebtRoot.SetActive(rented);
            ((RectTransform)rowDebtRoot.transform).anchoredPosition = new Vector2(52f, paysRent ? -708f : -644f);
            rowStaffWages.value.transform.parent.gameObject.SetActive(rented);
            DayAdvice advice = game.AdviseFor(s);
            string adviceMsg = DayAdvisor.Text(advice);
            bool hasAdvice = advice != DayAdvice.None && adviceMsg.Length > 0;
            adviceChip.SetActive(hasAdvice);
            if (hasAdvice) adviceText.text = "Gợi ý: " + adviceMsg;
            // cấp 2: thêm dòng thuê nhà + nợ, chừa chỗ để nút không che; có gợi ý thì cao thêm một dải
            card.sizeDelta = new Vector2(1000f, (paysRent ? 960f : (rented ? 900f : 800f + WeeklyExtra)) + (hasAdvice ? AdviceExtra : 0f));
            ShowWeekly(s.day);
            if (rented)
            {
                SetMoney(rowRent, -s.rent, true);
                rowDebt.label.text = s.debtPaid > 0 || s.debt > 0 ? string.Format(Strings.SumDebtFmt, Strings.FormatInt(s.debt)) : Strings.SumDebt;
                SetMoney(rowDebt, -s.debtPaid, true);
                if (s.debtPaid == 0 && s.debt > 0) rowDebt.value.color = HudBuilder.Red;
            }

            int net = s.NetProfit;
            profitValue.text = (net >= 0 ? "+" : "") + Strings.FormatInt(net) + " Xu";
            profitValue.color = net >= 0 ? HudBuilder.GoodDark : HudBuilder.Red;

            rowServed.value.text = s.served.ToString();
            rowServed.value.color = HudBuilder.GoodDark;
            rowLost.value.text = s.lost.ToString();
            rowLost.value.color = s.lost > 0 ? HudBuilder.Red : HudBuilder.InkSoft;
            int lvNow = game.Reputation.Level;
            rowRp.value.text = (s.rpGained >= 0f ? "+" : "") + s.rpGained.ToString("0.0") + " RP"
                + (s.levelStart < lvNow ? string.Format(Strings.SumLevelUpFmt, s.levelStart, lvNow) : string.Format(Strings.SumLevelFmt, lvNow));
            rowRp.value.color = s.rpGained >= 0f ? HudBuilder.GoodDark : HudBuilder.Red;

            bool showClean = CleaningRules.Enabled && s.cleanliness >= 0f;
            rowCleanRoot.SetActive(showClean);
            if (showClean)
            {
                rowClean.value.text = Mathf.RoundToInt(s.cleanliness) + "%";
                rowClean.value.color = s.cleanliness < GameBalance.CiWalkAwayBelow ? HudBuilder.Red : HudBuilder.GoodDark;
            }

            for (int i = 0; i < starIcons.Length; i++)
                starIcons[i].color = i < s.stars ? Color.white : new Color(0.35f, 0.28f, 0.24f, 0.4f);

            ShowMissions();

            root.SetActive(true);
            popTimer = 0f;
            card.localScale = new Vector3(0.9f, 0.9f, 1f);
            dim.color = new Color(0f, 0f, 0f, 0f);
        }

        /// Dòng "Tuần 3/7: khách  64/110" (đổi theo loại dự án của tuần); xong thì "XONG!".
        private void ShowWeekly(int day)
        {
            WeeklyProject w = game.Weekly;
            string fmt = w.Kind == WeeklyKind.Serve ? Strings.WeeklyServeFmt : (w.Kind == WeeklyKind.Tips ? Strings.WeeklyTipsFmt : Strings.WeeklyProfitFmt);
            rowWeekly.label.text = string.Format(fmt, WeeklyProject.DayInWeek(day));
            rowWeekly.value.text = w.Done ? Strings.WeeklyDone : Strings.FormatInt(w.Progress) + "/" + Strings.FormatInt(w.Target);
            rowWeekly.value.color = w.Done ? HudBuilder.Red : HudBuilder.GoodDark;
        }

        private void ShowMissions()
        {
            MissionBook book = game.Missions;
            bool any = book != null && book.Items.Count > 0;
            rowMissions.value.transform.parent.gameObject.SetActive(any);
            if (!any) return;
            int done = 0;
            for (int i = 0; i < book.Items.Count; i++)
                if (book.Items[i].IsDone) done++;
            rowMissions.value.text = done + "/" + book.Items.Count + (book.HasClaimable ? MissionStrings.SummaryHasReward : "");
            rowMissions.value.color = book.HasClaimable ? HudBuilder.Red : HudBuilder.GoodDark;
        }

        private static void SetMoney(Row row, int v, bool sign)
        {
            row.value.text = (v > 0 && sign ? "+" : "") + Strings.FormatInt(v) + " Xu";
            row.value.color = v < 0 ? HudBuilder.Red : (v == 0 ? HudBuilder.InkSoft : HudBuilder.GoodDark);
        }

        /// Gọi mỗi frame từ HudController: hiệu ứng bật lên.
        public void Tick()
        {
            if (popTimer < 0f) return;
            popTimer += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(popTimer / PopTime);
            float e = 1f - (1f - t) * (1f - t);
            float s = 0.9f + 0.1f * e;
            // nảy nhẹ quá 1.0 giữa chừng
            if (t < 1f) s += 0.04f * Mathf.Sin(t * Mathf.PI);
            card.localScale = new Vector3(s, s, 1f);
            dim.color = new Color(0f, 0f, 0f, 0.55f * e);
            if (t >= 1f)
            {
                popTimer = -1f;
                card.localScale = Vector3.one;
                dim.color = new Color(0f, 0f, 0f, 0.55f);
            }
        }
    }
}
