using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Munbanggu.Audio;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Munbanggu.EditorTools
{
    /// <summary>
    /// 합성 효과음·BGM을 WAV로 내보내서 직접 들어보고 레벨을 확인한다.
    /// 배치: bash tools/unity-compile.sh audio Munbanggu.EditorTools.AudioExport.ExportAll
    /// 결과: Munbanggu/Logs/audio/*.wav + 로그에 [AudioExport] 줄 (길이, 피크, RMS, 클리핑, 무음 여부)
    /// </summary>
    public static class AudioExport
    {
        [MenuItem("문방구/사운드 WAV 내보내기")]
        public static void ExportAll()
        {
            string dir = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "..", "Logs", "audio"));
            Directory.CreateDirectory(dir);
            var report = new StringBuilder();
            int files = 0, problems = 0;
            var total = Stopwatch.StartNew();

            foreach (SoundId id in Enum.GetValues(typeof(SoundId)))
            {
                int variants = SynthSfx.VariantCount(id);
                for (int v = 0; v < variants; v++)
                {
                    var sw = Stopwatch.StartNew();
                    var data = SynthSfx.Generate(id, v, SynthSfx.DefaultSampleRate);
                    sw.Stop();
                    problems += Write(dir, $"sfx_{id}_{v}.wav", data, SynthSfx.DefaultSampleRate, sw.ElapsedMilliseconds, false, report);
                    files++;
                }
            }

            foreach (BgmTrack track in Enum.GetValues(typeof(BgmTrack)))
            {
                var sw = Stopwatch.StartNew();
                var data = SynthBgm.Generate(track, SynthBgm.DefaultSampleRate);
                sw.Stop();
                problems += Write(dir, $"bgm_{track}.wav", data, SynthBgm.DefaultSampleRate, sw.ElapsedMilliseconds, true, report);
                files++;
            }

            total.Stop();
            report.Insert(0, $"[AudioExport] {files}개 파일, 문제 {problems}개, 총 {total.ElapsedMilliseconds}ms → {dir}\n");
            if (problems == 0) Debug.Log(report.ToString());
            else Debug.LogWarning(report.ToString());
        }

        /// <returns>문제(클리핑·무음·이음매 튐) 개수</returns>
        static int Write(string dir, string name, float[] data, int sr, long ms, bool loop, StringBuilder report)
        {
            float peak = 0f;
            double sum = 0;
            int clipped = 0;
            foreach (var x in data)
            {
                float a = Math.Abs(x);
                if (a > peak) peak = a;
                if (a >= 0.999f) clipped++;
                sum += x * x;
            }
            float rms = data.Length > 0 ? (float)Math.Sqrt(sum / data.Length) : 0f;
            bool silent = peak < 0.01f;
            bool nan = float.IsNaN(peak) || float.IsInfinity(peak);
            // 루프 이음매: 마지막 샘플 → 첫 샘플 차이가 평소 샘플 간 차이보다 크게 튀는지
            float seam = loop && data.Length > 1 ? Math.Abs(data[0] - data[data.Length - 1]) : 0f;
            bool seamBad = loop && seam > 0.15f;

            WriteWav(Path.Combine(dir, name), data, sr);

            int problems = (clipped > 0 ? 1 : 0) + (silent ? 1 : 0) + (nan ? 1 : 0) + (seamBad ? 1 : 0);
            string flag = problems == 0 ? "OK" : "문제:" + (clipped > 0 ? " 클리핑" : "") + (silent ? " 무음" : "") + (nan ? " NaN" : "") + (seamBad ? " 이음매" : "");
            report.AppendLine($"[AudioExport] {name,-28} {data.Length / (float)sr,6:0.000}s  peak {Db(peak),6:0.0}dBFS  rms {Db(rms),6:0.0}dBFS  clip {clipped}" +
                              (loop ? $"  seam {seam:0.000}" : "") + $"  {ms}ms  {flag}");
            return problems;
        }

        static float Db(float a) => a <= 1e-9f ? -180f : 20f * (float)Math.Log10(a);

        static void WriteWav(string path, float[] data, int sr)
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var w = new BinaryWriter(fs);
            int bytes = data.Length * 2;
            w.Write(Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + bytes);
            w.Write(Encoding.ASCII.GetBytes("WAVE"));
            w.Write(Encoding.ASCII.GetBytes("fmt "));
            w.Write(16);
            w.Write((short)1);      // PCM
            w.Write((short)1);      // 모노
            w.Write(sr);
            w.Write(sr * 2);        // 바이트/초
            w.Write((short)2);      // 블록 정렬
            w.Write((short)16);     // 비트
            w.Write(Encoding.ASCII.GetBytes("data"));
            w.Write(bytes);
            foreach (var x in data)
            {
                float c = x > 1f ? 1f : x < -1f ? -1f : x;
                w.Write((short)Math.Round(c * 32767f));
            }
        }
    }
}
