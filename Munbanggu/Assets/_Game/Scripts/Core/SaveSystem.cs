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
        public bool heroGirl = true;  // 주인공 손녀/손자 (docs/11)
        public string heroName;       // 비어 있으면 예전 세이브 → 기본 이름
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
        // ── 직원 ──
        public StaffState staff = new StaffState();
        // ── 시설(장식)·콤보 ──
        public FacilityState facilities = new FacilityState();
        // ── 가게 증축 ── (예전 세이브엔 없음 → 0단계, 붙은 진열대 순서는 빈 목록 = 예전처럼)
        public int district;     // 상권(동네) 번호 (docs/07)
        public int expansion;    // 그 동네에서 지은 증축 단계
        public List<string> slotGroups = new List<string>();
        // ── 진열대 옮기기 ── (옮긴 진열대만)
        public List<SlotPlacement> placements = new List<SlotPlacement>();
        // ── 동네 살이 (만화방·대회·라이벌·도감) ──
        public TownSave town = new TownSave();
    }

    /// <summary>로컬 JSON 저장 (슬롯 3개: save1~3.json). 나중에 Play Games 클라우드 저장을 붙일 자리.</summary>
    public static class SaveSystem
    {
        public const int SlotCount = 3;
        /// <summary>지금 쓰는 슬롯 (1~3). 타이틀 화면에서 고른다.</summary>
        public static int Slot = 1;

        static string PathOf(int slot) => Path.Combine(Application.persistentDataPath, $"save{slot}.json");
        static string FilePath => PathOf(Slot);
        static string LegacyPath => Path.Combine(Application.persistentDataPath, "save.json");

        /// <summary>슬롯이 생기기 전의 save.json은 1번 슬롯으로 옮긴다.</summary>
        static void MigrateLegacy()
        {
            try
            {
                if (File.Exists(LegacyPath) && !File.Exists(PathOf(1))) File.Move(LegacyPath, PathOf(1));
            }
            catch (Exception e) { Debug.LogWarning($"[Save] 옛 저장 옮기기 실패: {e.Message}"); }
        }

        /// <summary>슬롯 미리보기 (타이틀 화면용). 비었으면 null.</summary>
        public static SaveData Peek(int slot)
        {
            MigrateLegacy();
            try
            {
                string p = PathOf(slot);
                return File.Exists(p) ? JsonUtility.FromJson<SaveData>(File.ReadAllText(p)) : null;
            }
            catch { return null; }
        }

        /// <summary>추억 앨범 사진 폴더 (1번 슬롯은 예전 이름 그대로).</summary>
        public static string AlbumDir => Path.Combine(Application.persistentDataPath, Slot == 1 ? "album" : "album" + Slot);

        public static SaveData Load()
        {
            MigrateLegacy();
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

        public static void Delete() => Delete(Slot);

        public static void Delete(int slot)
        {
            try
            {
                if (File.Exists(PathOf(slot))) File.Delete(PathOf(slot));
                if (slot == 1 && File.Exists(LegacyPath)) File.Delete(LegacyPath);
                string album = Path.Combine(Application.persistentDataPath, slot == 1 ? "album" : "album" + slot);
                if (Directory.Exists(album)) Directory.Delete(album, true);
            }
            catch (Exception e) { Debug.LogWarning($"[Save] 지우기 실패: {e.Message}"); }
        }
    }
}

namespace Munbanggu.Core
{
    /// <summary>플레이어가 옮긴 진열대 하나의 자리·방향 (Layout.Slots 인덱스 기준).</summary>
    [System.Serializable]
    public class SlotPlacement
    {
        public int index;
        public float x, z, yaw;
    }
}
