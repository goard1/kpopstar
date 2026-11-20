using UnityEngine.UI;

namespace Munbanggu.UI
{
    /// <summary>글자를 넣을 때 Jua에 없는 기호를 대체 문자로 바꿔 주는 Text (UIKit.Label이 사용).</summary>
    public class SafeText : Text
    {
        public override string text
        {
            get => base.text;
            set => base.text = Glyphs.Safe(value);
        }
    }
}
