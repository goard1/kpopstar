using System;
using System.Collections;
using System.Threading.Tasks;
using Munbanggu.Core;
using Munbanggu.Customers;
using Munbanggu.Data;
using Munbanggu.Shop;
using UnityEngine;

namespace Munbanggu.Audio
{
    /// <summary>
    /// 사운드 총괄 (싱글턴). GameEvents만 구독해서 효과음·BGM을 낸다 — 게임 로직은 모름.
    /// - 효과음: AudioSource 풀 8개, 변주 클립 + 피치/볼륨 랜덤.
    /// - BGM: 소스 2개로 크로스페이드, 징글이 나올 때 살짝 줄임(덕킹).
    /// - 클립: SoundBank(Resources/SoundBank)에 녹음이 있으면 그걸, 없으면 시작 시 백그라운드 스레드에서 합성.
    /// - 볼륨/음소거는 PlayerPrefs에 저장 (설정 UI는 Set* 함수만 부르면 됨).
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        /// <summary>볼륨/음소거가 바뀌면 호출 (설정 UI 갱신용).</summary>
        public static event Action SettingsChanged;

        const int PoolSize = 8;
        const int MaxPlaceTaps = 6;
        const int SetDataChunk = 32768;
        const string KeyMaster = "audio.master";
        const string KeyBgm = "audio.bgm";
        const string KeySfx = "audio.sfx";
        const string KeyMuted = "audio.muted";

        public float MasterVolume { get; private set; } = 1f;
        public float BgmVolume { get; private set; } = 0.55f;
        public float SfxVolume { get; private set; } = 1f;
        public bool IsMuted { get; private set; }
        /// <summary>효과음 준비 완료 (BGM은 트랙별로 이어서 준비됨).</summary>
        public bool IsReady { get; private set; }

        AudioSource[] pool;
        float[] busyUntil;

        readonly AudioSource[] bgm = new AudioSource[2];
        readonly float[] bgmLevel = new float[2];
        readonly float[] bgmTarget = new float[2];
        readonly float[] bgmGain = { 1f, 1f };
        readonly float[] bgmGainTarget = { 1f, 1f };
        int bgmCurrent;
        float bgmFadeSpeed = 1f;
        bool hasWanted;
        BgmTrack wantedTrack;
        float wantedGain = 1f, wantedFade = 1.5f;

        float duck = 1f, duckTarget = 1f, duckUntil;

        AudioClip[][] sfxClips;
        float[] sfxBankVolume;
        int[] lastVariant;
        float[] lastPlayTime;
        float[] minInterval;
        AudioClip[] bgmClips;
        SoundBank bank;

        // 준비 전에 들어온 효과음 하나 (시작 직후 아침 징글 등)
        bool hasPending;
        SoundId pendingId;
        float pendingTime;

        GameManager game;
        bool rushMusic;
        float pollTimer;

        // ───────────── 생성 ─────────────

        /// <summary>Bootstrap에서 한 번 호출. 이미 있으면 그걸 돌려준다.</summary>
        public static AudioManager Create(Transform parent = null)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("AudioManager");
            if (parent != null) go.transform.SetParent(parent, false);
            return go.AddComponent<AudioManager>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Instance = null;
            SettingsChanged = null;
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            LoadSettings();
            int n = Enum.GetValues(typeof(SoundId)).Length;
            sfxClips = new AudioClip[n][];
            sfxBankVolume = new float[n];
            lastVariant = new int[n];
            lastPlayTime = new float[n];
            minInterval = new float[n];
            for (int i = 0; i < n; i++) { sfxBankVolume[i] = 1f; lastVariant[i] = -1; lastPlayTime[i] = -10f; }
            minInterval[(int)SoundId.UiClick] = 0.03f;
            minInterval[(int)SoundId.Error] = 0.12f;
            minInterval[(int)SoundId.ItemPick] = 0.04f;
            minInterval[(int)SoundId.DoorBell] = 0.15f;
            minInterval[(int)SoundId.Unhappy] = 0.2f;
            bgmClips = new AudioClip[Enum.GetValues(typeof(BgmTrack)).Length];

