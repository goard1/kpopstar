using System;

namespace Munbanggu.Audio
{
    /// <summary>
    /// 효과음 합성. 순수 C#이라 백그라운드 스레드에서 돌려도 된다 (UnityEngine API 사용 금지).
    /// 각 소리는 변주(variant) 여러 개를 만들어 두고 재생할 때 번갈아 써서 반복감을 줄인다.
    /// </summary>
    public static class SynthSfx
    {
        public const int DefaultSampleRate = 44100;

        public static int VariantCount(SoundId id)
        {
            switch (id)
            {
                case SoundId.CoinSmall:
                case SoundId.CoinMedium:
                case SoundId.PlacePlastic:
                case SoundId.PlacePaper:
                case SoundId.PlaceGlass:
                    return 4;
                case SoundId.CoinLarge:
                case SoundId.ItemPick:
                case SoundId.DoorBell:
                case SoundId.BoxRustle:
                    return 3;
                case SoundId.Register:
                case SoundId.UiClick:
                    return 2;
                default:
                    return 1;
            }
        }

        /// <summary>피크 기준 음량 (소리 간 밸런스). 정규화 후 이 값이 최대 진폭이 된다.</summary>
        static float TargetPeak(SoundId id)
        {
            switch (id)
            {
                case SoundId.UiClick: return 0.42f;
                case SoundId.ItemPick: return 0.38f;
                case SoundId.PlacePlastic:
                case SoundId.PlacePaper:
                case SoundId.PlaceGlass: return 0.6f;
                case SoundId.Error: return 0.3f;     // 지속음이라 체감 음량이 커서 낮게
                case SoundId.Unhappy: return 0.36f;
                case SoundId.ShutterOpen: return 0.55f;
                case SoundId.DoorBell: return 0.6f;
                case SoundId.JingleNewDay:
                case SoundId.JingleClosing:
                case SoundId.JingleSettle: return 0.62f;
                default: return 0.8f;
            }
        }

        public static float[] Generate(SoundId id, int variant, int sr = DefaultSampleRate)
        {
            var rng = new Rng((uint)((int)id * 7919 + variant * 104729 + 12345));
            float[] buf;
            switch (id)
            {
                case SoundId.CoinSmall: buf = Coins(sr, ref rng, 2, 0.12f, 0.75f, false); break;
                case SoundId.CoinMedium: buf = Coins(sr, ref rng, 4, 0.3f, 0.95f, false); break;
                case SoundId.CoinLarge: buf = Coins(sr, ref rng, 9, 0.55f, 1.5f, true); break;
                case SoundId.Register: buf = Register(sr, ref rng); break;
                case SoundId.PlacePlastic: buf = PlacePlastic(sr, ref rng); break;
                case SoundId.PlacePaper: buf = PlacePaper(sr, ref rng); break;
                case SoundId.PlaceGlass: buf = PlaceGlass(sr, ref rng); break;
                case SoundId.BoxRustle: buf = BoxRustle(sr, ref rng); break;
                case SoundId.DoorBell: buf = DoorBell(sr, ref rng, variant); break;
                case SoundId.ItemPick: buf = ItemPick(sr, ref rng); break;
                case SoundId.Unhappy: buf = Unhappy(sr); break;
                case SoundId.Error: buf = ErrorBeep(sr); break;
                case SoundId.UiClick: buf = UiClick(sr, ref rng, variant); break;
                case SoundId.SchoolBell: buf = SchoolBell(sr, ref rng); break;
                case SoundId.JingleNewDay: buf = JingleNewDay(sr, ref rng); break;
                case SoundId.ShutterOpen: buf = ShutterOpen(sr, ref rng); break;
                case SoundId.JingleClosing: buf = JingleClosing(sr, ref rng); break;
                case SoundId.JingleSettle: buf = JingleSettle(sr, ref rng); break;
                default: buf = new float[sr / 10]; break;
            }
            RemoveDc(buf);
            Dsp.FadeEdges(buf, sr);
            Dsp.Normalize(buf, TargetPeak(id));
            return buf;
        }

