using System.Collections.Generic;
using Munbanggu.Shop;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Munbanggu.Core
{
    /// <summary>화면 탭(마우스/터치)을 매대 탭으로 변환. UI 위 탭은 무시.</summary>
    public class TapInput : MonoBehaviour
    {
        GameManager gm;
        readonly RaycastHit[] hits = new RaycastHit[16];
        static readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        public void Init(GameManager gameManager) => gm = gameManager;

        void Update()
        {
            var pointer = Pointer.current;
            if (gm == null || pointer == null || !pointer.press.wasPressedThisFrame) return;

            Vector2 pos = pointer.position.ReadValue();
            if (IsOverUI(pos)) return;

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
            if (best != null) gm.OnSlotTapped(best);
        }

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
