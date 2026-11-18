using System;
using LabApi.Features.Audio;

namespace SCPGrade
{
    /// <summary>
    /// Процедурные звуки (без файлов): щелчок рулетки, старт, выигрыш, проигрыш, нажатие кнопки.
    /// Формат — float PCM, моно, 48 кГц (как требует AudioTransmitter).
    /// </summary>
    public static class Sounds
    {
        private const int Rate = AudioTransmitter.SampleRate;

        public static readonly float[] Tick = MakeTick();
        public static readonly float[] Start = MakeStart();
        public static readonly float[] Win = MakeWin();
        public static readonly float[] Lose = MakeLose();
        public static readonly float[] Click = MakeClick();

        private static float[] Buffer(float seconds) => new float[(int)(Rate * seconds)];

        // Короткий «тик» трещотки
        private static float[] MakeTick()
        {
            float[] b = Buffer(0.03f);
            Random rnd = new Random(1);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float env = (float)Math.Exp(-t * 180f);
                float tone = (float)Math.Sin(2 * Math.PI * 2400 * t);
                float noise = ((float)rnd.NextDouble() * 2f) - 1f;
                b[i] = ((tone * 0.6f) + (noise * 0.4f)) * env * 0.55f;
            }

            return b;
        }

        // Восходящий «вжух» при старте
        private static float[] MakeStart()
        {
            float[] b = Buffer(0.35f);
            double phase = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float k = t / 0.35f;
                double freq = 300 + (900 * k * k);
                phase += 2 * Math.PI * freq / Rate;
                float env = Math.Min(1f, t * 30f) * (1f - k);
                b[i] = (float)Math.Sin(phase) * env * 0.35f;
            }

            return b;
        }

        // Арпеджио C-E-G-C + аккорд
        private static float[] MakeWin()
        {
            double[] notes = { 523.25, 659.25, 783.99, 1046.5 };
            const float step = 0.09f;
            const float chord = 0.6f;
            float[] b = Buffer((step * notes.Length) + chord);

            for (int n = 0; n < notes.Length; n++)
                AddTone(b, notes[n], n * step, step * 1.6f, 0.3f);

            float chordStart = step * notes.Length;
            AddTone(b, 523.25, chordStart, chord, 0.18f);
            AddTone(b, 659.25, chordStart, chord, 0.18f);
            AddTone(b, 783.99, chordStart, chord, 0.18f);
            AddTone(b, 1046.5, chordStart, chord, 0.15f);
            return Normalize(b, 0.6f);
        }

        // Грустное «вау-вау-вау-ваааа»
        private static float[] MakeLose()
        {
            double[] notes = { 392.0, 369.99, 349.23, 329.63 };
            float[] lens = { 0.22f, 0.22f, 0.22f, 0.7f };
            float total = 0f;
            foreach (float l in lens)
                total += l;
            float[] b = Buffer(total);

            float t0 = 0f;
            for (int n = 0; n < notes.Length; n++)
            {
                bool last = n == notes.Length - 1;
                int s = (int)(t0 * Rate);
                int len = (int)(lens[n] * Rate);
                double phase = 0;
                for (int i = 0; i < len && s + i < b.Length; i++)
                {
                    float t = (float)i / Rate;
                    double freq = notes[n] * (last ? 1 + (0.025 * Math.Sin(2 * Math.PI * 6 * t)) : 1);
                    phase += 2 * Math.PI * freq / Rate;
                    double tri = (2 / Math.PI) * Math.Asin(Math.Sin(phase)); // треугольная волна
                    float env = Math.Min(1f, t * 40f) * Math.Min(1f, (lens[n] - t) * 15f);
                    b[s + i] += (float)tri * env * 0.4f;
                }

                t0 += lens[n];
            }

            return Normalize(b, 0.55f);
        }

        // Мягкий клик кнопки
        private static float[] MakeClick()
        {
            float[] b = Buffer(0.05f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                b[i] = (float)Math.Sin(2 * Math.PI * 1200 * t) * (float)Math.Exp(-t * 90f) * 0.35f;
            }

            return b;
        }

        private static void AddTone(float[] b, double freq, float start, float length, float amp)
        {
            int s = (int)(start * Rate);
            int len = (int)(length * Rate);
            for (int i = 0; i < len && s + i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float env = Math.Min(1f, t * 80f) * (float)Math.Exp(-t * 3.5f);
                double w = Math.Sin(2 * Math.PI * freq * t) + (0.3 * Math.Sin(2 * Math.PI * freq * 2 * t));
                b[s + i] += (float)w * env * amp;
            }
        }

        private static float[] Normalize(float[] b, float peak)
        {
            float max = 0f;
            foreach (float v in b)
                max = Math.Max(max, Math.Abs(v));
            if (max > 0f)
            {
                float k = peak / max;
                for (int i = 0; i < b.Length; i++)
                    b[i] *= k;
            }

            return b;
        }
    }
}