        static float[] New(float sec, int sr) => new float[Dsp.Samples(sec, sr)];

        static void RemoveDc(float[] buf)
        {
            // 아주 낮은 하이패스 (20Hz): 쿵 소리 뒤에 남는 직류 성분 제거
            float x1 = 0f, y1 = 0f;
            const float R = 0.997f;
            for (int i = 0; i < buf.Length; i++)
            {
                float x = buf[i];
                float y = x - x1 + R * y1;
                x1 = x; y1 = y;
                buf[i] = y;
            }
        }

        // ───────────── 동전 "짤랑" ─────────────

        /// <summary>
        /// 동전 여러 개가 손바닥/계산대에 떨어지는 소리. 동전 하나당 2~4번 부딪히고 점점 약해진다.
        /// sparkle=true면 큰 금액용 반짝이 아르페지오를 위에 얹는다.
        /// </summary>
        static float[] Coins(int sr, ref Rng rng, int coins, float spread, float lengthSec, bool sparkle)
        {
            var buf = New(lengthSec, sr);
            for (int c = 0; c < coins; c++)
            {
                float t = 0.005f + (coins == 1 ? 0f : spread * (float)Math.Pow(rng.Value(), 1.3));
                float baseHz = rng.Range(2600f, 4300f);
                float amp = rng.Range(0.6f, 1f);
                int bounces = rng.Range(2, 5);
                float gap = rng.Range(0.025f, 0.06f);
                for (int b = 0; b < bounces; b++)
                {
                    Instruments.CoinHit(buf, sr, t, baseHz * (1f + 0.01f * rng.Signed()), amp, ref rng, b == bounces - 1 ? 1.6f : 0.8f);
                    t += gap * rng.Range(0.6f, 1.1f);
                    gap *= 0.7f;
                    amp *= 0.55f;
                }
            }
            if (sparkle)
            {
                // 큰 금액: 동전 위로 "띠리링" 반짝이 (E6 G#6 B6 E7)
                float[] notes = { 88, 92, 95, 100 };
                for (int i = 0; i < notes.Length; i++)
                    Instruments.MusicBox(buf, sr, 0.18f + i * 0.055f, notes[i], 0.22f, ref rng);
            }
            Dsp.Reverb(buf, sr, 0.12f, 0.03f, 0.7f);
            return buf;
        }

        // ───────────── 금전등록기 "철컥-띵" ─────────────

        static readonly float[] RegBellRatios = { 1f, 2.0f, 2.76f, 5.4f };
        static readonly float[] RegBellAmps = { 1f, 0.22f, 0.42f, 0.14f };
        static readonly float[] RegBellDecays = { 0.85f, 0.45f, 0.32f, 0.12f };

        static float[] Register(int sr, ref Rng rng)
        {
            var buf = New(1.35f, sr);
            // "철": 레버 눌림 — 금속 걸쇠 소리
            Dsp.AddNoiseBurst(buf, sr, ref rng, 0f, 0.025f, 0.8f, Biquad.BandPass(2300f, 2f, sr), 0.0005f, 0.005f);
            Dsp.AddDampedSine(buf, sr, 0f, 1320f * rng.Range(0.97f, 1.03f), 0.25f, 0.018f, 0.0005f);
            Instruments.Sweep(buf, sr, 0f, 190f, 130f, 0.02f, 0.03f, 0.45f);
            // "컥": 서랍 걸쇠 풀림 — 더 낮고 묵직하게
            float t2 = rng.Range(0.045f, 0.06f);
            Dsp.AddNoiseBurst(buf, sr, ref rng, t2, 0.03f, 1f, Biquad.BandPass(950f, 1.5f, sr), 0.0005f, 0.01f);
            Instruments.Sweep(buf, sr, t2, 130f, 75f, 0.04f, 0.05f, 0.8f);
            Dsp.AddDampedSine(buf, sr, t2, 2450f, 0.15f, 0.025f, 0.0005f);
            // 서랍이 미끄러져 나오는 소리
            Dsp.AddNoiseBurst(buf, sr, ref rng, t2 + 0.02f, 0.17f, 0.18f, Biquad.BandPass(650f, 0.8f, sr), 0.03f, 0.07f);
            // 서랍 끝에 닿는 "탁"
            Instruments.Sweep(buf, sr, t2 + 0.17f, 110f, 70f, 0.03f, 0.04f, 0.35f);
            // "띵": 계산대 종
            float tb = t2 + rng.Range(0.07f, 0.09f);
            Instruments.Bell(buf, sr, tb, rng.Range(1720f, 1800f), 0.75f, RegBellRatios, RegBellAmps, RegBellDecays, ref rng);
            Dsp.Reverb(buf, sr, 0.14f, 0.035f, 0.72f);
            return buf;
        }

