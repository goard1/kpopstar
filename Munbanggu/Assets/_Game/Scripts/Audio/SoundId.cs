namespace Munbanggu.Audio
{
    /// <summary>
    /// 효과음 ID. 실제 녹음으로 바꿀 때는 SoundBank 에셋에 같은 ID로 클립을 넣으면 합성음 대신 그걸 쓴다.
    /// 순서를 바꾸면 SoundBank 에셋의 직렬화 값이 어긋나니 새 ID는 맨 뒤에 추가할 것.
    /// </summary>
    public enum SoundId
    {
        CoinSmall,      // 동전 한두 개 "짤랑"
        CoinMedium,     // 동전 여러 개
        CoinLarge,      // 동전 한 움큼 + 반짝
        Register,       // 금전등록기 "철컥-띵"
        PlacePlastic,   // 진열 "톡" (플라스틱)
        PlacePaper,     // 진열 "탁" (종이·과자 봉지)
        PlaceGlass,     // 진열 "딸깍" (구슬)
        BoxRustle,      // 도매 상자 "부스럭+툭"
        DoorBell,       // 문 종 "딸랑"
        ItemPick,       // 손님이 물건 집음 (작게)
        Unhappy,        // 실망하고 나감 (하강음)
        Error,          // 오류 "삑"
        UiClick,        // UI 클릭
        SchoolBell,     // 하교 종 "딩동댕동"
        JingleNewDay,   // 새 날 아침
        ShutterOpen,    // 영업 시작 셔터 "드르륵"
        JingleClosing,  // 마감
        JingleSettle,   // 정산
    }

    /// <summary>BGM 트랙. 같은 트랙끼리는 이어서 재생, 다른 트랙이면 크로스페이드.</summary>
    public enum BgmTrack
    {
        Calm,   // 아침·마감·정산: 오르골 + 하프 (느림)
        Shop,   // 영업: 토이피아노 + 베이스 + 셰이커
        Rush,   // 하교 러시: 빠른 템포, 칩튠 리드 + 드럼
        Story,  // 이야기 컷신: 66 BPM 오르골 자장가 + 하프 + 패드 (docs/11)
    }
}
