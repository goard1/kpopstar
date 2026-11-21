using System;
using System.Collections.Generic;
using Munbanggu.Core;
using Munbanggu.Shop;
using UnityEngine;
using UnityEngine.UI;

namespace Munbanggu.UI
{
    /// <summary>
    /// 이야기 컷신 (docs/11). 그림책처럼: 위에는 UI로 그린 삽화(또는 3D 가게), 아래에는 말 카드.
    /// 글은 한 글자씩 나오고, 탭하면 다 보이거나 다음 장면으로. 「건너뛰기」로 끝까지.
    /// 보는 동안 다른 UI 캔버스는 숨긴다.
    /// </summary>
    public sealed class CutscenePlayer : MonoBehaviour
    {
        const float CharsPerSec = 34f;

        StoryChapter chapter;
        int index = -1;
        Action onDone;
        Camera cam;
        ShopCamera shopCam;
        Transform world;
        float baseSize;
        readonly List<Canvas> hidden = new List<Canvas>();

        Image bg, barTop, barBottom;
        RectTransform artCard, textCard, artLayer;
        Text speaker, body, center, arrow;
        Image speakerBg;
        Text typingTarget;
        string full = "";
        float typed;
        float beatTime;

        // 삽화 움직임
        RectTransform shutter; float shutterFrom, shutterTo;
        readonly List<RectTransform> wavers = new List<RectTransform>();
        readonly List<RectTransform> bobbers = new List<RectTransform>();
        RectTransform credits;

        public static CutscenePlayer Play(StoryChapter chapter, Camera cam, ShopCamera shopCam, Transform world, Action onDone)
        {
            var go = new GameObject("Cutscene", typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;
            go.AddComponent<GraphicRaycaster>();

            var p = go.AddComponent<CutscenePlayer>();
            p.chapter = chapter; p.onDone = onDone; p.cam = cam; p.shopCam = shopCam; p.world = world;
            p.baseSize = cam != null ? cam.orthographicSize : 5f;
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c != canvas && c.isRootCanvas && c.enabled) { c.enabled = false; p.hidden.Add(c); }
            p.Build();
            p.Next();
            return p;
        }

        void Build()
        {
            bg = UIKit.Panel("Background", transform, Color.black, true);
            UIKit.Square(bg);
            UIKit.Stretch(bg.rectTransform);

            barTop = UIKit.Panel("Bar Top", transform, Color.black, false); UIKit.Square(barTop);
            UIKit.Top(barTop.rectTransform, 170, 0, 0);
            barBottom = UIKit.Panel("Bar Bottom", transform, new Color(0, 0, 0, 0.75f), false); UIKit.Square(barBottom);
            UIKit.Bottom(barBottom.rectTransform, 620, 0, 0);

            var safe = UIKit.Rect("Safe Area", transform);
            UIKit.Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();

            // 탭: 화면 어디든
            var tap = bg.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(OnTap);

            var artImg = UIKit.CardPanel("Art", safe, UIKit.Paper, 40, false, 12);
            artCard = artImg.rectTransform;
            UIKit.Top(artCard, 1000, 190, 50);
            var mask = artCard.gameObject.AddComponent<Mask>(); mask.showMaskGraphic = true;
            artLayer = UIKit.Rect("Layer", artCard);
            UIKit.Stretch(artLayer);

            var tc = UIKit.CardPanel("Text Card", safe, UIKit.Cream, 36, false, 10);
            textCard = tc.rectTransform;
            UIKit.Bottom(textCard, 400, 130, 50);
            speakerBg = UIKit.CardPanel("Speaker", textCard, UIKit.Red, 26, false, 4);
            UIKit.Place(speakerBg.rectTransform, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(36, 0), new Vector2(300, 68));
            speaker = UIKit.Label("Name", speakerBg.transform, "", 34, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(speaker.rectTransform, 12, 0, 12, 0);
            body = UIKit.Label("Body", textCard, "", 40, UIKit.Ink, TextAnchor.UpperLeft);
            UIKit.Stretch(body.rectTransform, 44, 60, 44, 56);
            body.lineSpacing = 1.15f;
            arrow = UIKit.Label("Arrow", textCard, "▼", 30, UIKit.Red, TextAnchor.LowerRight);
            UIKit.Stretch(arrow.rectTransform, 30, 22, 34, 22);

            center = UIKit.Label("Center", safe, "", 48, Color.white, TextAnchor.MiddleCenter);
            UIKit.Stretch(center.rectTransform, 90, 300, 90, 300);
            center.lineSpacing = 1.3f;

            var skip = UIKit.Button("Skip", safe, "건너뛰기 ▶▶", 28, new Color(0, 0, 0, 0.45f), Color.white, out _);
            UIKit.Place((RectTransform)skip.transform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -40), new Vector2(240, 80));
            skip.onClick.AddListener(Finish);
        }

