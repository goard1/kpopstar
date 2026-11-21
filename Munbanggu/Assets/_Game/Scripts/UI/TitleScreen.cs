using System;
using Munbanggu.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Munbanggu.UI
{
    /// <summary>
    /// 처음 켤 때 나오는 타이틀 화면: 로고, 저장 슬롯 3개(이어하기·새 게임·지우기), 언어 바꾸기.
    /// 슬롯을 고르면 onStart(slot)을 부르고 스스로 사라진다. 언어를 바꾸면 화면을 새로 그린다.
    /// </summary>
    public sealed class TitleScreen : MonoBehaviour
    {
        const string LastSlotPref = "lastSlot";
        Action<int> onStart;
        RectTransform safe;
        int confirmDelete = -1; // 지우기를 한 번 누른 슬롯 (한 번 더 누르면 지움)

        public static TitleScreen Show(Action<int> onStart)
        {
            var go = new GameObject("Title", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            go.AddComponent<GraphicRaycaster>();
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                es.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            var t = go.AddComponent<TitleScreen>();
            t.onStart = onStart;
            t.Rebuild();
            return t;
        }

        void Rebuild()
        {
            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);

            // 배경: 크래프트지 + 줄무늬, 그 위 공책 종이
            var bg = UIKit.Panel("Background", transform, UIKit.Kraft, true);
            UIKit.Square(bg);
            UIKit.Stretch(bg.rectTransform);
            var stripes = UIKit.Stripes(bg.transform, UIKit.WithAlpha(UIKit.KraftDark, 0.18f), 40f);
            UIKit.Stretch(stripes.rectTransform);

            safe = UIKit.Rect("Safe Area", transform);
            UIKit.Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();

            BuildLogo();
            for (int s = 1; s <= SaveSystem.SlotCount; s++) BuildSlot(s);
            BuildLanguage();
        }

        void BuildLogo()
        {
            var card = UIKit.CardPanel("Logo", safe, UIKit.Paper, 40, false, 12);
            UIKit.Top(card.rectTransform, 430, 110, 70);
            var lines = UIKit.NoteLines(card.transform, 58f, 60f);
            UIKit.Stretch(lines.rectTransform, 0, 0, 0, 0);
            UIKit.TapeStrip(card.transform, new Vector2(0.15f, 1f), new Vector2(0, -6), -8f);
            UIKit.TapeStrip(card.transform, new Vector2(0.85f, 1f), new Vector2(0, -6), 7f);

            // 가게 앞 차양 (빨강·흰 줄)
            var awningBg = UIKit.CardPanel("Awning", card.transform, UIKit.Red, 18, false, 6);
            UIKit.Top(awningBg.rectTransform, 70, 40, 60);
            var awning = awningBg.rectTransform;
            for (int i = 1; i < 9; i += 2) // 빨강 바탕 위 흰 줄 → 빨강·흰 차양
            {
                var st = UIKit.Panel("Stripe " + i, awning, Color.white, false);
                UIKit.Round(st, 8f);
                st.rectTransform.anchorMin = new Vector2(i / 9f, 0); st.rectTransform.anchorMax = new Vector2((i + 1) / 9f, 1);
                st.rectTransform.offsetMin = new Vector2(0, 8); st.rectTransform.offsetMax = new Vector2(0, -8);
            }

            var title = UIKit.Label("Title", card.transform, "추억의 문방구", 112, UIKit.Red, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Top(title.rectTransform, 150, 130, 20);
            UIKit.AddShadow(title, UIKit.WithAlpha(UIKit.GoldDark, 0.55f), 6f);
            var sub = UIKit.Label("Sub", card.transform, "할머니의 골목 가게 경영 이야기", 36, UIKit.Muted, TextAnchor.MiddleCenter);
            UIKit.Top(sub.rectTransform, 54, 290, 20);
            var hint = UIKit.Label("Hint", card.transform, "저장 슬롯을 골라요", 30, UIKit.Ink, TextAnchor.MiddleCenter);
            UIKit.Top(hint.rectTransform, 44, 356, 20);
        }

        void BuildSlot(int slot)
        {
            var data = SaveSystem.Peek(slot);
            float y = 590 + (slot - 1) * 300;
            bool isLast = PlayerPrefs.GetInt(LastSlotPref, 1) == slot && data != null;
            var card = UIKit.CardPanel("Slot " + slot, safe, UIKit.Card, 32, false, 10, isLast ? UIKit.Gold : (Color?)null);
            UIKit.Top(card.rectTransform, 260, y, 70);

            var badge = UIKit.CardPanel("Badge", card.transform, data != null ? UIKit.Red : UIKit.Muted, 46, false, 4);
            UIKit.Place(badge.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(34, 0), new Vector2(110, 110));
            var num = UIKit.Label("Num", badge.transform, slot.ToString(), 60, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(num.rectTransform);

            var name = UIKit.Label("Name", card.transform, $"슬롯 {slot}", 40, UIKit.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Place(name.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(170, -30), new Vector2(420, 56));
            string info = data == null ? "비어 있음"
                : $"{data.day}일차 · {ExpansionTable.District(data.district).name}\n{UIKit.Won(data.money)} · 평판 {Mathf.Max(1, data.reputation)}";
            var infoLbl = UIKit.Label("Info", card.transform, info, 28, UIKit.Muted, TextAnchor.UpperLeft);
            UIKit.Place(infoLbl.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(170, -92), new Vector2(460, 120));

            int s = slot;
            var play = UIKit.Button("Play", card.transform, data != null ? "이어하기" : "새 게임", 38, data != null ? UIKit.Green : UIKit.Blue, Color.white, out _);
            UIKit.Place((RectTransform)play.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-30, data != null ? 34 : 0), new Vector2(250, 110));
            play.onClick.AddListener(() => Begin(s));

            if (data != null)
            {
                bool confirm = confirmDelete == slot;
                var del = UIKit.Button("Delete", card.transform, confirm ? "지울게요" : "지우기", 26, confirm ? UIKit.Red : UIKit.Cream, confirm ? Color.white : UIKit.Muted, out _);
                UIKit.Place((RectTransform)del.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-30, -70), new Vector2(250, 64));
                del.onClick.AddListener(() =>
                {
                    if (confirmDelete == s) { SaveSystem.Delete(s); confirmDelete = -1; }
                    else confirmDelete = s;
                    Rebuild();
                });
                if (confirm)
                {
                    var warn = UIKit.Label("Warn", card.transform, "지우면 되돌릴 수 없어요.", 24, UIKit.Red, TextAnchor.LowerLeft);
                    UIKit.Place(warn.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(170, 18), new Vector2(420, 36));
                }
            }
        }

        void BuildLanguage()
        {
            var label = UIKit.Label("Lang Label", safe, "언어", 30, UIKit.Cream, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Bottom(label.rectTransform, 44, 190, 70);
            var langs = new[] { Lang.Ko, Lang.ZhTW };
            for (int i = 0; i < langs.Length; i++)
            {
                var l = langs[i];
                bool on = L.Current == l;
                // 언어 이름은 그 언어 글자로 (SafeText는 한글만 바꾸므로 "한국어"는 번역표에 넣지 않는다)
                var b = UIKit.Button("Lang " + l, safe, L.Name(l), 34, on ? UIKit.Red : UIKit.Cream, on ? Color.white : UIKit.Ink, out _);
                var rt = (RectTransform)b.transform;
                rt.anchorMin = new Vector2(i * 0.5f, 0); rt.anchorMax = new Vector2((i + 1) * 0.5f, 0);
                rt.pivot = new Vector2(0.5f, 0);
                rt.offsetMin = new Vector2(i == 0 ? 70 : 12, 70); rt.offsetMax = new Vector2(i == 0 ? -12 : -70, 170);
                b.onClick.AddListener(() =>
                {
                    if (L.Current == l) return;
                    L.Set(l);
                    UIKit.ResetFont();
                    confirmDelete = -1;
                    Rebuild();
                });
            }
        }

        void Begin(int slot)
        {
            PlayerPrefs.SetInt(LastSlotPref, slot);
            PlayerPrefs.Save();
            SaveSystem.Slot = slot;
            var cb = onStart;
            Destroy(gameObject);
            cb?.Invoke(slot);
        }
    }
}
