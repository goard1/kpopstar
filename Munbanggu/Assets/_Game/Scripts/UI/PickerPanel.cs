using System;
using System.Collections.Generic;
using Munbanggu.Core;
using Munbanggu.Customers;
using Munbanggu.Data;
using Munbanggu.Visual;
using UnityEngine;
using UnityEngine.UI;

namespace Munbanggu.UI
{
    /// <summary>
    /// 상담 손님 고르기 창: 아이의 말 + 가진 돈, 매대에 진열된 상품 카드 목록(예산 넘으면 흐리게),
    /// "미안, 맞는 게 없네" 버튼. 고르면 onPick(상품), 없다고 하면 onPick(null). 열린 동안 게임 시간 정지.
    /// 상품 특징은 상담으로 한 번 팔아 본 뒤에만 보인다 (그 전엔 ???).
    /// </summary>
    public sealed class PickerPanel
    {
        const int Columns = 3;

        readonly GameManager gm;
        readonly GameObject modal;
        readonly UIJuice juice;
        Text line, budget;
        RectTransform grid;
        Action<ProductData> onPick;

        PickerPanel(GameManager gm, GameObject modal, UIJuice juice)
        {
            this.gm = gm;
            this.modal = modal;
            this.juice = juice;
        }

        public bool IsOpen => modal != null && modal.activeSelf;

        public static PickerPanel Build(GameManager gm, GameObject modal, RectTransform card, UIJuice juice)
        {
            var p = new PickerPanel(gm, modal, juice);
            p.BuildCard(card);
            return p;
        }

        void BuildCard(RectTransform card)
        {
            // 아이 말풍선 (흰 카드)
            var speech = UIKit.CardPanel("Speech", card, Color.white, 30, false, 6);
            UIKit.Top(speech.rectTransform, 190, 44, 40);
            UIKit.Ring(speech.transform, UIKit.WithAlpha(UIKit.Ink, 0.85f), 30);
            line = UIKit.Label("Line", speech.transform, "", 40, UIKit.Ink, TextAnchor.MiddleLeft);
            UIKit.Stretch(line.rectTransform, 36, 60, 36, 18);
            budget = UIKit.Label("Budget", speech.transform, "", 30, UIKit.Red, TextAnchor.LowerLeft);
            UIKit.Stretch(budget.rectTransform, 36, 14, 36, 120);

            var hint = UIKit.Label("Hint", card, "매대에 있는 것 중에서 골라 주세요", 28, UIKit.Muted, TextAnchor.MiddleLeft);
            UIKit.Top(hint.rectTransform, 44, 246, 44);

            // 상품 카드 격자 (스크롤)
            var viewport = UIKit.Rect("List", card);
            UIKit.Stretch(viewport, 22, 176, 22, 300);
            viewport.gameObject.AddComponent<RectMask2D>();
            var hit = viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);

            grid = UIKit.Rect("Grid", viewport);
            grid.anchorMin = new Vector2(0, 1);
            grid.anchorMax = new Vector2(1, 1);
            grid.pivot = new Vector2(0.5f, 1);
            grid.offsetMin = grid.offsetMax = Vector2.zero;
            var g = grid.gameObject.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(286, 300);
            g.spacing = new Vector2(14, 14);
            g.padding = new RectOffset(8, 8, 8, 16);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = Columns;
            grid.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = card.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = grid;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.decelerationRate = 0.12f;

            var none = UIKit.Button("None", card, "미안, 맞는 게 없네", 38, UIKit.Muted, Color.white, out _);
            UIKit.Bottom((RectTransform)none.transform, 112, 44, 44);
            none.onClick.AddListener(() => Pick(null));
        }

        public void Open(CounselRequest r, Action<ProductData> pick)
        {
            onPick = pick;
            line.text = $"\"{r.line}\"";
            budget.text = $"가진 돈 {UIKit.Won(r.budget)}";

            for (int i = grid.childCount - 1; i >= 0; i--)
            {
                var child = grid.GetChild(i).gameObject;
                if (Application.isPlaying) UnityEngine.Object.Destroy(child);
                else UnityEngine.Object.DestroyImmediate(child);
            }
            foreach (var p in gm.ShelvedCounselProducts())
                BuildProductCard(p, r);

            modal.SetActive(true);
            if (juice != null) juice.Punch(0.06f);
        }

        void BuildProductCard(ProductData p, CounselRequest r)
        {
            bool affordable = p.retailPrice <= r.budget;
            var btn = UIKit.Button(p.id, grid, "", 28, UIKit.Card, UIKit.Ink, out var nameLabel);
            var group = btn.gameObject.AddComponent<CanvasGroup>();
            group.alpha = affordable ? 1f : 0.38f;
            btn.interactable = affordable;
            var product = p;
            btn.onClick.AddListener(() => Pick(product));

            var icon = UIKit.Rect("Icon", btn.transform).gameObject.AddComponent<Image>();
            icon.sprite = ProductIcons.Get(p);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            UIKit.Place(icon.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(110, 110));

            nameLabel.text = p.displayName;
            nameLabel.fontSize = p.displayName.Length > 5 ? 28 : 32;
            UIKit.Stretch(nameLabel.rectTransform, 8, 150, 8, 122);

            var price = UIKit.Label("Price", btn.transform, $"{UIKit.Won(p.retailPrice)} · {gm.ShelfCount(p)}개", 24, UIKit.Muted, TextAnchor.MiddleCenter);
            UIKit.Stretch(price.rectTransform, 8, 112, 8, 160);

            // 특징을 알면 #태그, 모르면 상품 설명을 힌트로 (작게)
            bool known = gm.IsTraitKnown(p);
            string traits = known ? "#" + string.Join(" #", ProductTraits.Of(p)) : p.blurb;
            var tags = UIKit.Label("Traits", btn.transform, traits, known ? 23 : 19, known ? UIKit.Blue : UIKit.Muted, TextAnchor.UpperCenter);
            UIKit.Stretch(tags.rectTransform, 12, 10, 12, 192);
            tags.lineSpacing = 0.9f;
        }

        void Pick(ProductData p)
        {
            var cb = onPick;
            onPick = null;
            modal.SetActive(false);
            cb?.Invoke(p);
        }

        /// <summary>강제로 닫기 (아무것도 안 고른 것으로 처리).</summary>
        public void Cancel()
        {
            if (IsOpen) Pick(null);
        }
    }
}
