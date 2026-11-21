using System.IO;
using Munbanggu.Audio;
using UnityEditor;
using UnityEngine;

namespace Munbanggu.EditorTools
{
    /// <summary>합성 BGM을 wav로 저장해서 들어 보기 (Logs/bgm-<트랙>.wav). 배치: Munbanggu.EditorTools.AudioCheck.ExportBgm</summary>
    public static class AudioCheck
    {
        [MenuItem("문방구/배경음 wav로 저장")]
        public static void ExportBgm()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs"));
            foreach (BgmTrack t in System.Enum.GetValues(typeof(BgmTrack)))
            {
                int sr = SynthBgm.DefaultSampleRate;
                var buf = SynthBgm.Generate(t, sr);
                float peak = 0f;
                foreach (var s in buf) peak = Mathf.Max(peak, Mathf.Abs(s));
                string path = Path.Combine(dir, $"bgm-{t}.wav");
                WriteWav(path, buf, sr);
                Debug.Log($"[AudioCheck] {t}: {buf.Length / (float)sr:0.0}초, peak {peak:0.00} → {path}");
            }
        }

        static void WriteWav(string path, float[] data, int sr)
        {
            using var w = new BinaryWriter(File.Create(path));
            int bytes = data.Length * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + bytes);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(sr); w.Write(sr * 2); w.Write((short)2); w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(bytes);
            foreach (var s in data) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), -32768, 32767));
        }
    }
}
