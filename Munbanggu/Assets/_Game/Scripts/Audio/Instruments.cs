using System;

namespace Munbanggu.Audio
{
    internal enum Wave { Sine, Triangle, Square, Pulse25 }

    /// <summary>
    /// 효과음과 BGM이 같이 쓰는 악기/타격음. 전부 버퍼에 "더하기"만 한다 (믹싱은 호출 쪽).
    /// wrap=true면 루프 버퍼 끝을 넘는 꼬리가 앞으로 돌아간다 (BGM 이음매 없애기).
    /// </summary>
    internal static class Instruments
    {
        // ───────────── 음정 악기 ─────────────

        /// <summary>오르골: 맑은 기본음 + 약한 배음, 빠른 감쇠, 빗살 튕기는 소리.</summary>
        public static void MusicBox(float[] buf, int sr, float t, float midi, float amp, ref Rng rng, bool wrap = false)
        {
            float f = Dsp.MidiToHz(midi);
            float d = 1.25f * (float)Math.Pow(0.5, (midi - 72f) / 24f); // 높은 음일수록 짧게
            Dsp.AddDampedSine(buf, sr, t, f, amp, d, 0.0015f, wrap);
            Dsp.AddDampedSine(buf, sr, t, f * 2.001f, amp * 0.22f, d * 0.4f, 0.001f, wrap);
            Dsp.AddDampedSine(buf, sr, t, f * 3.0f, amp * 0.10f, d * 0.18f, 0.001f, wrap);
            Dsp.AddDampedSine(buf, sr, t, f * 5.43f, amp * 0.05f, 0.05f, 0.0005f, wrap);
            Dsp.AddNoiseBurst(buf, sr, ref rng, t, 0.006f, amp * 0.08f, Biquad.HighPass(4000f, 0.7f, sr), 0.0003f, 0.0015f, wrap);
        }

        /// <summary>토이피아노: 쇠막대 특유의 비정수 배음(≈3.9배)과 해머 소리, 살짝 어긋난 이중음.</summary>
        public static void ToyPiano(float[] buf, int sr, float t, float midi, float amp, ref Rng rng, bool wrap = false)
        {
            float f = Dsp.MidiToHz(midi);
            float d = 0.85f * (float)Math.Pow(0.5, (midi - 72f) / 24f);
            Dsp.AddDampedSine(buf, sr, t, f, amp, d, 0.002f, wrap);
            Dsp.AddDampedSine(buf, sr, t, f * 1.0025f, amp * 0.3f, d * 0.8f, 0.002f, wrap, 0f, 1.3f);
            Dsp.AddDampedSine(buf, sr, t, f * 2.0f, amp * 0.25f, d * 0.4f, 0.0015f, wrap);
            Dsp.AddDampedSine(buf, sr, t, f * 3.93f, amp * 0.32f, d * 0.2f, 0.001f, wrap);
            Dsp.AddDampedSine(buf, sr, t, f * 7.1f, amp * 0.07f, 0.04f, 0.0005f, wrap);
            Dsp.AddNoiseBurst(buf, sr, ref rng, t, 0.008f, amp * 0.12f, Biquad.BandPass(2800f, 1.2f, sr), 0.0003f, 0.002f, wrap);
        }

        /// <summary>하프/뜯는 현: 정수배 배음, 높은 배음일수록 빨리 사라짐.</summary>
        public static void Pluck(float[] buf, int sr, float t, float midi, float amp, ref Rng rng, bool wrap = false, float decay = 1.2f)
        {
            float f = Dsp.MidiToHz(midi);
            for (int h = 1; h <= 6; h++)
            {
                float a = amp / (float)Math.Pow(h, 1.5);
                float d = decay / (float)Math.Pow(h, 0.85);
                Dsp.AddDampedSine(buf, sr, t, f * h * (1f + 0.0006f * h * h), a, d, 0.003f, wrap, 0f, rng.Value() * 6.28f);
            }
        }

