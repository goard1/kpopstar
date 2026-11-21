using System.Collections;
using System.Collections.Generic;
using System.IO;
using Munbanggu.Customers;
using Munbanggu.Data;
using Munbanggu.UI;
using UnityEngine;

namespace Munbanggu.Core
{
    /// <summary>
    /// "또 뭐가 나올까" (docs/09): 숨은 레시피 작업대, 깜짝 손님, 할머니 추억 앨범, 고양이 치즈 + 선물 룰렛.
    /// </summary>
    public partial class GameManager
    {
        public DiscoverySave Disc => Town.discovery;

        // ───────────── 특제 상품 (레시피) ─────────────

        /// <summary>레시피마다 특제 상품을 카탈로그(복제본)에 더한다. 발견 전엔 잠김.</summary>
        void AddSpecialProducts()
        {
            foreach (var r in RecipeTable.All)
            {
                if (Catalog.Find(r.ProductId) != null) continue;
                var a = Catalog.Find(r.a);
                var b = Catalog.Find(r.b);
                if (a == null || b == null) continue;
                var p = ScriptableObject.CreateInstance<ProductData>();
                p.name = r.ProductId;
                p.id = r.ProductId;
                p.displayName = r.name;
                p.category = a.category;
                p.wholesalePrice = a.wholesalePrice + b.wholesalePrice;
                p.retailPrice = Mathf.RoundToInt((a.retailPrice + b.retailPrice) * 1.5f / 10f) * 10;
                p.boxSize = 5;
                p.popularity = Mathf.Max(a.popularity, b.popularity) * 1.2f;
                p.unlockDay = 999;
                p.blurb = r.blurb;
                p.color = r.color;
                p.special = true;
                p.visualOf = a.id;
                Catalog.products.Add(p);
            }
        }

        bool SpecialUnlocked(ProductData p) => p.special && Disc.recipes.Contains(p.id.Substring(3));

        bool EarlyUnlocked(ProductData p)
        {
            foreach (var e in Disc.early) if (e.id == p.id && Day >= e.count) return true;
            return false;
        }

        /// <summary>작업대: 두 재료를 합친다. 레시피가 맞으면 특제 상품 5개 (재료 3개씩 씀). 틀리면 재료는 그대로.</summary>
        public RecipeDef Craft(ProductData a, ProductData b, out string message)
        {
            message = null;
            if (a == null || b == null) { message = "재료를 두 개 골라요"; return null; }
            if (a == b) { message = "서로 다른 물건 두 개를 골라요"; return null; }
            if (Phase != DayPhase.Morning) { message = "작업대는 아침에만 써요"; return null; }
            if (Stock(a) < RecipeTable.Need || Stock(b) < RecipeTable.Need) { message = $"창고에 재료가 {RecipeTable.Need}개씩 있어야 해요"; return null; }
            var r = RecipeTable.Find(a.id, b.id);
            if (r == null)
            {
                message = "뚝딱뚝딱… 아무것도 안 나왔어요. (재료는 그대로예요) 다른 조합을 해 봐요!";
                return null;
            }
            var sp = Catalog.Find(r.ProductId);
            RemoveStock(a, RecipeTable.Need);
            RemoveStock(b, RecipeTable.Need);
            AddStock(sp, RecipeTable.Yield);
            bool first = !Disc.recipes.Contains(r.id);
            if (first)
            {
                Disc.recipes.Add(r.id);
                UnlockAlbum("first_recipe");
                Dialogue.Show("할머니표 특제!", $"「{r.name}」이(가) 나왔어요! {r.blurb}\n이제 도매상에서도 살 수 있어요.", UIKit.Accent, "★");
            }
            message = $"「{r.name}」 {RecipeTable.Yield}개 완성! 창고에 넣었어요";
            GameEvents.RaiseRushStarted();
            Save();
            Notify();
            return r;
        }