        // ───────────── 진열 "톡톡" ─────────────

        static float[] PlacePlastic(int sr, ref Rng rng)
        {
            var buf = New(0.16f, sr);
            float f = rng.Range(950f, 1350f);
            Dsp.AddDampedSine(buf, sr, 0f, f, 0.7f, 0.016f, 0.0004f);
            Dsp.AddDampedSine(buf, sr, 0f, f * 2.31f, 0.3f, 0.007f, 0.0003f);
            Dsp.AddNoiseBurst(buf, sr, ref rng, 0f, 0.012f, 0.6f, Biquad.BandPass(rng.Range(2200f, 3000f), 1.1f, sr), 0.0003f, 0.0025f);
            // 나무 매대 울림
            Dsp.AddDampedSine(buf, sr, 0f, rng.Range(210f, 250f), 0.45f, 0.028f, 0.001f);
            return buf;
        }

        static float[] PlacePaper(int sr, ref Rng rng)
        {
            var buf = New(0.16f, sr);
            Dsp.AddNoiseBurst(buf, sr, ref rng, 0f, 0.03f, 1f, Biquad.BandPass(rng.Range(1000f, 1400f), 0.8f, sr), 0.0005f, 0.007f);
            // 봉지 바스락 알갱이
            int grains = rng.Range(3, 6);
            for (int g = 0; g < grains; g++)
                Dsp.AddNoiseBurst(buf, sr, ref rng, rng.Range(0.002f, 0.045f), 0.008f, rng.Range(0.15f, 0.3f),
                    Biquad.BandPass(rng.Range(3000f, 6500f), 2f, sr), 0.0005f, 0.002f);
            Dsp.AddDampedSine(buf, sr, 0f, rng.Range(150f, 180f), 0.5f, 0.022f, 0.001f);
            return buf;
        }

        static float[] PlaceGlass(int sr, ref Rng rng)
        {
            var buf = New(0.26f, sr);
            float f = rng.Range(3000f, 3900f);
            // 구슬끼리 "딸깍" 두 번
            for (int k = 0; k < 2; k++)
            {
                float t = k == 0 ? 0f : rng.Range(0.025f, 0.045f);
                float a = k == 0 ? 0.8f : 0.4f;
                Dsp.AddDampedSine(buf, sr, t, f, a, 0.05f, 0.0003f);
                Dsp.AddDampedSine(buf, sr, t, f * 2.7f, a * 0.45f, 0.025f, 0.0003f);
                Dsp.AddDampedSine(buf, sr, t, f * 5.1f, a * 0.2f, 0.012f, 0.0002f);
                Dsp.AddNoiseBurst(buf, sr, ref rng, t, 0.004f, a * 0.4f, Biquad.HighPass(5000f, 0.7f, sr), 0.0002f, 0.0008f);
                f *= rng.Range(1.03f, 1.1f);
            }
            Dsp.AddDampedSine(buf, sr, 0f, 420f, 0.3f, 0.015f, 0.0005f);
            return buf;
        }

        // ───────────── 도매 상자 "부스럭+툭" ─────────────

