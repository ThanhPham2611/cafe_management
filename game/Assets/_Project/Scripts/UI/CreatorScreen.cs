using UnityEngine;
using UnityEngine.UI;

namespace Cafe
{
    /// Màn tạo / sửa nhân vật chủ quán (GDD §2.1). Panel kem bên trái, chủ quán xem trực tiếp (3D) bên phải.
    /// Ô nhập tên nằm nửa trên để bàn phím mềm không che. Xác nhận -> GameBootstrap.ConfirmProfile.
    public class CreatorScreen : MonoBehaviour
    {
        private const float PanelW = 1000f;
        private const float PanelH = 1010f;
        private const float Pad = 40f;
        private const float FocusDistance = 4.4f; // 3.3 cắt mất đầu nhân vật

        private GameBootstrap game;
        private GameObject root;
        private RectTransform panel;
        private InputField ownerField;
        private InputField shopField;
        private Text errorText;
        private Text titleText;
        private Text confirmLabel;
        private ClayButton confirmButton;
        private ClayButton cancelButton;

        private readonly ClayButton[] presetButtons = new ClayButton[4];
        private Image[][] rings;
        private int[][] swatchSets;

        private OwnerLook look;
        private OwnerLook originalLook;
        private string originalShop;
        private bool edit;
        private bool open;

        public bool IsOpen { get { return open; } }

        public static CreatorScreen Create(GameBootstrap g)
        {
            var go = new GameObject("CreatorScreen");
            var c = go.AddComponent<CreatorScreen>();
            c.Build(g);
            return c;
        }

        // ------------------------------------------------------------------ dựng UI

        private void Build(GameBootstrap g)
        {
            game = g;

            Canvas canvas = HudBuilder.CreateCanvas("CreatorCanvas", 15, true, transform);
            RectTransform safe = HudBuilder.NewRect("SafeArea", canvas.transform);
            HudBuilder.Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();
            root = canvas.gameObject;

            Image p = HudBuilder.Panel(safe, "Panel", true);
            panel = p.rectTransform;
            HudBuilder.SetFixed(panel, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(PanelW, PanelH));

            titleText = HudBuilder.AddText(panel, "Title", Strings.CreatorTitle, 60, TextAnchor.MiddleCenter, HudBuilder.Ink, true, false);
            HudBuilder.SetFixed(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(PanelW - 80f, 80f));

            // --- Tên (nửa trên)
            float colW = (PanelW - Pad * 2f - 30f) * 0.5f;
            AddLabel(Strings.CreatorOwnerName, Pad, 104f, colW);
            AddLabel(Strings.CreatorShopName, Pad + colW + 30f, 104f, colW);
            ownerField = MakeField("OwnerName", Pad, 140f, colW, Strings.CreatorOwnerName, 16);
            shopField = MakeField("ShopName", Pad + colW + 30f, 140f, colW, Strings.DefaultShopName, 20);
            ownerField.onValueChanged.AddListener(OnNameEdited);
            shopField.onValueChanged.AddListener(OnShopEdited);

            // --- Dáng người
            AddLabel(Strings.CreatorLook, Pad, 246f, 600f);
            const float bw = 210f, bgap = 20f;
            for (int i = 0; i < 4; i++)
            {
                int index = i;
                Text lbl;
                ClayButton b = HudBuilder.AddLabelButton(panel, "Preset" + i, ClayStyle.Cream, Strings.PresetNames[i], 34, () => OnPreset(index), out lbl);
                HudBuilder.SetFixed((RectTransform)b.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Pad + i * (bw + bgap), -282f), new Vector2(bw, 100f));
                presetButtons[i] = b;
            }

            // --- Màu
            swatchSets = new[] { OwnerPalette.SkinSwatches, OwnerPalette.HairSwatches, OwnerPalette.ShirtSwatches };
            rings = new Image[3][];
            BuildSwatchRow(0, Strings.CreatorSkin, 404f);
            BuildSwatchRow(1, Strings.CreatorHair, 526f);
            BuildSwatchRow(2, Strings.CreatorShirt, 648f);

            // --- Lỗi + nút
            errorText = HudBuilder.AddText(panel, "Error", "", 28, TextAnchor.MiddleLeft, HudBuilder.Red, true, false);
            HudBuilder.SetFixed(errorText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Pad, -792f), new Vector2(PanelW - 80f, 40f));

