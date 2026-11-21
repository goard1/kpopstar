using System;
using Munbanggu.Core;
using Munbanggu.Data;
using Munbanggu.Visual;
using UnityEngine;
using UnityEngine.UI;

namespace Munbanggu.UI
{
    /// <summary>
    /// 「동네」 창 (docs/08): 오늘의 달력·날씨, 동네 대회, 라이벌 가게, 2층 만화방 운영, 도감.
    /// 한 창 안에서 화면(메인 / 만화방 / 도감 탭)을 바꿔 그린다.
    /// </summary>
    public sealed class TownPanel
    {
        enum View { Main, Comic, Dex }

        readonly GameManager gm;
        readonly GameObject modal;
        readonly UIJuice juice;
        RectTransform content;
        Text title, money;
        Button back;
        View view;
        int dexTab;

        TownPanel(GameManager gm, GameObject modal, UIJuice juice)
        {
            this.gm = gm;
            this.modal = modal;
            this.juice = juice;
        }

        public bool IsOpen => modal != null && modal.activeSelf;

        public static TownPanel Build(GameManager gm, GameObject modal, RectTransform card, UIJuice juice)
        {
            var p = new TownPanel(gm, modal, juice);
            p.BuildCard(card);
            gm.Changed += () => { if (p.IsOpen) p.Rebuild(); };
            return p;
        }

        public void Open() => Open(View.Main);
        public void OpenComic() => Open(View.Comic);
        public void OpenDex(int tab = 0) { dexTab = tab; Open(View.Dex); }

        void Open(View v)
        {
            view = v;
            Rebuild();
            modal.SetActive(true);
            if (juice != null) juice.Punch(0.06f);
        }

        public void Close() => modal.SetActive(false);

        void BuildCard(RectTransform card)
        {
            title = UIKit.Label("Title", card, "동네", 56, UIKit.Ink, TextAnchor.MiddleLeft);
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

            back = UIKit.Button("Back", card, "← 동네", 38, UIKit.Cream, UIKit.Ink, out _);
            var brt = (RectTransform)back.transform;
            brt.anchorMin = new Vector2(0, 0); brt.anchorMax = new Vector2(0.38f, 0);
            brt.pivot = new Vector2(0.5f, 0);
            brt.offsetMin = new Vector2(44, 40); brt.offsetMax = new Vector2(-8, 112);
            back.onClick.AddListener(() => Open(View.Main));

            var close = UIKit.Button("Close", card, "닫기", 42, UIKit.Red, Color.white, out _);
            var crt = (RectTransform)close.transform;
            crt.anchorMin = new Vector2(0.38f, 0); crt.anchorMax = new Vector2(1, 0);
            crt.pivot = new Vector2(0.5f, 0);
            crt.offsetMin = new Vector2(8, 40); crt.offsetMax = new Vector2(-44, 112);
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
            back.gameObject.SetActive(view != View.Main);
            switch (view)
            {
                case View.Comic: title.text = "2층 만화방"; BuildComic(); break;
                case View.Dex: title.text = "도감"; BuildDex(); break;
                default: title.text = "동네"; BuildMain(); break;
            }
        }

        // ───────────── 메인 ─────────────

