using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// UI của chuyển cấp (phong cách "Clay Café"):
    ///  - Cấp 1 / 2: thẻ tiến độ "Thuê mặt bằng" / "Mua nhà hàng" (Xu 12.400/18.000, RP 120/300) dưới tên quán, góc phải;
    ///    đủ điều kiện thì thành nút vàng lớn.
    ///  - Hộp xác nhận (đặt cọc 18.000 Xu / mua nhà hàng), màn đen mờ chuyển cảnh, pháo giấy + chữ "Khai trương!".
    ///  - Cấp 2: dòng nhỏ "Thuê nhà: còn N ngày / Nợ" dưới thẻ; cấp 3: chỉ hiện nợ còn lại (nếu có).
    /// Dựng bằng code từ HudController.Init (UpgradePanel.Create); TierManager gọi Tick() mỗi frame (không đăng ký sự kiện).
    public class UpgradePanel
    {
        private const float Margin = 24f;
        private const float TopOffset = Margin + 96f + 6f + 40f + 10f; // dưới chip ngày + tên quán
        private const float CardW = 440f;
        private const float CardH = 170f;
        private const float CtaH = 150f;

        private const int ConfettiCount = 72;
        private const float ConfettiMaxTime = 6f;
        private const float BannerTime = 2.6f;

        private readonly GameBootstrap game;
        private readonly TierManager tier;

        // chip trên HUD
        private RectTransform chipRoot;
        private ClayButton lockedCard;
        private Text cardTitle, ctaTitle;
        private Text xuText, rpText;
        private BarView xuBar, rpBar;
        private RectTransform ctaWrap;
        private Text ctaSub;
        private Text infoText;
        private int lastXu = int.MinValue, lastRp = int.MinValue, lastDays = int.MinValue, lastDebt = int.MinValue, lastNext = -1;
        private float pulse;

        // lớp phủ
        private Canvas overlay;
        private Image fade;
        private float fadeAlpha;
        private GameObject dialog;
        private Text dialogTitle, dialogBody, yesLabel;

        // pháo giấy + biểu ngữ
        private RectTransform confettiLayer;
        private Image[] cImg;
        private RectTransform[] cRt;
        private float[] cx, cy, cvy, cvr, cph, crot, cfreq;
        private bool confettiOn;
        private float confettiTime;
        private Text banner;
        private CanvasGroup bannerGroup;
        private float bannerTime = -1f;

        public float FadeAlpha { get { return fadeAlpha; } }

        public static UpgradePanel Create(GameBootstrap game, RectTransform safe)
        {
            if (game == null || game.Tier == null || safe == null) return null;
            var p = new UpgradePanel(game, safe);
            game.Tier.AttachPanel(p);
            return p;
        }

        private UpgradePanel(GameBootstrap game, RectTransform safe)
        {
            this.game = game;
            tier = game.Tier;

            Canvas hudCanvas = safe.GetComponentInParent<Canvas>();
            Transform overlayParent = hudCanvas != null && hudCanvas.transform.parent != null ? hudCanvas.transform.parent : game.transform;
            overlay = HudBuilder.CreateCanvas("TierCanvas", 40, true, overlayParent);

            BuildChip(safe);
            BuildDialog();
            BuildFade();
        }

        // ------------------------------------------------------------------ dựng UI

        private void BuildChip(RectTransform safe)
        {
            chipRoot = HudBuilder.NewRect("TierChip", safe);
            HudBuilder.SetFixed(chipRoot, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-Margin, -TopOffset), new Vector2(CardW, CardH));

            // --- thẻ tiến độ (chưa đủ điều kiện)
            lockedCard = HudBuilder.AddClayButton(chipRoot, "TierCard", HudBuilder.T.card, null, HudBuilder.Cream, OnTap);
            HudBuilder.Stretch((RectTransform)lockedCard.transform);
            RectTransform c = lockedCard.Content;

            cardTitle = HudBuilder.AddText(c, "Title", Strings.TierCardTitle, 34, TextAnchor.MiddleLeft, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(cardTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -6f), new Vector2(384f, 38f));
            HudBuilder.Fit(cardTitle, 22);

            xuText = HudBuilder.AddText(c, "Xu", "", 24, TextAnchor.MiddleLeft, HudBuilder.InkSoft, false, false);
            HudBuilder.SetFixed(xuText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -44f), new Vector2(384f, 26f));
            xuBar = HudBuilder.CreateBar(c, "XuBar");
            HudBuilder.SetFixed(xuBar.root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -72f), new Vector2(384f, 18f));

            rpText = HudBuilder.AddText(c, "Rp", "", 24, TextAnchor.MiddleLeft, HudBuilder.InkSoft, false, false);
            HudBuilder.SetFixed(rpText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -94f), new Vector2(384f, 26f));
            rpBar = HudBuilder.CreateBar(c, "RpBar");
            HudBuilder.SetFixed(rpBar.root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -122f), new Vector2(384f, 18f));

            // --- nút vàng lớn (đủ điều kiện)
            ctaWrap = HudBuilder.NewRect("TierCtaWrap", chipRoot);
            HudBuilder.SetFixed(ctaWrap, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(CardW, CtaH));
            ClayButton cta = HudBuilder.AddLabelButton(ctaWrap, "TierCta", ClayStyle.Gold, Strings.TierCtaTitle, 52, OnTap, out ctaTitle);
            HudBuilder.Stretch((RectTransform)cta.transform);
            ctaTitle.rectTransform.offsetMin = new Vector2(10f, 46f);
            ctaTitle.rectTransform.offsetMax = new Vector2(-10f, -2f);
            ctaSub = HudBuilder.AddText(cta.Content, "Sub", string.Format(Strings.TierCtaSubFmt, Strings.FormatInt(GameBalance.Tier2Deposit)),
                28, TextAnchor.MiddleCenter, HudBuilder.Ink, false, false);
            HudBuilder.SetRect(ctaSub.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(10f, 6f), new Vector2(-10f, 46f));

            // --- cấp 2+: dòng thông tin thuê nhà / nợ, nằm dưới thẻ
            infoText = HudBuilder.AddText(chipRoot, "Info", "", 28, TextAnchor.UpperRight, HudBuilder.Cream, true, true);
            infoText.horizontalOverflow = HorizontalWrapMode.Wrap;
            HudBuilder.SetFixed(infoText.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(0f, -(CardH + 8f)), new Vector2(CardW, 84f));

            lockedCard.gameObject.SetActive(false);
            ctaWrap.gameObject.SetActive(false);
            infoText.gameObject.SetActive(false);
            chipRoot.gameObject.SetActive(false);
        }

        private void BuildDialog()
        {
            Image dim = HudBuilder.Img(overlay.transform, "TierConfirm", HudBuilder.T.dim, Color.white, false, true);
            dim.color = new Color(0f, 0f, 0f, 0.55f);
            HudBuilder.Stretch(dim.rectTransform);
            dialog = dim.gameObject;

            Image panel = HudBuilder.Panel(dim.transform, "Card", true);
            HudBuilder.SetFixed(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940f, 640f));

            dialogTitle = HudBuilder.AddText(panel.transform, "Title", Strings.TierConfirmTitle, 64, TextAnchor.MiddleCenter, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(dialogTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(860f, 90f));

            dialogBody = HudBuilder.AddText(panel.transform, "Body", "", 36, TextAnchor.MiddleCenter, HudBuilder.Ink, false, false);
            HudBuilder.SetRect(dialogBody.rectTransform, Vector2.zero, Vector2.one, new Vector2(50f, 200f), new Vector2(-50f, -130f));
            HudBuilder.Fit(dialogBody, 24);

            Text lt;
            ClayButton yes = HudBuilder.AddLabelButton(panel.transform, "Yes", ClayStyle.Green, Strings.TierYes, 52, OnYes, out yesLabel);
            HudBuilder.SetFixed((RectTransform)yes.transform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(16f, 40f), new Vector2(380f, 130f));
            ClayButton no = HudBuilder.AddLabelButton(panel.transform, "No", ClayStyle.Cream, Strings.TierLater, 48, OnNo, out lt);
            HudBuilder.SetFixed((RectTransform)no.transform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 40f), new Vector2(380f, 130f));

            dialog.SetActive(false);
        }

        private void BuildFade()
        {
            // lớp pháo giấy nằm dưới màn đen
            confettiLayer = HudBuilder.NewRect("Confetti", overlay.transform);
            HudBuilder.Stretch(confettiLayer);

            banner = HudBuilder.AddText(overlay.transform, "Banner", Strings.GrandOpening, 150, TextAnchor.MiddleCenter, HudBuilder.Accent, true, true);
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            bannerGroup.blocksRaycasts = false;
            bannerGroup.interactable = false;
            Outline o = banner.GetComponent<Outline>();
            if (o != null) o.effectDistance = new Vector2(5f, -5f);
            banner.gameObject.SetActive(false);

            fade = HudBuilder.Img(overlay.transform, "Fade", null, Color.black, false, false);
            fade.color = new Color(0f, 0f, 0f, 0f);
            HudBuilder.Stretch(fade.rectTransform);
        }

        // ------------------------------------------------------------------ gọi từ TierManager

        public void SetFade(float a)
        {
            fadeAlpha = Mathf.Clamp01(a);
            if (fade == null) return;
            fade.color = new Color(0f, 0f, 0f, fadeAlpha);
            fade.raycastTarget = fadeAlpha > 0.001f; // đang tối: chặn mọi chạm
        }

        public void ShowConfirm()
        {
            if (dialog == null) return;
            if (tier.NextTier >= 3)
            {
                dialogTitle.text = Strings.Tier3ConfirmTitle;
                yesLabel.text = Strings.Tier3YesLabel;
                dialogBody.text = string.Format(Strings.Tier3ConfirmBodyFmt,
                    Strings.FormatInt(GameBalance.Tier3Price),
                    Strings.FormatInt(tier.Deposit),
                    Strings.FormatInt(tier.NextTierXu),
                    Strings.FormatInt(GameBalance.Tier3DailyFixedCost));
            }
            else
            {
                dialogTitle.text = Strings.TierConfirmTitle;
                yesLabel.text = Strings.TierYes;
                dialogBody.text = string.Format(Strings.TierConfirmBodyFmt,
                    Strings.FormatInt(GameBalance.Tier2Deposit),
                    Strings.FormatInt(GameBalance.Tier2RentPerWeek),
                    GameBalance.RentIntervalDays);
            }
            dialog.SetActive(true);
        }

        public void HideConfirm()
        {
            if (dialog != null) dialog.SetActive(false);
        }

        public void Dispose()
        {
            if (overlay != null) Object.Destroy(overlay.gameObject);
        }

        private void OnTap()
        {
            tier.RequestUpgrade();
        }

        private void OnYes()
        {
            HideConfirm();
            // Xu có thể vừa tụt xuống dưới mức cọc (mua nguyên liệu) trong lúc hộp thoại đang mở
            if (!tier.ConfirmUpgrade() && !tier.Transitioning)
                GameEvents.RaiseToast(tier.NeedMessage());
        }

        private void OnNo()
        {
            HideConfirm();
        }

        // ------------------------------------------------------------------ mỗi frame

        public void Tick(float dt)
        {
            TickChip(dt);
            if (confettiOn) TickConfetti(dt);
            if (bannerTime >= 0f) TickBanner(dt);
        }

        private void TickChip(float dt)
        {
            if (chipRoot == null) return;
            DayCycle day = game.Day;
            bool playing = day != null && (day.CurrentPhase == DayCycle.Phase.Open || day.CurrentPhase == DayCycle.Phase.Closing
                                           || day.CurrentPhase == DayCycle.Phase.Prep);
            bool hidden = tier.Transitioning || !playing
                          || (game.Builder != null && game.Builder.IsOpen)
                          || (game.Creator != null && game.Creator.IsOpen);

            bool hasNext = tier.NextTier != 0;
            bool affordable = hasNext && tier.CanUpgrade;
            bool showLocked = !hidden && hasNext && !affordable;
            bool showCta = !hidden && affordable;
            string info = hidden ? "" : RefreshInfo();
            bool showInfo = info.Length > 0;

            SetActive(lockedCard.gameObject, showLocked);
            SetActive(ctaWrap.gameObject, showCta);
            SetActive(infoText.gameObject, showInfo);
            SetActive(chipRoot.gameObject, showLocked || showCta || showInfo);

            if (showLocked) RefreshProgress();
            if (showCta)
            {
                RefreshCta();
                pulse += dt * 4.2f;
                float s = 1f + 0.035f * Mathf.Sin(pulse);
                ctaWrap.localScale = new Vector3(s, s, 1f);
            }
            if (showInfo) infoText.text = info;
        }

        private static void SetActive(GameObject go, bool v)
        {
            if (go.activeSelf != v) go.SetActive(v);
        }

        private void RefreshCta()
        {
            bool restaurant = tier.NextTier >= 3;
            ctaTitle.text = restaurant ? Strings.Tier3CtaTitle : Strings.TierCtaTitle;
            ctaSub.text = restaurant
                ? string.Format(Strings.Tier3CtaSubFmt, Strings.FormatInt(tier.NextTierXu))
                : string.Format(Strings.TierCtaSubFmt, Strings.FormatInt(GameBalance.Tier2Deposit));
        }

        private void RefreshProgress()
        {
            int xu = tier.XuNow;
            int rp = tier.RpNow;
            int next = tier.NextTier;
            if (xu == lastXu && rp == lastRp && next == lastNext) return;
            lastXu = xu;
            lastRp = rp;
            lastNext = next;

            cardTitle.text = next >= 3 ? Strings.Tier3CardTitle : Strings.TierCardTitle;
            int needXu = tier.NextTierXu;
            int needRp = tier.NextTierRp;
            xuText.text = string.Format(Strings.TierXuFmt, Strings.FormatInt(xu), Strings.FormatInt(needXu));
            rpText.text = string.Format(Strings.TierRpFmt, Strings.FormatInt(rp), Strings.FormatInt(needRp));
            xuBar.Set(xu / (float)needXu, xu >= needXu ? HudBuilder.Good : HudBuilder.Accent);
            rpBar.Set(rp / (float)needRp, rp >= needRp ? HudBuilder.Good : HudBuilder.Accent);
        }

        private string infoCache = "";

        /// Dòng dưới thẻ: cấp 2 = "Thuê nhà: còn N ngày" (+ nợ); cấp 3 = chỉ nợ còn lại. Rỗng = không hiện.
        private string RefreshInfo()
        {
            RentLedger l = tier.Ledger;
            if (l == null || tier.Current < 2) return "";
            int days = tier.IsRented ? l.DaysUntilRent : -1;
            int debt = l.Debt;
            if (days == lastDays && debt == lastDebt) return infoCache;
            lastDays = days;
            lastDebt = debt;

            string debtLine = debt > 0 ? string.Format(Strings.DebtInfoFmt, Strings.FormatInt(debt)) : "";
            if (days < 0) return infoCache = debtLine;
            string rent = days <= 0 ? Strings.RentInfoTomorrow : string.Format(Strings.RentInfoFmt, days);
            return infoCache = debt > 0 ? rent + "\n" + debtLine : rent;
        }

        // ------------------------------------------------------------------ pháo giấy

        private void EnsureConfetti()
        {
            if (cImg != null) return;
            cImg = new Image[ConfettiCount];
            cRt = new RectTransform[ConfettiCount];
            cx = new float[ConfettiCount];
            cy = new float[ConfettiCount];
            cvy = new float[ConfettiCount];
            cvr = new float[ConfettiCount];
            cph = new float[ConfettiCount];
            crot = new float[ConfettiCount];
            cfreq = new float[ConfettiCount];
            for (int i = 0; i < ConfettiCount; i++)
            {
                Image img = HudBuilder.Img(confettiLayer, "C" + i, null, Color.white, false, false);
                RectTransform rt = img.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(Random.Range(16f, 28f), Random.Range(10f, 18f));
                img.gameObject.SetActive(false);
                cImg[i] = img;
                cRt[i] = rt;
            }
        }

        /// Pháo giấy; withBanner: kèm biểu ngữ lớn (mặc định "Khai trương!", hoặc bannerText).
        public void PlayConfetti(bool withBanner = true, string bannerText = null)
        {
            EnsureConfetti();
            Vector2 size = confettiLayer.rect.size;
            if (size.x < 10f || size.y < 10f) size = new Vector2(1920f, 1080f);

            Color[] palette =
            {
                HudBuilder.Accent, HudBuilder.Red, HudBuilder.Good, HudBuilder.Mint, HudBuilder.Cream,
                new Color32(0x2E, 0x6F, 0xD1, 0xFF), new Color32(0xF2, 0x8D, 0xB2, 0xFF),
            };
            for (int i = 0; i < ConfettiCount; i++)
            {
                cx[i] = Random.value * size.x;
                cy[i] = size.y + Random.Range(20f, 700f);
                cvy[i] = -Random.Range(380f, 780f);
                cvr[i] = Random.Range(-360f, 360f);
                cph[i] = Random.value * 6.28f;
                cfreq[i] = Random.Range(2f, 4.5f);
                crot[i] = Random.value * 360f;
                cImg[i].color = palette[Random.Range(0, palette.Length)];
                cImg[i].gameObject.SetActive(true);
            }
            confettiTime = 0f;
            confettiOn = true;

            if (withBanner)
            {
                banner.text = bannerText ?? Strings.GrandOpening;
                banner.gameObject.SetActive(true);
                bannerTime = 0f;
            }
        }

        private void TickConfetti(float dt)
        {
            confettiTime += dt;
            int alive = 0;
            for (int i = 0; i < ConfettiCount; i++)
            {
                if (!cImg[i].gameObject.activeSelf) continue;
                cy[i] += cvy[i] * dt;
                cx[i] += Mathf.Sin(confettiTime * cfreq[i] + cph[i]) * 70f * dt;
                crot[i] += cvr[i] * dt;
                if (cy[i] < -40f)
                {
                    cImg[i].gameObject.SetActive(false);
                    continue;
                }
                alive++;
                cRt[i].anchoredPosition = new Vector2(cx[i], cy[i]);
                cRt[i].localEulerAngles = new Vector3(0f, 0f, crot[i]);
            }
            if (alive == 0 || confettiTime > ConfettiMaxTime)
            {
                for (int i = 0; i < ConfettiCount; i++) cImg[i].gameObject.SetActive(false);
                confettiOn = false;
            }
        }

        private void TickBanner(float dt)
        {
            bannerTime += dt;
            float t = bannerTime;
            float scale;
            if (t < 0.35f)
            {
                float k = t / 0.35f;
                scale = Mathf.Lerp(0.2f, 1.15f, 1f - (1f - k) * (1f - k));
            }
            else if (t < 0.55f)
            {
                scale = Mathf.Lerp(1.15f, 1f, (t - 0.35f) / 0.2f);
            }
            else
            {
                scale = 1f;
            }
            banner.rectTransform.localScale = new Vector3(scale, scale, 1f);
            float fadeOut = Mathf.Clamp01((BannerTime - t) / 0.6f);
            bannerGroup.alpha = Mathf.Min(1f, t / 0.15f) * fadeOut;
            if (t >= BannerTime)
            {
                bannerTime = -1f;
                banner.gameObject.SetActive(false);
            }
        }
    }
}