            pool = new AudioSource[PoolSize];
            busyUntil = new float[PoolSize];
            for (int i = 0; i < PoolSize; i++) pool[i] = NewSource(128);
            for (int i = 0; i < 2; i++)
            {
                bgm[i] = NewSource(0);
                bgm[i].loop = true;
            }
            ApplyMute();

            bank = Resources.Load<SoundBank>(SoundBank.ResourcePath);

            GameEvents.BoxBought += OnBoxBought;
            GameEvents.ItemsPlaced += OnItemsPlaced;
            GameEvents.CustomerEntered += OnCustomerEntered;
            GameEvents.ItemPicked += OnItemPicked;
            GameEvents.CheckedOut += OnCheckedOut;
            GameEvents.CustomerLeftUnhappy += OnCustomerLeftUnhappy;
            GameEvents.PhaseChanged += OnPhaseChanged;
            GameEvents.RushStarted += OnRushStarted;
            GameEvents.InvalidAction += OnInvalidAction;
            GameEvents.UiClick += OnUiClick;
        }

        void Start()
        {
            // 카메라를 코드로 만들어서 AudioListener가 없을 수 있음
            if (FindAnyObjectByType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            game = FindAnyObjectByType<GameManager>();
            StartCoroutine(PrepareClips());
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            GameEvents.BoxBought -= OnBoxBought;
            GameEvents.ItemsPlaced -= OnItemsPlaced;
            GameEvents.CustomerEntered -= OnCustomerEntered;
            GameEvents.ItemPicked -= OnItemPicked;
            GameEvents.CheckedOut -= OnCheckedOut;
            GameEvents.CustomerLeftUnhappy -= OnCustomerLeftUnhappy;
            GameEvents.PhaseChanged -= OnPhaseChanged;
            GameEvents.RushStarted -= OnRushStarted;
            GameEvents.InvalidAction -= OnInvalidAction;
            GameEvents.UiClick -= OnUiClick;
            PlayerPrefs.Save();
            Instance = null;
        }

        void OnApplicationPause(bool paused)
        {
            if (paused) PlayerPrefs.Save();
        }

        AudioSource NewSource(int priority)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.dopplerLevel = 0f;
            s.priority = priority;
            return s;
        }

        // ───────────── 클립 준비 ─────────────

        IEnumerator PrepareClips()
        {
            // 1) 효과음: 녹음 클립 우선, 나머지는 한 번에 백그라운드 합성
            var ids = (SoundId[])Enum.GetValues(typeof(SoundId));
            var synth = new float[ids.Length][][];
            for (int i = 0; i < ids.Length; i++)
            {
                var entry = bank != null ? bank.FindSfx(ids[i]) : null;
                if (entry != null)
                {
                    sfxClips[(int)ids[i]] = Array.FindAll(entry.clips, c => c != null);
                    sfxBankVolume[(int)ids[i]] = entry.volume;
                }
                else synth[i] = new float[SynthSfx.VariantCount(ids[i])][];
            }
            int sfxRate = SynthSfx.DefaultSampleRate;
            var sfxTask = Task.Run(() =>
            {
                for (int i = 0; i < ids.Length; i++)
                    if (synth[i] != null)
                        for (int v = 0; v < synth[i].Length; v++)
                            synth[i][v] = SynthSfx.Generate(ids[i], v, sfxRate);
            });
            while (!sfxTask.IsCompleted) yield return null;
            if (sfxTask.IsFaulted) Debug.LogException(sfxTask.Exception);
            else
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    if (synth[i] == null) continue;
                    var clips = new AudioClip[synth[i].Length];
                    for (int v = 0; v < clips.Length; v++)
                    {
                        var data = synth[i][v];
                        clips[v] = AudioClip.Create($"SFX_{ids[i]}_{v}", data.Length, 1, sfxRate, false);
                        clips[v].SetData(data, 0);
                    }
                    sfxClips[(int)ids[i]] = clips;
                }
            }
            IsReady = true;
            if (hasPending && Time.unscaledTime - pendingTime < 2f) Play(pendingId);
            hasPending = false;

            // 2) BGM: 지금 필요한 트랙부터 하나씩 (합성은 백그라운드, SetData는 나눠서)
            int trackCount = bgmClips.Length;
            for (int k = 0; k < trackCount; k++)
            {
                int ti = NextTrackToPrepare();
                if (ti < 0) break;
                var track = (BgmTrack)ti;
                var recorded = bank != null ? bank.FindBgm(track) : null;
                if (recorded != null)
                {
                    bgmClips[ti] = recorded;
                }
                else
                {
                    int rate = SynthBgm.DefaultSampleRate;
                    var task = Task.Run(() => SynthBgm.Generate(track, rate));
                    while (!task.IsCompleted) yield return null;
                    if (task.IsFaulted) { Debug.LogException(task.Exception); bgmClips[ti] = null; continue; }
                    var data = task.Result;
                    var clip = AudioClip.Create($"BGM_{track}", data.Length, 1, rate, false);
                    var chunk = new float[SetDataChunk];
                    for (int off = 0; off < data.Length; off += SetDataChunk)
                    {
                        int count = Math.Min(SetDataChunk, data.Length - off);
                        float[] part = count == SetDataChunk ? chunk : new float[count];
                        Array.Copy(data, off, part, 0, count);
                        clip.SetData(part, off);
                        yield return null;
                    }
                    bgmClips[ti] = clip;
                }
                if (hasWanted && wantedTrack == track) PlayBgm(track, wantedFade, wantedGain);
            }
        }

        readonly bool[] trackQueued = new bool[3];

        int NextTrackToPrepare()
        {
            if (hasWanted && !trackQueued[(int)wantedTrack])
            {
                trackQueued[(int)wantedTrack] = true;
                return (int)wantedTrack;
            }
            for (int i = 0; i < trackQueued.Length; i++)
                if (!trackQueued[i]) { trackQueued[i] = true; return i; }
            return -1;
        }

        // ───────────── 효과음 API ─────────────

        /// <summary>효과음 재생. volume은 0~1 상대값, pitchJitter는 ±비율 랜덤 변주.</summary>
        public void Play(SoundId id, float volume = 1f, float pitch = 1f, float delay = 0f, float pitchJitter = 0.04f)
        {
            int i = (int)id;
            var clips = sfxClips[i];
            if (clips == null || clips.Length == 0)
            {
                if (!IsReady && delay <= 0f) { hasPending = true; pendingId = id; pendingTime = Time.unscaledTime; }
                return;
            }
            float now = Time.unscaledTime;
            if (delay <= 0f)
            {
                if (now - lastPlayTime[i] < minInterval[i]) return;
                lastPlayTime[i] = now;
            }

            int v = 0;
            if (clips.Length > 1)
            {
                v = UnityEngine.Random.Range(0, clips.Length - 1);
                if (v >= lastVariant[i]) v++;
                if (v >= clips.Length) v = 0;
            }
            lastVariant[i] = v;
            var clip = clips[v];

            int s = PickSource(now);
            var src = pool[s];
            float p = pitch * (1f + UnityEngine.Random.Range(-pitchJitter, pitchJitter));
            src.clip = clip;
            src.pitch = p;
            src.volume = Mathf.Clamp01(volume * sfxBankVolume[i] * UnityEngine.Random.Range(0.92f, 1f) * SfxVolume * MasterVolume);
            if (delay > 0f) src.PlayDelayed(delay); else src.Play();
            busyUntil[s] = now + Mathf.Max(0f, delay) + clip.length / Mathf.Max(0.1f, p);
        }

        int PickSource(float now)
        {
            int best = 0;
            for (int i = 0; i < PoolSize; i++)
            {
                if (busyUntil[i] <= now) return i;
                if (busyUntil[i] < busyUntil[best]) best = i;
            }
            return best; // 다 쓰는 중이면 가장 먼저 끝날 소리를 끊는다
        }

        public static void PlaySfx(SoundId id, float volume = 1f)
        {
            if (Instance != null) Instance.Play(id, volume);
        }

        // ───────────── BGM API ─────────────

        /// <summary>BGM 트랙 전환 (크로스페이드). gain은 트랙 상대 볼륨 (정산 때 작게 등).</summary>
        public void PlayBgm(BgmTrack track, float fadeSec = 1.5f, float gain = 1f)
        {
            hasWanted = true;
            wantedTrack = track;
            wantedGain = gain;
            wantedFade = fadeSec;
            var clip = bgmClips[(int)track];
            if (clip == null) return; // 준비되면 PrepareClips가 다시 부름

            bgmFadeSpeed = 1f / Mathf.Max(0.05f, fadeSec);
            var cur = bgm[bgmCurrent];
            if (cur.clip == clip && cur.isPlaying)
            {
                bgmTarget[bgmCurrent] = 1f;
                bgmGainTarget[bgmCurrent] = gain;
                return;
            }
            int other = 1 - bgmCurrent;
            var src = bgm[other];
            src.Stop();
            src.clip = clip;
            src.time = 0f;
            src.Play();
            bgmLevel[other] = 0f;
            bgmTarget[other] = 1f;
            bgmGain[other] = bgmGainTarget[other] = gain;
            bgmTarget[bgmCurrent] = 0f;
            bgmCurrent = other;
            ApplyBgmVolume();
        }

        public void StopBgm(float fadeSec = 1f)
        {
            hasWanted = false;
            bgmFadeSpeed = 1f / Mathf.Max(0.05f, fadeSec);
            bgmTarget[0] = bgmTarget[1] = 0f;
        }

        /// <summary>징글 등이 잘 들리게 BGM을 잠깐 줄인다.</summary>
        public void Duck(float level, float seconds)
        {
            duckTarget = Mathf.Min(duckTarget, Mathf.Clamp01(level));
            duckUntil = Mathf.Max(duckUntil, Time.unscaledTime + seconds);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float now = Time.unscaledTime;
            if (now >= duckUntil) duckTarget = 1f;
            duck = Mathf.MoveTowards(duck, duckTarget, dt * (duckTarget < duck ? 4f : 0.8f));

            for (int i = 0; i < 2; i++)
            {
                bgmLevel[i] = Mathf.MoveTowards(bgmLevel[i], bgmTarget[i], bgmFadeSpeed * dt);
                bgmGain[i] = Mathf.MoveTowards(bgmGain[i], bgmGainTarget[i], 0.4f * dt);
                if (bgmLevel[i] <= 0f && bgmTarget[i] <= 0f && bgm[i].isPlaying) bgm[i].Stop();
            }
            ApplyBgmVolume();

            // 러시 끝 감지 (RushEnded 이벤트가 없어서 공개 상태만 읽음)
            if (rushMusic && (pollTimer -= dt) <= 0f)
            {
                pollTimer = 0.5f;
                if (game != null && game.Phase == DayPhase.Open && !game.IsRush)
                {
                    rushMusic = false;
                    PlayBgm(BgmTrack.Shop, 3f);
                }
            }
        }

        void ApplyBgmVolume()
        {
            for (int i = 0; i < 2; i++)
                bgm[i].volume = Mathf.Clamp01(bgmLevel[i] * bgmGain[i] * duck * BgmVolume * MasterVolume);
        }

        // ───────────── 설정 API (PlayerPrefs 저장) ─────────────

        public void SetMasterVolume(float v) { MasterVolume = Mathf.Clamp01(v); PlayerPrefs.SetFloat(KeyMaster, MasterVolume); OnSettings(); }
        public void SetBgmVolume(float v) { BgmVolume = Mathf.Clamp01(v); PlayerPrefs.SetFloat(KeyBgm, BgmVolume); OnSettings(); }
        public void SetSfxVolume(float v) { SfxVolume = Mathf.Clamp01(v); PlayerPrefs.SetFloat(KeySfx, SfxVolume); OnSettings(); }

        public void SetMuted(bool muted)
        {
            IsMuted = muted;
            PlayerPrefs.SetInt(KeyMuted, muted ? 1 : 0);
            ApplyMute();
            OnSettings();
        }

        public void ToggleMuted() => SetMuted(!IsMuted);

        void LoadSettings()
        {
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMaster, MasterVolume));
            BgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyBgm, BgmVolume));
            SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeySfx, SfxVolume));
            IsMuted = PlayerPrefs.GetInt(KeyMuted, 0) == 1;
        }

        void ApplyMute()
        {
            if (pool != null) foreach (var s in pool) s.mute = IsMuted;
            foreach (var s in bgm) if (s != null) s.mute = IsMuted;
        }

        void OnSettings()
        {
            ApplyBgmVolume();
            SettingsChanged?.Invoke();
        }

        // ───────────── 게임 이벤트 → 소리 ─────────────

        void OnBoxBought(ProductData p) => Play(SoundId.BoxRustle);

        void OnItemsPlaced(ShelfSlot slot, ProductData p, int count)
        {
            // 놓은 개수만큼 "톡톡톡" (최대 6번), 갈수록 살짝 빨라지고 높아짐
            var id = PlaceSoundFor(p);
            int taps = Mathf.Clamp(count, 1, MaxPlaceTaps);
            float t = 0f;
            for (int i = 0; i < taps; i++)
            {
                Play(id, 0.95f - i * 0.06f, 1f + i * 0.025f, t, 0.03f);
                t += UnityEngine.Random.Range(0.06f, 0.09f) * (1f - i * 0.06f);
            }
        }

        static SoundId PlaceSoundFor(ProductData p)
        {
            if (p == null) return SoundId.PlacePlastic;
            switch (p.id)
            {
                case "marble": return SoundId.PlaceGlass;
                case "ddakji":
                case "sticker":
                case "paperdoll":
                case "jjondeugi":
                    return SoundId.PlacePaper;
                default: return SoundId.PlacePlastic;
            }
        }

        void OnCustomerEntered(Customer c) => Play(SoundId.DoorBell, 0.85f, 1f, 0f, 0.02f);

        void OnItemPicked(Customer c, ShelfSlot s) => Play(SoundId.ItemPick, 0.7f);

        void OnCheckedOut(Customer c, int price)
        {
            Play(SoundId.Register, 0.9f, 1f, 0f, 0.02f);
            var coin = price <= 150 ? SoundId.CoinSmall : price <= 400 ? SoundId.CoinMedium : SoundId.CoinLarge;
            Play(coin, 1f, 1f, 0.16f, 0.05f);
        }

        void OnCustomerLeftUnhappy(Customer c) => Play(SoundId.Unhappy, 0.8f, 1f, 0f, 0.03f);

        void OnPhaseChanged(DayPhase phase)
        {
            rushMusic = false;
            switch (phase)
            {
                case DayPhase.Morning:
                    Play(SoundId.JingleNewDay, 1f, 1f, 0f, 0f);
                    Duck(0.35f, 1.6f);
                    PlayBgm(BgmTrack.Calm, 2f, 1f);
                    break;
                case DayPhase.Open:
                    Play(SoundId.ShutterOpen, 0.9f, 1f, 0f, 0.02f);
                    PlayBgm(BgmTrack.Shop, 1.5f, 1f);
                    break;
                case DayPhase.Closing:
                    Play(SoundId.JingleClosing, 1f, 1f, 0f, 0f);
                    Duck(0.4f, 1.5f);
                    PlayBgm(BgmTrack.Calm, 3f, 0.8f);
                    break;
                case DayPhase.Settlement:
                    Play(SoundId.JingleSettle, 1f, 1f, 0f, 0f);
                    Duck(0.35f, 1.8f);
                    PlayBgm(BgmTrack.Calm, 2f, 0.6f);
                    break;
            }
        }

        void OnRushStarted()
        {
            Play(SoundId.SchoolBell, 1f, 1f, 0f, 0f);
            Duck(0.25f, 2.3f);
            PlayBgm(BgmTrack.Rush, 2.5f, 0.9f);
            rushMusic = true;
            pollTimer = 1f;
        }

        void OnInvalidAction() => Play(SoundId.Error, 0.85f, 1f, 0f, 0.01f);

        void OnUiClick() => Play(SoundId.UiClick, 0.8f, 1f, 0f, 0.06f);
    }
}
