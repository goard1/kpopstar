using Munbanggu.Data;
using UnityEngine;

namespace Munbanggu.Core
{
    public enum Weather { Sunny, Cloudy, Rain }

    public enum SchoolDay
    {
        Normal,
        Saturday,     // 토요일: 오전 수업만 → 일찍부터 손님
        Sunday,       // 일요일: 학교 안 감 → 손님은 적지만 놀거리·만화방
        ExamWeek,     // 시험 기간: 학용품 날개, 장난감·만화방 썰렁
        SportsEve,    // 운동회 전날: 간식·밖 놀이
        PicnicEve,    // 소풍 전날: 간식 폭발
        Vacation,     // 방학: 하루 종일 손님, 학용품 썰렁, 놀이·만화방
    }

    /// <summary>하루의 학교 달력·날씨와 그날 장사에 주는 영향.</summary>
    public sealed class DayInfo
    {
        public int day;
        public int weekday;      // 0 월 … 6 일
        public Weather weather;
        public SchoolDay kind;

        static readonly string[] WeekNames = { "월", "화", "수", "목", "금", "토", "일" };
        public string WeekName => WeekNames[weekday];

        public string WeatherIcon => weather switch { Weather.Rain => "비", Weather.Cloudy => "흐림", _ => "맑음" };

        public string Title => kind switch
        {
            SchoolDay.Saturday => "토요일 (오전 수업)",
            SchoolDay.Sunday => "일요일",
            SchoolDay.ExamWeek => "시험 기간",
            SchoolDay.SportsEve => "운동회 전날",
            SchoolDay.PicnicEve => "소풍 전날",
            SchoolDay.Vacation => "방학",
            _ => "평일",
        };

        /// <summary>할머니가 아침에 하는 한마디 (장사 힌트).</summary>
        public string Hint
        {
            get
            {
                string k = kind switch
                {
                    SchoolDay.Saturday => "토요일이라 애들이 일찍 끝나. 손님이 좀 더 오겠어.",
                    SchoolDay.Sunday => "일요일이라 학교 가는 애들이 없어. 대신 놀러 나온 애들이 장난감·놀거리를 찾지.",
                    SchoolDay.ExamWeek => "시험 기간이야! 연필·지우개가 날개 돋친 듯 팔리고, 장난감은 엄마 눈치 보느라 덜 사.",
                    SchoolDay.SportsEve => "내일 운동회래! 오늘은 간식이랑 밖에서 갖고 놀 거 많이 찾을 거야.",
                    SchoolDay.PicnicEve => "내일 소풍이래! 과자·사탕 잔뜩 사 갈 테니 간식 넉넉히 채워 둬.",
                    SchoolDay.Vacation => "방학이야! 아침부터 애들이 놀러 와. 학용품은 덜 팔리고 놀거리가 잘 나가.",
                    _ => null,
                };
                string w = weather == Weather.Rain
                    ? "비가 와서 밖 놀이는 안 팔리고, 딱지·카드 같은 실내 놀이가 잘 팔려. 만화방도 붐비겠다."
                    : null;
                if (k == null) return w;
                return w == null ? k : k + "\n" + w;
            }
        }

        /// <summary>손님 수 배율.</summary>
        public float SpawnMultiplier
        {
            get
            {
                float m = kind switch
                {
                    SchoolDay.Saturday => 1.1f,
                    SchoolDay.Sunday => 0.75f,
                    SchoolDay.ExamWeek => 0.9f,
                    SchoolDay.SportsEve => 1.2f,
                    SchoolDay.PicnicEve => 1.3f,
                    SchoolDay.Vacation => 1.15f,
                    _ => 1f,
                };
                if (weather == Weather.Rain) m *= 0.8f;
                return m;
            }
        }

        /// <summary>상품별 수요 배율.</summary>
        public float Demand(ProductData p)
        {
            if (p == null) return 1f;
            float m = 1f;
            bool school = ProductTraits.Has(p, ProductTraits.School) || p.category == ProductCategory.Stationery;
            bool snack = p.category == ProductCategory.Snack;
            bool outside = ProductTraits.Has(p, ProductTraits.Outside);
            bool toy = p.category == ProductCategory.Toy || p.category == ProductCategory.Play;
            switch (kind)
            {
                case SchoolDay.Sunday: if (school) m *= 0.4f; if (toy) m *= 1.3f; break;
                case SchoolDay.ExamWeek: if (school) m *= 2f; if (toy) m *= 0.6f; break;
                case SchoolDay.SportsEve: if (snack) m *= 1.8f; if (outside) m *= 1.8f; break;
                case SchoolDay.PicnicEve: if (snack) m *= 2.4f; break;
                case SchoolDay.Vacation: if (school) m *= 0.5f; if (toy) m *= 1.3f; break;
            }
            if (weather == Weather.Rain)
            {
                if (outside) m *= 0.35f;
                else if (toy) m *= 1.3f; // 실내 놀이
            }
            return m;
        }

        /// <summary>2층 만화방 손님 배율.</summary>
        public float ComicMultiplier
        {
            get
            {
                float m = kind switch
                {
                    SchoolDay.Sunday => 1.5f,
                    SchoolDay.ExamWeek => 0.5f,
                    SchoolDay.Vacation => 1.6f,
                    SchoolDay.Saturday => 1.2f,
                    _ => 1f,
                };
                if (weather == Weather.Rain) m *= 1.5f;
                return m;
            }
        }
    }

    /// <summary>
    /// 학교 달력. 1일차 = 월요일, 날마다 같은 결과(날짜로 정해짐 → 저장 필요 없음).
    /// 4주(28일)마다: 2주차 목·금 시험, 3주차 수요일 운동회 전날, 4주차 목요일 소풍 전날.
    /// 29~42일, 85~98일… (8주마다 2주) 방학. 날씨는 날짜로 정해지는 난수 (비 25%, 흐림 25%).
    /// </summary>
    public static class SchoolCalendar
    {
        public static DayInfo For(int day)
        {
            day = Mathf.Max(1, day);
            var d = new DayInfo { day = day, weekday = (day - 1) % 7 };
            int inCycle = (day - 1) % 28;      // 4주 주기 안의 날 (0~27)
            int week = inCycle / 7;            // 0~3
            bool vacation = day >= 29 && ((day - 29) % 56) < 14;

            if (d.weekday == 6) d.kind = SchoolDay.Sunday;
            else if (vacation) d.kind = SchoolDay.Vacation;
            else if (d.weekday == 5) d.kind = SchoolDay.Saturday;
            else if (day == 4) d.kind = SchoolDay.PicnicEve; // 첫 주에 한 번 맛보기
            else if (day >= 8 && week == 1 && (d.weekday == 3 || d.weekday == 4)) d.kind = SchoolDay.ExamWeek;
            else if (day >= 8 && week == 2 && d.weekday == 2) d.kind = SchoolDay.SportsEve;
            else if (day >= 8 && week == 3 && d.weekday == 3) d.kind = SchoolDay.PicnicEve;
            else d.kind = SchoolDay.Normal;

            // 1·2일차는 튜토리얼이라 맑게
            uint h = (uint)(day * 2654435761u) ^ 0x9E3779B9u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
            float r = (h % 1000) / 1000f;
            d.weather = day <= 2 ? Weather.Sunny : r < 0.25f ? Weather.Rain : r < 0.5f ? Weather.Cloudy : Weather.Sunny;
            return d;
        }
    }
}
