using UnityEngine;

namespace Munbanggu.UI
{
    public enum SettlementLineKind
    {
        Value,   // 왼쪽 글 + 오른쪽 숫자 (카운트업)
        Total,   // 크게 강조되는 합계 (순이익 등), 위에 점선
        Text,    // 글만 한 줄
        Tip,     // 작은 회색 팁
        Gap      // 빈 줄
    }

    /// <summary>
    /// 정산(장부) 화면 한 줄. 다른 시스템이 줄을 추가하려면:
    /// <code>HUD.CollectSettlementLines += (gm, lines) => lines.Add(SettlementLine.Count("단골 손님", n));</code>
    /// </summary>
    public struct SettlementLine
    {
        public SettlementLineKind kind;
        public string label;
        public int value;
        /// <summary>숫자 뒤에 붙는 말 ("원", "명", "개"…). 돈이면 "원".</summary>
        public string unit;
        /// <summary>숫자 앞에 '-' 표시 (매입 등).</summary>
        public bool negative;
        /// <summary>숫자 색. a == 0이면 기본색.</summary>
        public Color color;

        public static SettlementLine Money(string label, int value, bool negative = false, Color color = default)
            => new SettlementLine { kind = SettlementLineKind.Value, label = label, value = value, unit = "원", negative = negative, color = color };

        public static SettlementLine Count(string label, int value, string unit = "명", Color color = default)
            => new SettlementLine { kind = SettlementLineKind.Value, label = label, value = value, unit = unit, color = color };

        /// <summary>강조 합계. 색을 안 주면 0 이상 초록, 음수 빨강.</summary>
        public static SettlementLine Total(string label, int value, string unit = "원", Color color = default)
            => new SettlementLine { kind = SettlementLineKind.Total, label = label, value = value, unit = unit, color = color };

        public static SettlementLine TextLine(string text, Color color = default)
            => new SettlementLine { kind = SettlementLineKind.Text, label = text, color = color };

        public static SettlementLine Tip(string text)
            => new SettlementLine { kind = SettlementLineKind.Tip, label = text };

        public static SettlementLine Gap()
            => new SettlementLine { kind = SettlementLineKind.Gap };

        /// <summary>숫자 표시 문자열 (카운트업 중간값도 이걸로).</summary>
        public string Format(int shown)
        {
            string num = unit == "원" ? UIKit.Won(Mathf.Abs(shown)) : Mathf.Abs(shown).ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + (unit ?? "");
            bool minus = negative ? shown != 0 : shown < 0;
            return minus ? "-" + num : num;
        }
    }
}