        static float[] BoxRustle(int sr, ref Rng rng)
        {
            var buf = New(0.9f, sr);
            const float RustleLen = 0.38f;
            // 비닐·종이 알갱이 (가운데가 가장 촘촘)
            int grains = rng.Range(55, 75);
            for (int g = 0; g < grains; g++)
            {
                float t = rng.Value() * RustleLen;
                float env = (float)Math.Pow(Math.Sin(Math.PI * t / RustleLen), 0.7);
                Dsp.AddNoiseBurst(buf, sr, ref rng, t, rng.Range(0.004f, 0.016f), rng.Range(0.2f, 0.7f) * env,
                    Biquad.BandPass(rng.Range(1500f, 6500f), rng.Range(1.2f, 3f), sr), 0.0004f, rng.Range(0.0015f, 0.005f));
            }
            // 골판지 끌리는 소리 (낮은 쉬익)
            Dsp.AddNoiseBurst(buf, sr, ref rng, 0.02f, RustleLen, 0.22f, Biquad.BandPass(850f, 0.8f, sr), 0.08f, 0.2f);
            // "툭": 상자가 바닥에 놓임
            float tt = RustleLen + rng.Range(0.03f, 0.06f);
            Instruments.Sweep(buf, sr, tt, 130f, 80f, 0.03f, 0.065f, 1f);
            Dsp.AddDampedSine(buf, sr, tt, rng.Range(170f, 200f), 0.45f, 0.035f, 0.001f);
            Dsp.AddNoiseBurst(buf, sr, ref rng, tt, 0.07f, 0.7f, Biquad.LowPass(550f, 0.7f, sr), 0.0006f, 0.02f);
            Dsp.AddNoiseBurst(buf, sr, ref rng, tt, 0.06f, 0.3f, Biquad.BandPass(360f, 3f, sr), 0.0006f, 0.025f);
            Dsp.Reverb(buf, sr, 0.1f, 0.03f, 0.65f);
            return buf;
        }

        // ───────────── 문 종 "딸랑" ─────────────

        static readonly float[] DoorRatios = { 1f, 2.31f, 3.94f, 5.86f };
        static readonly float[] DoorAmps = { 1f, 0.55f, 0.33f, 0.18f };
        static readonly float[] DoorDecays = { 1.0f, 0.65f, 0.42f, 0.28f };

        static float[] DoorBell(int sr, ref Rng rng, int variant)
        {
            var buf = New(1.9f, sr);
            float f = 1480f + variant * 70f + rng.Range(-20f, 20f);
            // 문이 열리며 종이 흔들림: 큰 종 + 작은 종이 번갈아 3~4번 울림
            int strikes = rng.Range(3, 5);
            float t = 0.004f;
            float amp = 1f;
            for (int s = 0; s < strikes; s++)
            {
                bool small = (s & 1) == 1;
                Instruments.Bell(buf, sr, t, small ? f * 1.19f : f, amp * (small ? 0.7f : 1f), DoorRatios, DoorAmps, DoorDecays, ref rng);
                t += rng.Range(0.07f, 0.12f) * (1f + s * 0.15f);
                amp *= rng.Range(0.55f, 0.7f);
            }
            Dsp.Reverb(buf, sr, 0.16f, 0.035f, 0.72f);
            return buf;
        }

        // ───────────── 손님이 물건 집음 ─────────────

        static float[] ItemPick(int sr, ref Rng rng)
        {
            var buf = New(0.13f, sr);
            Dsp.AddNoiseBurst(buf, sr, ref rng, 0f, 0.07f, 0.6f, Biquad.BandPass(rng.Range(2200f, 3200f), 0.8f, sr), 0.015f, 0.025f);
            Dsp.AddDampedSine(buf, sr, 0.005f, rng.Range(1600f, 2000f), 0.3f, 0.006f, 0.0003f);
            return buf;
        }

        // ───────────── 실망 하강음 ─────────────