        /// <summary>종 (문 종·학교 종·계산대 종 공용). ratios/amps/decays로 성격을 정한다.</summary>
        public static void Bell(float[] buf, int sr, float t, float f, float amp, float[] ratios, float[] amps, float[] decays,
            ref Rng rng, float detune = 0.004f, bool wrap = false)
        {
            for (int i = 0; i < ratios.Length; i++)
            {
                float r = ratios[i] * (1f + detune * rng.Signed());
                Dsp.AddDampedSine(buf, sr, t, f * r, amp * amps[i] * rng.Range(0.8f, 1.1f), decays[i], 0.0008f, wrap, 0f, rng.Value() * 6.28f);
            }
            Dsp.AddNoiseBurst(buf, sr, ref rng, t, 0.004f, amp * 0.15f, Biquad.HighPass(5000f, 0.7f, sr), 0.0002f, 0.001f, wrap);
        }

        static readonly float[] CoinRatios = { 1.0f, 1.47f, 2.09f, 2.56f, 3.18f };
        static readonly float[] CoinAmps = { 1.0f, 0.75f, 0.55f, 0.35f, 0.22f };
        static readonly float[] CoinDecays = { 0.24f, 0.16f, 0.11f, 0.08f, 0.055f };

        /// <summary>동전 하나가 부딪힘: 짧은 딸깍 + 원판의 비정수 공명.</summary>
        public static void CoinHit(float[] buf, int sr, float t, float baseHz, float amp, ref Rng rng, float ring = 1f)
        {
            Dsp.AddNoiseBurst(buf, sr, ref rng, t, 0.006f, amp * 0.45f, Biquad.HighPass(3500f, 0.8f, sr), 0.0002f, 0.0012f);
            for (int i = 0; i < CoinRatios.Length; i++)
            {
                float f = baseHz * CoinRatios[i] * (1f + 0.02f * rng.Signed());
                float d = CoinDecays[i] * ring * rng.Range(0.7f, 1.3f);
                Dsp.AddDampedSine(buf, sr, t, f, amp * CoinAmps[i] * rng.Range(0.6f, 1.1f), d, 0.0003f, false, 0f, rng.Value() * 6.28f);
            }
        }

        // ───────────── 오실레이터 ─────────────

        static float Blep(float t, float dt)
        {
            if (t < dt) { t /= dt; return t + t - t * t - 1f; }
            if (t > 1f - dt) { t = (t - 1f) / dt; return t * t + t + t + 1f; }
            return 0f;
        }

        /// <summary>
        /// 지속음 (칩튠 리드, 베이스, 삑 소리). f0→f1 지수 글라이드, 딜레이 비브라토, 1극 로우패스.
        /// </summary>
        public static void Tone(float[] buf, int sr, float t, float dur, float f0, float f1, Wave wave, float amp,
            float attack = 0.005f, float release = 0.03f, float vibDepth = 0f, float vibRate = 5.5f, float lpHz = 0f,
            bool wrap = false)
        {
            int start = (int)(t * sr);
            int nSus = Dsp.Samples(dur, sr);
            int nRel = Dsp.Samples(release, sr);
            int n = nSus + nRel;
            int att = Math.Max(1, (int)(attack * sr));
            float lpA = lpHz > 0f ? 1f - (float)Math.Exp(-Dsp.TwoPi * lpHz / sr) : 1f;
            float lp = 0f;
            double phase = 0.0;
            double ratio = f1 / f0;
            int len = buf.Length;
            for (int i = 0; i < n; i++)
            {
                float u = Math.Min(1f, (float)i / nSus);
                double f = f0 * Math.Pow(ratio, u);
                if (vibDepth > 0f)
                {
                    float sec = (float)i / sr;
                    float vibAmt = Math.Min(1f, Math.Max(0f, (sec - 0.08f) / 0.15f));
                    f *= 1.0 + vibDepth * vibAmt * Math.Sin(Dsp.TwoPi * vibRate * sec);
                }
                float dt = (float)(f / sr);
                phase += dt;
                if (phase >= 1.0) phase -= 1.0;
                float p = (float)phase;
                float x;
                switch (wave)
                {
                    case Wave.Triangle: x = 4f * Math.Abs(p - 0.5f) - 1f; break;
                    case Wave.Square:
                        x = (p < 0.5f ? 1f : -1f) + Blep(p, dt) - Blep(p + 0.5f >= 1f ? p - 0.5f : p + 0.5f, dt);
                        break;
                    case Wave.Pulse25:
                        x = (p < 0.25f ? 1f : -1f) + Blep(p, dt) - Blep(p + 0.75f >= 1f ? p - 0.25f : p + 0.75f, dt);
                        x += 0.5f; // 펄스폭 25%의 DC 성분 제거
                        break;
                    default: x = (float)Math.Sin(Dsp.TwoPi * p); break;
                }
                lp += lpA * (x - lp);
                float env = i < att ? (float)i / att : 1f;
                if (i >= nSus)
                {
                    float r = 1f - (float)(i - nSus) / nRel;
                    env *= r * r;
                }
                int idx = start + i;
                if (idx >= len)
                {
                    if (!wrap) break;
                    idx %= len;
                }
                if (idx < 0) continue;
                buf[idx] += lp * env * amp;
            }
        }