        void OnTap()
        {
            if (typingTarget != null && typed < full.Length) { typed = full.Length; typingTarget.text = full; return; }
            if (credits != null && beatTime < CreditsDuration - 0.5f) { beatTime = CreditsDuration - 0.5f; return; }
            Next();
        }

        void Next()
        {
            index++;
            if (chapter == null || index >= chapter.beats.Length) { Finish(); return; }
            ShowBeat(chapter.beats[index]);
        }

        void ShowBeat(StoryBeat b)
        {
            beatTime = 0f;
            for (int i = artLayer.childCount - 1; i >= 0; i--) Kill(artLayer.GetChild(i).gameObject);
            if (credits != null) Kill(credits.gameObject);
            shutter = null; credits = null; wavers.Clear(); bobbers.Clear();

            bool black = b.art == StoryArt.Black || b.art == StoryArt.Credits;
            bool worldShot = b.art == StoryArt.World;
            bg.color = black ? Color.black : worldShot ? new Color(0, 0, 0, 0.01f) : new Color(0.84f, 0.68f, 0.48f);
            barTop.gameObject.SetActive(worldShot);
            barBottom.gameObject.SetActive(worldShot);
            artCard.gameObject.SetActive(!black && !worldShot);
            textCard.gameObject.SetActive(!black && !string.IsNullOrEmpty(b.text));
            center.gameObject.SetActive(b.art == StoryArt.Black);

            if (!black && !worldShot) DrawArt(b);
            if (b.art == StoryArt.Credits) BuildCredits();

            // 카메라: World면 그곳으로, 아니면 원래대로
            if (shopCam != null && !worldShot) shopCam.Fit();

            speakerBg.gameObject.SetActive(!string.IsNullOrEmpty(b.speaker));
            speaker.text = b.speaker ?? "";
            speakerBg.color = b.color;

            typingTarget = b.art == StoryArt.Black ? center : body;
            full = Hero.Fill(L.Tr(b.text ?? ""));
            typed = 0f;
            body.text = ""; center.text = "";
            // 편지 장면은 말 없이 편지만 (말 카드 숨김)
        }

        void Finish()
        {
            if (this == null) return;
            if (shopCam != null) shopCam.Fit();
            foreach (var c in hidden) if (c != null) c.enabled = true;
            hidden.Clear();
            var cb = onDone; onDone = null;
            Kill(gameObject);
            cb?.Invoke();
        }

        static void Kill(GameObject g)
        {
            if (g == null) return;
            if (Application.isPlaying) Destroy(g); else DestroyImmediate(g);
        }

        public int BeatCount => chapter != null ? chapter.beats.Length : 0;

        /// <summary>스크린샷 도구용: i번째 장면을 글까지 다 보인 채로.</summary>
        public void DebugShow(int i)
        {
            index = i;
            ShowBeat(chapter.beats[i]);
            typed = full.Length;
            if (typingTarget != null) typingTarget.text = full;
            if (credits != null) credits.anchoredPosition = Vector2.zero;
            var b = chapter.beats[i];
            if (b.art == StoryArt.World && b.focus.HasValue && cam != null && world != null)
            {
                cam.transform.position = world.TransformPoint(b.focus.Value) - cam.transform.forward * 30f;
                cam.orthographicSize = baseSize / Mathf.Max(0.5f, b.zoom);
            }
        }

        public void DebugFinish() => Finish();

        const float CreditsDuration = 16f;

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            beatTime += dt;
            if (typingTarget != null && typed < full.Length)
            {
                typed = Mathf.Min(full.Length, typed + dt * CharsPerSec);
                typingTarget.text = full.Substring(0, (int)typed);
            }
            if (arrow != null) arrow.enabled = typed >= full.Length && Mathf.Repeat(beatTime, 0.8f) < 0.5f;

