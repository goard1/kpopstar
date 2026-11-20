using System;
using System.Collections.Generic;
using System.IO;
using Munbanggu.Customers;
using Munbanggu.Data;
using UnityEngine;

namespace Munbanggu.Core
{
    [Serializable]
    public class StockEntry
    {
        public string productId;
        public int count;
    }

    [Serializable]
    public class SaveData
    {
        /// <summary>v1: 돈·날짜·재고 / v2: + 유행 진행, 단골 진행도</summary>
        public const int CurrentVersion = 2;

        public int version = CurrentVersion;
        public int day = 1;
        public int money;
        public List<StockEntry> backroom = new List<StockEntry>();
        public List<StockEntry> shelves = new List<StockEntry>(); // 인덱스 = 매대 번호
        // ── v2 ── (v1 세이브엔 없음 → JsonUtility가 아래 기본값을 남김)
        public TrendState trend = new TrendState();
        public List<RegularState> regulars = new List<RegularState>();
        // ── 돌발 사건 ── (예전 세이브엔 없음 → 빈 목록)
        public List<CreditEntry> credits = new List<CreditEntry>();
        public List<string> seenEvents = new List<string>();
        // ── 가게 업그레이드 ──
        public List<UpgradeLevel> upgrades = new List<UpgradeLevel>();
        // ── 상담 손님 ── (0이면 예전 세이브 → 50으로 시작)
        public int reputation;
        public List<string> knownTraits = new List<string>();
    }

    /// <summary>로컬 JSON 저장. 나중에 Play Games 클라우드 저장을 붙일 자리.</summary>
    public static class SaveSystem
    {
        static string FilePath => Path.Combine(Application.persistentDataPath, "save.json");

        public static SaveData Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                if (data != null) Migrate(data);
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 불러오기 실패, 새로 시작합니다: {e.Message}");
                return null;
            }
        }

        /// <summary>옛 버전 세이브를 현재 형식으로. 빠진 필드는 새 게임 기본값.</summary>
        static void Migrate(SaveData data)
        {
            data.backroom ??= new List<StockEntry>();
            data.shelves ??= new List<StockEntry>();
            data.regulars ??= new List<RegularState>();
            if (data.version < 2 || data.trend == null)
            {
                // v1 → v2: 유행은 processedDay=0이라 불러온 날 아침에 새로 시작, 단골은 처음 만나는 상태
                data.trend = new TrendState();
                data.regulars.Clear();
            }
            data.version = SaveData.CurrentVersion;
        }

        public static void Save(SaveData data)
        {
            try
            {
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(data));
                File.Copy(tmp, FilePath, true);
                File.Delete(tmp);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 저장 실패: {e.Message}");
            }
        }

        public static void Delete()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
    }
}
