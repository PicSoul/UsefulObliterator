using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UsefulObliterator
{
    /// <summary>
    /// What is inside the Obliterator, drawn under the slot machine while it is up.
    /// </summary>
    /// <remarks>
    /// The contents are counted when the spin starts, and every refresh after that is set against
    /// that count: what the spin brought shows a green +N, what it took a red -N. Anything the
    /// spin took completely stays on the strip, dimmed, so a loss is seen and not just missing.
    ///
    /// It reads the container's inventory as this game sees it. Whoever runs the spin writes the
    /// result into the container, and the game passes that on to everyone nearby, so the strip
    /// changes when the payout lands - on every player's screen, not only the one who pulled.
    /// </remarks>
    internal class ContentsStrip
    {
        private const int Columns = 16;
        private const int MaxCells = Columns * 2;
        private const float Cell = 46f;
        private const float Gap = 4f;
        private const float Pad = 8f;

        private class Entry
        {
            public Sprite Icon;
            public int Count;
        }

        private class Slot
        {
            public GameObject Root;
            public Image Icon;
            public TMP_Text Count;
            public TMP_Text Delta;
        }

        private readonly RectTransform _root;
        private readonly TMP_Text _more;
        private readonly List<Slot> _slots = new List<Slot>();
        private readonly TMP_FontAsset _font;

        /// <summary>The contents when the spin started, in the order they were found.</summary>
        private readonly Dictionary<string, Entry> _before = new Dictionary<string, Entry>();
        private readonly List<string> _order = new List<string>();

        internal float Height => _root.gameObject.activeSelf ? _root.sizeDelta.y : 0f;

        internal ContentsStrip(Transform panel, TMP_FontAsset font)
        {
            _font = font;

            GameObject root = new GameObject("ObliteratorContents", typeof(RectTransform), typeof(Image), typeof(Outline));
            root.transform.SetParent(panel, false);
            _root = root.GetComponent<RectTransform>();
            _root.anchorMin = new Vector2(0.5f, 0f);
            _root.anchorMax = new Vector2(0.5f, 0f);
            _root.pivot = new Vector2(0.5f, 1f);
            _root.anchoredPosition = new Vector2(0f, -8f);

            root.GetComponent<Image>().color = new Color(0.08f, 0.06f, 0.05f, 0.92f);
            Outline outline = root.GetComponent<Outline>();
            outline.effectColor = new Color(0.72f, 0.55f, 0.32f, 0.95f);
            outline.effectDistance = new Vector2(2f, -2f);

            _more = Text(root.transform, "More", 13f, TextAlignmentOptions.Right, new Color(0.95f, 0.90f, 0.80f));
            RectTransform moreRect = _more.rectTransform;
            moreRect.anchorMin = new Vector2(1f, 0f);
            moreRect.anchorMax = new Vector2(1f, 0f);
            moreRect.pivot = new Vector2(1f, 0f);
            moreRect.anchoredPosition = new Vector2(-Pad, 2f);
            moreRect.sizeDelta = new Vector2(200f, 16f);

            root.SetActive(false);
        }

        internal void Hide()
        {
            _root.gameObject.SetActive(false);
        }

        /// <summary>Counts what is in the Obliterator now, as the starting point for this spin.</summary>
        internal void Snapshot(Inventory inventory)
        {
            _before.Clear();
            _order.Clear();
            Count(inventory, _before, _order);
        }

        /// <summary>Redraws the strip from what is in the Obliterator now.</summary>
        internal void Refresh(Inventory inventory)
        {
            if (inventory == null)
            {
                Hide();
                return;
            }

            Dictionary<string, Entry> now = new Dictionary<string, Entry>();
            List<string> order = new List<string>(_order);
            Count(inventory, now, order);

            if (order.Count == 0)
            {
                Hide();
                return;
            }

            int shown = Mathf.Min(order.Count, MaxCells);
            int columns = Mathf.Min(shown, Columns);
            int rows = (shown + Columns - 1) / Columns;
            float extra = order.Count > MaxCells ? 14f : 0f;
            _root.sizeDelta = new Vector2(
                columns * Cell + (columns - 1) * Gap + Pad * 2f,
                rows * Cell + (rows - 1) * Gap + Pad * 2f + extra);

            while (_slots.Count < shown)
            {
                _slots.Add(NewSlot());
            }

            for (int i = 0; i < _slots.Count; i++)
            {
                Slot slot = _slots[i];
                if (i >= shown)
                {
                    slot.Root.SetActive(false);
                    continue;
                }

                string key = order[i];
                _before.TryGetValue(key, out Entry was);
                now.TryGetValue(key, out Entry isNow);
                int before = was?.Count ?? 0;
                int after = isNow?.Count ?? 0;
                int delta = after - before;

                RectTransform rect = (RectTransform)slot.Root.transform;
                rect.anchoredPosition = new Vector2(
                    Pad + (i % Columns) * (Cell + Gap),
                    -Pad - (i / Columns) * (Cell + Gap));

                slot.Icon.sprite = isNow?.Icon ?? was?.Icon;
                slot.Icon.enabled = slot.Icon.sprite != null;
                slot.Icon.color = after > 0 ? Color.white : new Color(1f, 0.45f, 0.45f, 0.45f);
                slot.Count.text = after > 1 ? after.ToString() : after == 0 ? "0" : "";
                slot.Count.color = after > 0 ? new Color(0.95f, 0.90f, 0.80f) : new Color(1f, 0.35f, 0.3f);

                if (delta > 0)
                {
                    slot.Delta.text = "+" + delta;
                    slot.Delta.color = new Color(0.4f, 0.95f, 0.45f);
                }
                else if (delta < 0)
                {
                    slot.Delta.text = "−" + (-delta);
                    slot.Delta.color = new Color(1f, 0.35f, 0.3f);
                }
                else
                {
                    slot.Delta.text = "";
                }

                slot.Root.SetActive(true);
            }

            _more.text = order.Count > MaxCells ? $"+{order.Count - MaxCells} more" : "";
            _root.gameObject.SetActive(true);
        }

        /// <summary>
        /// Adds up the inventory by item and quality - a stack split over several slots is one
        /// entry - keeping the order things were first seen in, so a slot does not jump about.
        /// </summary>
        private static void Count(Inventory inventory, Dictionary<string, Entry> into, List<string> order)
        {
            if (inventory == null)
            {
                return;
            }

            foreach (ItemDrop.ItemData item in inventory.GetAllItems())
            {
                if (item?.m_shared == null)
                {
                    continue;
                }

                string name = item.m_dropPrefab != null ? item.m_dropPrefab.name : item.m_shared.m_name;
                string key = name + "|" + item.m_quality;

                if (!into.TryGetValue(key, out Entry entry))
                {
                    entry = new Entry { Icon = item.GetIcon() };
                    into[key] = entry;
                }

                entry.Count += item.m_stack;
                if (!order.Contains(key))
                {
                    order.Add(key);
                }
            }
        }

        private Slot NewSlot()
        {
            GameObject root = new GameObject("Slot", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(_root, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(Cell, Cell);
            root.GetComponent<Image>().color = new Color(0.18f, 0.14f, 0.10f, 0.9f);

            GameObject iconObj = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(root.transform, false);
            RectTransform iconRect = iconObj.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.sizeDelta = new Vector2(-6f, -6f);
            Image icon = iconObj.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            TMP_Text count = Text(root.transform, "Count", 13f, TextAlignmentOptions.BottomRight, Color.white);
            count.rectTransform.anchorMin = Vector2.zero;
            count.rectTransform.anchorMax = Vector2.one;
            count.rectTransform.sizeDelta = new Vector2(-4f, -2f);

            TMP_Text delta = Text(root.transform, "Delta", 13f, TextAlignmentOptions.TopLeft, Color.white);
            delta.rectTransform.anchorMin = Vector2.zero;
            delta.rectTransform.anchorMax = Vector2.one;
            delta.rectTransform.sizeDelta = new Vector2(-4f, -2f);
            delta.fontStyle = FontStyles.Bold;

            return new Slot { Root = root, Icon = icon, Count = count, Delta = delta };
        }

        private TMP_Text Text(Transform parent, string name, float size, TextAlignmentOptions align, Color color)
        {
            GameObject obj = SlotUI.TextObject(name, _font, typeof(Outline));
            obj.transform.SetParent(parent, false);
            TMP_Text text = obj.GetComponent<TMP_Text>();
            text.fontSize = size;
            text.alignment = align;
            text.color = color;
            text.text = "";
            text.raycastTarget = false;
            Outline outline = obj.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);
            return text;
        }
    }
}