            if (shutter != null)
            {
                float t = Mathf.Clamp01((beatTime - 0.4f) / 1.8f);
                t = t * t * (3f - 2f * t);
                var sd = shutter.sizeDelta; sd.y = Mathf.Lerp(shutterFrom, shutterTo, t); shutter.sizeDelta = sd;
            }
            for (int i = 0; i < wavers.Count; i++)
                if (wavers[i] != null) wavers[i].localRotation = Quaternion.Euler(0, 0, -28f + Mathf.Sin(beatTime * 7f + i) * 24f);
            for (int i = 0; i < bobbers.Count; i++)
                if (bobbers[i] != null) bobbers[i].anchoredPosition = new Vector2(bobbers[i].anchoredPosition.x, Mathf.Abs(Mathf.Sin(beatTime * 3f + i)) * 12f);
            if (credits != null)
            {
                float t = Mathf.Clamp01(beatTime / CreditsDuration);
                credits.anchoredPosition = new Vector2(0, Mathf.Lerp(-1500f, 1500f, t));
                if (beatTime >= CreditsDuration) Next();
            }

            // World: 카메라를 천천히 그곳으로
            if (index >= 0 && chapter != null && index < chapter.beats.Length)
            {
                var b = chapter.beats[index];
                if (b.art == StoryArt.World && b.focus.HasValue && cam != null && world != null)
                {
                    var target = world.TransformPoint(b.focus.Value) - cam.transform.forward * 30f;
                    float k = 1f - Mathf.Exp(-dt * 2.2f);
                    cam.transform.position = Vector3.Lerp(cam.transform.position, target, k);
                    cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, baseSize / Mathf.Max(0.5f, b.zoom), k);
                }
            }
        }

        // ───────────── 삽화 ─────────────

        static readonly Color Sky = new Color(0.74f, 0.87f, 0.98f);
        static readonly Color Ground = new Color(0.78f, 0.74f, 0.70f);
        static readonly Color Wall = new Color(1f, 0.95f, 0.86f);
        static readonly Color ShutterBlue = new Color(0.32f, 0.52f, 0.86f);
        static readonly Color Skin = new Color(1f, 0.86f, 0.74f);
        static readonly Color Cheek = new Color(1f, 0.62f, 0.62f, 0.8f);
        static readonly Color GreyHair = new Color(0.82f, 0.82f, 0.86f);
        static readonly Color DarkHair = new Color(0.25f, 0.18f, 0.15f);
        static readonly Color GrandmaShirt = new Color(0.62f, 0.80f, 0.60f);

        void DrawArt(StoryBeat b)
        {
            switch (b.art)
            {
                case StoryArt.Drawer: Drawer(); break;
                case StoryArt.Note: NoteArt(b.note); break;
                case StoryArt.ShutterOpen: ShopFrontArt(true, false); break;
                case StoryArt.ShopFront: ShopFrontArt(false, false); break;
                case StoryArt.ShopKid: ShopFrontArt(false, true); break;
                case StoryArt.Photo: PhotoArt(); break;
                case StoryArt.Notebook: NotebookArt(b.note); break;
                case StoryArt.Farewell: FarewellArt(); break;
                case StoryArt.Street: StreetArt(); break;
                case StoryArt.Grownups: GrownupsArt(); break;
                case StoryArt.Letter: LetterArt(b.note); break;
            }
        }

        RectTransform Box(Transform parent, Color c, float x, float y, float w, float h, float radius = 0f, float rot = 0f)
        {
            var img = UIKit.Panel("Shape", parent, c, false);
            if (radius <= 0f) UIKit.Square(img); else UIKit.Round(img, radius);
            var rt = img.rectTransform;
            UIKit.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(w, h));
            rt.localRotation = Quaternion.Euler(0, 0, rot);
            return rt;
        }

        RectTransform Circle(Transform parent, Color c, float x, float y, float d) => Box(parent, c, x, y, d, d, d * 0.5f);

        Text Write(Transform parent, string text, int size, Color c, float x, float y, float w, float h, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var t = UIKit.Label("Text", parent, text, size, c, align);
            UIKit.Place(t.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(w, h));
            t.lineSpacing = 1.2f;
            return t;
        }

        /// <summary>둥근 머리 꼬마 (발끝이 y). 반환값 = 몸 전체 (통통 튀게 하려고).</summary>
        RectTransform Chibi(Transform parent, float x, float y, float s, Color hair, Color shirt, bool grandma = false, bool adult = false, bool wave = false)
        {
            var root = UIKit.Rect("Chibi", parent);
            UIKit.Place(root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), new Vector2(x, y), new Vector2(10, 10));
            var r = UIKit.Rect("Body", root);
            UIKit.Place(r, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(10, 10));
            float bodyH = (adult ? 190f : 120f) * s, bodyW = (adult ? 120f : 110f) * s;
            Box(r, UIKit.Darker(shirt, 0.7f), -24 * s, 18 * s, 34 * s, 40 * s, 12 * s);   // 다리
            Box(r, UIKit.Darker(shirt, 0.7f), 24 * s, 18 * s, 34 * s, 40 * s, 12 * s);
            Box(r, shirt, 0, 30 * s + bodyH * 0.5f, bodyW, bodyH, 44 * s);
            float hy = 30 * s + bodyH + 64 * s;
            Circle(r, hair, 0, hy + 8 * s, 168 * s);
            Circle(r, Skin, 0, hy - 8 * s, 146 * s);
            Box(r, hair, 0, hy + 50 * s, 146 * s, 52 * s, 26 * s);
            if (grandma)
            {
                Circle(r, hair, 0, hy + 96 * s, 64 * s);
                var gl = new Color(0.4f, 0.3f, 0.25f);
                Circle(r, gl, -30 * s, hy - 10 * s, 40 * s); Circle(r, Skin, -30 * s, hy - 10 * s, 30 * s);
                Circle(r, gl, 30 * s, hy - 10 * s, 40 * s); Circle(r, Skin, 30 * s, hy - 10 * s, 30 * s);
            }
            Circle(r, UIKit.Ink, -30 * s, hy - 10 * s, 15 * s);
            Circle(r, UIKit.Ink, 30 * s, hy - 10 * s, 15 * s);
            Circle(r, Cheek, -50 * s, hy - 34 * s, 24 * s);
            Circle(r, Cheek, 50 * s, hy - 34 * s, 24 * s);
            Box(r, new Color(0.7f, 0.3f, 0.3f), 0, hy - 38 * s, 22 * s, 8 * s, 4 * s);
            if (wave)
            {
                // 머리 옆으로 든 팔 + 손 (머리보다 앞에 그려야 보임)
                var arm = Box(r, shirt, bodyW * 0.55f, 30 * s + bodyH * 0.75f, 28 * s, 130 * s, 14 * s);
                arm.pivot = new Vector2(0.5f, 0f);
                arm.anchoredPosition = new Vector2(bodyW * 0.55f, 30 * s + bodyH * 0.75f);
                Circle(arm, Skin, 0, 70 * s, 40 * s);
                wavers.Add(arm);
            }
            return r;
        }

        static readonly Color HeroHair = new Color(0.36f, 0.24f, 0.18f);
        static readonly Color HeroApron = new Color(1f, 0.78f, 0.30f);

        /// <summary>주인공 손주 (손녀는 긴 머리 + 리본, 손자는 짧은 머리), 노란 가게 앞치마.</summary>
        RectTransform HeroChibi(Transform p, float x, float y, float s, bool grown = false)
        {
            float bodyH = (grown ? 190f : 120f) * s;
            float hy = y + 30 * s + bodyH + 64 * s;
            if (Hero.Girl) Box(p, HeroHair, x, hy - 40 * s, 176 * s, 200 * s, 60 * s);   // 긴 머리 (뒤)
            var r = Chibi(p, x, y, s, HeroHair, HeroApron, adult: grown);
            Box(r, Color.white, 0, 30 * s + bodyH * 0.62f, 70 * s, 10 * s, 4 * s);          // 앞치마 주머니
            if (Hero.Girl) Circle(r, new Color(0.95f, 0.42f, 0.5f), 62 * s, 30 * s + bodyH + 128 * s, 44 * s); // 리본
            return r;
        }

        void Fill(Color c) { var f = Box(artLayer, c, 0, 0, 2000, 2000); f.SetAsFirstSibling(); }

        void Drawer()
        {
            Fill(new Color(0.96f, 0.90f, 0.78f));
            Circle(artLayer, new Color(1f, 0.95f, 0.7f, 0.6f), 0, 300, 520);              // 스탠드 불빛
            Box(artLayer, UIKit.Kraft, 0, -230, 880, 420, 30);                           // 책상
            Box(artLayer, UIKit.Darker(UIKit.Kraft, 0.85f), 0, -60, 720, 140, 16);        // 열린 서랍 안
            Box(artLayer, UIKit.KraftDark, 0, -250, 720, 240, 20);                        // 서랍 앞판
            Circle(artLayer, UIKit.Gold, 0, -250, 56);
            // 열쇠
            Circle(artLayer, UIKit.Gold, -190, -40, 100); Circle(artLayer, UIKit.Darker(UIKit.Kraft, 0.85f), -190, -40, 50);
            Box(artLayer, UIKit.Gold, -60, -40, 200, 26, 8);
            Box(artLayer, UIKit.Gold, 10, -66, 24, 40, 4); Box(artLayer, UIKit.Gold, 50, -66, 24, 30, 4);
            // 접힌 쪽지
            var note = Box(artLayer, UIKit.Card, 200, -40, 240, 170, 10, 9f);
            for (int i = 0; i < 3; i++) Box(note, UIKit.NoteLine, 0, 40 - i * 36, 190, 6);
            // 할머니 손 (빼꼼)
            Circle(artLayer, Skin, 360, 120, 120);
        }

        void NoteArt(string note)
        {
            Fill(new Color(0.94f, 0.86f, 0.72f));
            var paper = Box(artLayer, UIKit.Card, 0, 40, 760, 560, 14, -3f);
            var lines = UIKit.NoteLines(paper, 70f, 40f); UIKit.Stretch(lines.rectTransform, 0, 0, 0, 40);
            Box(paper, UIKit.Margin, -300, 0, 5, 560);
            Write(paper, note ?? "", 50, new Color(0.25f, 0.25f, 0.45f), 30, 10, 600, 400);
            // 열쇠
            Circle(artLayer, UIKit.Gold, -300, -340, 90); Circle(artLayer, new Color(0.94f, 0.86f, 0.72f), -300, -340, 44);
            Box(artLayer, UIKit.Gold, -170, -340, 180, 24, 8);
        }

        void ShopFrontArt(bool opening, bool kid)
        {
            Fill(Sky);
            Circle(artLayer, new Color(1f, 1f, 1f, 0.8f), -300, 400, 160); Circle(artLayer, new Color(1f, 1f, 1f, 0.8f), -210, 410, 120);
            Box(artLayer, Ground, 0, -440, 1200, 260);
            Box(artLayer, Wall, 0, -60, 780, 700, 16);
            // 간판
            Box(artLayer, UIKit.Darker(UIKit.Red, 0.8f), 0, 250, 720, 170, 18);
            Box(artLayer, UIKit.Red, 0, 255, 690, 140, 14);
            Write(artLayer, "문방구", 96, Color.white, 0, 252, 680, 140);
            // 차양
            for (int i = 0; i < 8; i++)
                Box(artLayer, i % 2 == 0 ? UIKit.Red : Color.white, -315 + i * 90, 140, 90, 60);
            // 가게 안 (진열대 + 상품)
            var inside = Box(artLayer, new Color(0.45f, 0.32f, 0.25f), 0, -130, 560, 400, 6);
            Color[] goods = { UIKit.Red, UIKit.Yellow, UIKit.Blue, UIKit.Green, new Color(0.95f, 0.55f, 0.75f) };
            for (int row = 0; row < 3; row++)
            {
                Box(inside, UIKit.Kraft, 0, 120 - row * 120, 520, 14);
                for (int i = 0; i < 7; i++)
                    Box(inside, goods[(i + row * 2) % goods.Length], -220 + i * 74, 150 - row * 120, 50, 48, 8);
            }
            // 셔터 (위에서 내려옴: 위쪽 고정)
            var sh = Box(artLayer, ShutterBlue, 0, 70, 580, opening ? 410 : 46, 6);
            sh.pivot = new Vector2(0.5f, 1f);
            sh.anchoredPosition = new Vector2(0, 70);
            var stripes = UIKit.Stripes(sh, UIKit.WithAlpha(UIKit.Darker(ShutterBlue, 0.7f), 0.5f), 24f);
            UIKit.Stretch(stripes.rectTransform);
            if (opening) { shutter = sh; shutterFrom = 410; shutterTo = 46; }
            if (kid)
            {
                var k = Chibi(artLayer, 300, -420, 1.15f, DarkHair, UIKit.Blue, wave: true);
                bobbers.Add(k);
            }
            HeroChibi(artLayer, -300, -420, 1.15f);
        }

        void PhotoArt()
        {
            Fill(new Color(0.90f, 0.82f, 0.70f));
            var frame = Box(artLayer, Color.white, 0, 30, 620, 760, 10, -4f);
            var pic = Box(frame, new Color(0.96f, 0.86f, 0.62f), 0, 60, 560, 560, 4);
            Box(pic, new Color(0.88f, 0.74f, 0.50f), 0, -200, 560, 160);
            Chibi(pic, -110, -260, 1.2f, DarkHair, new Color(0.92f, 0.35f, 0.38f));       // 젊은 할머니
            Chibi(pic, 110, -260, 1.25f, DarkHair, new Color(0.3f, 0.45f, 0.75f));       // 젊은 할아버지
            Write(frame, "그해 봄, 우리 둘", 40, UIKit.Ink, 0, -300, 520, 70);
            UIKit.TapeStrip(frame, new Vector2(0.5f, 1f), new Vector2(0, -6), 3f, 180f);
        }

        void NotebookArt(string note)
        {
            Fill(new Color(0.93f, 0.85f, 0.70f));
            Box(artLayer, new Color(0.45f, 0.30f, 0.22f), 12, -12, 800, 820, 24);
            var page = Box(artLayer, UIKit.Card, 0, 0, 770, 790, 18);
            var lines = UIKit.NoteLines(page, 66f, 120f); UIKit.Stretch(lines.rectTransform, 0, 0, 0, 120);
            Box(page, UIKit.Margin, -300, 0, 5, 790);
            Write(page, note ?? "", 44, new Color(0.22f, 0.22f, 0.42f), 40, 20, 620, 700, TextAnchor.UpperLeft);
            // 연필
            var pencil = Box(artLayer, UIKit.Yellow, 300, -400, 300, 30, 6, 30f);
            Box(pencil, new Color(0.95f, 0.75f, 0.55f), -160, 0, 40, 30);
        }

        void LetterArt(string note)
        {
            Fill(new Color(0.20f, 0.18f, 0.28f));                                         // 밤
            Circle(artLayer, new Color(1f, 0.92f, 0.6f, 0.25f), 0, 0, 900);              // 스탠드 불빛
            Box(artLayer, UIKit.Kraft, 0, -380, 720, 300, 12);                            // 봉투
            var letter = Box(artLayer, new Color(1f, 0.98f, 0.92f), 0, 40, 780, 880, 10, 1.5f);
            var lines = UIKit.NoteLines(letter, 60f, 60f); UIKit.Stretch(lines.rectTransform, 0, 0, 0, 60);
            Write(letter, note ?? "", 36, new Color(0.25f, 0.22f, 0.35f), 0, 0, 680, 800, TextAnchor.UpperLeft);
        }

        void FarewellArt()
        {
            Fill(new Color(1f, 0.82f, 0.62f));                                            // 노을
            Circle(artLayer, new Color(1f, 0.6f, 0.35f), 260, 280, 260);
            Box(artLayer, Ground, 0, -420, 1200, 300);
            // 닫힌 옛 가게 (작게)
            Box(artLayer, Wall, -260, 40, 380, 380, 10);
            Box(artLayer, UIKit.Red, -260, 200, 340, 80, 8);
            Write(artLayer, "문방구", 52, Color.white, -260, 200, 330, 80);
            var sh = Box(artLayer, ShutterBlue, -260, -40, 280, 220, 4);
            var st = UIKit.Stripes(sh, UIKit.WithAlpha(UIKit.Darker(ShutterBlue, 0.7f), 0.5f), 20f); UIKit.Stretch(st.rectTransform);
            // 이삿짐 트럭
            Box(artLayer, UIKit.Blue, 230, -120, 300, 200, 14);
            Box(artLayer, UIKit.Yellow, 420, -150, 110, 140, 14);
            Circle(artLayer, UIKit.Ink, 160, -230, 70); Circle(artLayer, UIKit.Ink, 400, -230, 70);
            Box(artLayer, UIKit.Kraft, 200, 10, 90, 70, 6); Box(artLayer, UIKit.Kraft, 280, 20, 70, 90, 6);
            // 손 흔드는 아이들
            Color[] shirts = { UIKit.Blue, new Color(0.95f, 0.5f, 0.7f), UIKit.Green, UIKit.Yellow };
            for (int i = 0; i < 4; i++)
                bobbers.Add(Chibi(artLayer, -420 + i * 160, -470, 0.7f, DarkHair, shirts[i], wave: true));
        }

        void StreetArt()
        {
            Fill(Sky);
            Box(artLayer, new Color(0.45f, 0.45f, 0.5f), 0, -430, 1200, 240);
            for (int i = 0; i < 5; i++) Box(artLayer, Color.white, -400 + i * 200, -430, 110, 14);
            Box(artLayer, new Color(0.85f, 0.82f, 0.78f), 0, -290, 1200, 50);
            Box(artLayer, Wall, 0, 0, 920, 560, 12);
            Box(artLayer, UIKit.Yellow, 0, 300, 900, 200, 22);
            Box(artLayer, UIKit.Red, 0, 300, 860, 160, 18);
            Write(artLayer, "문방구", 120, Color.white, 0, 296, 840, 160);
            for (int i = 0; i < 10; i++) Circle(artLayer, UIKit.Yellow, -410 + i * 91, 405, 22);   // 전구
            for (int i = 0; i < 3; i++)
            {
                var win = Box(artLayer, new Color(0.55f, 0.42f, 0.32f), -280 + i * 280, -40, 240, 300, 8);
                Color[] goods = { UIKit.Red, UIKit.Blue, UIKit.Green, UIKit.Yellow };
                for (int g = 0; g < 6; g++) Box(win, goods[(g + i) % goods.Length], -70 + (g % 3) * 70, 70 - (g / 3) * 110, 52, 60, 8);
            }
            Color[] shirts = { UIKit.Blue, new Color(0.95f, 0.5f, 0.7f), UIKit.Green };
            for (int i = 0; i < 3; i++) bobbers.Add(Chibi(artLayer, -300 + i * 300, -440, 0.85f, DarkHair, shirts[i]));
        }

        void GrownupsArt()
        {
            Fill(Wall);
            for (int x = 0; x < 12; x++)
                for (int y = 0; y < 3; y++)
                    Box(artLayer, (x + y) % 2 == 0 ? new Color(0.98f, 0.82f, 0.82f) : new Color(1f, 0.92f, 0.90f), -550 + x * 100, -320 - y * 100, 100, 100);
            Box(artLayer, UIKit.Kraft, 0, 230, 900, 26);
            Color[] goods = { UIKit.Red, UIKit.Yellow, UIKit.Blue, UIKit.Green };
            for (int i = 0; i < 12; i++) Box(artLayer, goods[i % 4], -390 + i * 71, 275, 50, 60, 8);
            HeroChibi(artLayer, -330, -380, 0.95f, grown: true);
            Chibi(artLayer, -90, -380, 0.95f, DarkHair, UIKit.Blue, adult: true);                       // 철수: 체육복
            Box(artLayer, new Color(0.45f, 0.25f, 0.2f), 130, -95, 170, 190, 40);                           // 영희 긴 머리 (뒤)
            Chibi(artLayer, 130, -380, 0.95f, new Color(0.45f, 0.25f, 0.2f), new Color(0.95f, 0.5f, 0.7f), adult: true); // 영희
            Chibi(artLayer, 350, -380, 0.95f, DarkHair, UIKit.Green, adult: true);                       // 민수: 양복
            Box(artLayer, UIKit.Ink, -90, -205, 4, 60);                                                    // 철수 호루라기 끈
            Circle(artLayer, UIKit.Gold, -90, -238, 24);
            Box(artLayer, new Color(0.7f, 0.15f, 0.2f), 350, -200, 22, 80, 4);                              // 민수 넥타이
        }

        void BuildCredits()
        {
            var t = UIKit.Label("Credits", transform, StoryTable.CreditsText, 44, Color.white, TextAnchor.MiddleCenter);
            credits = t.rectTransform;
            UIKit.Place(credits, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -1500), new Vector2(900, 1700));
            t.lineSpacing = 1.3f;
            credits.SetAsLastSibling();
            beatTime = 0f;
        }
    }
}
