using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UsefulObliterator
{
    public class SlotUI : MonoBehaviour
    {
        public static SlotUI Instance { get; private set; }

        private CanvasGroup _canvasGroup;
        private RectTransform _panelRect;
        private TMP_Text _topPayoutText;
        private TMP_Text _titleText;
        private TMP_Text _subtitleText;
        private TMP_Text _paytableHeaderText;
        private TMP_Text _statusText;
        private TMP_Text _summaryText;
        private GameObject _respinPromptBanner;
        private TMP_Text _respinPromptText;

        private Incinerator _currentIncinerator;
        private bool _isPuller;
        private bool _canRespin;
        private bool _respinTriggered;

        // Paytable Rows
        private class PaytableRow
        {
            public GameObject RowObj;
            public Image Background;
            public Outline Highlight;
            public TMP_Text LeftLabel;
            public TMP_Text RightLabel;
            public OutcomeType Outcome;
        }
        private readonly List<PaytableRow> _paytableRows = new List<PaytableRow>();

        // 3-Row Physical Slot Reel Column
        private class ReelColumn
        {
            public GameObject BoxObj;
            public RectTransform Strip;
            public Image BottomImg;
            public Image CenterImg;
            public Image TopImg;
            public Image BufferImg;

            public TrophySymbol BottomSymbol;
            public TrophySymbol CenterSymbol;
            public TrophySymbol TopSymbol;
            public TrophySymbol BufferSymbol;

            public float StripY = 0f;
            public float CurrentSpeed = 0f;
            public bool IsStopping = false;
            public bool IsStopped = false;
            public bool TargetQueued = false;
            public TrophySymbol TargetSymbol;
        }
        private readonly List<ReelColumn> _reels = new List<ReelColumn>();

        private AudioSource _audioSource;
        private AudioClip _clickClip;
        private AudioClip _stopClip;
        private AudioClip _jackpotClip;

        private Coroutine _activeSpinCoroutine;
        private Texture2D _whitePixel;

        private ContentsStrip _contents;
        private float _contentsTimer;

        public static void EnsureCreated()
        {
            if (Instance != null) return;
            if (Hud.instance == null) return;

            GameObject uiObj = new GameObject("UsefulObliterator_SlotsHUD", typeof(RectTransform), typeof(CanvasGroup));
            uiObj.transform.SetParent(Hud.instance.transform, false);
            Instance = uiObj.AddComponent<SlotUI>();
        }

        private void Awake()
        {
            Instance = this;
            _canvasGroup = GetComponent<CanvasGroup>();
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;

            _whitePixel = new Texture2D(1, 1);
            _whitePixel.SetPixel(0, 0, Color.white);
            _whitePixel.Apply();

            InitAudio();
            BuildUIHierarchy();
            ApplyLabels();

            // Relabel straight away when the language is changed in the settings, rather than
            // keeping the old one until the next world.
            Localization.OnLanguageChange += ApplyLabels;
        }

        /// <summary>
        /// Every fixed label on the cabinet, in the current language: the title, the subtitle,
        /// the paytable heading and its rows. The percentages are numbers and need no words,
        /// except the jackpot's "+LOOT".
        /// </summary>
        private void ApplyLabels()
        {
            if (_titleText != null)
            {
                _titleText.text = "<color=#d4af37>«</color> <color=#ffd700>◆</color> <color=#d4af37>»</color>   <color=#ffdf78><b>"
                    + L("$uo_title") + "</b></color>   <color=#d4af37>«</color> <color=#ffd700>◆</color> <color=#d4af37>»</color>";
            }

            if (_subtitleText != null)
            {
                _subtitleText.text = "<color=#8fe4ff>~  " + L("$uo_subtitle") + "  ~</color>";
            }

            if (_paytableHeaderText != null)
            {
                _paytableHeaderText.text = "<color=#ffd700>« " + L("$uo_paytable") + " »</color>";
            }

            foreach (PaytableRow row in _paytableRows)
            {
                switch (row.Outcome)
                {
                    case OutcomeType.BossJackpot:
                        row.LeftLabel.text = "<color=#ffd700><b>" + L("$uo_pay_boss") + "</b></color>";
                        row.RightLabel.text = $"<color=#ffd700><b>{ModConfig.BossJackpotPayoutPercent.Value:0}%</b></color> <size=9><color=#ffe082>{L("$uo_pay_loot")}</color></size>";
                        break;
                    case OutcomeType.Quad:
                        row.LeftLabel.text = "<color=#ffb74d><b>" + L("$uo_pay_four") + "</b></color>";
                        break;
                    case OutcomeType.Triple:
                        row.LeftLabel.text = "<color=#81c784><b>" + L("$uo_pay_three") + "</b></color>";
                        break;
                    case OutcomeType.Pair:
                        row.LeftLabel.text = "<color=#64b5f6><b>" + L("$uo_pay_two") + "</b></color>";
                        break;
                    default:
                        row.LeftLabel.text = "<color=#e57373><b>" + L("$uo_pay_bust") + " ⚡</b></color>";
                        break;
                }
            }
        }

        private void InitAudio()
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // 2D UI sound

            _clickClip = GenerateClickClip();
            _stopClip = GenerateStopClip();
            _jackpotClip = GenerateJackpotClip();
        }

        private void BuildUIHierarchy()
        {
            TMP_FontAsset font = Hud.instance?.m_buildSelection?.font;

            // Look up Valheim native 9-slice wood panel sprite
            Sprite woodSprite = null;
            if (InventoryGui.instance != null && InventoryGui.instance.m_container != null)
            {
                woodSprite = InventoryGui.instance.m_container.GetComponent<Image>()?.sprite;
            }
            if (woodSprite == null)
            {
                Sprite[] allSprites = Resources.FindObjectsOfTypeAll<Sprite>();
                foreach (Sprite s in allSprites)
                {
                    if (s != null && s.name.StartsWith("woodpanel_settings", StringComparison.OrdinalIgnoreCase))
                    {
                        woodSprite = s;
                        break;
                    }
                    if (s != null && woodSprite == null && s.name.StartsWith("woodpanel", StringComparison.OrdinalIgnoreCase))
                    {
                        woodSprite = s;
                    }
                }
            }

            // Main Background Panel (Expanded Norse Timber Cabinet)
            // Sized 880x345 to cleanly accommodate Paytable on left and 4 Reels on right with spacious margins
            GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(transform, false);
            _panelRect = panel.GetComponent<RectTransform>();
            _panelRect.anchorMin = new Vector2(0.5f, 0.74f);
            _panelRect.anchorMax = new Vector2(0.5f, 0.74f);
            _panelRect.pivot = new Vector2(0.5f, 0.5f);
            _panelRect.sizeDelta = new Vector2(880f, 345f);

            // Floating Large Yellow Payout Text Banner (Floats directly above the top of the cabinet panel)
            GameObject topPayoutObj = TextObject("TopPayoutBanner", font, typeof(Outline));
            topPayoutObj.transform.SetParent(panel.transform, false);
            RectTransform tpRect = topPayoutObj.GetComponent<RectTransform>();
            tpRect.anchorMin = new Vector2(0.5f, 1f);
            tpRect.anchorMax = new Vector2(0.5f, 1f);
            tpRect.pivot = new Vector2(0.5f, 0f);
            tpRect.anchoredPosition = new Vector2(0f, 12f);
            tpRect.sizeDelta = new Vector2(900f, 36f);

            _topPayoutText = topPayoutObj.GetComponent<TMP_Text>();
            if (font != null) _topPayoutText.font = font;
            _topPayoutText.text = "";
            _topPayoutText.fontSize = 22f;
            _topPayoutText.alignment = TextAlignmentOptions.Center;
            _topPayoutText.color = new Color(1f, 0.90f, 0.20f); // Bright Golden Yellow
            Outline tpOutline = topPayoutObj.GetComponent<Outline>();
            tpOutline.effectColor = new Color(0.04f, 0.03f, 0.02f, 0.98f);
            tpOutline.effectDistance = new Vector2(2f, -2f);

            Image panelImg = panel.GetComponent<Image>();
            if (woodSprite != null)
            {
                panelImg.sprite = woodSprite;
                panelImg.type = Image.Type.Sliced;
                panelImg.color = new Color(0.92f, 0.88f, 0.82f, 0.98f);
            }
            else
            {
                panelImg.color = new Color(0.11f, 0.08f, 0.06f, 0.97f); // Rich dark Nordic oak
            }

            // Outer Bronze Carved Border
            GameObject border = new GameObject("Border", typeof(RectTransform), typeof(Outline));
            border.transform.SetParent(panel.transform, false);
            RectTransform borderRect = border.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.sizeDelta = Vector2.zero;
            Outline outline = border.GetComponent<Outline>();
            outline.effectColor = new Color(0.72f, 0.55f, 0.32f, 0.95f);
            outline.effectDistance = new Vector2(3f, -3f);

            // Inner Shadow Inset
            GameObject innerShadow = new GameObject("InnerShadow", typeof(RectTransform), typeof(Outline));
            innerShadow.transform.SetParent(panel.transform, false);
            RectTransform shadowRect = innerShadow.GetComponent<RectTransform>();
            shadowRect.anchorMin = Vector2.zero;
            shadowRect.anchorMax = Vector2.one;
            shadowRect.sizeDelta = new Vector2(-4f, -4f);
            Outline shadowOutline = innerShadow.GetComponent<Outline>();
            shadowOutline.effectColor = new Color(0.04f, 0.03f, 0.02f, 0.85f);
            shadowOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Cabinet Header Plaque
            GameObject headerObj = new GameObject("HeaderPlaque", typeof(RectTransform), typeof(Image), typeof(Outline));
            headerObj.transform.SetParent(panel.transform, false);
            RectTransform headerRect = headerObj.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = new Vector2(0f, -8f);
            headerRect.sizeDelta = new Vector2(-32f, 54f);
            Image headerImg = headerObj.GetComponent<Image>();
            headerImg.color = new Color(0.07f, 0.05f, 0.04f, 0.96f);
            Outline headerOutline = headerObj.GetComponent<Outline>();
            headerOutline.effectColor = new Color(0.72f, 0.55f, 0.32f, 0.95f);
            headerOutline.effectDistance = new Vector2(2f, -2f);

            // Corner Bronze Rivets
            CreateRivet(headerObj.transform, new Vector2(0f, 1f), new Vector2(10f, -8f));
            CreateRivet(headerObj.transform, new Vector2(1f, 1f), new Vector2(-10f, -8f));
            CreateRivet(headerObj.transform, new Vector2(0f, 0f), new Vector2(10f, 8f));
            CreateRivet(headerObj.transform, new Vector2(1f, 0f), new Vector2(-10f, 8f));

            // Flanking Ornamental Bronze Rune Badges
            CreateHeaderSideBadge(headerObj.transform, -380f, font);
            CreateHeaderSideBadge(headerObj.transform, 380f, font);

            // Title
            GameObject titleObj = TextObject("Title", font);
            titleObj.transform.SetParent(headerObj.transform, false);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.1f, 0.44f);
            titleRect.anchorMax = new Vector2(0.9f, 1f);
            titleRect.sizeDelta = Vector2.zero;

            _titleText = titleObj.GetComponent<TMP_Text>();
            if (font != null) _titleText.font = font;
            _titleText.fontSize = 19f;
            _titleText.alignment = TextAlignmentOptions.Center;
            _titleText.color = new Color(1f, 0.88f, 0.50f);

            // Divider
            GameObject dividerObj = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            dividerObj.transform.SetParent(headerObj.transform, false);
            RectTransform divRect = dividerObj.GetComponent<RectTransform>();
            divRect.anchorMin = new Vector2(0.24f, 0.42f);
            divRect.anchorMax = new Vector2(0.76f, 0.42f);
            divRect.pivot = new Vector2(0.5f, 0.5f);
            divRect.sizeDelta = new Vector2(0f, 1.5f);
            Image divImg = dividerObj.GetComponent<Image>();
            divImg.color = new Color(0.68f, 0.52f, 0.30f, 0.75f);

            // Subtitle
            GameObject subObj = TextObject("Subtitle", font);
            subObj.transform.SetParent(headerObj.transform, false);
            RectTransform subRect = subObj.GetComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.1f, 0.06f);
            subRect.anchorMax = new Vector2(0.9f, 0.38f);
            subRect.sizeDelta = Vector2.zero;

            TMP_Text subText = subObj.GetComponent<TMP_Text>();
            _subtitleText = subText;
            if (font != null) subText.font = font;
            subText.fontSize = 11f;
            subText.alignment = TextAlignmentOptions.Center;
            subText.color = new Color(0.55f, 0.85f, 0.95f, 0.90f);

            // -------------------------------------------------------------
            // SECTION 1: VALHEIM PAYTABLE (Left Side)
            // -------------------------------------------------------------
            BuildPaytableSection(panel.transform, font);

            // -------------------------------------------------------------
            // SECTION 2: 3-ROW PHYSICAL REELS CHAMBER (Right Side)
            // -------------------------------------------------------------
            BuildReelsChamber(panel.transform);

            // -------------------------------------------------------------
            // SECTION 3: FOOTER (Status Banner & Salvage Ledger)
            // -------------------------------------------------------------
            GameObject statusObj = TextObject("Status", font);
            statusObj.transform.SetParent(panel.transform, false);
            RectTransform statusRect = statusObj.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 0.14f);
            statusRect.anchorMax = new Vector2(1f, 0.14f);
            statusRect.pivot = new Vector2(0.5f, 0.5f);
            statusRect.anchoredPosition = new Vector2(0f, 0f);
            statusRect.sizeDelta = new Vector2(-40f, 26f);

            _statusText = statusObj.GetComponent<TMP_Text>();
            if (font != null) _statusText.font = font;
            _statusText.text = "◆ " + L("$uo_rolling") + " ◆";
            _statusText.fontSize = 18f;
            _statusText.alignment = TextAlignmentOptions.Center;
            _statusText.color = new Color(0.85f, 0.85f, 0.85f);

            GameObject summaryObj = TextObject("Summary", font);
            summaryObj.transform.SetParent(panel.transform, false);
            RectTransform summaryRect = summaryObj.GetComponent<RectTransform>();
            summaryRect.anchorMin = new Vector2(0f, 0.04f);
            summaryRect.anchorMax = new Vector2(1f, 0.04f);
            summaryRect.pivot = new Vector2(0.5f, 0.5f);
            summaryRect.anchoredPosition = new Vector2(0f, 0f);
            summaryRect.sizeDelta = new Vector2(-30f, 22f);

            _summaryText = summaryObj.GetComponent<TMP_Text>();
            if (font != null) _summaryText.font = font;
            _summaryText.text = "";
            _summaryText.fontSize = 13f;
            _summaryText.alignment = TextAlignmentOptions.Center;
            _summaryText.color = new Color(0.4f, 0.95f, 0.55f);

            // -------------------------------------------------------------
            // SECTION 4: FLOATING RE-SPIN PROMPT BANNER (Beneath Cabinet)
            // -------------------------------------------------------------
            _respinPromptBanner = new GameObject("RespinPromptBanner", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Button));
            _respinPromptBanner.transform.SetParent(panel.transform, false);
            RectTransform promptRect = _respinPromptBanner.GetComponent<RectTransform>();
            promptRect.anchorMin = new Vector2(0.5f, 0f);
            promptRect.anchorMax = new Vector2(0.5f, 0f);
            promptRect.pivot = new Vector2(0.5f, 1f);
            promptRect.anchoredPosition = new Vector2(0f, -8f);
            promptRect.sizeDelta = new Vector2(720f, 36f);

            Image promptImg = _respinPromptBanner.GetComponent<Image>();
            promptImg.color = new Color(0.08f, 0.06f, 0.05f, 0.95f); // Rich dark Nordic oak
            Outline promptOutline = _respinPromptBanner.GetComponent<Outline>();
            promptOutline.effectColor = new Color(0.72f, 0.55f, 0.32f, 0.95f); // Bronze outline
            promptOutline.effectDistance = new Vector2(2f, -2f);

            Button promptBtn = _respinPromptBanner.GetComponent<Button>();
            promptBtn.targetGraphic = promptImg;
            promptBtn.onClick.AddListener(TriggerRespin);

            // Text inside prompt banner
            GameObject promptTextObj = TextObject("PromptText", font);
            promptTextObj.transform.SetParent(_respinPromptBanner.transform, false);
            RectTransform ptTextRect = promptTextObj.GetComponent<RectTransform>();
            ptTextRect.anchorMin = Vector2.zero;
            ptTextRect.anchorMax = Vector2.one;
            ptTextRect.sizeDelta = Vector2.zero;

            _respinPromptText = promptTextObj.GetComponent<TMP_Text>();
            if (font != null) _respinPromptText.font = font;
            _respinPromptText.fontSize = 14f;
            _respinPromptText.alignment = TextAlignmentOptions.Center;
            _respinPromptText.color = new Color(0.95f, 0.90f, 0.80f);
            _respinPromptText.text = "";

            _respinPromptBanner.SetActive(false);

            // What is inside the Obliterator, beneath the cabinet; the re-spin banner sits under it.
            _contents = new ContentsStrip(panel.transform, font);
        }

        private void BuildPaytableSection(Transform parent, TMP_FontAsset font)
        {
            _paytableRows.Clear();

            // Paytable Frame
            GameObject paytableObj = new GameObject("PaytableSection", typeof(RectTransform), typeof(Image), typeof(Outline));
            paytableObj.transform.SetParent(parent, false);
            RectTransform ptRect = paytableObj.GetComponent<RectTransform>();
            ptRect.anchorMin = new Vector2(0.5f, 0.52f);
            ptRect.anchorMax = new Vector2(0.5f, 0.52f);
            ptRect.pivot = new Vector2(0.5f, 0.5f);
            ptRect.anchoredPosition = new Vector2(-285f, 6f);
            ptRect.sizeDelta = new Vector2(250f, 180f);

            Image ptImg = paytableObj.GetComponent<Image>();
            ptImg.color = new Color(0.04f, 0.03f, 0.03f, 0.97f); // Deep carved dark slate
            Outline ptOutline = paytableObj.GetComponent<Outline>();
            ptOutline.effectColor = new Color(0.58f, 0.44f, 0.26f, 0.90f);
            ptOutline.effectDistance = new Vector2(2f, -2f);

            // Paytable Header Label
            GameObject ptHeader = TextObject("Header", font);
            ptHeader.transform.SetParent(paytableObj.transform, false);
            RectTransform pthRect = ptHeader.GetComponent<RectTransform>();
            pthRect.anchorMin = new Vector2(0f, 1f);
            pthRect.anchorMax = new Vector2(1f, 1f);
            pthRect.pivot = new Vector2(0.5f, 1f);
            pthRect.anchoredPosition = new Vector2(0f, -4f);
            pthRect.sizeDelta = new Vector2(0f, 22f);

            TMP_Text pthText = ptHeader.GetComponent<TMP_Text>();
            _paytableHeaderText = pthText;
            if (font != null) pthText.font = font;
            pthText.fontSize = 13f;
            pthText.alignment = TextAlignmentOptions.Center;

            // Divider beneath header
            GameObject div = new GameObject("HeaderDiv", typeof(RectTransform), typeof(Image));
            div.transform.SetParent(paytableObj.transform, false);
            RectTransform divRect = div.GetComponent<RectTransform>();
            divRect.anchorMin = new Vector2(0.06f, 1f);
            divRect.anchorMax = new Vector2(0.94f, 1f);
            divRect.pivot = new Vector2(0.5f, 1f);
            divRect.anchoredPosition = new Vector2(0f, -26f);
            divRect.sizeDelta = new Vector2(0f, 1f);
            div.GetComponent<Image>().color = new Color(0.55f, 0.42f, 0.25f, 0.6f);

            // 5 Payout Tier Rows (Stacked downwards)
            float startY = -30f;
            float rowHeight = 28f;

            CreatePaytableRow(paytableObj.transform, font, startY + 0 * -rowHeight,
                OutcomeType.BossJackpot,
                "", "",
                new Color(0.08f, 0.06f, 0.03f, 0.85f));

            CreatePaytableRow(paytableObj.transform, font, startY + 1 * -rowHeight,
                OutcomeType.Quad,
                "",
                $"<color=#ffb74d><b>{ModConfig.QuadPayoutPercent.Value:0}%</b></color>",
                new Color(0.05f, 0.04f, 0.03f, 0.85f));

            CreatePaytableRow(paytableObj.transform, font, startY + 2 * -rowHeight,
                OutcomeType.Triple,
                "",
                "<color=#81c784><b>100%</b></color>",
                new Color(0.08f, 0.06f, 0.03f, 0.85f));

            CreatePaytableRow(paytableObj.transform, font, startY + 3 * -rowHeight,
                OutcomeType.Pair,
                "",
                $"<color=#64b5f6><b>{ModConfig.PairPayoutPercent.Value:0}%</b></color>",
                new Color(0.05f, 0.04f, 0.03f, 0.85f));

            CreatePaytableRow(paytableObj.transform, font, startY + 4 * -rowHeight,
                OutcomeType.Bust,
                "",
                $"<color=#e57373><b>{ModConfig.BustPayoutPercent.Value:0}%</b></color>",
                new Color(0.08f, 0.06f, 0.03f, 0.85f));
        }

        private void CreatePaytableRow(Transform parent, TMP_FontAsset font, float yPos, OutcomeType outcome, string leftStr, string rightStr, Color bgColor)
        {
            GameObject rowObj = new GameObject($"Row_{outcome}", typeof(RectTransform), typeof(Image), typeof(Outline));
            rowObj.transform.SetParent(parent, false);
            RectTransform rRect = rowObj.GetComponent<RectTransform>();
            rRect.anchorMin = new Vector2(0f, 1f);
            rRect.anchorMax = new Vector2(1f, 1f);
            rRect.pivot = new Vector2(0.5f, 1f);
            rRect.anchoredPosition = new Vector2(0f, yPos);
            rRect.sizeDelta = new Vector2(-12f, 26f);

            Image rowBg = rowObj.GetComponent<Image>();
            rowBg.color = bgColor;

            Outline rowHighlight = rowObj.GetComponent<Outline>();
            rowHighlight.effectColor = new Color(1f, 0.85f, 0.25f, 1f); // Glowing Gold
            rowHighlight.effectDistance = new Vector2(2f, -2f);
            rowHighlight.enabled = false;

            // Left Label
            GameObject leftObj = TextObject("LeftLabel", font);
            leftObj.transform.SetParent(rowObj.transform, false);
            RectTransform leftRect = leftObj.GetComponent<RectTransform>();
            leftRect.anchorMin = new Vector2(0f, 0f);
            leftRect.anchorMax = new Vector2(0.55f, 1f);
            leftRect.anchoredPosition = new Vector2(8f, 0f);
            leftRect.sizeDelta = new Vector2(-8f, 0f);
            TMP_Text leftText = leftObj.GetComponent<TMP_Text>();
            if (font != null) leftText.font = font;
            leftText.text = leftStr;
            leftText.fontSize = 12f;
            leftText.alignment = TextAlignmentOptions.Left;

            // Right Label
            GameObject rightObj = TextObject("RightLabel", font);
            rightObj.transform.SetParent(rowObj.transform, false);
            RectTransform rightRect = rightObj.GetComponent<RectTransform>();
            rightRect.anchorMin = new Vector2(0.55f, 0f);
            rightRect.anchorMax = new Vector2(1f, 1f);
            rightRect.anchoredPosition = new Vector2(-8f, 0f);
            rightRect.sizeDelta = new Vector2(-8f, 0f);
            TMP_Text rightText = rightObj.GetComponent<TMP_Text>();
            if (font != null) rightText.font = font;
            rightText.text = rightStr;
            rightText.fontSize = 12f;
            rightText.alignment = TextAlignmentOptions.Right;

            _paytableRows.Add(new PaytableRow
            {
                RowObj = rowObj,
                Background = rowBg,
                Highlight = rowHighlight,
                LeftLabel = leftText,
                RightLabel = rightText,
                Outcome = outcome
            });
        }

        private void BuildReelsChamber(Transform parent)
        {
            _reels.Clear();

            // Right side chamber frame (sized 536x180)
            GameObject chamber = new GameObject("ReelsChamber", typeof(RectTransform), typeof(Image), typeof(Outline));
            chamber.transform.SetParent(parent, false);
            RectTransform chamberRect = chamber.GetComponent<RectTransform>();
            chamberRect.anchorMin = new Vector2(0.5f, 0.52f);
            chamberRect.anchorMax = new Vector2(0.5f, 0.52f);
            chamberRect.pivot = new Vector2(0.5f, 0.5f);
            chamberRect.anchoredPosition = new Vector2(145f, 6f);
            chamberRect.sizeDelta = new Vector2(536f, 180f);

            Image chamberImg = chamber.GetComponent<Image>();
            chamberImg.color = new Color(0.02f, 0.02f, 0.03f, 0.98f); // Sunken machine well
            Outline chamberOutline = chamber.GetComponent<Outline>();
            chamberOutline.effectColor = new Color(0.58f, 0.44f, 0.26f, 0.90f);
            chamberOutline.effectDistance = new Vector2(2f, -2f);

            // 4 Reel Columns positioned across the chamber
            float[] xPositions = { -186f, -62f, 62f, 186f };
            for (int i = 0; i < 4; i++)
            {
                _reels.Add(Create3RowReelColumn(chamber.transform, xPositions[i], i));
            }
        }

        private ReelColumn Create3RowReelColumn(Transform parent, float xPos, int index)
        {
            // Column Window with RectMask2D for clean symbol clipping
            GameObject colObj = new GameObject($"ReelColumn_{index + 1}", typeof(RectTransform), typeof(Image), typeof(Outline), typeof(RectMask2D));
            colObj.transform.SetParent(parent, false);
            RectTransform colRect = colObj.GetComponent<RectTransform>();
            colRect.anchoredPosition = new Vector2(xPos, 0f);
            colRect.sizeDelta = new Vector2(116f, 174f);

            Image colImg = colObj.GetComponent<Image>();
            colImg.color = new Color(0.02f, 0.02f, 0.03f, 1f); // Deep cylinder cavity

            Outline colOutline = colObj.GetComponent<Outline>();
            colOutline.effectColor = new Color(0.68f, 0.52f, 0.30f, 0.95f); // Carved bronze rim
            colOutline.effectDistance = new Vector2(2f, -2f);

            // Moving symbol strip container
            GameObject stripObj = new GameObject("Strip", typeof(RectTransform));
            stripObj.transform.SetParent(colObj.transform, false);
            RectTransform stripRect = stripObj.GetComponent<RectTransform>();
            stripRect.anchorMin = new Vector2(0.5f, 0.5f);
            stripRect.anchorMax = new Vector2(0.5f, 0.5f);
            stripRect.pivot = new Vector2(0.5f, 0.5f);
            stripRect.anchoredPosition = Vector2.zero;
            stripRect.sizeDelta = new Vector2(104f, 400f);

            // 4 Symbols in the vertical strip
            Image bottomImg = CreateSymbolImage(stripObj.transform, "Bottom", -106f);
            Image centerImg = CreateSymbolImage(stripObj.transform, "Center", 0f);
            Image topImg = CreateSymbolImage(stripObj.transform, "Top", 106f);
            Image bufferImg = CreateSymbolImage(stripObj.transform, "Buffer", 212f);

            // Payline Indicator Notches on left & right of column (at y=0)
            CreatePaylineTick(colObj.transform, new Vector2(0f, 0.5f), new Vector2(3f, 0f));
            CreatePaylineTick(colObj.transform, new Vector2(1f, 0.5f), new Vector2(-3f, 0f));

            // Cylindrical Top & Bottom Depth Shadows
            CreateCylinderShadow(colObj.transform, true);
            CreateCylinderShadow(colObj.transform, false);

            return new ReelColumn
            {
                BoxObj = colObj,
                Strip = stripRect,
                BottomImg = bottomImg,
                CenterImg = centerImg,
                TopImg = topImg,
                BufferImg = bufferImg,
                StripY = 0f
            };
        }

        private Image CreateSymbolImage(Transform parent, string name, float yPos)
        {
            GameObject symObj = new GameObject(name, typeof(RectTransform), typeof(Image));
            symObj.transform.SetParent(parent, false);
            RectTransform symRect = symObj.GetComponent<RectTransform>();
            symRect.anchorMin = new Vector2(0.5f, 0.5f);
            symRect.anchorMax = new Vector2(0.5f, 0.5f);
            symRect.pivot = new Vector2(0.5f, 0.5f);
            symRect.anchoredPosition = new Vector2(0f, yPos);
            symRect.sizeDelta = new Vector2(102f, 102f);

            Image img = symObj.GetComponent<Image>();
            img.preserveAspect = true;
            img.color = Color.white;
            return img;
        }

        private static void CreatePaylineTick(Transform parent, Vector2 anchor, Vector2 offset)
        {
            GameObject tick = new GameObject("PaylineTick", typeof(RectTransform), typeof(Image));
            tick.transform.SetParent(parent, false);
            RectTransform rt = tick.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(6f, 3f);

            Image img = tick.GetComponent<Image>();
            img.color = new Color(0.95f, 0.78f, 0.35f, 0.85f);
        }

        private static void CreateCylinderShadow(Transform parent, bool isTop)
        {
            GameObject shade = new GameObject(isTop ? "TopShade" : "BtmShade", typeof(RectTransform), typeof(Image));
            shade.transform.SetParent(parent, false);
            RectTransform rt = shade.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, isTop ? 1f : 0f);
            rt.anchorMax = new Vector2(1f, isTop ? 1f : 0f);
            rt.pivot = new Vector2(0.5f, isTop ? 1f : 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 34f);

            Image img = shade.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.58f);
        }

        private static void CreateRivet(Transform parent, Vector2 anchor, Vector2 anchoredPos)
        {
            GameObject rivet = new GameObject("Rivet", typeof(RectTransform), typeof(Image), typeof(Outline));
            rivet.transform.SetParent(parent, false);
            RectTransform rt = rivet.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(6f, 6f);

            Image img = rivet.GetComponent<Image>();
            img.color = new Color(0.85f, 0.68f, 0.40f);
            Outline o = rivet.GetComponent<Outline>();
            o.effectColor = new Color(0.1f, 0.08f, 0.05f, 0.9f);
            o.effectDistance = new Vector2(1f, -1f);
        }

        private static void CreateHeaderSideBadge(Transform parent, float xPos, TMP_FontAsset font)
        {
            GameObject badge = new GameObject("RuneBadge", typeof(RectTransform), typeof(Image), typeof(Outline));
            badge.transform.SetParent(parent, false);
            RectTransform rt = badge.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(xPos, 0f);
            rt.sizeDelta = new Vector2(28f, 28f);

            Image img = badge.GetComponent<Image>();
            img.color = new Color(0.12f, 0.09f, 0.07f, 0.95f);
            Outline o = badge.GetComponent<Outline>();
            o.effectColor = new Color(0.72f, 0.55f, 0.32f, 0.95f);
            o.effectDistance = new Vector2(1.5f, -1.5f);

            GameObject diamond = TextObject("DiamondGlyph", font);
            diamond.transform.SetParent(badge.transform, false);
            RectTransform dt = diamond.GetComponent<RectTransform>();
            dt.anchorMin = Vector2.zero;
            dt.anchorMax = Vector2.one;
            dt.sizeDelta = Vector2.zero;

            TMP_Text text = diamond.GetComponent<TMP_Text>();
            if (font != null) text.font = font;
            text.text = "◆";
            text.fontSize = 12f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.95f, 0.78f, 0.42f, 0.9f);
        }

        public void UpdateSummary(string text)
        {
            if (_summaryText != null && !string.IsNullOrEmpty(text))
            {
                _summaryText.text = text;
            }

            // If player is the puller and the machine still has items to gamble, show the re-spin prompt!
            if (_isPuller && _currentIncinerator != null && _currentIncinerator.m_container != null && _currentIncinerator.m_container.GetInventory().NrOfItems() > 0)
            {
                string keyName = Localization.instance != null ? Localization.instance.Localize("<color=yellow><b>$KEY_Use</b></color>") : "<color=yellow><b>E</b></color>";
                if (_respinPromptText != null)
                {
                    _respinPromptText.text = Localization.instance.Localize("$uo_respin", keyName);
                }
                if (_respinPromptBanner != null)
                {
                    _respinPromptBanner.SetActive(true);
                }
                _canRespin = true;
            }
            else
            {
                _canRespin = false;
                if (_respinPromptBanner != null)
                {
                    _respinPromptBanner.SetActive(false);
                }
            }
        }

        public void TriggerSpin(RollOutcome outcome, Incinerator incinerator, bool isPuller)
        {
            if (!ModConfig.ShowReelHUD.Value) return;

            if (_activeSpinCoroutine != null)
            {
                StopCoroutine(_activeSpinCoroutine);
                _activeSpinCoroutine = null;
            }

            _currentIncinerator = incinerator;
            _isPuller = isPuller;
            _canRespin = false;
            _respinTriggered = false;
            if (_respinPromptBanner != null) _respinPromptBanner.SetActive(false);

            // What goes in now is what the spin's wins and losses are counted against.
            _contents?.Snapshot(incinerator != null && incinerator.m_container != null ? incinerator.m_container.GetInventory() : null);
            _contentsTimer = 0f;

            _activeSpinCoroutine = StartCoroutine(SpinRoutine(outcome));
        }

        private IEnumerator SpinRoutine(RollOutcome outcome)
        {
            _statusText.text = "◆ " + L("$uo_rolling") + " ◆";
            _statusText.color = new Color(0.85f, 0.85f, 0.85f);
            _summaryText.text = "";

            // Reset Paytable Row Highlights
            foreach (var row in _paytableRows)
            {
                row.Highlight.enabled = false;
            }

            // Populate initial random symbols into all 4 reels so 3 rows are visible immediately
            System.Random rng = new System.Random();
            TrophySymbol[] targets = { outcome.Reel1, outcome.Reel2, outcome.Reel3, outcome.Reel4 };

            for (int i = 0; i < 4; i++)
            {
                ReelColumn r = _reels[i];
                r.StripY = 0f;
                r.Strip.anchoredPosition = Vector2.zero;
                r.CurrentSpeed = 1350f;
                r.IsStopping = false;
                r.IsStopped = false;
                r.TargetQueued = false;
                r.TargetSymbol = targets[i];

                r.BottomSymbol = SlotEngine.PickRandomSymbol(rng);
                r.CenterSymbol = SlotEngine.PickRandomSymbol(rng);
                r.TopSymbol = SlotEngine.PickRandomSymbol(rng);
                r.BufferSymbol = SlotEngine.PickRandomSymbol(rng);

                r.BottomImg.sprite = r.BottomSymbol.GetSprite();
                r.CenterImg.sprite = r.CenterSymbol.GetSprite();
                r.TopImg.sprite = r.TopSymbol.GetSprite();
                r.BufferImg.sprite = r.BufferSymbol.GetSprite();
            }

            // Fade in HUD
            float fadeInTime = 0.25f;
            for (float t = 0; t < fadeInTime; t += Time.deltaTime)
            {
                _canvasGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeInTime);
                yield return null;
            }
            _canvasGroup.alpha = 1f;

            float elapsed = 0f;
            float totalDuration = 5.2f;

            // Staggered Deceleration and Lock Timestamps:
            // Reel 1: Decels at 1.0s, locks at 2.0s
            // Reel 2: Decels at 1.9s, locks at 2.9s
            // Reel 3: Decels at 2.8s, locks at 3.8s
            // Reel 4: Decels at 3.7s, locks at 4.8s
            float[] decelStarts = { 1.0f, 1.9f, 2.8f, 3.7f };
            float[] lockTimes = { 2.0f, 2.9f, 3.8f, 4.8f };
            float[] lockPitches = { 0.95f, 1.05f, 1.15f, 1.28f };

            float nextTick = 0f;

            while (elapsed < totalDuration)
            {
                float dt = Time.deltaTime;
                elapsed += dt;

                // Audio mechanical clicks during fast spin
                if (elapsed >= nextTick && ModConfig.EnableSlotAudio.Value && !AllReelsStopped())
                {
                    nextTick = elapsed + 0.08f;
                    PlaySound(_clickClip, 0.35f, UnityEngine.Random.Range(0.95f, 1.05f));
                }

                // Update each reel's physical scroll and deceleration
                for (int i = 0; i < 4; i++)
                {
                    ReelColumn r = _reels[i];
                    if (r.IsStopped) continue;

                    // Phase Check
                    if (elapsed >= lockTimes[i])
                    {
                        // Lock Reel into place!
                        r.IsStopped = true;
                        r.CenterSymbol = r.TargetSymbol;
                        r.CenterImg.sprite = r.TargetSymbol.GetSprite();
                        r.StripY = 0f;
                        r.Strip.anchoredPosition = Vector2.zero;

                        // Heavy mechanical lock sound
                        if (i == 3 && (outcome.Outcome == OutcomeType.BossJackpot || outcome.Outcome == OutcomeType.Quad))
                        {
                            PlaySound(_jackpotClip, 0.9f, 1.0f);
                        }
                        else
                        {
                            PlaySound(_stopClip, 0.70f + i * 0.05f, lockPitches[i]);
                        }

                        // Trigger settling bounce
                        StartCoroutine(ReelSettleBounce(r.Strip));

                        // When Reel 4 locks, reveal outcome title and highlight paytable row!
                        if (i == 3)
                        {
                            _statusText.text = outcome.Title;
                            _statusText.color = outcome.TitleColor;
                            HighlightWinningPaytableRow(outcome.Outcome);
                        }
                        continue;
                    }
                    else if (elapsed >= decelStarts[i])
                    {
                        // Deceleration curve: smoothly ease speed from 1350 down to 180
                        r.IsStopping = true;
                        float decelProgress = Mathf.Clamp01((elapsed - decelStarts[i]) / (lockTimes[i] - decelStarts[i]));
                        r.CurrentSpeed = Mathf.Lerp(1350f, 180f, Mathf.SmoothStep(0f, 1f, decelProgress));

                        // Queue target symbol when entering final slow roll
                        if (!r.TargetQueued && decelProgress > 0.45f)
                        {
                            r.TargetQueued = true;
                            r.BufferSymbol = r.TargetSymbol;
                            r.BufferImg.sprite = r.BufferSymbol.GetSprite();
                        }
                    }

                    // Physical vertical scroll downward
                    r.StripY -= r.CurrentSpeed * dt;
                    r.Strip.anchoredPosition = new Vector2(0f, r.StripY);

                    // Continuous symbol cycling when moved past threshold (106px)
                    if (r.StripY <= -106f)
                    {
                        r.StripY += 106f;

                        // Shift symbols downward
                        r.BottomSymbol = r.CenterSymbol;
                        r.CenterSymbol = r.TopSymbol;
                        r.TopSymbol = r.BufferSymbol;

                        // If target not yet queued, pick random buffer symbol
                        if (!r.TargetQueued)
                        {
                            r.BufferSymbol = SlotEngine.PickRandomSymbol(rng);
                        }
                        else
                        {
                            // After target is placed, pick random neighbor
                            r.BufferSymbol = SlotEngine.PickRandomSymbol(rng);
                        }

                        r.BottomImg.sprite = r.BottomSymbol.GetSprite();
                        r.CenterImg.sprite = r.CenterSymbol.GetSprite();
                        r.TopImg.sprite = r.TopSymbol.GetSprite();
                        r.BufferImg.sprite = r.BufferSymbol.GetSprite();
                    }
                }

                yield return null;
            }

            // At 5.2s: If player is the puller and the machine still has items, show the re-spin prompt!
            if (_isPuller && _currentIncinerator != null && _currentIncinerator.m_container != null && _currentIncinerator.m_container.GetInventory().NrOfItems() > 0)
            {
                string keyName = Localization.instance != null ? Localization.instance.Localize("<color=yellow><b>$KEY_Use</b></color>") : "<color=yellow><b>E</b></color>";
                if (_respinPromptText != null)
                {
                    _respinPromptText.text = Localization.instance.Localize("$uo_respin", keyName);
                }
                if (_respinPromptBanner != null)
                {
                    _respinPromptBanner.SetActive(true);
                }
                _canRespin = true;
            }
            else
            {
                _canRespin = false;
                if (_respinPromptBanner != null)
                {
                    _respinPromptBanner.SetActive(false);
                }
            }

            // Display on screen for review or until re-spin triggered
            float displayTime = ModConfig.ResultDisplayDuration != null ? ModConfig.ResultDisplayDuration.Value : 3.0f;
            float timer = 0f;
            while (timer < displayTime)
            {
                if (_respinTriggered) yield break;
                timer += Time.deltaTime;
                yield return null;
            }

            // Fade out
            _canRespin = false;
            float fadeOutTime = 0.4f;
            for (float t = 0; t < fadeOutTime; t += Time.deltaTime)
            {
                if (_respinTriggered) yield break;
                _canvasGroup.alpha = Mathf.Lerp(1f, 0f, t / fadeOutTime);
                yield return null;
            }
            _canvasGroup.alpha = 0f;
            if (_respinPromptBanner != null) _respinPromptBanner.SetActive(false);
            _activeSpinCoroutine = null;
        }

        private void Update()
        {
            RefreshContents();

            if (!_canRespin || !_isPuller || _currentIncinerator == null || Player.m_localPlayer == null)
            {
                return;
            }

            // Don't intercept when typing in chat, console, or in any menu/inventory GUI
            if (Chat.instance != null && Chat.instance.HasFocus()) return;
            if (Console.IsVisible() || TextInput.IsVisible() || StoreGui.IsVisible() || InventoryGui.IsVisible() || Menu.IsVisible()) return;

            // Distance check: must remain within interaction range (within 5m)
            if (Vector3.Distance(Player.m_localPlayer.transform.position, _currentIncinerator.transform.position) > 5.0f)
            {
                _canRespin = false;
                if (_respinPromptBanner != null) _respinPromptBanner.SetActive(false);
                return;
            }

            // Detect Use key (E or Gamepad Use)
            if (ZInput.GetButtonDown("Use") || ZInput.GetButtonDown("JoyUse"))
            {
                TriggerRespin();
            }
        }

        /// <summary>
        /// Keeps the contents strip in step with the Obliterator while the machine is on screen,
        /// and the re-spin banner below it.
        /// </summary>
        private void RefreshContents()
        {
            if (_contents == null)
            {
                return;
            }

            bool showing = ModConfig.ShowObliteratorContents != null && ModConfig.ShowObliteratorContents.Value
                && _currentIncinerator != null && _currentIncinerator.m_container != null
                && _canvasGroup != null && _canvasGroup.alpha > 0f;

            if (!showing)
            {
                _contents.Hide();
            }
            else
            {
                _contentsTimer -= Time.deltaTime;
                if (_contentsTimer <= 0f)
                {
                    _contentsTimer = 0.2f;
                    _contents.Refresh(_currentIncinerator.m_container.GetInventory());
                }
            }

            if (_respinPromptBanner != null)
            {
                float below = _contents.Height > 0f ? _contents.Height + 8f : 0f;
                ((RectTransform)_respinPromptBanner.transform).anchoredPosition = new Vector2(0f, -8f - below);
            }
        }

        public void TriggerRespin()
        {
            if (!_canRespin || _currentIncinerator == null || Player.m_localPlayer == null) return;

            // Consume input so game doesn't open chest or trigger other interactions
            ZInput.ResetButtonStatus("Use");
            ZInput.ResetButtonStatus("JoyUse");

            _canRespin = false;
            _respinTriggered = true;
            if (_respinPromptBanner != null) _respinPromptBanner.SetActive(false);

            // Verify incinerator is not currently active
            bool inUse = (bool)Traverse.Create(_currentIncinerator).Field("isInUse").GetValue();
            if (inUse || (_currentIncinerator.m_container != null && _currentIncinerator.m_container.IsInUse()))
            {
                return;
            }

            // Verify container has items
            if (_currentIncinerator.m_container != null && _currentIncinerator.m_container.GetInventory().NrOfItems() == 0)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "$piece_incinerator_empty");
                return;
            }

            // Verify ward access
            if (!PrivateArea.CheckAccess(_currentIncinerator.transform.position))
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "$piece_noaccess");
                return;
            }

            UsefulObliteratorPlugin.Log.LogInfo("[SlotUI] Player pressed E / clicked to spin again from results window!");

            if (_activeSpinCoroutine != null)
            {
                StopCoroutine(_activeSpinCoroutine);
                _activeSpinCoroutine = null;
            }

            // Trigger the lever pull
            if (_currentIncinerator.m_incinerateSwitch != null && _currentIncinerator.m_incinerateSwitch.m_onUse != null)
            {
                _currentIncinerator.m_incinerateSwitch.m_onUse(_currentIncinerator.m_incinerateSwitch, Player.m_localPlayer, null);
            }
            else
            {
                long playerID = Game.instance.GetPlayerProfile().GetPlayerID();
                IdolCatalog.SendKnownBiomes(_currentIncinerator);
                _currentIncinerator.GetComponent<ZNetView>()?.InvokeRPC("RPC_RequestIncinerate", playerID);
            }
        }

        private bool AllReelsStopped()
        {
            foreach (var r in _reels)
            {
                if (!r.IsStopped) return false;
            }
            return true;
        }

        private static IEnumerator ReelSettleBounce(RectTransform strip)
        {
            if (strip == null) yield break;

            // Physical bounce: dips downward slightly by 6px then rebounds to 0
            float bounceDuration = 0.12f;
            for (float t = 0; t < bounceDuration; t += Time.deltaTime)
            {
                float progress = t / bounceDuration;
                float yOffset = -Mathf.Sin(progress * Mathf.PI) * 6f;
                strip.anchoredPosition = new Vector2(0f, yOffset);
                yield return null;
            }
            strip.anchoredPosition = Vector2.zero;
        }

        private void HighlightWinningPaytableRow(OutcomeType outcome)
        {
            foreach (var row in _paytableRows)
            {
                if (row.Outcome == outcome)
                {
                    row.Highlight.enabled = true;
                    row.Background.color = new Color(0.20f, 0.15f, 0.05f, 0.95f);
                }
                else
                {
                    row.Highlight.enabled = false;
                }
            }
        }

        private void PlaySound(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null || _audioSource == null || !ModConfig.EnableSlotAudio.Value) return;
            _audioSource.pitch = pitch;
            _audioSource.PlayOneShot(clip, volume);
        }

        // Procedural Audio Generators
        private static AudioClip GenerateClickClip()
        {
            int sampleRate = 44100;
            int length = (int)(sampleRate * 0.035f); // 35 ms
            float[] samples = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 130f);
                float noise = (UnityEngine.Random.value * 2f - 1f) * 0.5f;
                float tone = Mathf.Sin(2f * Mathf.PI * 920f * t) * 0.5f;
                samples[i] = (noise + tone) * env;
            }
            AudioClip clip = AudioClip.Create("SlotClick", length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip GenerateStopClip()
        {
            int sampleRate = 44100;
            int length = (int)(sampleRate * 0.15f); // 150 ms
            float[] samples = new float[length];
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Exp(-t * 30f);
                float body = Mathf.Sin(2f * Mathf.PI * 260f * t);
                float metallic = Mathf.Sin(2f * Mathf.PI * 1480f * t) * 0.35f;
                samples[i] = (body + metallic) * env;
            }
            AudioClip clip = AudioClip.Create("SlotStop", length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip GenerateJackpotClip()
        {
            int sampleRate = 44100;
            int length = (int)(sampleRate * 1.8f);
            float[] samples = new float[length];
            float[] freqs = { 523.25f, 659.25f, 783.99f, 1046.50f }; // C5, E5, G5, C6
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / sampleRate;
                float sample = 0f;
                for (int n = 0; n < freqs.Length; n++)
                {
                    float noteStart = n * 0.18f;
                    if (t >= noteStart)
                    {
                        float noteT = t - noteStart;
                        float env = Mathf.Exp(-noteT * 3.5f);
                        sample += Mathf.Sin(2f * Mathf.PI * freqs[n] * noteT) * env * 0.25f;
                    }
                }
                samples[i] = Mathf.Clamp(sample, -1f, 1f);
            }
            AudioClip clip = AudioClip.Create("SlotJackpot", length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void OnDestroy()
        {
            Localization.OnLanguageChange -= ApplyLabels;

            if (_whitePixel != null)
            {
                Destroy(_whitePixel);
            }
        }

        /// <summary>
        /// A text object built inactive, so TextMeshPro has its font before it wakes.
        /// </summary>
        /// <remarks>
        /// new GameObject(name, typeof(TextMeshProUGUI)) hands back an ACTIVE object, so TMP's
        /// Awake runs there and then - before the next line can hand it Valheim's font. Finding
        /// none, it reaches for TMP_Settings.defaultFontAsset, which is LiberationSans SDF and is
        /// not shipped with the game, and logs a warning for every label built.
        ///
        /// The text itself was always right, because the real font is assigned a line later. Only
        /// the log suffered, and it suffered a lot: two dozen warnings per panel, which is enough
        /// noise to hide something that matters.
        ///
        /// A GameObject that is inactive when a component is added does not run that component's
        /// Awake until it is activated, so building it inactive puts the font in place before TMP
        /// ever goes looking for one.
        /// </remarks>
        /// <summary>A key in this player's language.</summary>
        private static string L(string key)
        {
            return Localization.instance != null ? Localization.instance.Localize(key) : key;
        }

        internal static GameObject TextObject(string name, TMP_FontAsset font, params Type[] extra)
        {
            GameObject obj = new GameObject(name);
            obj.SetActive(false);

            obj.AddComponent<RectTransform>();
            TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();

            // Added after the text, and in the order they were given, so anything needing a
            // Graphic to sit on still finds one.
            foreach (Type component in extra)
            {
                obj.AddComponent(component);
            }

            if (font != null)
            {
                text.font = font;
            }

            obj.SetActive(true);
            return obj;
        }

    }
}