        /// <summary>레시피 힌트 하나 열기 (고양이 룰렛 등). 다 열렸으면 false.</summary>
        bool RevealRecipeHint()
        {
            foreach (var r in RecipeTable.All)
            {
                if (Disc.recipes.Contains(r.id) || Disc.hints.Contains(r.id)) continue;
                Disc.hints.Add(r.id);
                Dialogue.Show("레시피 힌트", r.hint, UIKit.Accent, "?");
                return true;
            }
            return false;
        }

        // ───────────── 깜짝 손님 ─────────────

        SpecialVisitorDef visitorToday;
        float visitorAt;
        bool visitorSpawned, reporterBoost;
        float catBoostToday = 1f;

        /// <summary>깜짝 손님·고양이 선물이 손님 수에 주는 배율.</summary>
        float DiscoverySpawnMultiplier => (reporterBoost ? 1.3f : 1f) * catBoostToday;

        void PlanVisitor()
        {
            visitorToday = null;
            visitorSpawned = false;
            reporterBoost = false;
            // 하루 40% 정도 (날짜로 정해짐)
            uint h = (uint)(Day * 2246822519u) ^ 0x7F4A7C15u;
            h ^= h >> 13; h *= 3266489917u; h ^= h >> 16;
            if (Day < 3 || (h % 100) >= 40) return;
            var pool = new List<SpecialVisitorDef>();
            foreach (var v in SpecialVisitors.All)
            {
                if (Day < v.minDay || Reputation < v.minReputation) continue;
                if (v.rainOnly && Today.weather != Weather.Rain) continue;
                pool.Add(v);
                if (v.rainOnly) { pool.Add(v); pool.Add(v); } // 비 오는 날엔 도깨비가 잘 나옴
            }
            if (pool.Count == 0) return;
            visitorToday = pool[(int)((h / 100) % (uint)pool.Count)];
            visitorAt = 0.3f + ((h >> 8) % 40) / 100f;
        }

        void TickVisitor()
        {
            if (visitorToday == null || visitorSpawned || OpenProgress < visitorAt) return;
            visitorSpawned = true;
            var v = visitorToday;
            var wanted = RandomStockedProduct();
            if (wanted == null) return;
            var c = SpawnCustomer(null, CustomerKind.Normal, wanted, $"{v.ask}\n{wanted.displayName} 주세요!", v.DexKey);
            if (c == null) return;
            c.SpecialId = v.id;
            c.transform.localScale = Vector3.one * (v.id == "dokkaebi" ? 0.9f : v.id == "banjang" || v.id == "neighbor" || v.id == "mayor" || v.id == "reporter" ? 1.3f : 1.05f);
            Dialogue.Show($"깜짝 손님 · {v.name}", v.intro, new Color(0.95f, 0.5f, 0.7f), "!");
            Hud.Toast($"깜짝 손님 「{v.name}」 등장!");
            if (!Disc.visitorsMet.Contains(v.id)) Disc.visitorsMet.Add(v.id);
            UnlockAlbum("special_visitor");
            switch (v.id)
            {
                case "reporter":
                    reporterBoost = true;
                    AddReputation(5);
                    break;
                case "transfer":
                {
                    // 아직 안 들어온 물건 하나를 내일부터 미리 들여옴
                    ProductData next = null;
                    foreach (var p in Catalog.products)
                        if (!p.special && !IsUnlocked(p) && (next == null || p.unlockDay < next.unlockDay)) next = p;
                    if (next != null && !Disc.early.Exists(e => e.id == next.id))
                    {
                        Disc.early.Add(new SoldCount { id = next.id, count = Day + 1 });
                        Dialogue.Show("전학생", $"\"서울에선 「{next.displayName}」 엄청 유행이에요!\"\n도매상에 부탁해 뒀더니 내일부터 들어온대요.", UIKit.Blue, "전");
                    }
                    break;
                }
                case "neighbor":
                    UnlockAlbum("special_visitor");
                    break;
            }
            GameEvents.RaiseRushStarted();
        }

