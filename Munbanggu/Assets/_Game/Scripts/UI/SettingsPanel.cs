using System;
using Munbanggu.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace Munbanggu.UI
{
    /// <summary>
    /// 소리 설정 창 (배경음·효과음 슬라이더, 소리 끄기). HUD.Modal 카드 위에 만든다.
    /// 값은 AudioManager가 PlayerPrefs에 저장한다. 게임 시간은 멈추지 않는다
    /// (GameManager.Update가 매 프레임 Time.timeScale을 다시 적용하므로).
    /// </summary>
    public sealed class SettingsPanel
    {
        readonly GameObject modal;
        readonly UIJuice juice;
        Slider bgmSlider, sfxSlider;
        Text bgmPercent, sfxPercent, muteLabel;
        Image muteBg;
        bool building = true;

        SettingsPanel(GameObject modal, UIJuice juice)
        {
            this.modal = modal;
            this.juice = juice;
        }

        public bool IsOpen => modal != null && modal.activeSelf;

        /// <summary>모달 카드 안에 설정 내용을 채운다.</summary>
        public static SettingsPanel Build(GameObject modal, RectTransform card, UIJuice juice)
        {
            var p = new SettingsPanel(modal, juice);
            p.BuildCard(card);
            return p;
        }

        /// <summary>상단 바 옆 톱니바퀴 버튼 (모달보다 위 레이어에 둔다).</summary>
        public static Button BuildGearButton(Transform parent, Vector2 anchoredPos, float size, Action onClick)
        {
            var btn = UIKit.Button("Settings Gear", parent, "", 20, UIKit.Cream, UIKit.Ink, out _);
            var rt = (RectTransform)btn.transform;
            UIKit.Place(rt, new Vector2(1, 1), new Vector2(0.5f, 0.5f), anchoredPos, new Vector2(size, size));
            var bg = (Image)btn.targetGraphic;
            UIKit.Round(bg, size * 0.5f);
            UIKit.Ring(rt, UIKit.WithAlpha(UIKit.KraftDark, 0.7f), size * 0.5f);
            var icon = UIKit.Rect("Gear", rt);
            UIKit.Stretch(icon, size * 0.2f, size * 0.2f, size * 0.2f, size * 0.2f);
            var img = icon.gameObject.AddComponent<Image>();
            img.sprite = UIKit.GearSprite;
            img.color = UIKit.Ink;
            img.raycastTarget = false;
            btn.onClick.AddListener(() => onClick());
            return btn;
        }

        void BuildCard(RectTransform card)
        {
            var title = UIKit.Label("Title", card, "소리 설정", 60, UIKit.Ink, TextAnchor.MiddleLeft);
            UIKit.Top(title.rectTransform, 80, 40, 54);
            var sub = UIKit.Label("Sub", card, "바꾸면 바로 적용돼요", 27, UIKit.Muted);
            UIKit.Top(sub.rectTransform, 44, 122, 54);
            var divider = UIKit.DashLine(card, UIKit.WithAlpha(UIKit.KraftDark, 0.6f), 4);
            UIKit.Top(divider.rectTransform, 4, 178, 40);

            bgmSlider = BuildRow(card, "배경음", 210, out bgmPercent, v => { var a = AudioManager.Instance; if (a != null) a.SetBgmVolume(v); });
            sfxSlider = BuildRow(card, "효과음", 410, out sfxPercent, v => { var a = AudioManager.Instance; if (a != null) a.SetSfxVolume(v); });

            var mute = UIKit.Button("Mute", card, "", 40, UIKit.Green, Color.white, out muteLabel);
            UIKit.Top((RectTransform)mute.transform, 96, 622, 54);
            muteBg = (Image)mute.targetGraphic;
            mute.onClick.AddListener(() =>
            {
                var a = AudioManager.Instance;
                if (a != null) a.ToggleMuted();
                Refresh();
            });

            var close = UIKit.Button("Close", card, "닫기", 46, UIKit.Red, Color.white, out _);
            UIKit.Bottom((RectTransform)close.transform, 100, 40, 54);
            close.onClick.AddListener(Close);

            // 카드 밖(어두운 곳)을 탭해도 닫힘
            var dim = modal.AddComponent<Button>();
            dim.transition = Selectable.Transition.None;
            dim.onClick.AddListener(Close);

            building = false;
        }

        Slider BuildRow(RectTransform card, string name, float y, out Text percent, Action<float> onChanged)
        {
            var label = UIKit.Label(name + " Label", card, name, 42, UIKit.Ink, TextAnchor.MiddleLeft);
            UIKit.Top(label.rectTransform, 60, y, 54);
            percent = UIKit.Label(name + " Percent", card, "", 40, UIKit.Muted, TextAnchor.MiddleRight);
            UIKit.Top(percent.rectTransform, 60, y, 54);

            // 슬라이더: 막대 + 채움 + 큰 손잡이 (손가락으로 잡기 쉽게)
            var root = UIKit.Rect(name + " Slider", card);
            UIKit.Top(root, 90, y + 84, 54);
            var track = UIKit.Panel("Track", root, UIKit.WithAlpha(UIKit.KraftDark, 0.35f), false);
            UIKit.Round(track, 18);
            track.rectTransform.anchorMin = new Vector2(0, 0.5f);
            track.rectTransform.anchorMax = new Vector2(1, 0.5f);
            track.rectTransform.offsetMin = new Vector2(0, -18);
            track.rectTransform.offsetMax = new Vector2(0, 18);

            var fillArea = UIKit.Rect("Fill Area", root);
            fillArea.anchorMin = new Vector2(0, 0.5f);
            fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.offsetMin = new Vector2(0, -18);
            fillArea.offsetMax = new Vector2(0, 18);
            var fill = UIKit.Panel("Fill", fillArea, UIKit.Red, false);
            UIKit.Round(fill, 18);
            // 세로는 막대 전체, 가로는 Slider가 값에 맞춰 조절
            fill.rectTransform.anchorMin = Vector2.zero;
            fill.rectTransform.anchorMax = Vector2.one;
            fill.rectTransform.offsetMin = fill.rectTransform.offsetMax = Vector2.zero;

            var slide = UIKit.Rect("Handle Slide Area", root);
            UIKit.Stretch(slide, 38, 0, 38, 0);
            var handle = UIKit.CardPanel("Handle", slide, UIKit.Cream, 38, true, 5);
            handle.rectTransform.sizeDelta = new Vector2(76, 76);
            UIKit.Ring(handle.transform, UIKit.Red, 38);

            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.direction = Slider.Direction.LeftToRight;
            s.minValue = 0f;
            s.maxValue = 1f;
            s.transition = Selectable.Transition.None;
            var pct = percent;
            s.onValueChanged.AddListener(v =>
            {
                pct.text = Mathf.RoundToInt(v * 100f) + "%";
                if (!building) onChanged(v);
            });
            return s;
        }

        /// <summary>AudioManager의 현재 값으로 화면 갱신 (값을 다시 쓰지는 않는다).</summary>
        void Refresh()
        {
            var a = AudioManager.Instance;
            float bgm = a != null ? a.BgmVolume : 0.55f, sfx = a != null ? a.SfxVolume : 1f;
            bool muted = a != null && a.IsMuted;
            bgmSlider.SetValueWithoutNotify(bgm);
            sfxSlider.SetValueWithoutNotify(sfx);
            bgmPercent.text = Mathf.RoundToInt(bgm * 100f) + "%";
            sfxPercent.text = Mathf.RoundToInt(sfx * 100f) + "%";
            muteLabel.text = muted ? "소리 꺼짐 (탭하면 켜요)" : "소리 켜짐 (탭하면 꺼요)";
            muteBg.color = muted ? UIKit.Muted : UIKit.Green;
        }

        public void Open()
        {
            if (IsOpen) return;
            Refresh();
            modal.SetActive(true);
            if (juice != null) juice.Punch(0.06f);
        }

        public void Close() => modal.SetActive(false);

        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }
    }
}
