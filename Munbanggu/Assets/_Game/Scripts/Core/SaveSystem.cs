using System;
using System.Collections.Generic;
using System.IO;
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
        public int version = 1;
        public int day = 1;
        public int money;
        public List<StockEntry> backroom = new List<StockEntry>();
        public List<StockEntry> shelves = new List<StockEntry>(); // 인덱스 = 매대 번호
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
                return JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Save] 불러오기 실패, 새로 시작합니다: {e.Message}");
                return null;
            }
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
