using Munbanggu.Core;
using Munbanggu.Data;
using Munbanggu.Shop;
using Munbanggu.UI;
using Munbanggu.Visual;
using UnityEngine;

namespace Munbanggu.Customers
{
    /// <summary>손님 종류. Normal 말고는 하루 몇 번 일어나는 돌발 사건 (DayEvents가 예약).</summary>
    public enum CustomerKind
    {
        Normal,
        Sneaky,  // 물건을 집어 계산 없이 문으로 살금살금 → 나가기 전에 탭하면 잡음
        Credit,  // 계산할 때 "외상 돼요?" → 해 줄지 선택
        Crying,  // 돈 잃어버려 우는 아이 → 탭하면 싼 물건 하나 주고 달램 → 친구들 데려옴
    }

    /// <summary>
    /// 어린이 손님. 입장 → 원하는 물건 매대로 → 고르기 → 계산 줄 → (탭하면 계산) → 퇴장.
    /// 원하는 물건이 없으면 일정 확률로 다른 물건을 사거나 그냥 간다.
    /// </summary>
    public class Customer : MonoBehaviour
    {
        enum State { Searching, ToShelf, Browsing, Queued, Leaving, Sneaking, AwaitCredit, Crying, Pulling }

        const float PullPatience = 9f;      // 뽑기판 앞에서 기다려 주는 시간

        const float Speed = 1.9f;
        const float SneakSpeedMul = 0.8f;   // 살금살금 (잡을 시간 3~4초)
        const float CryTime = 11f;          // 이 시간 동안 안 달래 주면 그냥 감
        const float CreditPayAnywayChance = 0.6f; // 외상 거절해도 그냥 사 갈 확률
        const float BrowseTime = 1.1f;
        const float SearchTime = 2.6f;   // 원하는 물건이 없을 때 두리번거리는 시간 (그 사이 진열하면 산다)
        const float LookTurnTime = 0.7f;
        const float SadLeaveFlash = 2.2f;
        const float ArriveDistance = 0.04f;
        const float UrgentRatio = 0.35f;

        GameManager gm;
        CustomerBubble bubble;
        Transform visual;
        State state;
        ProductData wanted;
        ShelfSlot target;
        float timer;
        float patience;
        float maxPatience;
        bool retried;
        bool moving;
        float bobSeed;
        string askText;
        Vector3 lookPoint;
        bool lookArrived;
        bool lookRight;
        float lookTurnTimer;
        Vector3[] route = System.Array.Empty<Vector3>();   // 문을 지나는 경유점 (들어올 때·나갈 때)
        int routeIdx;
        float doorX;
        float speedMul = 1f;

        public CustomerKind Kind { get; private set; }
        public ProductData Carried { get; private set; }
        public ProductData Wanted => wanted;
        public CustomerLook Look { get; private set; }
        /// <summary>단골이면 그 정의, 일반 손님이면 null.</summary>
        public RegularProfile Regular { get; private set; }

