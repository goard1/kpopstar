using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Munbanggu.EditorTools
{
    /// <summary>
    /// 코드 속 한국어 문구를 전부 뽑아 번역표 틀로 만든다 (docs/10).
    /// $"…{식}…" 의 구멍은 {0} {1}… 로 바꾼다. 주석·디버그 로그·에디터 코드·[Header] 등은 뺀다.
    /// 결과: Logs/loc-extract-ko.txt (한 줄에 하나, 줄바꿈은 \n).
    /// </summary>
    public static class LocExtract
    {
        [MenuItem("문방구/번역할 문구 뽑기")]
        public static void Run()
        {
            string root = Path.Combine(Application.dataPath, "_Game/Scripts");
            var seen = new HashSet<string>();
            var outLines = new List<string>();
            foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string rel = file.Replace('\\', '/');
                if (rel.Contains("/Editor/")) continue;
                var found = new List<string>();
                Scan(File.ReadAllText(file), found);
                outLines.Add("# " + Path.GetFileName(file));
                foreach (var s in found)
                    if (seen.Add(s)) outLines.Add(s.Replace("\r", "").Replace("\n", "\\n").Replace("\t", "\\t"));
            }
            string outPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/loc-extract-ko.txt"));
            File.WriteAllLines(outPath, outLines, new UTF8Encoding(false));
            Debug.Log($"[LocExtract] {seen.Count}개 → {outPath}");
        }

        static bool HasHangul(string s)
        {
            foreach (char c in s) if (c >= 0xAC00 && c <= 0xD7A3) return true;
            return false;
        }

        static void Scan(string src, List<string> found)
        {
            int i = 0, n = src.Length;
            while (i < n)
            {
                char c = src[i];
                // 주석
                if (c == '/' && i + 1 < n && src[i + 1] == '/') { while (i < n && src[i] != '\n') i++; continue; }
                if (c == '/' && i + 1 < n && src[i + 1] == '*') { i += 2; while (i + 1 < n && !(src[i] == '*' && src[i + 1] == '/')) i++; i += 2; continue; }
                // 문자 리터럴
                if (c == '\'') { i++; while (i < n && src[i] != '\'') { if (src[i] == '\\') i++; i++; } i++; continue; }
                bool interp = false, verbatim = false;
                int j = i;
                while (j < n && (src[j] == '$' || src[j] == '@')) { if (src[j] == '$') interp = true; else verbatim = true; j++; }
                if (j < n && src[j] == '"' && (j == i || interp || verbatim))
                {
                    int lineStart = src.LastIndexOf('\n', i) + 1;
                    string line = src.Substring(lineStart, i - lineStart);
                    var sb = new StringBuilder();
                    i = ReadString(src, j + 1, interp, verbatim, sb, found);
                    string s = sb.ToString();
                    bool skip = line.Contains("Debug.Log") || line.TrimStart().StartsWith("[") || line.Contains("MenuItem")
                        || line.Contains("GameObject(") || line.Contains("Find(\"") || line.Contains("Label(\"") && false;
                    if (!skip && HasHangul(s)) found.Add(s);
                    continue;
                }
                i++;
            }
        }

        /// <summary>문자열 몸통을 읽고 끝 다음 위치를 돌려준다. 보간 구멍은 {k}로, 구멍 안의 문자열은 따로 found에.</summary>
        static int ReadString(string src, int i, bool interp, bool verbatim, StringBuilder sb, List<string> found)
        {
            int n = src.Length, hole = 0;
            while (i < n)
            {
                char c = src[i];
                if (verbatim)
                {
                    if (c == '"') { if (i + 1 < n && src[i + 1] == '"') { sb.Append('"'); i += 2; continue; } return i + 1; }
                }
                else
                {
                    if (c == '\\' && i + 1 < n)
                    {
                        char e = src[i + 1];
                        sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e == 'r' ? '\r' : e);
                        i += 2;
                        continue;
                    }
                    if (c == '"') return i + 1;
                }
                if (interp && c == '{')
                {
                    if (i + 1 < n && src[i + 1] == '{') { sb.Append('{'); i += 2; continue; }
                    // 구멍: 짝 맞는 } 까지 (안의 문자열은 따로 뽑음)
                    int depth = 1; i++;
                    var inner = new StringBuilder();
                    while (i < n && depth > 0)
                    {
                        char d = src[i];
                        bool iInterp = false, iVerb = false; int k = i;
                        while (k < n && (src[k] == '$' || src[k] == '@')) { if (src[k] == '$') iInterp = true; else iVerb = true; k++; }
                        if (k < n && src[k] == '"')
                        {
                            var nested = new StringBuilder();
                            i = ReadString(src, k + 1, iInterp, iVerb, nested, found);
                            if (HasHangul(nested.ToString())) found.Add(nested.ToString());
                            continue;
                        }
                        if (d == '\'') { i++; while (i < n && src[i] != '\'') { if (src[i] == '\\') i++; i++; } i++; continue; }
                        if (d == '{') depth++;
                        else if (d == '}') { depth--; if (depth == 0) { i++; break; } }
                        inner.Append(d);
                        i++;
                    }
                    sb.Append('{').Append(hole++).Append('}');
                    continue;
                }
                if (interp && c == '}' && i + 1 < n && src[i + 1] == '}') { sb.Append('}'); i += 2; continue; }
                sb.Append(c);
                i++;
            }
            return i;
        }
    }
}