        ProductData RandomStockedProduct()
        {
            var list = new List<ProductData>();
            foreach (var s in Layout.Slots)
                if (s != null && !s.IsBoard && s.Product != null && s.Count > 0) list.Add(s.Product);
            return list.Count > 0 ? list[Random.Range(0, list.Count)] : null;
        }

        /// <summary>깜짝 손님이 계산을 마쳤을 때 (Checkout에서).</summary>
        void OnSpecialServed(Customer c, ProductData bought)
        {
            var v = SpecialVisitors.Find(c.SpecialId);
            if (v == null) return;
            switch (v.id)
            {
                case "banjang":
                {
                    var p = bought;
                    int n = 10;
                    int pay = p != null ? p.retailPrice * n * 2 : 0;
                    if (p != null && Stock(p) >= n)
                        Hud.ShowChoice("반장 아저씨의 대량 주문", $"\"반상회 선물로 「{p.displayName}」 {n}개 더 주세요! 두 배 값 쳐 드릴게요.\"\n창고에 {Stock(p)}개 있어요.",
                            $"팔기 (+{UIKit.Won(pay)})", "죄송해요",
                            yes =>
                            {
                                if (!yes || Stock(p) < n) { Hud.Toast("반장 아저씨가 아쉬워하며 갔어요"); return; }
                                RemoveStock(p, n);
                                Money += pay; Stats.revenue += pay;
                                AddReputation(2);
                                Hud.Toast($"대량 주문 성공! +{UIKit.Won(pay)}");
                                Notify();
                            });
                    else Hud.Toast($"반장 아저씨: \"다음엔 「{p?.displayName}」 넉넉히 갖다 놔요~\"");
                    break;
                }
                case "dokkaebi":
                {
                    int gold = 3000;
                    Money += gold; Stats.revenue += gold;
                    AddReputation(2);
                    Dialogue.Show("도깨비 꼬마", $"\"뚝딱! 금 나와라~\" 도깨비 금화로 계산했어요! (+{UIKit.Won(gold)})", new Color(0.95f, 0.5f, 0.7f), "!");
                    break;
                }
                case "mayor":
                {
                    int prize = 5000;
                    Money += prize; Stats.revenue += prize;
                    AddReputation(5);
                    Dialogue.Show("구청장님", $"\"모범 가게 상을 드립니다!\" 상금 {UIKit.Won(prize)}과 평판 +5", new Color(0.95f, 0.5f, 0.7f), "상");
                    break;
                }
                case "neighbor":
                    Dialogue.Show("옆집 할아버지", "\"이거 자네 할머니랑 할아버지 젊을 적 사진이야. 두 분이 이 골목에서 처음 만난 날이지…\" 앨범에 꽂아 두래요.", UIKit.Muted, "옛");
                    break;
            }
            Notify();
        }

        // ───────────── 추억 앨범 ─────────────

        readonly Dictionary<string, Texture2D> albumPhotos = new Dictionary<string, Texture2D>();

        public bool HasAlbum(string id) => Disc.album.Contains(id);

        public void UnlockAlbum(string id)
        {
            if (string.IsNullOrEmpty(id) || Disc.album.Contains(id) || Album.Find(id) == null) return;
            Disc.album.Add(id);
            var page = Album.Find(id);
            if (Hud != null) Hud.Toast($"추억 앨범에 「{page.title}」 사진이 꽂혔어요!");
            if (Application.isPlaying) StartCoroutine(CapturePhoto(id));
            // 마지막 장
            int count = 0;
            foreach (var p in Album.Pages) if (p.id != "ending" && Disc.album.Contains(p.id)) count++;
            if (count >= Album.EndingNeeds && !Disc.album.Contains("ending"))
            {
                Disc.album.Add("ending");
                var end = Album.Find("ending");
                Dialogue.Show("추억 앨범 · 마지막 장", end.line, UIKit.Accent, "♥");
                if (Application.isPlaying) StartCoroutine(CapturePhoto("ending"));
            }
        }

        static string PhotoPath(string id) => Path.Combine(SaveSystem.AlbumDir, id + ".jpg");