        public void Init(GameManager gameManager, ProductData wantedProduct, CustomerBubble customerBubble,
            RegularProfile regular = null, CustomerKind kind = CustomerKind.Normal)
        {
            gm = gameManager;
            wanted = wantedProduct;
            bubble = customerBubble;
            Regular = regular;
            Kind = regular != null ? CustomerKind.Normal : kind;
            bubble.onClick = OnBubbleClicked;
            bobSeed = Random.value * 10f;
            Look = regular != null
                ? new CustomerLook { seed = regular.Seed, isRegular = true, regularId = regular.id }
                : new CustomerLook { seed = Random.Range(0, int.MaxValue) };
            maxPatience = gm.queuePatience * (regular != null ? regular.patienceMultiplier : 1f);
            visual = CustomerVisual.Build(transform, gm.Materials, Look);

            // 문 열린 폭 안에서 x를 살짝 흩어 겹치지 않게. 들어올 때: 바깥 → 문 → 문 안쪽 → (기존 목적지)
            doorX = ShopLayout.DoorX + Random.Range(-ShopLayout.DoorSpread, ShopLayout.DoorSpread);
            Vector3 p0 = transform.position;
            p0.x = doorX;
            transform.position = p0;
            route = new[]
            {
                new Vector3(doorX, 0f, ShopLayout.DoorZ),
                new Vector3(doorX, 0f, ShopLayout.DoorInsideZ),
            };
            routeIdx = 0;
            GameEvents.RaiseCustomerEntered(this);

            if (Kind == CustomerKind.Crying)
            {
                state = State.Crying;
                timer = CryTime;
                lookArrived = false;
                lookPoint = new Vector3(0.6f + Random.Range(-0.6f, 0.6f), 0f, -1.4f + Random.Range(-0.4f, 0.4f));
                bubble.SetIcon(null);
                bubble.Show("엉엉… 돈을 잃어버렸어요", UIKit.BubbleSad, true);
                gm.ShowEventTip(Kind);
                return;
            }

            string ask = regular != null && wanted.id == regular.favoriteId && !string.IsNullOrEmpty(regular.askFavorite)
                ? regular.askFavorite
                : $"{wanted.displayName} 있어요?";
            if (regular != null) ask = $"{regular.name}: {ask}";
            askText = ask;
            bubble.SetIcon(ProductIcons.Get(wanted));
            bubble.Flash(ask, UIKit.BubbleNormal, regular != null ? 2f : 1.4f);
            PickTarget();
            // 가게 앞 뽑기판으로 가는 아이는 가게 안에 들어오지 않는다
            if (target != null && target.IsBoard) route = System.Array.Empty<Vector3>();
        }

        void PickTarget()
        {
            target = gm.FindSlotWith(wanted);
            if (target != null) { state = State.ToShelf; return; }

            // 매대에 없다 → 바로 나가지 않고 가게 안에서 두리번거린다.
            // 플레이어가 무엇을 찾는지 볼 수 있고, 그 사이 진열해 주면 산다.
            state = State.Searching;
            timer = SearchTime;
            lookArrived = false;
            lookTurnTimer = 0f;
            lookPoint = new Vector3(0.5f + Random.Range(-0.8f, 0.8f), 0f, 0.5f + Random.Range(-0.6f, 0.6f));
        }

        void UpdateSearching()
        {
            bubble.Show(askText, UIKit.BubbleNormal, false);

            // 찾는 사이 진열됐으면 바로 그쪽으로
            var found = gm.FindSlotWith(wanted);
            if (found != null)
            {
                target = found;
                state = State.ToShelf;
                bubble.Flash("앗, 있다!", UIKit.BubbleNormal, 1f);
                return;
            }

            if (!lookArrived)
            {
                if (!MoveTo(lookPoint)) return;
                lookArrived = true;
            }

            // 뒷벽 매대 ↔ 오른벽 매대를 번갈아 보며 두리번
            lookTurnTimer -= Time.deltaTime;
            if (lookTurnTimer <= 0f) { lookRight = !lookRight; lookTurnTimer = LookTurnTime; }
            Vector3 p = transform.position;
            Face(lookRight ? new Vector3(3.35f, 0f, p.z) : new Vector3(p.x, 0f, 3.35f));

            timer -= Time.deltaTime;
            if (timer <= 0f) GiveUpSearch();
        }

        void GiveUpSearch()
        {
            if (Random.value < gm.alternativeChance)
            {
                var alt = gm.RandomStockedSlot();
                if (alt != null)
                {
                    target = alt;
                    state = State.ToShelf;
                    bubble.SetIcon(ProductIcons.Get(alt.Product));
                    bubble.Flash($"{wanted.displayName} 없네… 이거라도 살래", UIKit.BubbleNormal, 1.8f);
                    return;
                }
            }

            gm.RecordLostNoStock();
            gm.Hud.Toast($"{wanted.displayName} 찾던 손님이 그냥 갔어요");
            LeaveUnhappy($"{wanted.displayName} 없네…");
        }

