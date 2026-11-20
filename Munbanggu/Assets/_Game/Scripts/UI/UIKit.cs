using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace Munbanggu.UI
{
    /// <summary>
    /// 코드로 uGUI를 만들기 위한 작은 도우미 + 색상표 + 런타임 생성 스프라이트.
    /// 컨셉: 90년대 문방구 — 크래프트지, 공책 줄, 빨강·파랑·노랑 원색, 스티커.
    /// Panel / Button은 자동으로 둥근 모서리(9-slice)와 스티커 그림자를 입는다.
    /// </summary>
    public static class UIKit
    {
        // ───────────── 색상표 ─────────────
        // 기존 이름 (다른 코드가 사용 중 — 이름 유지)
        public static readonly Color Paper = new Color(1f, 0.965f, 0.88f, 0.98f);     // 공책 종이
        public static readonly Color Card = new Color(1f, 0.99f, 0.96f, 1f);          // 흰 카드
        public static readonly Color Ink = new Color(0.22f, 0.17f, 0.16f);            // 진한 연필
        public static readonly Color Muted = new Color(0.50f, 0.43f, 0.38f);          // 흐린 연필
        public static readonly Color Accent = new Color(0.89f, 0.26f, 0.22f);         // 빨강 (메인 포인트)
        public static readonly Color Green = new Color(0.20f, 0.62f, 0.36f);
        public static readonly Color Blue = new Color(0.18f, 0.42f, 0.80f);
        public static readonly Color Dim = new Color(0.10f, 0.07f, 0.05f, 0.62f);
        public static readonly Color Slot = new Color(0.97f, 0.92f, 0.80f, 1f);

        public static readonly Color BubbleNormal = new Color(1f, 1f, 1f, 0.97f);
        public static readonly Color BubbleReady = new Color(1f, 0.88f, 0.32f, 1f);
        public static readonly Color BubbleUrgent = new Color(1f, 0.58f, 0.48f, 1f);
        public static readonly Color BubbleSad = new Color(0.80f, 0.84f, 0.93f, 0.97f);
        public static readonly Color BubbleMoney = new Color(0.60f, 0.90f, 0.58f, 1f);

        // 추가 색
        public static readonly Color Red = Accent;
        public static readonly Color Yellow = new Color(1f, 0.80f, 0.16f);
        public static readonly Color Kraft = new Color(0.80f, 0.64f, 0.44f);           // 크래프트지
        public static readonly Color KraftDark = new Color(0.58f, 0.43f, 0.28f);
        public static readonly Color Cream = new Color(1f, 0.97f, 0.89f, 1f);
        public static readonly Color NoteLine = new Color(0.45f, 0.65f, 0.92f, 0.38f); // 공책 가로줄
        public static readonly Color Margin = new Color(0.93f, 0.40f, 0.40f, 0.55f);   // 공책 빨간 세로줄
        public static readonly Color Tape = new Color(1f, 0.95f, 0.70f, 0.72f);        // 마스킹 테이프
        public static readonly Color ShadowColor = new Color(0.18f, 0.11f, 0.06f, 0.28f);
        public static readonly Color Gold = new Color(1f, 0.78f, 0.15f);
        public static readonly Color GoldDark = new Color(0.82f, 0.52f, 0.08f);

        // ───────────── 폰트 ─────────────

        const string FontResource = "Fonts/Jua-Regular";
        static Font font;

        /// <summary>내장 폰트(Resources/Fonts/Jua, OFL)를 쓰는 중인지.</summary>
        public static bool EmbeddedFont { get; private set; }

        /// <summary>
        /// 한글 폰트. 내장 Jua(OFL) → 없으면 OS 한글 폰트 → 없으면 Unity 기본 폰트.
        /// Jua에 없는 기호(▶ × · … 등)는 OS 폰트로 대체되므로 UI 문구엔 되도록 쓰지 않는다.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (font != null) return font;
                font = Resources.Load<Font>(FontResource);
                if (font != null)
                {
                    EmbeddedFont = true;
                    try { font.fontNames = new[] { "Jua", "Malgun Gothic", "Noto Sans CJK KR", "Apple SD Gothic Neo", "Roboto", "Arial" }; }
                    catch { /* 일부 플랫폼에서 대체 글꼴 지정이 막혀 있어도 무시 */ }
                    return font;
                }
                EmbeddedFont = false;
                string[] wanted = { "Malgun Gothic", "맑은 고딕", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Noto Sans KR", "NanumGothic" };
                var installed = Font.GetOSInstalledFontNames();
                foreach (var name in wanted)
                    if (System.Array.IndexOf(installed, name) >= 0)
                        return font = Font.CreateDynamicFontFromOSFont(name, 40);
                return font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
        }

        /// <summary>제목용 폰트 (지금은 본문과 같은 Jua).</summary>
        public static Font TitleFont => Font;

        public static string Won(int value) => value.ToString("N0", CultureInfo.InvariantCulture) + "원";

        // ───────────── 기본 배치 ─────────────

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>위쪽에 붙는 고정 높이 띠.</summary>
        public static void Top(RectTransform rt, float height, float y = 0, float side = 0)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.offsetMin = new Vector2(side, -y - height);
            rt.offsetMax = new Vector2(-side, -y);
        }

        /// <summary>아래쪽에 붙는 고정 높이 띠.</summary>
        public static void Bottom(RectTransform rt, float height, float y = 0, float side = 0)
        {
            rt.anchorMin = new Vector2(0, 0);
            rt.anchorMax = new Vector2(1, 0);
            rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(side, y);
            rt.offsetMax = new Vector2(-side, y + height);
        }

        /// <summary>한 점(앵커)에 고정 크기로 놓기.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        // ───────────── 위젯 ─────────────

        /// <summary>둥근 모서리 패널 (기본 반지름 24). 네모가 필요하면 <see cref="Square"/>.</summary>
        public static Image Panel(string name, Transform parent, Color color, bool raycast = true)
        {
            var rt = Rect(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = raycast;
            Round(img, 24f);
            return img;
        }

        /// <summary>둥근 모서리 + 스티커 그림자 + (선택) 테두리가 있는 카드.</summary>
        public static Image CardPanel(string name, Transform parent, Color color, float radius = 28f, bool raycast = true,
            float shadow = 10f, Color? border = null)
        {
            var img = Panel(name, parent, color, raycast);
            Round(img, radius);
            if (shadow > 0f) AddShadow(img, ShadowColor, shadow);
            if (border.HasValue) Ring(img.transform, border.Value, radius);
            return img;
        }

        public static Text Label(string name, Transform parent, string text, int size, Color color,
            TextAnchor align = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            var rt = Rect(name, parent);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            // Jua는 원래 굵은 글꼴이라 가짜 볼드를 끈다 (번져 보임)
            t.fontStyle = EmbeddedFont && style == FontStyle.Bold ? FontStyle.Normal : style;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.supportRichText = true;
            return t;
        }

        /// <summary>
        /// 통통한 스티커 버튼: 둥근 모서리, 아래쪽 진한 두께(그림자), 누르면 쏙 들어가는 스케일.
        /// 클릭 시 GameEvents.RaiseUiClick (사운드 담당이 사용).
        /// </summary>
        public static Button Button(string name, Transform parent, string text, int size, Color bg, Color fg, out Text label)
        {
            var img = Panel(name, parent, bg);
            Round(img, 28f);
            AddShadow(img, Darker(bg, 0.62f), 8f);
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = Color.white;
            cb.selectedColor = Color.white;
            cb.pressedColor = new Color(0.88f, 0.88f, 0.88f, 1f);
            cb.disabledColor = new Color(0.72f, 0.70f, 0.68f, 0.55f);
            cb.fadeDuration = 0.06f;
            btn.colors = cb;
            btn.onClick.AddListener(Munbanggu.Core.GameEvents.RaiseUiClick);
            img.gameObject.AddComponent<UIJuice>().pressable = true;
            label = Label("Label", img.transform, text, size, fg, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(label.rectTransform, 8, 6, 8, 2);
            return btn;
        }

        public static LayoutElement Layout(Component c, float preferredWidth = -1, float preferredHeight = -1)
        {
            var le = c.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = preferredWidth;
            le.preferredHeight = preferredHeight;
            return le;
        }

        // ───────────── 꾸미기 ─────────────

        /// <summary>둥근 모서리 적용 (radius: 캔버스 단위).</summary>
        public static Image Round(Image img, float radius)
        {
            img.sprite = RoundedSprite;
            img.type = Image.Type.Sliced;
            img.fillCenter = true;
            img.pixelsPerUnitMultiplier = SpriteRadius / Mathf.Max(1f, radius);
            return img;
        }

        /// <summary>모서리 없는 네모로 되돌림.</summary>
        public static Image Square(Image img)
        {
            img.sprite = null;
            img.type = Image.Type.Simple;
            return img;
        }

        /// <summary>둥근 테두리(링). 부모 크기에 맞춰 늘어남. 두께 ≈ radius/4.</summary>
        public static Image Ring(Transform parent, Color color, float radius = 24f, float pad = 0f)
        {
            var rt = Rect("Ring", parent);
            Stretch(rt, -pad, -pad, -pad, -pad);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = RingSprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = SpriteRadius / Mathf.Max(1f, radius + pad);
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>단단한 스티커 그림자 (메시 복사 방식이라 레이아웃·슬라이스와 잘 맞음).</summary>
        public static Shadow AddShadow(Graphic g, Color color, float down = 8f, float right = 0f)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = color;
            s.effectDistance = new Vector2(right, -down);
            s.useGraphicAlpha = true;
            return s;
        }

        /// <summary>글자 테두리.</summary>
        public static Outline AddOutline(Graphic g, Color color, float size = 3f)
        {
            var o = g.gameObject.AddComponent<Outline>();
            o.effectColor = color;
            o.effectDistance = new Vector2(size, -size);
            o.useGraphicAlpha = true;
            return o;
        }

        /// <summary>마스킹 테이프 조각 (카드 모서리 장식).</summary>
        public static Image TapeStrip(Transform parent, Vector2 anchor, Vector2 pos, float angle, float width = 130f)
        {
            var rt = Rect("Tape", parent);
            Place(rt, anchor, new Vector2(0.5f, 0.5f), pos, new Vector2(width, 40f));
            rt.localRotation = Quaternion.Euler(0, 0, angle);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = Tape;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>점선 (바느질·절취선 느낌). 가로 띠로 깔림.</summary>
        public static Image DashLine(Transform parent, Color color, float thickness = 4f)
        {
            var rt = Rect("Dash", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = DashSprite;
            img.type = Image.Type.Tiled;
            img.pixelsPerUnitMultiplier = DashTexHeight / Mathf.Max(1f, thickness);
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>공책 가로줄 배경 (부모에 꽉 차게).</summary>
        public static Image NoteLines(Transform parent, float spacing = 52f, float topSkip = 0f)
        {
            var rt = Rect("Lines", parent);
            Stretch(rt, 0, 0, 0, topSkip);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = LinesSprite;
            img.type = Image.Type.Tiled;
            img.pixelsPerUnitMultiplier = LinesTexHeight / Mathf.Max(8f, spacing);
            img.color = NoteLine;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>사선 줄무늬 (러시 구간 표시 등).</summary>
        public static Image Stripes(Transform parent, Color color, float tile = 28f)
        {
            var rt = Rect("Stripes", parent);
            Stretch(rt);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = StripeSprite;
            img.type = Image.Type.Tiled;
            img.pixelsPerUnitMultiplier = StripeTexSize / Mathf.Max(4f, tile);
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>동전 그림 (노란 원 + 진한 테두리 + 반짝이).</summary>
        public static Image Coin(Transform parent, float size)
        {
            var img = Panel("Coin", parent, Gold, false);
            Round(img, size * 0.5f);
            img.rectTransform.sizeDelta = new Vector2(size, size);
            Ring(img.transform, GoldDark, size * 0.5f);
            var shine = Panel("Shine", img.transform, new Color(1f, 1f, 1f, 0.65f), false);
            Round(shine, size * 0.12f);
            Place(shine.rectTransform, new Vector2(0.32f, 0.68f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.22f, size * 0.22f));
            return img;
        }

        public static Color Darker(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1f);
        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

        // ───────────── 런타임 생성 스프라이트 ─────────────

        const int SpriteRadius = 64;      // 둥근 스프라이트의 모서리 반지름(px) = 9-slice 테두리
        const float RingThickness = 16f;  // 링 두께(px, 반지름 64 기준)
        const int DashTexHeight = 6;
        const int LinesTexHeight = 64;
        const int StripeTexSize = 32;

        static Sprite rounded, ring, triangle, arrow, dash, lines, stripes;

        /// <summary>흰색 둥근 사각형 (9-slice). Image.color로 색을 입힌다.</summary>
        public static Sprite RoundedSprite => rounded != null ? rounded : rounded = MakeRounded(false);

        /// <summary>흰색 둥근 테두리 (9-slice).</summary>
        public static Sprite RingSprite => ring != null ? ring : ring = MakeRounded(true);

        /// <summary>아래를 가리키는 삼각형 (말풍선 꼬리).</summary>
        public static Sprite TriangleSprite => triangle != null ? triangle : triangle = MakeShape(64, 48, TriangleCoverage, "UI Triangle");

        /// <summary>아래를 가리키는 화살표 (튜토리얼).</summary>
        public static Sprite ArrowSprite => arrow != null ? arrow : arrow = MakeShape(96, 120, ArrowCoverage, "UI Arrow");

        public static Sprite DashSprite => dash != null ? dash : dash = MakeDash();
        public static Sprite LinesSprite => lines != null ? lines : lines = MakeLines();
        public static Sprite StripeSprite => stripes != null ? stripes : stripes = MakeStripes();

        static Texture2D NewTexture(int w, int h, string name, TextureWrapMode wrap)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = wrap,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };
            return tex;
        }

        static Sprite Finish(Texture2D tex, Color32[] px, Vector4 border)
        {
            tex.SetPixels32(px);
            tex.Apply(false, true);
            var s = Sprite.Create(tex, new UnityEngine.Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border);
            s.name = tex.name;
            s.hideFlags = HideFlags.DontSave;
            return s;
        }

        static Sprite MakeRounded(bool ringOnly)
        {
            const int r = SpriteRadius;
            int size = r * 2 + 4;
            var tex = NewTexture(size, size, ringOnly ? "UI Ring" : "UI Rounded", TextureWrapMode.Clamp);
            var px = new Color32[size * size];
            float half = size * 0.5f, innerHalf = half - r;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - half) - innerHalf, 0f);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - half) - innerHalf, 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(r - d + 0.5f);
                if (ringOnly) a *= Mathf.Clamp01(d - (r - RingThickness) + 0.5f);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            return Finish(tex, px, new Vector4(r, r, r, r));
        }

        delegate float Coverage(float x, float y, float w, float h);

        static Sprite MakeShape(int w, int h, Coverage cov, string name)
        {
            var tex = NewTexture(w, h, name, TextureWrapMode.Clamp);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // 2x2 슈퍼샘플링으로 가장자리 부드럽게
                float a = 0f;
                for (int sy = 0; sy < 2; sy++)
                for (int sx = 0; sx < 2; sx++)
                    a += cov(x + 0.25f + sx * 0.5f, y + 0.25f + sy * 0.5f, w, h);
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 0.25f * 255f));
            }
            return Finish(tex, px, Vector4.zero);
        }

        // 꼭짓점이 아래(y=0) 가운데인 삼각형
        static float TriangleCoverage(float x, float y, float w, float h)
        {
            float halfW = y / h * w * 0.5f;
            return Mathf.Abs(x - w * 0.5f) <= halfW ? 1f : 0f;
        }

        // 아래를 가리키는 화살표: 위쪽 막대 + 아래쪽 삼각 머리
        static float ArrowCoverage(float x, float y, float w, float h)
        {
            float cx = Mathf.Abs(x - w * 0.5f);
            float headH = h * 0.52f;
            if (y <= headH) return cx <= y / headH * w * 0.5f ? 1f : 0f;
            return cx <= w * 0.19f ? 1f : 0f;
        }

        static Sprite MakeDash()
        {
            const int w = 24, h = DashTexHeight;
            var tex = NewTexture(w, h, "UI Dash", TextureWrapMode.Repeat);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = new Color32(255, 255, 255, (byte)(x >= 2 && x < 15 ? 255 : 0));
            return Finish(tex, px, Vector4.zero);
        }

        static Sprite MakeLines()
        {
            const int w = 8, h = LinesTexHeight;
            var tex = NewTexture(w, h, "UI Lines", TextureWrapMode.Repeat);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                byte a = (byte)(y < 3 ? 255 : y == 3 ? 90 : 0);
                for (int x = 0; x < w; x++) px[y * w + x] = new Color32(255, 255, 255, a);
            }
            return Finish(tex, px, Vector4.zero);
        }

        static Sprite MakeStripes()
        {
            const int s = StripeTexSize;
            var tex = NewTexture(s, s, "UI Stripes", TextureWrapMode.Repeat);
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float t = ((x + y) % s) / (float)s;     // 0..1 사선 위치
                float a = Mathf.Clamp01(Mathf.Min(t, 0.5f - t) * s + 0.5f); // 절반은 칠하고 경계는 부드럽게
                if (t > 0.5f) a = 0f;
                px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            return Finish(tex, px, Vector4.zero);
        }
    }
}
