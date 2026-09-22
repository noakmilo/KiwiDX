namespace KiwiDX;

// Streaming complex FIR channel filter, fractional rate conversion to 24 kHz,
// analog detector, audio anti-alias FIR and 12 kHz mono PCM. No USB/device drivers.
internal sealed class SpyServerDemodulator
{
    private readonly double rate, shiftStep;
    private readonly string mode;
    private readonly float[] real = new float[257], imag = new float[257], cr = new float[257], ci = new float[257];
    private readonly float[] audioHistory = new float[63], audioFilter = new float[63];
    private int position, audioPosition, audioDecimation;
    private double phase, clock, oldReal, oldImag, previousReal, previousImag, dc, envelope = .01, bfo;
    public SpyServerDemodulator(double sampleRate, string modulation, int bandwidth, double offset)
    {
        if (sampleRate is < 12000 or > 500000) throw new InvalidDataException("SpyServer IQ sample rate is outside the supported reduced-IQ range (12-500 kHz).");
        rate = sampleRate; mode = modulation.ToLowerInvariant();
        if (mode is not ("am" or "usb" or "lsb" or "cw" or "nfm" or "fm")) throw new ArgumentException("Unsupported SpyServer demodulation mode."); shiftStep = -2 * Math.PI * offset / rate;
        double width = Math.Clamp(bandwidth, 50, Math.Min(12000, rate * .75));
        double low = -width / 2, high = width / 2;
        if (mode == "usb") { low = 200; high = Math.Min(5500, width + 200); }
        if (mode == "lsb") { low = -Math.Min(5500, width + 200); high = -200; }
        if (mode == "cw") { low = -width / 2; high = -low; }
        for (int k = 0; k < cr.Length; k++)
        {
            double t = k - (cr.Length - 1) / 2.0;
            double w = .42 - .5 * Math.Cos(2 * Math.PI * k / (cr.Length - 1)) + .08 * Math.Cos(4 * Math.PI * k / (cr.Length - 1));
            double h = t == 0 ? (high - low) / rate : Math.Sin(Math.PI * (high - low) * t / rate) / (Math.PI * t);
            double angle = Math.PI * (high + low) * t / rate;
            cr[k] = (float)(w * h * Math.Cos(angle)); ci[k] = (float)(w * h * Math.Sin(angle));
        }
        for (int k = 0; k < audioFilter.Length; k++)
        {
            double t = k - 31;
            audioFilter[k] = (float)((t == 0 ? 10000.0 / 24000 : Math.Sin(2 * Math.PI * 5000 * t / 24000) / (Math.PI * t)) * (.54 - .46 * Math.Cos(2 * Math.PI * k / 62)));
        }
    }
    public byte[] Process(ReadOnlySpan<float> iq)
    {
        var pcm = new byte[(int)Math.Ceiling(iq.Length / 2.0 * 12000 / rate + 4) * 2]; int written = 0;
        for (int n = 0; n < iq.Length; n += 2)
        {
            double c = Math.Cos(phase), s = Math.Sin(phase); phase = Math.IEEERemainder(phase + shiftStep, 2 * Math.PI);
            real[position] = (float)(iq[n] * c - iq[n + 1] * s); imag[position] = (float)(iq[n] * s + iq[n + 1] * c);
            double r = 0, j = 0; int p = position;
            for (int k = 0; k < cr.Length; k++)
            {
                r += real[p] * cr[k] - imag[p] * ci[k]; j += real[p] * ci[k] + imag[p] * cr[k];
                if (--p < 0) p = real.Length - 1;
            }
            position = (position + 1) % real.Length;
            clock += 24000;
            while (clock >= rate)
            {
                clock -= rate;
                double fraction = 1 - clock / 24000;
                double re = oldReal + (r - oldReal) * fraction, im = oldImag + (j - oldImag) * fraction;
                double value;
                if (mode == "am") value = Math.Sqrt(re * re + im * im);
                else if (mode is "nfm" or "fm") value = Math.Atan2(im * previousReal - re * previousImag, re * previousReal + im * previousImag) / Math.PI;
                else if (mode == "cw") { value = re * Math.Cos(bfo) - im * Math.Sin(bfo); bfo = (bfo + 2 * Math.PI * 700 / 24000) % (2 * Math.PI); }
                else value = re;
                previousReal = re; previousImag = im;
                dc += .001 * (value - dc); value -= dc;
                audioHistory[audioPosition] = (float)value;
                if (++audioDecimation == 2)
                {
                    audioDecimation = 0; double filtered = 0; int ap = audioPosition;
                    for (int k = 0; k < audioFilter.Length; k++) { filtered += audioHistory[ap] * audioFilter[k]; if (--ap < 0) ap = audioHistory.Length - 1; }
                    double magnitude = Math.Abs(filtered); envelope += (magnitude > envelope ? .03 : .0001) * (magnitude - envelope);
                    short sample = (short)(Math.Clamp(filtered * Math.Min(1000, .18 / Math.Max(.00001, envelope)), -.95, .95) * 32767);
                    pcm[written++] = (byte)sample; pcm[written++] = (byte)(sample >> 8);
                }
                audioPosition = (audioPosition + 1) % audioHistory.Length;
            }
            oldReal = r; oldImag = j;
        }
        if (written != pcm.Length) Array.Resize(ref pcm, written);
        return pcm;
    }
}
