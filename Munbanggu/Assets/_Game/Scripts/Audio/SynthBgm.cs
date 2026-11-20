using System;

namespace Munbanggu.Audio
{
    /// <summary>
    /// 90년대 동요·오르골 풍 BGM을 런타임 합성 (16마디 루프). 순수 C#: 백그라운드 스레드에서 호출.
    /// 세 트랙이 같은 멜로디·화성을 공유하고 템포와 악기 레이어만 다르다.
    ///   Calm  80 BPM  오르골(한 옥타브 위) + 하프 아르페지오, 드럼 없음         (48초)
    ///   Shop  96 BPM  토이피아노 + 하프 반주 + 삼각파 베이스 + 셰이커          (40초)
    ///   Rush 120 BPM  칩튠 펄스 리드 + 토이피아노 + 16분 아르페지오 + 드럼    (32초)
    /// </summary>
    public static class SynthBgm
    {
        public const int DefaultSampleRate = 22050;
        const int Bars = 16;

        // 8분음표 8칸 = 한 마디. "-"는 앞 음을 이어감, "."은 쉼표.
        static readonly string[] Melody =
        {
            "E5 - D5 C5 D5 - E5 G5",
            "A5 - G5 - E5 - - -",
            "F5 - E5 D5 C5 - A4 -",
            "D5 - E5 D5 B4 - G4 -",
            "E5 - D5 C5 D5 - E5 G5",
            "C6 - B5 A5 G5 - E5 -",
            "F5 - A5 - G5 - F5 E5",
            "D5 - - - - - . .",
            "A5 - - G5 F5 - G5 A5",
            "B5 - - A5 G5 - - -",
            "G5 - E5 - B4 - E5 G5",
            "A5 - - - E5 - - -",
            "F5 - E5 D5 A4 - D5 F5",
            "G5 - F5 E5 D5 - B4 -",
            "C5 - E5 G5 C6 - G5 E5",
            "D5 - - - - - . .",
        };

        // 마디별 화음 (근음 음이름 0=C, 단조 여부): C Am F G | C Am Dm G | F G Em Am | Dm G C G
        static readonly (int root, bool minor)[] Chords =
        {
            (0, false), (9, true), (5, false), (7, false),
            (0, false), (9, true), (2, true), (7, false),
            (5, false), (7, false), (4, true), (9, true),
            (2, true), (7, false), (0, false), (7, false),
        };

        struct Note { public float beat; public float lenBeats; public int midi; }

        public static float Bpm(BgmTrack track) => track == BgmTrack.Calm ? 80f : track == BgmTrack.Shop ? 96f : 120f;

        public static float LengthSec(BgmTrack track) => Bars * 4f * 60f / Bpm(track);

        public static float[] Generate(BgmTrack track, int sr = DefaultSampleRate)
        {
            float spb = 60f / Bpm(track); // 초/박
            int len = (int)Math.Round(LengthSec(track) * sr);
            var buf = new float[len];
            var rng = new Rng(0xC0FFEEu + (uint)track * 977u);
            var melody = ParseMelody();

            switch (track)
            {
                case BgmTrack.Calm: Calm(buf, sr, spb, melody, ref rng); break;
                case BgmTrack.Shop: Shop(buf, sr, spb, melody, ref rng); break;
                default: Rush(buf, sr, spb, melody, ref rng); break;
            }

            // 잔향 (루프 모드: 끝의 잔향이 앞부분으로 이어짐)
            float wet = track == BgmTrack.Calm ? 0.45f : track == BgmTrack.Shop ? 0.3f : 0.2f;
            Dsp.Reverb(buf, sr, wet, 0.05f, 0.8f, 0.4f, true);
            // 피크 정리: 살짝 소프트클립 후 정규화 (클리핑 없이 고른 음량)
            Dsp.Normalize(buf, 1.1f);
            for (int i = 0; i < buf.Length; i++) buf[i] = Dsp.SoftClip(buf[i]);
            Dsp.Normalize(buf, 0.7f);
            return buf;
        }

        // ───────────── 편곡 ─────────────