        /// <summary>그 순간 화면을 작게 찍어 앨범 사진으로 저장.</summary>
        IEnumerator CapturePhoto(string id)
        {
            yield return new WaitForSecondsRealtime(0.35f);
            yield return new WaitForEndOfFrame();
            Texture2D full = null;
            RenderTexture rt = null;
            try
            {
                full = ScreenCapture.CaptureScreenshotAsTexture();
                int w = 360, h = Mathf.Max(1, Mathf.RoundToInt(360f * full.height / Mathf.Max(1, full.width)));
                rt = RenderTexture.GetTemporary(w, h, 0);
                Graphics.Blit(full, rt);
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var small = new Texture2D(w, h, TextureFormat.RGB24, false);
                small.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                small.Apply();
                RenderTexture.active = prev;
                Directory.CreateDirectory(Path.GetDirectoryName(PhotoPath(id)));
                File.WriteAllBytes(PhotoPath(id), small.EncodeToJPG(78));
                albumPhotos[id] = small;
            }
            finally
            {
                if (full != null) Destroy(full);
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
            }
        }

        /// <summary>앨범 사진 (없으면 null).</summary>
        public Texture2D AlbumPhoto(string id)
        {
            if (albumPhotos.TryGetValue(id, out var t) && t != null) return t;
            string path = PhotoPath(id);
            if (!File.Exists(path)) return null;
            var tex = new Texture2D(2, 2);
            if (!tex.LoadImage(File.ReadAllBytes(path))) return null;
            albumPhotos[id] = tex;
            return tex;
        }

        // ───────────── 고양이 치즈 ─────────────

        GameObject catGo;

        void CatMorning()
        {
            catBoostToday = 1f;
            var cat = Disc.cat;
            if (!cat.met && Day >= CatRules.FirstDay)
            {
                cat.met = true;
                Dialogue.Show("문 앞에…", "가게 문 앞에 노란 고양이가 앉아 있어요. 배가 고픈가 봐요.\n(동네 → 고양이 치즈에서 밥을 줘 보세요)", new Color(1f, 0.7f, 0.3f), "냥");
            }
            else if (CatGiftReady)
                Dialogue.Show("치즈", "치즈가 뭘 물어 왔어요! 냥~ (동네 → 고양이에서 선물 룰렛을 돌려요)", new Color(1f, 0.7f, 0.3f), "냥");
            RebuildCat();
        }

        /// <summary>어제 밥을 줬으면 오늘 아침 선물.</summary>
        public bool CatGiftReady
        {
            get
            {
                var c = Disc.cat;
                return c.adopted && c.lastFedDay > 0 && c.lastFedDay < Day && c.lastFedDay >= Day - 2 && c.lastGiftDay < Day;
            }
        }

        public bool FeedCat()
        {
            var c = Disc.cat;
            if (!c.met || !MorningOnly("밥 주기")) return false;
            if (c.lastFedDay == Day) { Fail("오늘은 벌써 밥을 줬어요"); return false; }
            if (Money < CatRules.FeedCost) { Fail("돈이 부족해요"); return false; }
            Money -= CatRules.FeedCost;
            c.lastFedDay = Day;
            c.affection++;
            if (!c.adopted && c.affection >= CatRules.AdoptAfter)
            {
                c.adopted = true;
                UnlockAlbum("cat");
                Dialogue.Show("치즈", "고양이가 가게 안으로 들어와 가운데 러그에 자리를 잡았어요! 이름은 「치즈」. 이제 우리 가게 식구예요.\n밥을 주면 다음 날 아침 선물을 물어 와요.", new Color(1f, 0.7f, 0.3f), "냥");
            }
            else Hud.Toast(c.adopted ? "치즈가 냠냠 먹어요. 내일 아침 선물을 기대해요!" : $"고양이가 냠냠… 조금 가까워졌어요 ({c.affection}/{CatRules.AdoptAfter})");
            RebuildCat();
            Save();
            Notify();
            return true;
        }

