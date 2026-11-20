using System;

namespace Munbanggu.Audio
{
    /// <summary>
    /// 합성용 난수 (xorshift32). UnityEngine.Random은 메인 스레드 전용이라 백그라운드 합성에서는 이걸 쓴다.
    /// 시드가 같으면 같은 소리가 나와서 WAV 내보내기 결과가 재현된다.
    /// </summary>
    internal struct Rng
    {
        uint state;

        public Rng(uint seed) { state = seed == 0 ? 0x9E3779B9u : seed; }

        public uint NextU()
        {
            uint x = state;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            state = x;
            return x;
        }

        /// <summary>[0, 1)</summary>
        public float Value() => (NextU() >> 8) * (1f / 16777216f);
        /// <summary>[-1, 1)</summary>
        public float Signed() => Value() * 2f - 1f;
        public float Range(float a, float b) => a + (b - a) * Value();
        public int Range(int a, int bExclusive) => a + (int)(Value() * (bExclusive - a));
    }

    /// <summary>RBJ 바이쿼드 필터 (노이즈 다듬기용).</summary>
    internal struct Biquad
    {
        float b0, b1, b2, a1, a2, z1, z2;

        public static Biquad LowPass(float fc, float q, int sr)
        {
            Coeffs(fc, q, sr, out float cw, out float alpha);
            float a0 = 1f + alpha;
            return Make((1f - cw) * 0.5f, 1f - cw, (1f - cw) * 0.5f, a0, -2f * cw, 1f - alpha);
        }

        public static Biquad HighPass(float fc, float q, int sr)
        {
            Coeffs(fc, q, sr, out float cw, out float alpha);
            float a0 = 1f + alpha;
            return Make((1f + cw) * 0.5f, -(1f + cw), (1f + cw) * 0.5f, a0, -2f * cw, 1f - alpha);
        }

        /// <summary>중심 주파수에서 이득 0dB인 밴드패스.</summary>
        public static Biquad BandPass(float fc, float q, int sr)
        {
            Coeffs(fc, q, sr, out float cw, out float alpha);
            float a0 = 1f + alpha;
            return Make(alpha, 0f, -alpha, a0, -2f * cw, 1f - alpha);
        }

        static void Coeffs(float fc, float q, int sr, out float cw, out float alpha)
        {
            fc = Math.Min(fc, sr * 0.45f);
            float w = 2f * (float)Math.PI * fc / sr;
            cw = (float)Math.Cos(w);
            alpha = (float)Math.Sin(w) / (2f * Math.Max(0.05f, q));
        }

        static Biquad Make(float b0, float b1, float b2, float a0, float a1, float a2)
        {
            float inv = 1f / a0;
            return new Biquad { b0 = b0 * inv, b1 = b1 * inv, b2 = b2 * inv, a1 = a1 * inv, a2 = a2 * inv };
        }

        public float Process(float x)
        {
            float y = b0 * x + z1;
            z1 = b1 * x - a1 * y + z2;
            z2 = b2 * x - a2 * y;
            return y;
        }
    }

    /// <summary>합성 공용 도구: 감쇠 사인, 노이즈 버스트, 리버브, 정규화.</summary>
    internal static class Dsp
    {
        public const float TwoPi = (float)(Math.PI * 2.0);

        public static float MidiToHz(float midi) => 440f * (float)Math.Pow(2.0, (midi - 69f) / 12.0);

        public static int Samples(float sec, int sr) => Math.Max(1, (int)(sec * sr));

        /// <summary>
        /// 지수 감쇠 사인을 버퍼에 더한다 (재귀식 오실레이터라 sin 호출이 없다).
        /// wrap=true면 버퍼 끝을 넘는 꼬리를 앞으로 돌려 써서 루프가 끊기지 않게 한다.
        /// </summary>
        public static void AddDampedSine(float[] buf, int sr, float startSec, float freq, float amp, float decaySec,
            float attackSec = 0.002f, bool wrap = false, float maxSec = 0f, float phase = 0f)
        {
            if (freq <= 0f || freq >= sr * 0.48f || amp == 0f) return;
            int start = (int)(startSec * sr);
            float life = maxSec > 0f ? maxSec : decaySec * 6.5f;
            int n = Samples(life, sr);
            int att = Math.Max(1, (int)(attackSec * sr));
            double w = 2.0 * Math.PI * freq / sr;
            double r = Math.Exp(-1.0 / (Math.Max(0.001f, decaySec) * sr));
            double c = 2.0 * r * Math.Cos(w);
            double r2 = r * r;
            // y[-1], y[-2]: 위상 phase에서 시작하는 사인
            double y2 = amp * Math.Sin(phase - 2.0 * w) / r2;
            double y1 = amp * Math.Sin(phase - w) / r;
            int len = buf.Length;
            // 끝부분 0.6% 구간은 0으로 페이드 (뚝 끊기는 클릭 방지)
            int fadeStart = n - Math.Max(1, n / 160);
            for (int i = 0; i < n; i++)
            {
                double y = c * y1 - r2 * y2;
                y2 = y1; y1 = y;
                int idx = start + i;
                if (idx >= len)
                {
                    if (!wrap) break;
                    idx %= len;
                }
                if (idx < 0) continue;
                float g = i < att ? (float)i / att : 1f;
                if (i > fadeStart) g *= (float)(n - i) / (n - fadeStart);
                buf[idx] += (float)y * g;
            }
        }