        void BuildMain()
        {
            var t = gm.Today;
            Header("오늘");
            {
                var c = Card(t.Hint != null ? 220 : 120);
                Label(c, $"{gm.Day}일차 {t.WeekName}요일 · {t.WeatherIcon} · {t.Title}", 32, UIKit.Ink, 26, 16, 26, 44);
                if (t.Hint != null) Label(c, t.Hint, 23, UIKit.Muted, 26, 66, 26, 140);
                var tm = gm.Tomorrow;
                Label(c, $"내일: {tm.WeekName}요일 · {tm.WeatherIcon} · {tm.Title}", 23, UIKit.Blue, 26, t.Hint != null ? 180 : 70, 26, 34);
            }

            Header("동네 대회 (하루 손님 ×1.4, 그 물건 ×3, 평판 +3)");
            if (!gm.TournamentsUnlocked)
                Note("가게 앞 평상을 지으면(증축 2단계) 평상 앞에서 대회를 열 수 있어요.", UIKit.Muted);
            else
                foreach (var def in TournamentTable.All)
                {
                    var p = gm.Catalog.Find(def.productId);
                    if (p == null || !gm.IsUnlocked(p)) continue;
                    string blocker = gm.TournamentBlocker(def);
                    bool today = gm.TournamentToday == def;
                    var c = Card(130);
                    Icon(c, p);
                    Label(c, def.name + (today ? "  ★ 오늘!" : ""), 30, today ? UIKit.Red : UIKit.Ink, 110, 14, 250, 44);
                    Label(c, blocker ?? $"상품: {p.displayName} {def.prize}개 (창고에서)", 22, blocker != null && !today ? UIKit.Red : UIKit.Muted, 110, 62, 250, 56);
                    string id = def.id;
                    Side(c, -1, today ? "여는 중" : "열기", UIKit.Green, blocker == null, () => gm.HoldTournament(id));
                }

            Header("라이벌 가게");
            {
                var r = gm.Town.rival;
                string name = RivalTable.Name(gm.District);
                var c = Card(170);
                if (r.closed && r.district == gm.District)
                {
                    Label(c, $"{name} — 문 닫음!", 30, UIKit.Green, 26, 16, 26, 44);
                    Label(c, "아이들이 전부 할머니 가게로 돌아왔어요.", 23, UIKit.Muted, 26, 66, 26, 80);
                }
                else if (r.active && r.district == gm.District)
                {
                    int bars = Mathf.RoundToInt(r.strength / 10f);
                    Label(c, $"{name}  기세 {r.strength}", 30, r.strength >= 60 ? UIKit.Red : UIKit.Ink, 26, 16, 26, 44);
                    Label(c, new string('■', bars) + new string('□', 10 - bars) + $"  손님 -{Mathf.RoundToInt(RivalTable.MaxSteal * r.strength)}%", 24, UIKit.Red, 26, 62, 26, 34);
                    Label(c, "이기는 법: 상담 딱 맞히기, 동네 대회, 콤보, 만화 완결, 평판 80 이상", 22, UIKit.Muted, 26, 104, 26, 56);
                }
                else
                    Label(c, "아직 조용해요. 장사가 잘되면 누군가 길 건너에 가게를 낼지도…", 23, UIKit.Muted, 26, 16, 26, 140);
            }

            Header("할머니 스킬 (배우면 계속 · 보라색 버튼은 영업 중에 눌러 써요)");
            foreach (var s in GrandmaSkills.All)
            {
                string blocker = gm.GrandmaSkillBlocker(s);
                bool learned = gm.HasGrandmaSkill(s.id);
                bool locked = !learned && blocker != null;
                var c = Card(130);
                var tag = UIKit.CardPanel("Kind", c.transform, s.active ? new Color(0.56f, 0.40f, 0.80f) : UIKit.Green, 12, false, 3);
                UIKit.Place(tag.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -18), new Vector2(84, 34));
                var tl = UIKit.Label("Text", tag.transform, s.active ? "버튼" : "늘 켜짐", 20, Color.white, TextAnchor.MiddleCenter);
                UIKit.Stretch(tl.rectTransform);
                Label(c, locked && gm.Day < s.minDay - 2 ? "???" : s.name, 30, learned ? UIKit.Green : UIKit.Ink, 122, 14, 250, 40);
                Label(c, locked && gm.Day < s.minDay - 2 ? $"{s.minDay}일차쯤 알 수 있어요" : (learned ? "✓ " : "") + s.desc, 21, locked ? UIKit.Muted : UIKit.Muted, 26, 60, 250, 60);
                string id = s.id;
                Side(c, -1, learned ? "배움" : blocker != null ? "아직" : $"배우기\n{UIKit.Won(s.cost)}", UIKit.Accent,
                    !learned && blocker == null && gm.Money >= s.cost, () => gm.BuyGrandmaSkill(id));
            }

            Header("2층 만화방");
            if (!gm.HasComicRoom)
                Note("증축으로 2층 만화방을 지으면 열려요. 만화책을 모으고 자리·요금을 정해 운영해요.", UIKit.Muted);
            else
            {
                var cs = gm.Town.comic;
                var c = Card(150);
                Label(c, $"{ComicTable.SeatNames[cs.seatLevel]} · 요금 {UIKit.Won(cs.Fee)}", 30, UIKit.Ink, 26, 14, 250, 44);
                Label(c, $"어제 손님 {cs.lastReaders}명 · 오늘 예상 {gm.PlannedComicReaders()}명", 23, UIKit.Muted, 26, 62, 250, 60);
                Side(c, -1, "관리하기", UIKit.Blue, true, () => Open(View.Comic));
            }