        static void Calm(float[] buf, int sr, float spb, Note[] melody, ref Rng rng)
        {
            foreach (var n in melody)
                Instruments.MusicBox(buf, sr, Human(n.beat * spb, ref rng), n.midi + 12, 0.42f * Vel(ref rng), ref rng, true);

            for (int bar = 0; bar < Bars; bar++)
            {
                int[] tri = Triad(bar, 55);
                float bt = bar * 4f * spb;
                // 하프 8분 아르페지오: 1-5-3-5 / 8-5-3-5
                int[] pat = { 0, 2, 1, 2, 3, 2, 1, 2 };
                for (int e = 0; e < 8; e++)
                {
                    int m = pat[e] == 3 ? tri[0] + 12 : tri[pat[e]];
                    Instruments.Pluck(buf, sr, Human(bt + e * 0.5f * spb, ref rng), m, (e == 0 ? 0.2f : 0.13f) * Vel(ref rng), ref rng, true, 1.4f);
                }
                // 베이스: 2분음표 근음 (부드러운 하프 저음)
                int root = BassRoot(bar);
                Instruments.Pluck(buf, sr, bt, root, 0.34f, ref rng, true, 2.0f);
                Instruments.Pluck(buf, sr, bt + 2f * spb, root + 7, 0.22f, ref rng, true, 1.6f);
            }
        }

        static void Shop(float[] buf, int sr, float spb, Note[] melody, ref Rng rng)
        {
            foreach (var n in melody)
            {
                float t = Human(n.beat * spb, ref rng);
                Instruments.ToyPiano(buf, sr, t, n.midi, 0.45f * Vel(ref rng), ref rng, true);
                // 후반 8마디는 오르골이 한 옥타브 위에서 겹쳐 반짝임 (같은 루프 안의 레이어 변화)
                if (n.beat >= 32f) Instruments.MusicBox(buf, sr, t, n.midi + 12, 0.12f, ref rng, true);
            }

            for (int bar = 0; bar < Bars; bar++)
            {
                int[] tri = Triad(bar, 57);
                float bt = bar * 4f * spb;
                // 반주: 2·4박에 화음 (하프)
                for (int b = 1; b < 4; b += 2)
                    foreach (var m in tri)
                        Instruments.Pluck(buf, sr, Human(bt + b * spb, ref rng), m, 0.085f * Vel(ref rng), ref rng, true, 0.7f);
                // 베이스: 1박 근음, 3박 5도, 4박 반 경과음
                int root = BassRoot(bar);
                int next = BassRoot((bar + 1) % Bars);
                AddBass(buf, sr, bt, 0.9f * spb, root, 0.4f);
                AddBass(buf, sr, bt + 2f * spb, 0.9f * spb, root + 7 > 57 ? root - 5 : root + 7, 0.33f);
                AddBass(buf, sr, bt + 3.5f * spb, 0.4f * spb, next + (next > root ? -1 : 1), 0.22f);
                // 셰이커 (뒷박) + 1박 킥
                for (int e = 1; e < 8; e += 2) Instruments.Shaker(buf, sr, Human(bt + e * 0.5f * spb, ref rng), 0.07f * Vel(ref rng), ref rng, true);
                Instruments.Kick(buf, sr, bt, 0.28f, ref rng, true);
            }
        }

        static void Rush(float[] buf, int sr, float spb, Note[] melody, ref Rng rng)
        {
            foreach (var n in melody)
            {
                float t = n.beat * spb;
                float dur = Math.Max(0.12f, n.lenBeats * spb - 0.03f);
                Dsp_Lead(buf, sr, t, dur, n.midi);
                Instruments.ToyPiano(buf, sr, t, n.midi, 0.25f, ref rng, true);
            }

            for (int bar = 0; bar < Bars; bar++)
            {
                int[] tri = Triad(bar, 67);
                float bt = bar * 4f * spb;
                // 16분 아르페지오 (칩튠 사각파, 짧게)
                int[] pat = { 0, 1, 2, 3, 2, 1 };
                for (int s = 0; s < 16; s++)
                {
                    int p = pat[s % pat.Length];
                    int m = p == 3 ? tri[0] + 12 : tri[p];
                    Instruments.Tone(buf, sr, bt + s * 0.25f * spb, 0.05f, Dsp.MidiToHz(m), Dsp.MidiToHz(m), Wave.Square,
                        s % 4 == 0 ? 0.05f : 0.035f, 0.002f, 0.03f, 0f, 5f, 2600f, true);
                }
                // 옥타브 베이스 8분
                int root = BassRoot(bar);
                for (int e = 0; e < 8; e++)
                    AddBass(buf, sr, bt + e * 0.5f * spb, 0.42f * spb, (e & 1) == 0 ? root : root + 12, (e & 1) == 0 ? 0.36f : 0.24f);
                // 드럼: 킥 1·3(+3박 반), 스네어 2·4, 하이햇 8분
                Instruments.Kick(buf, sr, bt, 0.5f, ref rng, true);
                Instruments.Kick(buf, sr, bt + 2f * spb, 0.45f, ref rng, true);
                Instruments.Kick(buf, sr, bt + 2.5f * spb, 0.3f, ref rng, true);
                Instruments.Snare(buf, sr, bt + spb, 0.2f, ref rng, true);
                Instruments.Snare(buf, sr, bt + 3f * spb, 0.22f, ref rng, true);
                for (int e = 0; e < 8; e++) Instruments.Hat(buf, sr, bt + e * 0.5f * spb, (e & 1) == 0 ? 0.09f : 0.06f, ref rng, true);
            }
        }

