using System;
using System.Collections.Generic;
using UnityEngine;

namespace Munbanggu.Audio
{
    /// <summary>
    /// 실제 녹음 클립 지정용 (선택). 여기 클립이 들어 있는 ID는 합성 대신 이 클립을 쓴다.
    /// 사용법: Assets/_Game/Audio/Resources/SoundBank.asset 으로 만들면 AudioManager가 자동으로 읽는다.
    /// (메뉴: Assets > Create > 문방구 > 사운드 뱅크)
    /// </summary>
    [CreateAssetMenu(menuName = "문방구/사운드 뱅크", fileName = "SoundBank")]
    public class SoundBank : ScriptableObject
    {
        public const string ResourcePath = "SoundBank";

        [Serializable]
        public class SfxEntry
        {
            public SoundId id;
            [Tooltip("여러 개면 재생할 때마다 번갈아 사용")]
            public AudioClip[] clips;
            [Range(0f, 2f)] public float volume = 1f;
        }

        [Serializable]
        public class BgmEntry
        {
            public BgmTrack track;
            public AudioClip clip;
        }

        public List<SfxEntry> sfx = new List<SfxEntry>();
        public List<BgmEntry> bgm = new List<BgmEntry>();

        public SfxEntry FindSfx(SoundId id)
        {
            foreach (var e in sfx)
                if (e != null && e.id == id && e.clips != null && e.clips.Length > 0 && e.clips[0] != null)
                    return e;
            return null;
        }

        public AudioClip FindBgm(BgmTrack track)
        {
            foreach (var e in bgm)
                if (e != null && e.track == track && e.clip != null)
                    return e.clip;
            return null;
        }
    }
}