            Header("도감");
            {
                var c = Card(190);
                string[] names = { "상품", "손님", "콤보", "만화" };
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < 4; i++)
                {
                    var (have, total) = gm.DexProgress(i);
                    sb.Append($"{names[i]} {have}/{total}");
                    if (i < 3) sb.Append(i == 1 ? "\n" : "   ");
                }
                Label(c, sb.ToString(), 28, UIKit.Ink, 26, 20, 250, 100);
                Label(c, "다 채우면 보상 (+3,000원, 평판 +5)", 22, UIKit.Muted, 26, 124, 250, 40);
                Side(c, -1, "도감 보기", UIKit.Accent, true, () => OpenDex(0));
            }
        }

        // ───────────── 만화방 ─────────────

        void BuildComic()
        {
            var cs = gm.Town.comic;
            Note($"오늘 예상 손님 {gm.PlannedComicReaders()}명 / 자리 {cs.Seats} · 아이들은 만화 보다 1층에 간식 사러 내려와요.", UIKit.Muted);

            {
                var c = Card(130);
                Label(c, ComicTable.SeatNames[cs.seatLevel], 30, UIKit.Ink, 26, 16, 250, 44);
                Label(c, "자리가 많을수록 손님을 더 받아요", 22, UIKit.Muted, 26, 64, 250, 40);
                bool max = cs.seatLevel >= ComicTable.SeatCosts.Length;
                int cost = max ? 0 : ComicTable.SeatCosts[cs.seatLevel];
                Side(c, -1, max ? "최고" : $"자리 늘리기\n{UIKit.Won(cost)}", UIKit.Green, !max && gm.Money >= cost, () => gm.UpgradeComicSeats());
            }
            {
                var c = Card(140);
                Label(c, "시간당 요금 (비싸면 덜 오고, 싸면 꽉 차요)", 24, UIKit.Ink, 26, 14, 26, 36);
                for (int i = 0; i < ComicTable.Fees.Length; i++)
                {
                    int idx = i;
                    bool sel = cs.feeIndex == i;
                    var b = UIKit.Button("Fee", c.transform, UIKit.Won(ComicTable.Fees[i]), 28, sel ? UIKit.Accent : UIKit.Cream, sel ? Color.white : UIKit.Ink, out _);
                    var rt = (RectTransform)b.transform;
                    rt.anchorMin = new Vector2(i / 3f, 0); rt.anchorMax = new Vector2((i + 1) / 3f, 0);
                    rt.pivot = new Vector2(0.5f, 0);
                    rt.offsetMin = new Vector2(i == 0 ? 26 : 8, 16); rt.offsetMax = new Vector2(i == 2 ? -26 : -8, 76);
                    b.onClick.AddListener(() => gm.SetComicFee(idx));
                }
            }

            Header("만화책 (권마다 사서 꽂아 둬요 · 완결까지 모으면 보너스)");
            var fresh = ComicTable.NewToday(gm.Day);
            foreach (var s in ComicTable.All)
            {
                int owned = cs.Owned(s.id), released = ComicTable.Released(s, gm.Day);
                bool isNew = fresh == s && owned < released;
                bool done = owned >= s.total;
                var c = Card(150);
                var swatch = UIKit.CardPanel("Cover", c.transform, s.color, 10, false, 3);
                UIKit.Place(swatch.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(66, 96));
                Label(c, s.name + (done ? "  ★완결" : isNew ? "  신간!" : ""), 30, isNew ? UIKit.Red : done ? UIKit.Green : UIKit.Ink, 110, 12, 250, 44);
                Label(c, $"{owned}권 보유 / {released}권 나옴 (전 {s.total}권)\n{s.blurb}", 21, UIKit.Muted, 110, 58, 250, 80);
                string blocker = gm.ComicBuyBlocker(s);
                string id = s.id;
                Side(c, -1, blocker == null ? $"{owned + 1}권 사기\n{UIKit.Won(s.pricePerVolume)}" : done ? "완결" : "대기", UIKit.Blue,
                    blocker == null && gm.Money >= s.pricePerVolume, () => gm.BuyComicVolume(id));
            }
        }

        // ───────────── 도감 ─────────────

        void BuildDex()
        {
            string[] tabs = { "상품", "손님", "콤보", "만화" };
            {
                var c = Card(96);
                for (int i = 0; i < tabs.Length; i++)
                {
                    int idx = i;
                    var (have, total) = gm.DexProgress(i);
                    bool sel = dexTab == i;
                    var b = UIKit.Button("Tab", c.transform, $"{tabs[i]} {have}/{total}", 24, sel ? UIKit.Accent : UIKit.Cream, sel ? Color.white : UIKit.Ink, out _);
                    var rt = (RectTransform)b.transform;
                    rt.anchorMin = new Vector2(i / 4f, 0); rt.anchorMax = new Vector2((i + 1) / 4f, 1);
                    rt.offsetMin = new Vector2(8, 12); rt.offsetMax = new Vector2(-8, -12);
                    b.onClick.AddListener(() => { dexTab = idx; Rebuild(); });
                }
            }
            var col = gm.Town.collection;
            switch (dexTab)
            {
                case 0:
                    foreach (var p in gm.Catalog.products)
                    {
                        int sold = col.Sold(p.id);
                        var c = Card(120);
                        if (sold > 0) Icon(c, p);
                        Label(c, sold > 0 ? $"{p.displayName}  ({sold}개 팖)" : "???", 28, sold > 0 ? UIKit.Ink : UIKit.Muted, 110, 12, 26, 40);
                        Label(c, sold > 0 ? p.blurb : $"{p.unlockDay}일차부터 들어오는 물건. 팔아 보면 알 수 있어요.", 21, UIKit.Muted, 110, 56, 26, 60);
                    }
                    break;
                case 1:
                    foreach (var k in CustomerDex.Kinds)
                    {
                        bool met = col.customers.Contains(k.key);
                        var c = Card(110);
                        Label(c, met ? k.name : "???", 28, met ? UIKit.Ink : UIKit.Muted, 26, 12, 26, 40);
                        Label(c, met ? k.desc : "아직 못 만난 손님이에요.", 21, UIKit.Muted, 26, 56, 26, 46);
                    }
                    break;
                case 2:
                    foreach (ProductCategory cat in Enum.GetValues(typeof(ProductCategory)))
                    {
                        bool found = gm.Facilities.discovered.Contains(FacilityTable.CornerId(cat));
                        var c = Card(100);
                        Label(c, found ? FacilityTable.CornerName(cat) : "??? 코너", 28, found ? UIKit.Ink : UIKit.Muted, 26, 12, 26, 40);
                        Label(c, "코너 콤보: 진열대 종류에 맞는 장식을 올려요", 21, UIKit.Muted, 26, 56, 26, 36);
                    }
                    foreach (var s in FacilityTable.Sets)
                    {
                        bool found = gm.Facilities.discovered.Contains(s.id);
                        var pa = gm.Catalog.Find(s.a);
                        var pb = gm.Catalog.Find(s.b);
                        var c = Card(110);
                        Label(c, found ? s.name : "???", 28, found ? UIKit.Ink : UIKit.Muted, 26, 12, 26, 40);
                        string hint = found
                            ? $"{pa?.displayName} + {pb?.displayName} 를 옆 진열대에"
                            : $"힌트: {Cat(pa)} 하나 + {Cat(pb)} 하나를 옆 진열대에";
                        Label(c, hint, 21, UIKit.Muted, 26, 56, 26, 46);
                    }
                    break;
                default:
                    foreach (var s in ComicTable.All)
                    {
                        int owned = gm.Town.comic.Owned(s.id);
                        var c = Card(110);
                        Label(c, owned > 0 ? $"{s.name}  {owned}/{s.total}권" + (owned >= s.total ? " ★" : "") : "???", 28, owned > 0 ? UIKit.Ink : UIKit.Muted, 26, 12, 26, 40);
                        Label(c, owned > 0 ? s.blurb : "만화방에 들여놓으면 알 수 있어요.", 21, UIKit.Muted, 26, 56, 26, 46);
                    }
                    break;
            }
        }

        static string Cat(ProductData p) => p == null ? "?" : p.category switch
        {
            ProductCategory.Snack => "간식",
            ProductCategory.Stationery => "학용품",
            ProductCategory.Toy => "장난감",
            _ => "놀이",
        };

        // ───── 도우미 ─────

        Image Card(float height)
        {
            var card = UIKit.CardPanel("Card", content, UIKit.Card, 24, true, 6);
            UIKit.Layout(card, -1, height);
            return card;
        }

        void Header(string text)
        {
            var h = UIKit.Label("Header", content, text, 27, UIKit.Accent, TextAnchor.LowerLeft);
            UIKit.Layout(h, -1, 54);
        }

        void Note(string text, Color color)
        {
            var n = UIKit.Label("Note", content, text, 24, color, TextAnchor.MiddleLeft);
            UIKit.Layout(n, -1, 80);
        }

        static void Icon(Image card, ProductData p)
        {
            var sprite = ProductIcons.Get(p);
            if (sprite == null) return;
            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(card.transform, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            UIKit.Place(img.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(76, 76));
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

        static void Side(Image card, int index, string label, Color bg, bool interactable, Action onClick)
        {
            var b = UIKit.Button("Side", card.transform, label, 26, bg, Color.white, out _);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = new Vector2(1, index == 0 ? 0.5f : 0);
            rt.anchorMax = new Vector2(1, index == 1 ? 0.5f : 1);
            rt.pivot = new Vector2(1, 0.5f);
            rt.offsetMin = new Vector2(-226, index == 0 ? 4 : 16);
            rt.offsetMax = new Vector2(-16, index == 1 ? -4 : -16);
            b.interactable = interactable;
            b.onClick.AddListener(() => onClick());
        }
    }
}
