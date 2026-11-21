using System;
using Munbanggu.Core;
using Munbanggu.Shop;
using Munbanggu.UI;
using Munbanggu.Visual;
using UnityEngine;

namespace Munbanggu.Customers
{
    /// <summary>
    /// 가게 안의 직원 한 명 (보이는 몸 + 말풍선). 가게 안을 걸어 다니며 일한다:
    /// GoTo(자리, 도착하면 할 일) → 그 자리에 머묾 / GoHome() → 쉬는 자리로.
    /// 길은 ShopNav로 찾아서 진열대를 돌아 다닌다. 일 자체의 효과는 GameManager가 처리.
    /// 앞치마 색으로 맡은 일을 구분한다.
    /// </summary>
    public class StaffAgent : MonoBehaviour
    {
        const float Speed = 2.3f;

        public StaffMember Member { get; private set; }
        Vector3 home;
        Quaternion homeRot;
        CustomerBubble bubble;
        Transform visual;
        Vector3 target;
        Vector3? lookAt;
        Action onArrive;
        bool busy, moving;
        float bobSeed;
        readonly NavWalker walker = new NavWalker();

        static readonly MeshKit kit = new MeshKit();

        public static Color ApronColor(StaffJob job) => job switch
        {
            StaffJob.Cashier => new Color(0.30f, 0.62f, 0.38f),
            StaffJob.Stocker => new Color(0.95f, 0.62f, 0.2f),
            StaffJob.Guard => new Color(0.25f, 0.35f, 0.65f),
            _ => new Color(0.86f, 0.38f, 0.58f),
        };

        /// <summary>맡은 일·순번별 쉬는 자리 (계산대 옆 / 가게 가운데 뒤 / 문 안쪽 / 계산대 옆).</summary>
        public static (Vector3 pos, float yaw) Post(StaffJob job, int order)
        {
            // 같은 일을 맡은 직원이 3명 이상이면(가게 증축 후) 세 번째 자리부터 조금씩 옆으로
            int i = Mathf.Clamp(order, 0, 2);
            var (pos, yaw) = job switch
            {
                StaffJob.Cashier => i == 0 ? (new Vector3(-3.62f, 0f, -0.45f), 110f) : i == 1 ? (new Vector3(-3.62f, 0f, -2.05f), 70f) : (new Vector3(-1.0f, 0f, 0.2f), 200f),
                StaffJob.Stocker => i == 0 ? (new Vector3(0.6f, 0f, 1.7f), 200f) : i == 1 ? (new Vector3(-0.8f, 0f, 1.7f), 160f) : (new Vector3(2.0f, 0f, 0.9f), 230f),
                StaffJob.Guard => i == 0 ? (new Vector3(2.45f, 0f, -3.15f), 230f) : i == 1 ? (new Vector3(0.1f, 0f, -3.15f), 160f) : (new Vector3(2.2f, 0f, -1.6f), 250f),
                _ => i == 0 ? (new Vector3(-2.75f, 0f, 0.45f), 120f) : i == 1 ? (new Vector3(-2.75f, 0f, 1.05f), 120f) : (new Vector3(-2.75f, 0f, 1.65f), 120f),
            };
            if (order > 2) pos += new Vector3(0.4f * (order - 2), 0f, 0f);
            return (pos, yaw);
        }

        public static StaffAgent Create(Transform parent, MaterialCache mats, HUD hud, StaffMember m, int order)
        {
            var (pos, yaw) = Post(m.job, order);
            var go = new GameObject($"Staff {m.name}");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var a = go.AddComponent<StaffAgent>();
            a.Member = m;
            a.home = go.transform.position;
            a.homeRot = go.transform.rotation;
            a.bobSeed = UnityEngine.Random.value * 10f;
            a.visual = CustomerVisual.Build(go.transform, mats, new CustomerLook { seed = m.seed });

            // 앞치마 (맡은 일 색) + 목끈
            kit.Clear();
            var apron = ApronColor(m.job);
            kit.Box(new Vector3(0f, 0.38f, 0.135f), new Vector3(0.2f, 0.25f, 0.02f), apron, apron);
            kit.FlatBox(new Vector3(0f, 0.53f, 0.1f), new Vector3(0.18f, 0.025f, 0.06f), apron);
            kit.FlatBox(new Vector3(0f, 0.4f, 0.147f), new Vector3(0.09f, 0.05f, 0.004f), Color.white); // 명찰
            kit.Build("Apron", a.visual, mats.VertexColor);

            if (hud != null) a.bubble = hud.CreateBubble(go.transform);
            return a;
        }

        /// <summary>잠깐 말풍선.</summary>
        public void Say(string text, Color color, float seconds = 1.2f)
        {
            if (bubble != null) bubble.Flash(text, color, seconds);
        }

        /// <summary>걸어가는 중인지.</summary>
        public bool Busy => busy;
        public bool AtHome => !busy && (transform.position - home).sqrMagnitude < 0.04f;
        public Vector3 Position => transform.position;

        /// <summary>point까지 걸어가서 arrive()를 부르고 그 자리에 머문다. face가 있으면 도착 후 그쪽을 본다.</summary>
        public void GoTo(Vector3 point, Action arrive, Vector3? face = null)
        {
            busy = true;
            target = point;
            onArrive = arrive;
            lookAt = face;
            walker.Reset();
        }

        /// <summary>쉬는 자리로 돌아간다.</summary>
        public void GoHome()
        {
            if (AtHome) return;
            GoTo(home, null);
            lookAt = null;
            goingHome = true;
        }
        bool goingHome;

        /// <summary>point까지 갔다가 arrive() 후 쉬는 자리로 돌아온다.</summary>
        public void Errand(Vector3 point, Action arrive) => GoTo(point, () => { arrive?.Invoke(); GoHome(); });

        void Update()
        {
            moving = false;
            if (busy)
            {
                if (MoveTo(target))
                {
                    busy = false;
                    var cb = onArrive;
                    onArrive = null;
                    bool wasHome = goingHome;
                    goingHome = false;
                    cb?.Invoke();
                    if (wasHome && !busy) lookAt = null;
                }
            }
            else
            {
                Quaternion want = homeRot;
                if (lookAt.HasValue)
                {
                    Vector3 d = lookAt.Value - transform.position; d.y = 0f;
                    if (d.sqrMagnitude > 1e-4f) want = Quaternion.LookRotation(d);
                }
                else if (!AtHome)
                    want = transform.rotation;
                transform.rotation = Quaternion.Slerp(transform.rotation, want, 6f * Time.deltaTime);
            }
            if (visual != null)
                visual.localPosition = new Vector3(0f, moving ? Mathf.Abs(Mathf.Sin(Time.time * 14f + bobSeed)) * 0.06f : 0f, 0f);
        }

        bool MoveTo(Vector3 dest)
        {
            dest.y = 0f;
            Vector3 pos = transform.position;
            Vector3 wp = walker.Next(pos, dest);
            Vector3 to = wp - pos;
            to.y = 0f;
            float dist = to.magnitude;
            if (walker.OnLast && dist <= 0.05f) return true;
            float step = Speed * Time.deltaTime;
            transform.position = step >= dist ? new Vector3(wp.x, pos.y, wp.z) : pos + to / Mathf.Max(dist, 1e-4f) * step;
            if (to.sqrMagnitude > 1e-4f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to), 12f * Time.deltaTime);
            moving = true;
            return false;
        }

        void OnDestroy()
        {
            if (bubble != null) Destroy(bubble.gameObject);
        }
    }
}
