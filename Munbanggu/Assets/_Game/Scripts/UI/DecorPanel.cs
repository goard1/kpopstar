using System;
using Munbanggu.Core;
using Munbanggu.Data;
using UnityEngine;
using UnityEngine.UI;

namespace Munbanggu.UI
{
    /// <summary>
    /// 진열대 하나의 장식 창: 지금 진열 상품·종류, 놓인 장식(레벨업·치우기), 놓을 수 있는 장식 목록(콤보 힌트).
    /// 배치 모드에서 진열대 표시를 탭하면 열린다.
    /// </summary>
    public sealed class DecorPanel
    {
        readonly GameManager gm;
        readonly GameObject modal;
        readonly UIJuice juice;
        RectTransform content;
        Text title, money;
        int slot = -1;

        DecorPanel(GameManager gm, GameObject modal, UIJuice juice)
        {
            this.gm = gm;
            this.modal = modal;
            this.juice = juice;
        }

        public bool IsOpen => modal != null && modal.activeSelf;

        public static DecorPanel Build(GameManager gm, GameObject modal, RectTransform card, UIJuice juice)
        {
            var p = new DecorPanel(gm, modal, juice);
            p.BuildCard(card);
            return p;
        }

        public void Open(int slotIndex)
        {
            slot = slotIndex;
            Rebuild();
            modal.SetActive(true);
            if (juice != null) juice.Punch(0.06f);
        }

        public void Close() => modal.SetActive(false);

        static string CategoryName(ProductCategory c) => c switch
        {
            ProductCategory.Snack => "간식",
            ProductCategory.Stationery => "학용품",
            ProductCategory.Toy => "장난감",
            _ => "놀이",
        };

        void BuildCard(RectTransform card)
        {
            title = UIKit.Label("Title", card, "진열대 장식", 54, UIKit.Ink, TextAnchor.MiddleLeft);
            UIKit.Top(title.rectTransform, 80, 40, 44);

            var pill = UIKit.CardPanel("Money", card, UIKit.Yellow, 34, false, 5);
            UIKit.Place(pill.rectTransform, new Vector2(1, 1), new Vector2(1, 0.5f), new Vector2(-40, -80), new Vector2(300, 70));
            money = UIKit.Label("Text", pill.transform, "", 36, UIKit.Ink, TextAnchor.MiddleRight);
            UIKit.Stretch(money.rectTransform, 20, 2, 22, 0);
            money.horizontalOverflow = HorizontalWrapMode.Overflow;

            var viewport = UIKit.Rect("List", card);
            UIKit.Stretch(viewport, 22, 176, 22, 140);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);

            content = UIKit.Rect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12;
            vlg.padding = new RectOffset(8, 8, 10, 16);
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = card.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.decelerationRate = 0.12f;

            var close = UIKit.Button("Close", card, "닫기", 42, UIKit.Red, Color.white, out _);
            UIKit.Bottom((RectTransform)close.transform, 112, 44, 44);
            close.onClick.AddListener(Close);
        }

        void Rebuild()
        {
            money.text = UIKit.Won(gm.Money);
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var child = content.GetChild(i).gameObject;
                if (Application.isPlaying) UnityEngine.Object.Destroy(child);
                else UnityEngine.Object.DestroyImmediate(child);
            }

            var product = slot >= 0 && slot < gm.Layout.Slots.Count ? gm.Layout.Slots[slot].Product : null;
            string onShelf = product != null ? $"지금 진열: {product.displayName} ({CategoryName(product.category)})" : "지금 비어 있는 진열대";
            string combo = gm.SlotCombo(slot);
            Note(combo != null ? $"{onShelf}\n★ 콤보: {combo}" : onShelf, combo != null ? UIKit.Red : UIKit.Muted);

            var placed = gm.Facilities.At(slot);
            if (placed != null)
            {
                var def = FacilityTable.FindDecor(placed.id);
                Header("놓인 장식");
                var card = Card(150);
                Label(card, $"{def.name}  Lv{placed.level}", 34, UIKit.Ink, 26, 18, 260, 50);
                Label(card, def.desc, 23, UIKit.Muted, 26, 70, 260, 60);
                bool maxed = placed.level >= def.MaxLevel;
                int cost = maxed ? 0 : def.costs[placed.level];
                Side(card, 0, maxed ? "최고 레벨" : $"레벨업 {UIKit.Won(cost)}", UIKit.Green, !maxed && gm.Money >= cost,
                    () => { gm.LevelUpDecor(slot); Rebuild(); });
                Side(card, 1, "치우기", UIKit.Muted, true, () => { gm.RemoveDecor(slot); Rebuild(); });
            }

            Header(placed != null ? "다른 장식으로 바꾸기 (지금 것은 절반 환불)" : "놓을 장식 고르기");
            foreach (var def in FacilityTable.Decors)
            {
                if (placed != null && placed.id == def.id) continue;
                bool corner = product != null && def.affinity != null && def.affinity.Value == product.category;
                var card = Card(140);
                Label(card, def.name + (corner ? "   ★ 코너 콤보!" : ""), 32, corner ? UIKit.Red : UIKit.Ink, 26, 16, 260, 48);
                Label(card, def.desc, 23, UIKit.Muted, 26, 64, 260, 56);
                int cost = def.costs[0];
                string id = def.id;
                Side(card, -1, UIKit.Won(cost), UIKit.Green, gm.Money >= cost, () => { gm.PlaceDecor(slot, id); Rebuild(); });
            }
        }

        // ───── 도우미 ─────

        Image Card(float height)
        {
            var card = UIKit.CardPanel("Card", content, UIKit.Card, 24, true, 6);
            UIKit.Layout(card, -1, height);
            return card;
        }

        void Header(string text)
        {
            var h = UIKit.Label("Header", content, text, 28, UIKit.Accent, TextAnchor.LowerLeft);
            UIKit.Layout(h, -1, 54);
        }

        void Note(string text, Color color)
        {
            var n = UIKit.Label("Note", content, text, 30, color, TextAnchor.MiddleLeft);
            UIKit.Layout(n, -1, 96);
        }

        static void Label(Image card, string text, int size, Color color, float left, float top, float right, float height)
        {
            var t = UIKit.Label("Text", card.transform, text, size, color, TextAnchor.UpperLeft);
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(left, -top - height);
            rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>오른쪽 버튼. index -1 = 세로 꽉 참, 0/1 = 위/아래 반쪽.</summary>
        static void Side(Image card, int index, string label, Color bg, bool interactable, Action onClick)
        {
            var b = UIKit.Button("Side", card.transform, label, 28, bg, Color.white, out _);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = new Vector2(1, index == 0 ? 0.5f : 0);
            rt.anchorMax = new Vector2(1, index == 1 ? 0.5f : 1);
            rt.pivot = new Vector2(1, 0.5f);
            rt.offsetMin = new Vector2(-236, index == 0 ? 4 : 16);
            rt.offsetMax = new Vector2(-16, index == 1 ? -4 : -16);
            b.interactable = interactable;
            b.onClick.AddListener(() => onClick());
        }
    }
}
