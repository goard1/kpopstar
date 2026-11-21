using System;
using Munbanggu.Core;
using Munbanggu.UI;
using Munbanggu.Visual;
using UnityEngine;

namespace Munbanggu.Customers
{
    /// <summary>
    /// 가게 안의 직원 한 명 (보이는 몸 + 말풍선). 평소엔 맡은 자리에 서 있고,
    /// 심부름(진열 등)이 있으면 그 자리로 걸어갔다가 돌아온다. 일 자체의 효과는 GameManager가 처리.
    /// 앞치마 색으로 맡은 일을 구분한다.
    /// </summary>
    public class StaffAgent : MonoBehaviour
    {
        const float Speed = 2.2f;

        public StaffMember Member { get; private set; }
        Vector3 home;
        Quaternion homeRot;
        CustomerBubble bubble;
        Transform visual;
        Vector3 errand;
        Action onArrive;
        bool busy, returning, moving;
        float bobSeed;

        static readonly MeshKit kit = new MeshKit();

        public static Color ApronColor(StaffJob job) => job switch
        {
            StaffJob.Cashier => new Color(0.30f, 0.62f, 0.38f),
            StaffJob.Stocker => new Color(0.95f, 0.62f, 0.2f),
            StaffJob.Guard => new Color(0.25f, 0.35f, 0.65f),
            _ => new Color(0.86f, 0.38f, 0.58f),
        };

        /// <summary>맡은 일·순번별 서 있는 자리 (계산대 뒤 / 가게 가운데 뒤 / 문 안쪽 / 계산대 옆).</summary>
        public static (Vector3 pos, float yaw) Post(StaffJob job, int order)
        {
            // 같은 일을 맡은 직원이 3명 이상이면(가게 증축 후) 세 번째 자리부터 조금씩 옆으로
            int i = Mathf.Clamp(order, 0, 2);
            var (pos, yaw) = job switch
            {
                StaffJob.Cashier => i == 0 ? (new Vector3(-3.62f, 0f, -0.45f), 110f) : i == 1 ? (new Vector3(-3.62f, 0f, -2.05f), 70f) : (new Vector3(-3.62f, 0f, -1.25f), 90f),
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

        public bool Busy => busy;

        /// <summary>point까지 걸어가서 arrive()를 부르고 제자리로 돌아온다 (옆칸이면 경유점을 지나).</summary>
        public void Errand(Vector3 point, Action arrive)
        {
            busy = true;
            returning = false;
            errand = point;
            onArrive = arrive;
            path.Clear();
            Munbanggu.Shop.ShopLayout.AppendPath(path, transform.position, point);
        }

        readonly System.Collections.Generic.List<Vector3> path = new System.Collections.Generic.List<Vector3>();

        void Update()
        {
            moving = false;
            if (busy)
            {
                if (path.Count == 0) Munbanggu.Shop.ShopLayout.AppendPath(path, transform.position, returning ? home : errand);
                if (MoveTo(path[0]))
                {
                    path.RemoveAt(0);
                    if (path.Count > 0) { /* 경유점 하나 지남 */ }
                    else if (!returning)
                    {
                        var cb = onArrive;
                        onArrive = null;
                        cb?.Invoke();
                        returning = true;
                        Munbanggu.Shop.ShopLayout.AppendPath(path, errand, home);
                    }
                    else
                    {
                        busy = false;
                        returning = false;
                    }
                }
            }
            else
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, homeRot, 6f * Time.deltaTime);
            }
            if (visual != null)
                visual.localPosition = new Vector3(0f, moving ? Mathf.Abs(Mathf.Sin(Time.time * 14f + bobSeed)) * 0.06f : 0f, 0f);
        }

        bool MoveTo(Vector3 dest)
        {
            dest.y = 0f;
            Vector3 pos = transform.position;
            Vector3 to = dest - pos;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist <= 0.04f) return true;
            float step = Speed * Time.deltaTime;
            transform.position = step >= dist ? dest : pos + to / dist * step;
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
