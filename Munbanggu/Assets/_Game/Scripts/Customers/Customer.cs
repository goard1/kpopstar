using Munbanggu.Core;
using Munbanggu.Data;
using Munbanggu.Shop;
using Munbanggu.UI;
using Munbanggu.Visual;
using UnityEngine;

namespace Munbanggu.Customers
{
    /// <summary>
    /// 어린이 손님. 입장 → 원하는 물건 매대로 → 고르기 → 계산 줄 → (탭하면 계산) → 퇴장.
    /// 원하는 물건이 없으면 일정 확률로 다른 물건을 사거나 그냥 간다.
    /// </summary>
    public class Customer : MonoBehaviour
    {
        enum State { Searching, ToShelf, Browsing, Queued, Leaving }

        const float Speed = 1.9f;
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

        public ProductData Carried { get; private set; }
        public ProductData Wanted => wanted;
        public CustomerLook Look { get; private set; }
        /// <summary>단골이면 그 정의, 일반 손님이면 null.</summary>
        public RegularProfile Regular { get; private set; }

        public void Init(GameManager gameManager, ProductData wantedProduct, CustomerBubble customerBubble,
            RegularProfile regular = null)
        {
            gm = gameManager;
            wanted = wantedProduct;
            bubble = customerBubble;
            Regular = regular;
            bubble.onClick = OnBubbleClicked;
            bobSeed = Random.value * 10f;
            Look = regular != null
                ? new CustomerLook { seed = regular.Seed, isRegular = true, regularId = regular.id }
                : new CustomerLook { seed = Random.Range(0, int.MaxValue) };
            maxPatience = gm.queuePatience * (regular != null ? regular.patienceMultiplier : 1f);
            visual = CustomerVisual.Build(transform, gm.Materials, Look);
            GameEvents.RaiseCustomerEntered(this);

            string ask = regular != null && wanted.id == regular.favoriteId && !string.IsNullOrEmpty(regular.askFavorite)
                ? regular.askFavorite
                : $"{wanted.displayName} 있어요?";
            if (regular != null) ask = $"{regular.name}: {ask}";
            askText = ask;
            bubble.SetIcon(ProductIcons.Get(wanted));
            bubble.Flash(ask, UIKit.BubbleNormal, regular != null ? 2f : 1.4f);
            PickTarget();
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
                    if (MoveTo(gm.Layout.ExitPoint))
                    {
                        gm.OnCustomerGone(this);
                        Destroy(gameObject);
                    }
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
                state = State.Queued;
                patience = maxPatience;
                gm.Layout.Queue.Join(this);
                bubble.Hide();
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
                bubble.Show($"{Carried.displayName} {UIKit.Won(Carried.retailPrice)}",
                    ratio < UrgentRatio ? UIKit.BubbleUrgent : UIKit.BubbleReady, true, ratio);
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
            if (state != State.Queued || Carried == null || gm.Layout.Queue.Front != this) return;
            int price = Carried.retailPrice;
            int tip = gm.Checkout(this);
            gm.Layout.Queue.Leave(this);
            Carried = null;
            bubble.SetIcon(null);
            GameEvents.RaiseCheckedOut(this, price);
            string msg = $"+{UIKit.Won(price + tip)}";
            if (Regular != null && !string.IsNullOrEmpty(Regular.happyLine)) msg = $"{Regular.happyLine}\n{msg}";
            Leave(msg, UIKit.BubbleMoney);
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
            bubble.Flash(message, color, seconds);
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

            float step = Speed * Time.deltaTime;
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
