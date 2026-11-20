using System;
using Munbanggu.Customers;
using Munbanggu.Data;
using Munbanggu.Shop;
using UnityEngine;

namespace Munbanggu.Core
{
    /// <summary>
    /// 게임 이벤트 허브. 사운드·연출·UI는 여기만 구독하고 게임 로직은 건드리지 않는다.
    /// 이벤트 추가는 OK, 기존 시그니처 변경은 금지 (여러 시스템이 구독 중).
    /// </summary>
    public static class GameEvents
    {
        public static event Action<ProductData> BoxBought;                       // 도매상에서 상자 구매
        public static event Action<ShelfSlot, ProductData, int> ItemsPlaced;     // 매대에 진열 (개수)
        public static event Action<Customer> CustomerEntered;                    // 손님 입장
        public static event Action<Customer, ShelfSlot> ItemPicked;              // 손님이 물건 집음
        public static event Action<Customer, int> CheckedOut;                    // 계산 완료 (가격)
        public static event Action<Customer> CustomerLeftUnhappy;                // 품절·대기로 그냥 감
        public static event Action<DayPhase> PhaseChanged;                       // 하루 단계 변경
        public static event Action RushStarted;                                  // 하교 러시 시작
        public static event Action InvalidAction;                                // 돈 부족, 재고 없음 등
        public static event Action UiClick;                                      // 일반 UI 버튼 클릭

        public static void RaiseBoxBought(ProductData p) => BoxBought?.Invoke(p);
        public static void RaiseItemsPlaced(ShelfSlot s, ProductData p, int n) => ItemsPlaced?.Invoke(s, p, n);
        public static void RaiseCustomerEntered(Customer c) => CustomerEntered?.Invoke(c);
        public static void RaiseItemPicked(Customer c, ShelfSlot s) => ItemPicked?.Invoke(c, s);
        public static void RaiseCheckedOut(Customer c, int price) => CheckedOut?.Invoke(c, price);
        public static void RaiseCustomerLeftUnhappy(Customer c) => CustomerLeftUnhappy?.Invoke(c);
        public static void RaisePhaseChanged(DayPhase p) => PhaseChanged?.Invoke(p);
        public static void RaiseRushStarted() => RushStarted?.Invoke();
        public static void RaiseInvalidAction() => InvalidAction?.Invoke();
        public static void RaiseUiClick() => UiClick?.Invoke();

        /// <summary>도메인 리로드를 꺼도 이전 Play의 구독이 남지 않게.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetAll()
        {
            BoxBought = null; ItemsPlaced = null; CustomerEntered = null; ItemPicked = null;
            CheckedOut = null; CustomerLeftUnhappy = null; PhaseChanged = null; RushStarted = null;
            InvalidAction = null; UiClick = null;
        }
    }
}