        /// <summary>필터를 거친 노이즈 버스트. 엔벨로프: 선형 어택 후 지수 감쇠.</summary>
        public static void AddNoiseBurst(float[] buf, int sr, ref Rng rng, float startSec, float durSec, float amp,
            Biquad filter, float attackSec = 0.001f, float decaySec = 0.03f, bool wrap = false)
        {
            int start = (int)(startSec * sr);
            int n = Samples(durSec, sr);
            int att = Math.Max(1, (int)(attackSec * sr));
            float k = (float)Math.Exp(-1.0 / (Math.Max(0.0005f, decaySec) * sr));
            float env = 1f;
            int len = buf.Length;
            int fadeStart = n - Math.Max(1, n / 20);
            for (int i = 0; i < n; i++)
            {
                float x = filter.Process(rng.Signed());
                float g = i < att ? (float)i / att : (env *= k);
                if (i > fadeStart) g *= (float)(n - i) / (n - fadeStart);
                int idx = start + i;
                if (idx >= len)
                {
                    if (!wrap) break;
                    idx %= len;
                }
                if (idx < 0) continue;
                buf[idx] += x * g * amp;
            }
        }

        public static float Peak(float[] buf)
        {
            float p = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float a = Math.Abs(buf[i]);
                if (a > p) p = a;
            }
            return p;
        }

        public static float Rms(float[] buf)
        {
            double s = 0;
            for (int i = 0; i < buf.Length; i++) s += buf[i] * buf[i];
            return buf.Length == 0 ? 0f : (float)Math.Sqrt(s / buf.Length);
        }

        /// <summary>피크를 target으로 맞춘다 (클리핑 방지 + 소리 간 음량 통일).</summary>
        public static void Normalize(float[] buf, float target)
        {
            float p = Peak(buf);
            if (p < 1e-6f) return;
            float g = target / p;
            for (int i = 0; i < buf.Length; i++) buf[i] *= g;
        }

        /// <summary>효과음 시작·끝을 아주 짧게 페이드해서 클릭 방지.</summary>
        public static void FadeEdges(float[] buf, int sr, float inSec = 0.0005f, float outSec = 0.01f)
        {
            int a = Math.Min(buf.Length, Math.Max(1, (int)(inSec * sr)));
            for (int i = 0; i < a; i++) buf[i] *= (float)i / a;
            int b = Math.Min(buf.Length, Math.Max(1, (int)(outSec * sr)));
            for (int i = 0; i < b; i++) buf[buf.Length - 1 - i] *= (float)i / b;
        }

        /// <summary>
        /// 간단한 Schroeder 리버브 (콤 4 + 올패스 2). loop=true면 버퍼를 두 번 돌려서
        /// 잔향이 루프 앞부분으로 자연스럽게 이어지게 한다.
        /// </summary>
        public static void Reverb(float[] buf, int sr, float wet, float roomSec = 0.05f, float feedback = 0.78f,
            float damp = 0.35f, bool loop = false)
        {
            float scale = sr / 44100f;
            int[] combLen =
            {
                (int)(1557 * scale * roomSec / 0.035f), (int)(1617 * scale * roomSec / 0.035f),
                (int)(1491 * scale * roomSec / 0.035f), (int)(1422 * scale * roomSec / 0.035f)
            };
            int[] apLen = { (int)(556 * scale), (int)(441 * scale) };
            var combs = new float[4][];
            var combIdx = new int[4];
            var combLp = new float[4];
            for (int c = 0; c < 4; c++) combs[c] = new float[Math.Max(8, combLen[c])];
            var aps = new float[2][];
            var apIdx = new int[2];
            for (int a = 0; a < 2; a++) aps[a] = new float[Math.Max(8, apLen[a])];

            var dry = (float[])buf.Clone();
            int len = buf.Length;
            int passes = loop ? 2 : 1;
            for (int pass = 0; pass < passes; pass++)
            {
                bool write = pass == passes - 1;
                for (int i = 0; i < len; i++)
                {
                    float x = dry[i] * 0.25f;
                    float acc = 0f;
                    for (int c = 0; c < 4; c++)
                    {
                        float[] cb = combs[c];
                        int ci = combIdx[c];
                        float o = cb[ci];
                        combLp[c] = o * (1f - damp) + combLp[c] * damp;
                        cb[ci] = x + combLp[c] * feedback;
                        combIdx[c] = ci + 1 >= cb.Length ? 0 : ci + 1;
                        acc += o;
                    }
                    for (int a = 0; a < 2; a++)
                    {
                        float[] ab = aps[a];
                        int ai = apIdx[a];
                        float bo = ab[ai];
                        float o = -acc + bo;
                        ab[ai] = acc + bo * 0.5f;
                        apIdx[a] = ai + 1 >= ab.Length ? 0 : ai + 1;
                        acc = o;
                    }
                    if (write) buf[i] = dry[i] + acc * wet;
                }
            }
        }

        /// <summary>부드러운 소프트 클리퍼 (tanh 근사). 순간 피크만 살짝 눌러준다.</summary>
        public static float SoftClip(float x)
        {
            if (x > 3f) return 1f;
            if (x < -3f) return -1f;
            float x2 = x * x;
            return x * (27f + x2) / (27f + 9f * x2);
        }
    }
}