            confirmButton = HudBuilder.AddLabelButton(panel, "Confirm", ClayStyle.Red, Strings.CreatorOpen, 54, OnConfirm, out confirmLabel);
            cancelButton = HudBuilder.AddLabelButton(panel, "Cancel", ClayStyle.Cream, Strings.CreatorCancel, 40, OnCancel, out confirmLabelUnused);
            LayoutButtons(false);

            root.SetActive(false);
        }

        private Text confirmLabelUnused;

        private void LayoutButtons(bool editMode)
        {
            RectTransform c = (RectTransform)confirmButton.transform;
            RectTransform x = (RectTransform)cancelButton.transform;
            if (editMode)
            {
                HudBuilder.SetFixed(c, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(Pad, 28f), new Vector2(560f, 140f));
                HudBuilder.SetFixed(x, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-Pad, 28f), new Vector2(300f, 140f));
            }
            else
            {
                HudBuilder.SetFixed(c, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 28f), new Vector2(640f, 150f));
            }
            cancelButton.gameObject.SetActive(editMode);
        }

        private Text AddLabel(string text, float x, float y, float w)
        {
            Text t = HudBuilder.AddText(panel, "Label_" + text, text, 28, TextAnchor.MiddleLeft, HudBuilder.InkSoft, true, false);
            HudBuilder.SetFixed(t.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(w, 36f));
            return t;
        }

        private InputField MakeField(string name, float x, float y, float w, string placeholder, int limit)
        {
            Image bg = HudBuilder.Img(panel, name, HudBuilder.T.barTrack, HudBuilder.CreamDark, true, true);
            HudBuilder.SetFixed(bg.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(w, 88f));

            Text text = HudBuilder.AddText(bg.transform, "Text", "", 36, TextAnchor.MiddleLeft, HudBuilder.Ink, false, false);
            HudBuilder.SetRect(text.rectTransform, Vector2.zero, Vector2.one, new Vector2(22f, 6f), new Vector2(-22f, -6f));
            text.supportRichText = false;

            Text ph = HudBuilder.AddText(bg.transform, "Placeholder", placeholder, 32, TextAnchor.MiddleLeft,
                new Color(HudBuilder.InkSoft.r, HudBuilder.InkSoft.g, HudBuilder.InkSoft.b, 0.55f), false, false);
            HudBuilder.SetRect(ph.rectTransform, Vector2.zero, Vector2.one, new Vector2(22f, 6f), new Vector2(-22f, -6f));
            ph.fontStyle = FontStyle.Italic;

            var f = bg.gameObject.AddComponent<InputField>();
            f.targetGraphic = bg;
            f.textComponent = text;
            f.placeholder = ph;
            f.lineType = InputField.LineType.SingleLine;
            f.characterLimit = limit;
            f.caretWidth = 3;
            f.customCaretColor = true;
            f.caretColor = HudBuilder.Ink;
            f.selectionColor = new Color(HudBuilder.Accent.r, HudBuilder.Accent.g, HudBuilder.Accent.b, 0.5f);
            return f;
        }

        private void BuildSwatchRow(int kind, string label, float y)
        {
            AddLabel(label, Pad, y, 600f);
            int[] set = swatchSets[kind];
            rings[kind] = new Image[set.Length];
            for (int i = 0; i < set.Length; i++)
            {
                int swatch = set[i];
                RectTransform holder = HudBuilder.NewRect("Swatch_" + swatch, panel);
                HudBuilder.SetFixed(holder, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(Pad + i * 106f, -(y + 40f)), new Vector2(100f, 100f));

                Image ring = HudBuilder.Img(holder, "Ring", HudBuilder.T.disc, HudBuilder.Ink, false, false);
                ring.color = HudBuilder.Ink;
                HudBuilder.SetFixed(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(98f, 98f));
                ring.enabled = false;
                rings[kind][i] = ring;

                int k = kind;
                ClayButton b = HudBuilder.AddClayButton(holder, "Btn", HudBuilder.T.Button(ClayStyle.Cream, false), HudBuilder.T.Button(ClayStyle.Cream, true),
                    OwnerPalette.Color(swatch), () => OnSwatch(k, swatch));
                HudBuilder.SetFixed((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 80f));
                b.SetTint(OwnerPalette.Color(swatch));
            }
        }

        // ------------------------------------------------------------------ mở / đóng

        public void Open(bool editMode)
        {
            edit = editMode;
            originalLook = game.OwnerLook;
            originalShop = game.ShopName;
            look = game.OwnerLook;

            ownerField.text = game.OwnerName ?? "";
            shopField.text = string.IsNullOrEmpty(game.ShopName) ? Strings.DefaultShopName : game.ShopName;
            errorText.text = "";
            confirmLabel.text = editMode ? Strings.CreatorSave : Strings.CreatorOpen;
            LayoutButtons(editMode);
            RefreshSelection();

            open = true;
            root.SetActive(true);
            if (game.Hud != null) game.Hud.SetHudActive(false);

            OwnerController o = game.Owner;
            if (o != null)
            {
                o.SetLook(look);
                o.SetHoldFacing(true);
                Vector3 outward = -o.CartFaceDirection; // phía khách đứng, nơi camera đặt
                o.FaceDirection(outward);
                if (game.CameraRig != null)
                {
                    Camera cam = Camera.main;
                    float shift = 0f;
                    if (cam != null)
                    {
                        // Nhân vật nằm giữa vùng trống bên phải panel (~0.55 nửa bề rộng màn hình)
                        float halfH = FocusDistance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                        shift = 0.55f * halfH * (cam.aspect > 0.1f ? cam.aspect : 16f / 9f);
                    }
                    game.CameraRig.FocusOn(o.FocusPoint, FocusDistance, outward, 14f, shift);
                }
            }
        }

        private void Close(bool applyLook)
        {
            open = false;
            root.SetActive(false);
            if (ownerField != null && ownerField.isFocused) ownerField.DeactivateInputField();
            if (shopField != null && shopField.isFocused) shopField.DeactivateInputField();
            if (game.Hud != null) game.Hud.SetHudActive(true);
            if (game.CameraRig != null) game.CameraRig.ClearFocus();
            if (game.Owner != null)
            {
                game.Owner.SetHoldFacing(false);
                if (!applyLook) game.Owner.SetLook(originalLook);
            }
        }

        // ------------------------------------------------------------------ sự kiện

        private void OnPreset(int i)
        {
            look.preset = i;
            Apply();
        }

        private void OnSwatch(int kind, int swatch)
        {
            if (kind == 0) look.skinSwatch = swatch;
            else if (kind == 1) look.hairSwatch = swatch;
            else look.shirtSwatch = swatch;
            Apply();
        }

        private void Apply()
        {
            if (game.Owner != null)
            {
                game.Owner.SetLook(look);
                game.Owner.FaceDirection(-game.Owner.CartFaceDirection);
            }
            RefreshSelection();
        }

        private void OnNameEdited(string s)
        {
            errorText.text = "";
        }

        private void OnShopEdited(string s)
        {
            errorText.text = "";
            if (game.Sign != null) game.Sign.SetText(s.Trim());
        }

        private void RefreshSelection()
        {
            bool themed = HudBuilder.T.btnCream != null;
            Color sel = themed ? new Color(1f, 0.82f, 0.42f, 1f) : HudBuilder.Accent;
            Color normal = themed ? Color.white : HudBuilder.Cream;
            for (int i = 0; i < presetButtons.Length; i++)
                presetButtons[i].SetTint(i == look.preset ? sel : normal);

            int[] current = { look.skinSwatch, look.hairSwatch, look.shirtSwatch };
            for (int k = 0; k < rings.Length; k++)
                for (int i = 0; i < rings[k].Length; i++)
                    rings[k][i].enabled = swatchSets[k][i] == current[k];
        }

        private void OnConfirm()
        {
            string owner = ownerField.text.Trim();
            string shop = shopField.text.Trim();
            if (owner.Length == 0)
            {
                errorText.text = Strings.CreatorNeedOwnerName;
                return;
            }
            if (shop.Length == 0)
            {
                errorText.text = Strings.CreatorNeedShopName;
                return;
            }
            Close(true);
            game.ConfirmProfile(owner, shop, look);
        }

        private void OnCancel()
        {
            Close(false);
            if (game.Sign != null) game.Sign.SetText(originalShop);
        }

        /// Bot test (-autoplay): điền mặc định và xác nhận.
        public void AutoComplete(string owner, string shop)
        {
            if (!open) return;
            ownerField.text = owner;
            shopField.text = shop;
            OnConfirm();
        }
    }
}
