using System.Collections.Generic;
using Munbanggu.Data;
using UnityEngine;
using T = Munbanggu.Data.ProductTraits;

namespace Munbanggu.Customers
{
    /// <summary>상담 손님의 요청 하나: 하는 말 + 핵심 특징 + 덤 특징 + 예산.</summary>
    public sealed class CounselRequest
    {
        public readonly string line;      // 아이가 하는 말
        public readonly string need;      // 핵심 특징 (없으면 "이건 아닌데…")
        public readonly string bonus;     // 덤 특징 (맞으면 "딱 맞아요!")
        public readonly int budget;       // 가진 돈 (넘는 건 못 삼)
        public readonly string perfectLine, okLine, wrongLine;

        public CounselRequest(string line, string need, string bonus, int budget,
            string perfectLine, string okLine, string wrongLine)
        {
            this.line = line; this.need = need; this.bonus = bonus; this.budget = budget;
            this.perfectLine = perfectLine; this.okLine = okLine; this.wrongLine = wrongLine;
        }
    }

    public enum CounselResult { Perfect, Ok, Wrong, NoAnswer }

    /// <summary>요청 목록과 판정 (docs/05-fun-redesign.md 2-1, 2-3).</summary>
    public static class CounselRequests
    {
        static readonly CounselRequest[] all =
        {
            new CounselRequest("500원 있는데 친구랑 같이 놀 수 있는 거요!", T.Friends, T.Play, 500,
                "우와 이거면 반 애들 다 모이겠다!", "이거 같이 하면 되겠다", "음… 이건 혼자 하는 거잖아요"),
            new CounselRequest("짝꿍 생일이에요! 선물할 거 추천해 주세요", T.Gift, T.Cute, 1000,
                "이거 주면 엄청 좋아하겠다!", "이 정도면 괜찮겠죠?", "이건 선물로는 좀…"),
            new CounselRequest("학원 가기 전에 배고파요… 300원 있어요", T.Snack, T.Long, 300,
                "오래 먹을 수 있어서 딱이에요!", "고맙습니다~ 냠", "이건 먹는 게 아니잖아요"),
            new CounselRequest("100원밖에 없어요. 맛있는 거요!", T.Snack, T.Sweet, 100,
                "달콤해! 할머니 최고!", "냠냠, 괜찮네요", "이건 못 먹잖아요…"),
            new CounselRequest("형한테 이길 수 있는 거요!", T.Cool, T.Skill, 1000,
                "이걸로 형 이긴다!! 연습해야지", "멋있긴 하네요", "이걸로는 못 이겨요…"),
            new CounselRequest("운동장에서 놀 거요!", T.Outside, T.Friends, 500,
                "애들 다 불러야지!", "밖에서 하면 재밌겠다", "이건 교실에서 하는 거잖아요"),
            new CounselRequest("친구 놀래킬 장난감이요, 히히", T.Prank, T.Squishy, 500,
                "히히히 이거 대박이다!", "이것도 웃기겠다", "이걸로는 안 놀라요…"),
            new CounselRequest("비 와서 집에만 있어야 해요. 혼자 놀 거요", T.Alone, T.Cute, 1000,
                "이거면 하루 종일 놀 수 있어요!", "심심하진 않겠다", "이건 혼자 못 하잖아요"),
            new CounselRequest("모으는 거 좋아해요. 새로운 거 없어요?", T.Collect, T.Luck, 300,
                "반짝이 나오면 자랑해야지!", "하나 더 모았다~", "이건 모으는 게 아닌데…"),
            new CounselRequest("말랑말랑한 거요!", T.Squishy, T.Cute, 300,
                "말랑말랑~ 계속 만지고 싶어요", "말랑하네요", "이건 딱딱하잖아요!"),
            new CounselRequest("새 학기 준비물 사러 왔어요", T.School, T.Gift, 500,
                "이걸로 공부 열심히 할게요!", "준비 끝!", "이건 학교에 못 가져가요…"),
            new CounselRequest("단짝이랑 나눠 먹을 거요", T.Snack, T.Friends, 300,
                "반씩 나눠 먹어야지!", "같이 먹으면 맛있겠다", "이건 먹는 게 아니잖아요"),
            new CounselRequest("오래오래 씹을 수 있는 거요", T.Long, T.Snack, 200,
                "학원 끝날 때까지 씹어야지", "오래 가겠다", "금방 끝나잖아요…"),
            new CounselRequest("엄마가 1000원 주셨어요. 제일 멋진 거!", T.Cool, T.Skill, 1000,
                "이거 들고 가면 애들이 다 쳐다보겠다!", "멋있다~", "이건 별로 안 멋진데…"),
            new CounselRequest("동생이 울어요… 달래 줄 거 없어요?", T.Cute, T.Sweet, 500,
                "동생이 좋아하겠다! 고맙습니다", "이거면 그칠 것 같아요", "이걸로는 안 그칠 거예요…"),
            new CounselRequest("운 좋으면 대박 나는 거요!", T.Luck, T.Collect, 300,
                "제발 반짝이 나와라!!", "두근두근", "이건 운이랑 상관없잖아요"),
            new CounselRequest("너무 더워요… 시원하게 놀 거 없어요?", T.Summer, T.Outside, 500,
                "물싸움이다!!", "시원하겠다", "이건 하나도 안 시원해요…"),
            new CounselRequest("50원 있어요… 그래도 뭐 살 수 있어요?", T.Cheap, T.Snack, 50,
                "와 50원으로도 되네요!", "고맙습니다!", "이건 50원이 아니잖아요…"),
        };

        /// <summary>해금된 상품 중 핵심 특징이 맞는 게 하나라도 있는 요청만 고른다. 없으면 null.</summary>
        public static CounselRequest Roll(IEnumerable<ProductData> unlocked)
        {
            var pool = new List<CounselRequest>();
            foreach (var r in all)
                foreach (var p in unlocked)
                    if (T.Has(p, r.need) && p.retailPrice <= r.budget) { pool.Add(r); break; }
            return pool.Count == 0 ? null : pool[Random.Range(0, pool.Count)];
        }

        public static CounselResult Judge(CounselRequest r, ProductData p)
        {
            if (p == null) return CounselResult.NoAnswer;
            if (!T.Has(p, r.need)) return CounselResult.Wrong;
            return T.Has(p, r.bonus) ? CounselResult.Perfect : CounselResult.Ok;
        }
    }
}