        void Update()
        {
            moving = false;
            if (routeIdx < route.Length)
            {
                if (MoveTo(route[routeIdx])) routeIdx++;
                if (routeIdx >= route.Length && (state == State.Leaving || state == State.Sneaking))
                {
                    if (state == State.Sneaking) gm.OnStolen(Carried); // 못 잡았다
                    gm.OnCustomerGone(this);
                    Destroy(gameObject);
                    return;
                }
                Bob();
                return;
            }
            switch (state)
            {
                case State.Searching:
                    UpdateSearching();
                    break;

                case State.ToShelf:
                    if (MoveTo(target.StandPoint))
                    {
                        state = State.Browsing;
                        timer = BrowseTime;
                    }
                    break;

                case State.Browsing:
                    Face(target.transform.position);
                    timer -= Time.deltaTime;
                    if (timer <= 0f) TryTake();
                    break;

                case State.Queued:
                    UpdateQueue();
                    break;

                case State.Leaving:
                case State.Sneaking:
                    // 경유점(문)을 다 지나면 위쪽 route 처리에서 사라진다
                    break;

                case State.AwaitCredit:
                    break; // 선택 창이 열린 동안은 게임 시간이 멈춤

                case State.Crying:
                    UpdateCrying();
                    break;

                case State.Pulling:
                    UpdatePull();
                    break;
            }
            Bob();
        }

        void TryTake()
        {
            var product = target.Product;
            if (product != null && target.TakeOne())
            {
                Carried = product;
                bubble.SetIcon(ProductIcons.Get(product));
                GameEvents.RaiseItemPicked(this, target);
                if (target.IsBoard)
                {
                    BeginPull();
                    return;
                }
                if (Kind == CustomerKind.Sneaky)
                {
                    BeginSneak();
                    return;
                }
                state = State.Queued;
                patience = maxPatience;
                gm.Layout.Queue.Join(this);
                bubble.Hide();
                return;
            }
            // 뽑기판이 그새 다 뽑혔다 → 가게 안까지 찾으러 들어가지 않고 그냥 감
            if (target.IsBoard)
            {
                gm.RecordLostNoStock();
                LeaveUnhappy("뽑기 다 떨어졌네…");
                return;
            }
            // 오는 사이 다 팔렸다 → 한 번만 다시 찾아봄 (다른 매대 또는 두리번)
            if (!retried)
            {
                retried = true;
                PickTarget();
                return;
            }
            gm.RecordLostNoStock();
            gm.Hud.Toast($"{(product != null ? product.displayName : wanted.displayName)} 다 팔려서 손님이 그냥 갔어요");
            LeaveUnhappy("다 팔렸잖아…");
        }

        void UpdateQueue()
        {
            var queue = gm.Layout.Queue;
            bool arrived = MoveTo(queue.SpotFor(this));
            if (arrived) Face(transform.position + queue.Facing);

            patience -= Time.deltaTime;
            float ratio = maxPatience > 0f ? patience / maxPatience : 0f;

            if (arrived && queue.Front == this)
            {
                string text = Kind == CustomerKind.Credit
                    ? "외상 돼요? 내일 갚을게요!"
                    : $"{Carried.displayName} {UIKit.Won(Carried.retailPrice)}";
                bubble.Show(text, ratio < UrgentRatio ? UIKit.BubbleUrgent : UIKit.BubbleReady, true, ratio);
            }
            else if (ratio < UrgentRatio)
                bubble.Show("빨리요~", UIKit.BubbleUrgent, false, ratio);
            else
                bubble.Hide();

            if (patience <= 0f)
            {
                queue.Leave(this);
                gm.ReturnUnpaid(Carried);
                Carried = null;
                LeaveUnhappy("너무 오래 걸려요!");
            }
        }

        void OnBubbleClicked()
        {
            if (state == State.Sneaking) { CatchSneak(); return; }
            if (state == State.Crying) { Comfort(); return; }
            if (state == State.Pulling) { DoPull(); return; }
            if (state != State.Queued || Carried == null || gm.Layout.Queue.Front != this) return;
            if (Kind == CustomerKind.Credit) { AskCredit(); return; }
            PayAndLeave(null);
        }