        static float[] Unhappy(int sr)
        {
            var buf = New(0.6f, sr);
            // "띠-로~": 두 음이 내려가며 끝은 살짝 처짐 (부드러운 삼각파 + 옥타브 사인)
            Instruments.Tone(buf, sr, 0f, 0.12f, 523f, 523f, Wave.Triangle, 0.7f, 0.006f, 0.03f, 0f, 5f, 2500f);
            Instruments.Tone(buf, sr, 0f, 0.12f, 1046f, 1046f, Wave.Sine, 0.15f, 0.006f, 0.03f);
            Instruments.Tone(buf, sr, 0.16f, 0.28f, 392f, 330f, Wave.Triangle, 0.75f, 0.008f, 0.1f, 0.012f, 6f, 2000f);
            Instruments.Tone(buf, sr, 0.16f, 0.28f, 784f, 660f, Wave.Sine, 0.12f, 0.008f, 0.1f);
            return buf;
        }

        // ───────────── 오류 "삑" ─────────────

        static float[] ErrorBeep(int sr)
        {
            var buf = New(0.24f, sr);
            // 90년대 전자 부저: 사각파 + 살짝 어긋난 두 번째 사각파 (맥놀이)
            Instruments.Tone(buf, sr, 0f, 0.15f, 880f, 860f, Wave.Square, 0.5f, 0.003f, 0.035f, 0f, 5f, 3200f);
            Instruments.Tone(buf, sr, 0f, 0.15f, 886f, 866f, Wave.Square, 0.3f, 0.003f, 0.035f, 0f, 5f, 2400f);
            return buf;
        }

        // ───────────── UI 클릭 ─────────────

        static float[] UiClick(int sr, ref Rng rng, int variant)
        {
            var buf = New(0.07f, sr);
            float f = variant == 0 ? 2100f : 1900f;
            Instruments.Sweep(buf, sr, 0f, f * 1.25f, f, 0.004f, 0.007f, 0.6f);
            Dsp.AddDampedSine(buf, sr, 0f, f * 0.5f, 0.35f, 0.012f, 0.0005f);
            Dsp.AddNoiseBurst(buf, sr, ref rng, 0f, 0.005f, 0.3f, Biquad.HighPass(3500f, 0.7f, sr), 0.0002f, 0.001f);
            return buf;
        }

        // ───────────── 하교 종 "딩동댕동" ─────────────

        static readonly float[] ChimeRatios = { 1f, 2.0f, 3.0f, 4.07f, 6.2f };
        static readonly float[] ChimeAmps = { 1f, 0.35f, 0.14f, 0.12f, 0.04f };
        static readonly float[] ChimeDecays = { 1.7f, 0.9f, 0.5f, 0.3f, 0.12f };

        static float[] SchoolBell(int sr, ref Rng rng)
        {
            var buf = New(3.8f, sr);
            // 미-도-레-솔 (E5 C5 D5 G4)
            float[] notes = { 76, 72, 74, 67 };
            for (int i = 0; i < notes.Length; i++)
                Instruments.Bell(buf, sr, 0.01f + i * 0.5f, Dsp.MidiToHz(notes[i]), i == 3 ? 1f : 0.9f,
                    ChimeRatios, ChimeAmps, ChimeDecays, ref rng, 0.001f);
            // 학교 스피커 느낌: 저음·초고음 깎기
            var hp = Biquad.HighPass(280f, 0.7f, sr);
            var lp = Biquad.LowPass(5500f, 0.7f, sr);
            for (int i = 0; i < buf.Length; i++) buf[i] = lp.Process(hp.Process(buf[i]));
            Dsp.Reverb(buf, sr, 0.3f, 0.05f, 0.8f, 0.3f);
            return buf;
        }

        // ───────────── 징글 ─────────────

        static float[] JingleNewDay(int sr, ref Rng rng)
        {
            var buf = New(1.9f, sr);
            // 오르골 상승 아르페지오: 솔-도-미-솔-도
            float[] notes = { 79, 84, 88, 91, 96 };
            for (int i = 0; i < notes.Length; i++)
                Instruments.MusicBox(buf, sr, 0.01f + i * 0.1f, notes[i], i == notes.Length - 1 ? 0.9f : 0.7f, ref rng);
            Instruments.Pluck(buf, sr, 0.41f, 60, 0.35f, ref rng, false, 1.0f);
            Instruments.Pluck(buf, sr, 0.41f, 67, 0.25f, ref rng, false, 1.0f);
            Dsp.Reverb(buf, sr, 0.25f, 0.045f, 0.78f);
            return buf;
        }