        /// <summary>주파수가 떨어지며 감쇠하는 사인 (킥 드럼·상자 쿵·버튼 톡).</summary>
        public static void Sweep(float[] buf, int sr, float t, float f0, float f1, float sweepSec, float decaySec, float amp, bool wrap = false)
        {
            int start = (int)(t * sr);
            int n = Dsp.Samples(decaySec * 6f, sr);
            float k = (float)Math.Exp(-1.0 / (decaySec * sr));
            float env = 1f;
            double phase = 0.0;
            int len = buf.Length;
            int att = Math.Max(1, sr / 2000);
            for (int i = 0; i < n; i++)
            {
                float u = Math.Min(1f, (float)i / (sweepSec * sr));
                double f = f1 + (f0 - f1) * (1f - u) * (1f - u);
                phase += f / sr;
                float x = (float)Math.Sin(Dsp.TwoPi * phase);
                env *= k;
                float g = i < att ? (float)i / att : 1f;
                if (i > n - 64) g *= (n - i) / 64f;
                int idx = start + i;
                if (idx >= len)
                {
                    if (!wrap) break;
                    idx %= len;
                }
                if (idx < 0) continue;
                buf[idx] += x * env * g * amp;
            }
        }

        // ───────────── 타악기 (BGM) ─────────────

        public static void Kick(float[] buf, int sr, float t, float amp, ref Rng rng, bool wrap)
        {
            Sweep(buf, sr, t, 160f, 52f, 0.06f, 0.09f, amp, wrap);
            Dsp.AddNoiseBurst(buf, sr, ref rng, t, 0.01f, amp * 0.2f, Biquad.LowPass(1500f, 0.7f, sr), 0.0003f, 0.003f, wrap);
        }

        public static void Snare(float[] buf, int sr, float t, float amp, ref Rng rng, bool wrap)
        {
            Sweep(buf, sr, t, 240f, 180f, 0.03f, 0.045f, amp * 0.5f, wrap);
            Dsp.AddNoiseBurst(buf, sr, ref rng, t, 0.14f, amp, Biquad.BandPass(2200f, 0.7f, sr), 0.001f, 0.045f, wrap);
        }

        public static void Hat(float[] buf, int sr, float t, float amp, ref Rng rng, bool wrap)
        {
            Dsp.AddNoiseBurst(buf, sr, ref rng, t, 0.05f, amp, Biquad.HighPass(6500f, 0.8f, sr), 0.0005f, 0.012f, wrap);
        }

        public static void Shaker(float[] buf, int sr, float t, float amp, ref Rng rng, bool wrap)
        {
            Dsp.AddNoiseBurst(buf, sr, ref rng, t, 0.07f, amp, Biquad.BandPass(5200f, 0.9f, sr), 0.012f, 0.025f, wrap);
        }
    }
}