        /// <summary>선물 룰렛: 칸을 정하고 보상을 바로 준다 (화면은 그 칸에 멈추는 연출만). 결과 칸 번호, 못 돌리면 -1.</summary>
        public int SpinCatRoulette(out string result)
        {
            result = null;
            if (!CatGiftReady) return -1;
            var c = Disc.cat;
            c.lastGiftDay = Day;
            int idx = CatRules.Pick(new System.Random(Day * 131 + c.affection * 17 + 5), c.affection);
            switch (idx)
            {
                case 0: Money += 500; result = "동전 500원!"; break;
                case 1: Money += 1500; result = "동전 1,500원!"; break;
                case 2: Money += 5000; result = "황금 동전 5,000원! 대박!"; break;
                case 3:
                case 4:
                {
                    var pool = System.Array.FindAll(StaffRules.Items, i => i.rarity == (idx == 3 ? 0 : 1));
                    var item = pool[new System.Random(Day * 7 + idx).Next(pool.Length)];
                    Staff.ownedItems.Add(item.id);
                    result = $"직원 아이템 「{item.name}」! ({item.desc})";
                    break;
                }
                case 5: AddReputation(3); result = "평판 +3! 치즈 보러 아이들이 와요"; break;
                case 6: catBoostToday = 1.2f; result = "오늘 손님 +20%! 치즈가 가게 앞에서 손님을 불러요"; break;
                case 7: result = RevealRecipeHint() ? "레시피 힌트 쪽지! (동네 → 작업대에서 확인)" : "레시피 힌트… 이미 다 알아요! 대신 1,000원"; if (result.Contains("대신")) Money += 1000; break;
                default: result = "생선뼈… 꽝! 치즈는 뿌듯해해요"; break;
            }
            Save();
            Notify();
            return idx;
        }

        /// <summary>고양이 모습: 만나기 전엔 없음, 만났으면 문 앞 보도, 식구가 되면 가운데 러그 위.</summary>
        void RebuildCat()
        {
            if (catGo != null) { if (Application.isPlaying) Destroy(catGo); else DestroyImmediate(catGo); }
            catGo = null;
            var c = Disc.cat;
            if (!c.met) return;
            var pos = c.adopted ? new Vector3(1.55f, 0.004f, -1.25f) : new Vector3(0.25f, 0f, -4.7f); // 식구가 되면 가운데 러그 위
            catGo = Visual.CatVisual.Build(Layout.Root, Materials, pos, 225f);
            catGo.transform.localScale = Vector3.one * 1.35f;
        }

        void LoadDiscovery()
        {
            Town.discovery ??= new DiscoverySave();
            Disc.recipes ??= new List<string>();
            Disc.hints ??= new List<string>();
            Disc.album ??= new List<string>();
            Disc.visitorsMet ??= new List<string>();
            Disc.early ??= new List<SoldCount>();
            Disc.cat ??= new CatState();
            // 처음 두 가지 힌트는 그냥 알려 줌
            for (int i = 0; i < 2 && i < RecipeTable.All.Length; i++)
                if (!Disc.hints.Contains(RecipeTable.All[i].id)) Disc.hints.Add(RecipeTable.All[i].id);
        }

#if UNITY_EDITOR
        /// <summary>스크린샷 도구용: 고양이(선물 대기)·작업대 재료·앨범 몇 장.</summary>
        public void DebugDiscoveryForCapture()
        {
            Money = Mathf.Max(Money, 30000);
            var cat = Disc.cat;
            cat.met = cat.adopted = true;
            cat.affection = 4;
            cat.lastFedDay = Day - 1;
            cat.lastGiftDay = 0;
            foreach (var id in new[] { "ddakji", "sticker", "jjondeugi", "beercandy", "marble" })
            {
                var p = Catalog.Find(id);
                if (p != null && Stock(p) < 6) AddStock(p, 6);
            }
            foreach (var id in new[] { "first_day", "first_regular", "first_combo", "cat" })
                if (!Disc.album.Contains(id)) Disc.album.Add(id);
            RebuildCat();
            Notify();
        }
#endif
    }
}