        static void Dsp_Lead(float[] buf, int sr, float t, float dur, int midi)
        {
            float f = Dsp.MidiToHz(midi);
            Instruments.Tone(buf, sr, t, dur, f, f, Wave.Pulse25, 0.16f, 0.004f, 0.05f, 0.006f, 5.5f, 3800f, true);
        }

        static void AddBass(float[] buf, int sr, float t, float dur, int midi, float amp)
        {
            float f = Dsp.MidiToHz(midi);
            Instruments.Tone(buf, sr, t, dur, f, f, Wave.Triangle, amp, 0.006f, 0.05f, 0f, 5f, 1200f, true);
            // 폰 스피커에서도 들리게 옥타브 위 사인을 살짝
            Instruments.Tone(buf, sr, t, dur, f * 2f, f * 2f, Wave.Sine, amp * 0.25f, 0.006f, 0.05f, 0f, 5f, 0f, true);
        }

        // ───────────── 도구 ─────────────

        static float Human(float t, ref Rng rng) => Math.Max(0f, t + rng.Range(-0.006f, 0.006f));

        static float Vel(ref Rng rng) => rng.Range(0.85f, 1.05f);

        /// <summary>마디 화음의 3화음을 low 이상 가장 가까운 자리바꿈으로.</summary>
        static int[] Triad(int bar, int low)
        {
            var (root, minor) = Chords[bar];
            int[] tones = { root, root + (minor ? 3 : 4), root + 7 };
            var res = new int[3];
            for (int i = 0; i < 3; i++)
            {
                int m = tones[i] % 12;
                while (m < low) m += 12;
                res[i] = m;
            }
            Array.Sort(res);
            return res;
        }

        /// <summary>베이스 근음 (G2 ~ G#3 사이: 폰 스피커에서 너무 낮지 않게).</summary>
        static int BassRoot(int bar)
        {
            int m = Chords[bar].root;
            while (m < 45) m += 12;
            return m;
        }

        static Note[] ParseMelody()
        {
            var list = new System.Collections.Generic.List<Note>();
            for (int bar = 0; bar < Melody.Length; bar++)
            {
                string[] tok = Melody[bar].Split(' ');
                for (int e = 0; e < tok.Length; e++)
                {
                    string s = tok[e];
                    if (s == "-" || s == ".") continue;
                    int len = 1;
                    while (e + len < tok.Length && tok[e + len] == "-") len++;
                    // 마디 끝 "-"가 다음 마디로 이어지지는 않음 (악보 단순화)
                    list.Add(new Note { beat = bar * 4f + e * 0.5f, lenBeats = len * 0.5f, midi = NoteToMidi(s) });
                }
            }
            return list.ToArray();
        }

        static int NoteToMidi(string s)
        {
            int pc;
            switch (s[0])
            {
                case 'C': pc = 0; break;
                case 'D': pc = 2; break;
                case 'E': pc = 4; break;
                case 'F': pc = 5; break;
                case 'G': pc = 7; break;
                case 'A': pc = 9; break;
                default: pc = 11; break;
            }
            int i = 1;
            if (s[i] == '#') { pc++; i++; }
            else if (s[i] == 'b') { pc--; i++; }
            int oct = s[i] - '0';
            return 12 * (oct + 1) + pc;
        }
    }
}