        /// <summary>계산하고 나간다. extraLine이 있으면 말풍선 첫 줄에.</summary>
        void PayAndLeave(string extraLine)
        {
            int price = Carried.retailPrice;
            int tip = gm.Checkout(this);
            gm.Layout.Queue.Leave(this);
            Carried = null;
            bubble.SetIcon(null);
            GameEvents.RaiseCheckedOut(this, price);
            string msg = $"+{UIKit.Won(price + tip)}";
            if (Regular != null && !string.IsNullOrEmpty(Regular.happyLine)) msg = $"{Regular.happyLine}\n{msg}";
            if (!string.IsNullOrEmpty(extraLine)) msg = $"{extraLine}\n{msg}";
            Leave(msg, UIKit.BubbleMoney);
        }

        // ───────────── 번호 뽑기판 ─────────────

        /// <summary>뽑기판 앞에서 "한 판!" → 탭해 주면 100원 받고 종이를 뜯는다.</summary>
        void BeginPull()
        {
            state = State.Pulling;
            patience = PullPatience;
        }

        void UpdatePull()
        {
            Face(target.transform.position);
            patience -= Time.deltaTime;
            float ratio = patience / PullPatience;
            bubble.Show($"뽑기 한 판! {UIKit.Won(Carried.retailPrice)}",
                ratio < UrgentRatio ? UIKit.BubbleUrgent : UIKit.BubbleReady, true, ratio);
            if (patience <= 0f)
            {
                target.Add(Carried, 1); // 안 뜯은 종이는 판에 그대로
                Carried = null;
                bubble.SetIcon(null);
                LeaveUnhappy("에이, 아무도 안 봐 주네");
            }
        }

        void DoPull()
        {
            if (Carried == null) return;
            int price = Carried.retailPrice;
            int tier = gm.PullTicket(this, target);
            Carried = null;
            bubble.SetIcon(null);
            GameEvents.RaiseCheckedOut(this, price);
            string result = tier switch
            {
                1 => "1등!!! 대왕 로봇이다!!",
                2 => "2등! 딱지 한 묶음!",
                3 => "3등! 예쁜 지우개!",
                4 => "4등! 사탕 하나",
                _ => "꽝… 다음엔 꼭!",
            };
            Leave($"{result}\n+{UIKit.Won(price)}", tier == 0 ? UIKit.BubbleNormal : UIKit.BubbleMoney, tier == 1 ? 3f : 2f);
        }

        // ───────────── 돌발 사건 ─────────────

        /// <summary>물건을 들고 계산 줄 대신 문으로 살금살금.</summary>
        void BeginSneak()
        {
            state = State.Sneaking;
            speedMul = SneakSpeedMul;
            route = ExitRoute();
            routeIdx = 0;
            bubble.Show("쉿… 몰래 나가야지", UIKit.BubbleSneak, true);
            gm.ShowEventTip(Kind);
        }

        /// <summary>문 나가기 전에 잡았다 → 제값 받고 보냄.</summary>
        void CatchSneak()
        {
            if (Carried == null) return;
            speedMul = 1f;
            int price = Carried.retailPrice;
            gm.CatchThief(this);
            Carried = null;
            bubble.SetIcon(null);
            GameEvents.RaiseCheckedOut(this, price);
            Leave($"헉! 죄송해요… 돈 낼게요\n+{UIKit.Won(price)}", UIKit.BubbleMoney, 1.8f);
        }

        void AskCredit()
        {
            state = State.AwaitCredit;
            gm.ShowEventTip(Kind);
            gm.Hud.ShowChoice("외상 장부",
                $"꼬마 손님이 {Josa.Eul(Carried.displayName)} 외상으로 달래요. ({UIKit.Won(Carried.retailPrice)})\n\n해 주면 내일 갚으러 올지도 몰라요. (고맙다고 덤까지?)\n안 해 주면 그냥 살 수도, 삐져서 갈 수도 있어요.",
                "외상 해 줄게", "안 돼",
                OnCreditDecision);
        }

