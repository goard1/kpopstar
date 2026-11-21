using System.Collections.Generic;
using Munbanggu.Shop;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Munbanggu.Core
{
    /// <summary>
    /// 가게 화면 입력: 탭 → 매대 탭, 두 손가락 → 확대/축소, 한 손가락 끌기 → 화면 이동 (확대했을 때),
    /// 빈 곳 두 번 탭 → 원래 크기. PC에서는 마우스 휠로 확대.
    /// 탭은 손을 뗄 때 판정 (끌거나 확대한 동작은 탭으로 치지 않음). UI 위에서 시작한 입력은 무시.
    /// </summary>
    public class TapInput : MonoBehaviour
    {
        const float DragThresholdInch = 0.08f;   // 이만큼 넘게 움직이면 탭이 아니라 끌기
        const float DoubleTapTime = 0.3f;

        GameManager gm;
        ShopCamera shopCam;
        readonly RaycastHit[] hits = new RaycastHit[16];
        static readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        bool tracking;        // 가게 화면에서 누르기 시작함
        bool dragging;        // 탭이 아니라 끌기로 판정됨
        bool pinched;         // 이번 누름 동안 두 손가락을 썼음
        Vector2 pressPos, lastPos;
        float lastPinchDist;
        float lastEmptyTapTime = -1f;

        public void Init(GameManager gameManager)
        {
            gm = gameManager;
            shopCam = gm.Layout.Camera.GetComponent<ShopCamera>();
        }

        void Update()
        {
            if (gm == null) return;
            if (HandlePinch()) return;

            var pointer = Pointer.current;
            if (pointer == null) return;
            Vector2 pos = pointer.position.ReadValue();

            if (pointer.press.wasPressedThisFrame)
            {
                tracking = !IsOverUI(pos);
                dragging = pinched = false;
                pressPos = lastPos = pos;
            }
            else if (tracking && pointer.press.isPressed)
            {
                if (!dragging && (pos - pressPos).magnitude > DragThreshold) dragging = true;
                if (dragging && !pinched && shopCam != null) shopCam.PanBy(pos - lastPos);
                lastPos = pos;
            }

            if (tracking && pointer.press.wasReleasedThisFrame)
            {
                tracking = false;
                if (!dragging && !pinched) Tap(pressPos);
            }

            // PC: 마우스 휠 확대
            var mouse = Mouse.current;
            if (mouse != null && shopCam != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f && !IsOverUI(mouse.position.ReadValue()))
                    shopCam.ZoomAt(mouse.position.ReadValue(), scroll > 0 ? 1.12f : 1f / 1.12f);
            }
        }

        /// <summary>두 손가락 확대/축소. 두 손가락이 눌린 동안 true.</summary>
        bool HandlePinch()
        {
            var ts = Touchscreen.current;
            if (ts == null || shopCam == null) return false;

            int found = 0;
            Vector2 a = default, b = default;
            foreach (var t in ts.touches)
            {
                if (!t.press.isPressed) continue;
                if (found == 0) a = t.position.ReadValue();
                else if (found == 1) b = t.position.ReadValue();
                found++;
            }
            if (found < 2)
            {
                lastPinchDist = 0f;
                return false;
            }
            if (!tracking)
            {
                // 두 손가락이 같은 프레임에 닿았으면 여기서 시작. UI 위에서 시작했으면 무시.
                if (lastPinchDist > 0f || IsOverUI(a) || IsOverUI(b)) return true;
                tracking = true;
                dragging = false;
            }

            pinched = true;
            float dist = Vector2.Distance(a, b);
            Vector2 mid = (a + b) * 0.5f;
            if (lastPinchDist > 1f)
            {
                shopCam.ZoomAt(mid, dist / lastPinchDist);
                shopCam.PanBy(mid - lastPos);
            }
            lastPinchDist = dist;
            lastPos = mid;
            return true;
        }

        void Tap(Vector2 pos)
        {
            // 진열대 옮기기 모드면 HUD가 먼저 처리
            if (gm.Hud != null && gm.Hud.HandleWorldTap(pos)) return;
            TapWorld(pos);
        }

        void TapWorld(Vector2 pos)
        {
            var ray = gm.Layout.Camera.ScreenPointToRay(pos);
            int n = Physics.RaycastNonAlloc(ray, hits, 200f);
            ShelfSlot best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var slot = hits[i].collider.GetComponentInParent<ShelfSlot>();
                if (slot != null && hits[i].distance < bestDist)
                {
                    best = slot;
                    bestDist = hits[i].distance;
                }
            }
            if (best != null)
            {
                gm.OnSlotTapped(best);
                return;
            }

            // 빈 곳 두 번 탭 → 원래 크기로
            float now = Time.unscaledTime;
            if (now - lastEmptyTapTime < DoubleTapTime && shopCam != null)
            {
                shopCam.ResetView();
                lastEmptyTapTime = -1f;
            }
            else lastEmptyTapTime = now;
        }

        static float DragThreshold => (Screen.dpi > 0 ? Screen.dpi : 320f) * DragThresholdInch;

        static bool IsOverUI(Vector2 pos)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            uiHits.Clear();
            es.RaycastAll(new PointerEventData(es) { position = pos }, uiHits);
            return uiHits.Count > 0;
        }
    }
}
