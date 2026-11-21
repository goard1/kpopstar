namespace Munbanggu.Core
{
    /// <summary>
    /// 주인공 = 할머니·할아버지의 손주 (docs/11). 새 게임에서 손녀/손자와 이름을 고른다 (슬롯마다 저장).
    /// 글 속 표시는 화면에 나갈 때(SafeText) 바꾼다:
    ///   〔NAME〕  주인공 이름
    ///   〔CALL〕  아이들이 부르는 호칭 (남자아이 기준: 누나/형 · 대만판 姊姊/哥哥)
    ///   〔CALLG〕 여자아이가 부를 때 (언니/오빠 · 대만판 姊姊/哥哥)
    /// 표시 바로 뒤에 조사를 붙이지 않게 문장을 쓴다 (누나는/형은처럼 받침이 달라서). 〔CALLG〕는 언니/오빠라 "가"까지 괜찮다.
    /// </summary>
    public static class Hero
    {
        public const string NameTok = "〔NAME〕", CallTok = "〔CALL〕", CallGTok = "〔CALLG〕";

        public static bool Girl = true;
        public static string Name = "";

        public static string DefaultName(bool girl) => L.Zh ? (girl ? "小雯" : "阿傑") : L.En ? (girl ? "Emma" : "Jake") : (girl ? "지은" : "준호");
        public static string DisplayName => string.IsNullOrWhiteSpace(Name) ? DefaultName(Girl) : Name.Trim();
        // 영어판은 아이들이 이름으로 부른다
        public static string Call => L.Zh ? (Girl ? "姊姊" : "哥哥") : L.En ? DisplayName : (Girl ? "누나" : "형");
        public static string CallG => L.Zh ? (Girl ? "姊姊" : "哥哥") : L.En ? DisplayName : (Girl ? "언니" : "오빠");

        public static void Set(bool girl, string name)
        {
            Girl = girl;
            Name = string.IsNullOrWhiteSpace(name) ? DefaultName(girl) : name.Trim();
        }

        public static string Fill(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('〔') < 0) return s;
            return s.Replace(CallGTok, CallG).Replace(CallTok, Call).Replace(NameTok, DisplayName);
        }
    }
}