        void OnCreditDecision(bool yes)
        {
            if (this == null || Carried == null) return;
            state = State.Queued; // 아래에서 바로 Leave로 바뀜
            if (yes)
            {
                gm.GiveCredit(this);
                gm.Layout.Queue.Leave(this);
                Carried = null;
                bubble.SetIcon(null);
                Leave("고맙습니다! 내일 꼭 갚을게요!", UIKit.BubbleMoney, 1.8f);
            }
            else if (Random.value < CreditPayAnywayChance)
            {
                PayAndLeave("칫… 그럼 그냥 낼게요");
            }
            else
            {
                gm.Layout.Queue.Leave(this);
                gm.ReturnItem(Carried);
                Carried = null;
                LeaveUnhappy("치사해… 안 살래요");
            }
        }

        void UpdateCrying()
        {
            if (!lookArrived)
            {
                if (MoveTo(lookPoint)) lookArrived = true;
            }
            else Face(lookPoint + Vector3.back); // 카메라 쪽을 보고 운다

            bubble.Show("엉엉… 돈을 잃어버렸어요", UIKit.BubbleSad, true, Mathf.Clamp01(timer / CryTime));
            timer -= Time.deltaTime;
            if (timer <= 0f) Leave("훌쩍…", UIKit.BubbleSad, 1.6f);
        }

        /// <summary>달래 주기: 제일 싼 물건 하나를 공짜로 → 친구들을 데려온다.</summary>
        void Comfort()
        {
            var gift = gm.GiveFreebie();
            if (gift == null)
            {
                gm.Hud.Toast("줄 수 있는 물건이 매대에 없어요");
                GameEvents.RaiseInvalidAction();
                return;
            }
            bubble.SetIcon(ProductIcons.Get(gift));
            gm.ScheduleFriends(Random.Range(2, 4));
            Leave("고맙습니다!! 친구들 데려올게요!", UIKit.BubbleMoney, 2f);
        }

        void LeaveUnhappy(string message)
        {
            if (Regular != null)
            {
                if (!string.IsNullOrEmpty(Regular.sadLine)) message = Regular.sadLine;
                gm.OnRegularUnhappy(this);
            }
            Leave(message, UIKit.BubbleSad, SadLeaveFlash);
            GameEvents.RaiseCustomerLeftUnhappy(this);
        }

        void Leave(string message, Color color, float seconds = 1.2f)
        {
            state = State.Leaving;
            speedMul = 1f;
            route = ExitRoute();
            routeIdx = 0;
            bubble.Flash(message, color, seconds);
        }

        /// <summary>나가는 길: 현재 위치 → 문 안쪽 → 문 → 바깥 → 사라짐 (계산대·뽑기 기계는 문 반대쪽 -X라 직선으로 안 걸림).</summary>
        Vector3[] ExitRoute()
        {
            Vector3 exit = gm.Layout.ExitPoint;
            exit.x = doorX;
            // 이미 가게 밖(뽑기판 앞 등)이면 문을 거치지 않고 바로 길로
            if (transform.position.z < ShopLayout.DoorZ - 0.05f) return new[] { exit };
            return new[]
            {
                new Vector3(doorX, 0f, ShopLayout.DoorInsideZ),
                new Vector3(doorX, 0f, ShopLayout.DoorZ),
                new Vector3(doorX, 0f, ShopLayout.DoorOutsideZ),
                exit,
            };
        }

        // ───────────── 이동·연출 ─────────────

        bool MoveTo(Vector3 dest)
        {
            dest.y = 0f;
            Vector3 pos = transform.position;
            Vector3 to = dest - pos;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist <= ArriveDistance) return true;

            float step = Speed * speedMul * Time.deltaTime;
            transform.position = step >= dist ? dest : pos + to / dist * step;
            Face(dest);
            moving = true;
            return false;
        }

        void Face(Vector3 point)
        {
            Vector3 dir = point - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 12f * Time.deltaTime);
        }

        void Bob()
        {
            float y = moving ? Mathf.Abs(Mathf.Sin(Time.time * 14f + bobSeed)) * 0.06f : 0f;
            visual.localPosition = new Vector3(0f, y, 0f);
        }

        void OnDestroy()
        {
            if (bubble != null) Destroy(bubble.gameObject);
        }
    }
}
