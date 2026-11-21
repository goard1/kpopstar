namespace Munbanggu.Data
{
    /// <summary>받침에 따라 조사를 붙이는 도우미. 예: Josa.Eul("요요") = "요요를", Josa.Eul("딱지") = "딱지를".</summary>
    public static class Josa
    {
        static bool HasBatchim(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            char c = word[word.Length - 1];
            if (c < 0xAC00 || c > 0xD7A3) return false; // 한글 음절이 아니면 받침 없음으로 취급
            return (c - 0xAC00) % 28 != 0;
        }

        static string Pick(string word, string withBatchim, string without) => Munbanggu.Core.L.Zh ? word :
            word + (HasBatchim(word) ? withBatchim : without);

        public static string Eul(string w) => Pick(w, "을", "를");
        public static string I(string w) => Pick(w, "이", "가");
        public static string Eun(string w) => Pick(w, "은", "는");
        public static string Wa(string w) => Pick(w, "과", "와");
    }
}