        static float[] ShutterOpen(int sr, ref Rng rng)
        {
            var buf = New(1.25f, sr);
            // 셔터 올리는 "드르르륵": 쇠 날개가 하나씩 말려 올라가는 딸깍들
            const float RollLen = 0.8f;
            float t = 0.01f;
            while (t < RollLen)
            {
                float u = t / RollLen;
                float env = (float)Math.Sin(Math.PI * Math.Min(1f, u * 1.3f + 0.1f));
                Dsp.AddNoiseBurst(buf, sr, ref rng, t, 0.014f, 0.55f * env * rng.Range(0.6f, 1f),
                    Biquad.BandPass(rng.Range(1600f, 2800f), 3f, sr), 0.0004f, 0.004f);
                Dsp.AddDampedSine(buf, sr, t, rng.Range(2300f, 3300f), 0.1f * env, 0.03f, 0.0004f);
                t += 1f / rng.Range(20f, 28f);
            }
            // 함석 울림
            Dsp.AddNoiseBurst(buf, sr, ref rng, 0f, RollLen, 0.25f, Biquad.BandPass(420f, 1.2f, sr), 0.1f, 0.5f);
            // 끝까지 올라가서 "철컹"
            Instruments.Sweep(buf, sr, RollLen + 0.02f, 150f, 90f, 0.03f, 0.06f, 0.8f);
            Dsp.AddDampedSine(buf, sr, RollLen + 0.02f, 1250f, 0.25f, 0.08f, 0.0005f);
            Dsp.AddDampedSine(buf, sr, RollLen + 0.02f, 2900f, 0.12f, 0.05f, 0.0005f);
            Dsp.AddNoiseBurst(buf, sr, ref rng, RollLen + 0.02f, 0.05f, 0.6f, Biquad.BandPass(1100f, 1.2f, sr), 0.0005f, 0.012f);
            Dsp.Reverb(buf, sr, 0.15f, 0.04f, 0.7f);
            return buf;
        }

        static float[] JingleClosing(int sr, ref Rng rng)
        {
            var buf = New(2.0f, sr);
            // 솔-미-도 내려가는 오르골 (하루 끝)
            float[] notes = { 79, 76, 72 };
            for (int i = 0; i < notes.Length; i++)
                Instruments.MusicBox(buf, sr, 0.01f + i * 0.24f, notes[i], 0.8f, ref rng);
            Instruments.Pluck(buf, sr, 0.49f, 48, 0.4f, ref rng, false, 1.3f);
            Dsp.Reverb(buf, sr, 0.28f, 0.05f, 0.8f);
            return buf;
        }

        static float[] JingleSettle(int sr, ref Rng rng)
        {
            var buf = New(2.0f, sr);
            // 정산 "따라란~": 토이피아노 도-미-솔 + 높은 도 화음, 위에 동전 반짝
            float[] notes = { 72, 76, 79 };
            for (int i = 0; i < notes.Length; i++)
                Instruments.ToyPiano(buf, sr, 0.01f + i * 0.08f, notes[i], 0.6f, ref rng);
            float[] chord = { 72, 76, 79, 84 };
            foreach (var m in chord) Instruments.ToyPiano(buf, sr, 0.3f, m, 0.42f, ref rng);
            Instruments.Pluck(buf, sr, 0.3f, 48, 0.45f, ref rng, false, 1.2f);
            for (int c = 0; c < 3; c++)
                Instruments.CoinHit(buf, sr, 0.32f + c * 0.06f + rng.Range(0f, 0.02f), rng.Range(3000f, 4200f), 0.18f, ref rng);
            Dsp.Reverb(buf, sr, 0.22f, 0.045f, 0.78f);
            return buf;
        }
    }
}
