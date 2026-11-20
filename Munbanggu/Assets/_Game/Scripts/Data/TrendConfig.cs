using System;
using UnityEngine;

namespace Munbanggu.Data
{
    /// <summary>
    /// 유행(붐) 수치 모음. 나중에 Remote Config 키 하나당 필드 하나로 그대로 옮기면 된다.
    /// 붐 상품의 수요는 "배수"가 아니라 "전체 손님 중 그 상품을 찾는 비율(share)"로 정한다
    /// → 상품 수가 8종에서 20종으로 늘어도 붐의 체감 세기가 같다.
    /// </summary>
    [Serializable]
    public class TrendConfig
    {
        [Header("붐 길이·간격 (일)")]
        [Min(1)] public int minBoomDays = 3;
        [Min(1)] public int maxBoomDays = 5;
        [Tooltip("붐이 끝난 뒤 다음 붐까지 쉬는 날 (0이면 바로 이어짐)")]
        [Min(0)] public int minGapDays = 0;
        [Min(0)] public int maxGapDays = 1;
        [Tooltip("다음 붐 며칠 전부터 아침에 소문이 도는지")]
        [Min(1)] public int rumorDays = 2;
        [Tooltip("소문이 맞을 확률. 틀리면 다른 상품이 붐이 됨")]
        [Range(0f, 1f)] public float rumorReliability = 0.8f;
        [Tooltip("같은 상품이 다시 붐이 되기 전에 거쳐야 하는 다른 붐 수")]
        [Min(0)] public int noRepeatCount = 2;

        [Header("붐 곡선 (손님 중 붐 상품을 찾는 비율)")]
        [Range(0f, 0.9f)] public float startShare = 0.12f;   // 첫날·마지막 날 쪽
        [Range(0f, 0.9f)] public float peakShare = 0.38f;    // 정점
        [Tooltip("붐 기간 중 정점 위치 (0~1). 0.4 = 빨리 오르고 천천히 식음")]
        [Range(0.1f, 0.9f)] public float peakAt = 0.4f;
        [Tooltip("곡선 값이 이 이상이면 '정점'으로 표시")]
        [Range(0f, 1f)] public float peakStageThreshold = 0.7f;

        [Header("붐 이후 (재고 리스크)")]
        [Tooltip("붐이 끝난 상품의 인기 배수")]
        [Range(0f, 1f)] public float crashMultiplier = 0.25f;
        [Min(0)] public int crashDays = 3;

        /// <summary>붐 L일 중 i번째 날(0부터)의 곡선 값 0~1. 시작-정점-하락.</summary>
        public float Curve(int dayIndex, int length)
        {
            if (length <= 0) return 0f;
            float t = (dayIndex + 0.5f) / length;
            return t < peakAt ? t / peakAt : (1f - t) / (1f - peakAt);
        }

        public float ShareFor(int dayIndex, int length) =>
            Mathf.Lerp(startShare, peakShare, Mathf.Clamp01(Curve(dayIndex, length)));
    }
}
