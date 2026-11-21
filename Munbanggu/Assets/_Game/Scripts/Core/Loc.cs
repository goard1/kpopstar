using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Munbanggu.Core
{
    public enum Lang { Ko = 0, ZhTW = 1, En = 2 }

    /// <summary>
    /// 언어 (docs/10). 게임 글은 한국어로 쓰고, 화면에 나갈 때(SafeText) 지금 언어로 바꾼다.
    /// 번역표: Resources/Loc/zh-TW.txt — 한 줄에 "한국어⇒中文" (#은 주석, "~조각⇒中文"은 이어 붙인 문장 조각).
    /// {0} {1}… 이 있으면 틀(템플릿), 단위(개·명·원…) 바로 앞 빈칸은 숫자만 받는다.
    /// 틀에 끼운 값(상품 이름 등)도 다시 번역한다. {0:yuan}은 그 숫자를 10으로 나눠 "元"으로.
    /// 대만판은 번역이 아니라 현지화: 상품·이름·이야기를 대만 아이들 추억(柑仔店)에 맞게 바꿔 적는다.
    /// </summary>
    public static class L
    {
        const string Pref = "lang";
        static bool loaded;
        static Lang current;

        public static Lang Current
        {
            get
            {
                if (!loaded) { loaded = true; current = (Lang)PlayerPrefs.GetInt(Pref, 0); Load(); }
                return current;
            }
        }

        public static bool Zh => Current == Lang.ZhTW;
        public static bool En => Current == Lang.En;
        /// <summary>한국어가 아니면 (번역표를 쓰는 언어).</summary>
        public static bool Translated => Current != Lang.Ko;

        public static string Name(Lang l) => l == Lang.ZhTW ? "繁體中文 (台灣)" : l == Lang.En ? "English" : "한국어";

        static string TableName(Lang l) => l == Lang.ZhTW ? "zh-TW" : l == Lang.En ? "en" : null;

        public static void Set(Lang l)
        {
            loaded = true;
            current = l;
            PlayerPrefs.SetInt(Pref, (int)l);
            PlayerPrefs.Save();
            Load();
        }

        // ───────────── 번역표 ─────────────

        static readonly Dictionary<string, string> exact = new Dictionary<string, string>();
        sealed class Template { public Regex re; public string zh; public int literal; }
        static readonly List<Template> templates = new List<Template>();
        static readonly List<KeyValuePair<string, string>> glossary = new List<KeyValuePair<string, string>>();
        static readonly Dictionary<string, string> cache = new Dictionary<string, string>();
        static readonly HashSet<string> missing = new HashSet<string>();
        static readonly Regex Hole = new Regex(@"\{(\d+)(:yuan)?\}");
        static readonly Regex HangulRe = new Regex("[가-힣]");
        static readonly Regex Deco = new Regex(@"^((?:<[^>/]+>|[·★●○▶•※""\s])*)(.*?)((?:</[^>]+>|[""\s])*)$", RegexOptions.Singleline);

        static void Load()
        {
            exact.Clear(); templates.Clear(); glossary.Clear(); cache.Clear(); missing.Clear();
            string table = TableName(current);
            if (table == null) return;
            var ta = Resources.Load<TextAsset>("Loc/" + table);
            if (ta == null) { Debug.LogWarning("[Loc] Resources/Loc/" + table + ".txt 없음"); return; }
            foreach (var raw in ta.text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0 || line.StartsWith("#")) continue;
                int tab = line.IndexOf('⇒'); // 구분자 (한국어 문장 속 = | 와 안 겹치게)
                if (tab <= 0) continue;
                // "~조각⇒中文": 코드에서 이어 붙이는 문장 조각. 앞뒤 빈칸까지 그대로 바꾼다.
                if (line[0] == '~')
                {
                    string fk = Unescape(line.Substring(1, tab - 1));
                    string fz = Unescape(line.Substring(tab + 1));
                    if (fk.Length > 0) glossary.Add(new KeyValuePair<string, string>(fk, fz));
                    continue;
                }
                string ko = Unescape(line.Substring(0, tab).TrimEnd(' '));
                string zh = Unescape(line.Substring(tab + 1).Trim());
                if (zh == "∅") zh = "";                  // 일부러 비우는 번역 (영어판 단위 "명·개" 등)
                else if (string.IsNullOrEmpty(zh)) continue;
                if (Hole.IsMatch(ko)) AddTemplate(ko, zh);
                else
                {
                    exact[ko] = zh;
                    // 낱말·이름·짧은 구절은 틀에 안 맞는 문장(이어 붙인 글)에서도 바꿔 쓸 수 있게
                    if (ko.Length >= 2 && ko.Length <= 20) glossary.Add(new KeyValuePair<string, string>(ko, zh));
                }
            }
            templates.Sort((a, b) => b.literal.CompareTo(a.literal)); // 글자가 많은(구체적인) 틀 먼저
            glossary.Sort((a, b) => b.Key.Length.CompareTo(a.Key.Length));
        }

        static string Unescape(string s) => s.Replace("\\n", "\n").Replace("\\t", "\t");

        static void AddTemplate(string ko, string zh)
        {
            var sb = new StringBuilder("^");
            int last = 0, literal = 0;
            foreach (Match m in Hole.Matches(ko))
            {
                string lit = ko.Substring(last, m.Index - last);
                literal += lit.Length;
                sb.Append(Regex.Escape(lit));
                // 바로 뒤가 단위(개·명·원·일·권…)면 숫자만 받는다 → "캐릭터 카드 10개"에서 이름과 개수를 제대로 나눔
                int after = m.Index + m.Length;
                bool numeric = !m.Groups[2].Success && (after < ko.Length && "개명원일권위번건배%".IndexOf(ko[after]) >= 0);
                sb.Append("(?<g").Append(m.Groups[1].Value).Append(numeric ? @">[-+]?[\d,.]+)" : ">.*?)");
                last = m.Index + m.Length;
            }
            string tail = ko.Substring(last);
            literal += tail.Length;
            sb.Append(Regex.Escape(tail)).Append('$');
            // 틀에 줄바꿈이 없으면 빈칸도 줄을 넘지 않는다 (여러 줄 글은 줄마다 맞춰 보게)
            var opt = ko.Contains("\n") ? RegexOptions.Singleline : RegexOptions.None;
            try { templates.Add(new Template { re = new Regex(sb.ToString(), opt), zh = zh, literal = literal }); }
            catch { Debug.LogWarning("[Loc] 틀을 못 만듦: " + ko); }
        }

        // ───────────── 바꾸기 ─────────────

        /// <summary>화면에 보낼 글을 지금 언어로. 한국어면 그대로.</summary>
        public static string Tr(string s)
        {
            if (string.IsNullOrEmpty(s) || !Translated || !HangulRe.IsMatch(s)) return s;
            if (cache.TryGetValue(s, out var hit)) return hit;
            string r = TrCore(s, 0);
            if (HangulRe.IsMatch(r) && !exact.ContainsKey(s)) NoteMissing("» " + s); // 전체 문장 (어디서 왔는지 찾기 쉽게)
            if (cache.Count > 6000) cache.Clear();
            cache[s] = r;
            return r;
        }

        static string TrCore(string s, int depth)
        {
            if (string.IsNullOrEmpty(s) || !HangulRe.IsMatch(s)) return s;
            if (exact.TryGetValue(s, out var e)) return e;
            string trimmed = s.Trim();
            if (trimmed != s && exact.TryGetValue(trimmed, out e)) return s.Replace(trimmed, e);

            // 앞뒤 장식(★ · 따옴표 · 색/크기 태그)을 떼면 아는 문장인지
            var dm = Deco.Match(s);
            string pre = dm.Groups[1].Value, inner = dm.Groups[2].Value, post = dm.Groups[3].Value;
            bool deco = dm.Success && pre.Length + post.Length > 0 && inner.Length > 0;
            if (deco && exact.TryGetValue(inner, out e)) return OpenQ(pre) + e + CloseQ(post);

            // 틀: 1) 글자가 많은(구체적인) 틀 → 2) 장식 떼고 다시 → 3) "{0}, {1}" 같은 짧은 틀
            if (depth < 4)
            {
                string r1 = ApplyTemplates(s, depth, false);
                if (r1 != null) return r1;
            }

            if (deco && depth < 6) return OpenQ(pre) + TrCore(inner, depth + 1) + CloseQ(post);

            // 여러 줄이면 줄마다 (짧은 틀이 줄을 넘어 나누지 않게 먼저)
            if (s.Contains("\n"))
            {
                var parts = s.Split('\n');
                for (int i = 0; i < parts.Length; i++) parts[i] = TrCore(parts[i], depth + 1);
                return string.Join("\n", parts);
            }

            if (depth < 4)
            {
                string r2 = ApplyTemplates(s, depth, true);
                if (r2 != null) return r2;
            }

            // 마지막: 아는 낱말만이라도 바꾸고, 못 바꾼 문장은 기록 (에디터에서 번역표를 채우려고)
            string w = s;
            foreach (var kv in glossary)
                if (w.Contains(kv.Key)) w = w.Replace(kv.Key, kv.Value);
            w = Regex.Replace(w, @"([\d,]+)원", mm => Yuan(mm.Groups[1].Value));
            if (Zh)
            {
                w = Regex.Replace(w, @"(\d+)위", "第$1名");
                w = Regex.Replace(w, @"(\d+)권", "第$1集");
                w = Regex.Replace(w, @"사인본 (\d+)", "簽名本$1");
                w = Regex.Replace(w, @"초판 (\d+)", "初版$1");
            }
            else if (En)
            {
                w = Regex.Replace(w, @"(\d+)위", "#$1");
                w = Regex.Replace(w, @"(\d+)권", "Vol. $1");
                w = Regex.Replace(w, @"사인본 (\d+)", "Signed $1");
                w = Regex.Replace(w, @"초판 (\d+)", "1st ed. $1");
                w = Regex.Replace(w, @"(\d+)개", "$1");
                w = Regex.Replace(w, @"(\d+)명", "$1");
            }
            // 번역된 낱말 뒤에 남은 조사 (예: "四驅車를", "Marbles를") 지우기
            w = Regex.Replace(w, @"(?<=[一-鿿）」』》A-Za-z0-9)'])(으로|을|를|이|가|은|는|과|와|로|에)(?![가-힣])", "");
            if (HangulRe.IsMatch(w)) NoteMissing(s);
            return w;
        }

        // 한국어 곧은따옴표 → 대만식 「」
        static string OpenQ(string s) => Zh ? s.Replace("\"", "「") : s;
        static string CloseQ(string s) => Zh ? s.Replace("\"", "」") : s;

        const int GenericLiteral = 3; // 이보다 글자가 적은 틀은 "짧은 틀" (쉼표·가운뎃점으로 나누기)

        static string ApplyTemplates(string s, int depth, bool generic)
        {
            foreach (var t in templates)
            {
                if ((t.literal <= GenericLiteral) != generic) continue;
                var m = t.re.Match(s);
                if (!m.Success) continue;
                return Hole.Replace(t.zh, h =>
                {
                    var g = m.Groups["g" + h.Groups[1].Value];
                    if (!g.Success) return h.Value;
                    string v = g.Value;
                    if (h.Groups[2].Success) return Yuan(v);
                    return TrCore(v, depth + 1);
                });
            }
            return null;
        }

        /// <summary>"3,000" → "300元" (원 → 대만판 元: 10분의 1, 옛날 柑仔店 물가 느낌).</summary>
        static string Yuan(string number)
        {
            string digits = number.Replace(",", "").Replace("원", "").Trim();
            if (!int.TryParse(digits, out int v)) return number;
            return Money(v);
        }

        /// <summary>돈 표시. 한국어 "1,000원", 대만판 "100元".</summary>
        public static string Money(int won)
        {
            if (En) return UsDollars(won);   // 미국판: 1,000원 = $1 (100원 = $0.10)
            if (!Zh) return won.ToString("N0", CultureInfo.InvariantCulture) + "원";
            if (won % 10 == 0) return (won / 10).ToString("N0", CultureInfo.InvariantCulture) + "元";
            return (won / 10f).ToString("0.#", CultureInfo.InvariantCulture) + "元";
        }

        /// <summary>미국판 돈: 원 ÷ 1000 달러. 딱 떨어지면 "$3", 아니면 "$0.50".</summary>
        static string UsDollars(int won)
        {
            string sign = won < 0 ? "-" : "";
            int a = Mathf.Abs(won);
            if (a % 1000 == 0) return sign + "$" + (a / 1000).ToString("N0", CultureInfo.InvariantCulture);
            return sign + "$" + (a / 1000f).ToString("N2", CultureInfo.InvariantCulture);
        }

        static void NoteMissing(string s)
        {
#if UNITY_EDITOR
            if (missing.Add(s))
                System.IO.File.AppendAllText(System.IO.Path.Combine(Application.dataPath, "../Logs/loc-missing-" + (TableName(current) ?? "ko") + ".txt"), s.Replace("\n", "\\n") + "\n");
#endif
        }
    }
}
