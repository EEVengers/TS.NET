namespace TS.NET.Sequences;

public static class Windows
{
    /// <param name="optimumOverlap">Optimum overlap in ratio of width, used for Welch method</param>
    /// <param name="NENBW">Normalized Equivalent Noise BandWidth with unit of bins</param>
    public static Memory<double> HFT95(int width, out double optimumOverlap, out double NENBW)
    {
        // Heinzel, G., Rüdiger, A.and Schilling, R. (2002).
        // Spectrum and spectral density estimation by the Discrete Fourier transform (DFT), including a comprehensive list of window functions and some new flat-top windows.
        // [online] Available at: https://holometer.fnal.gov/GH_FFT.pdf.

        // HFT95
        // wj = 1 − 1.9383379 cos(z) + 1.3045202 cos(2z) − 0.4028270 cos(3z) + 0.0350665 cos(4z).
        optimumOverlap = 0.756;
        NENBW = 3.8112;
        Memory<double> window = new double[width];
        var windowSpan = window.Span;
        double angleIncrement = 2.0 * Math.PI / width;
        for (int i = 0; i < width; i++)
        {
            double z = angleIncrement * i;
            double wj = 1 - (1.9383379 * Math.Cos(z)) + (1.3045202 * Math.Cos(2 * z)) - (0.4028270 * Math.Cos(3 * z)) + (0.0350665 * Math.Cos(4 * z));
            windowSpan[i] = wj;
        }
        return window;
    }

    /// <param name="optimumOverlap">Optimum overlap in ratio of width, used for Welch method</param>
    /// <param name="NENBW">Normalized Equivalent Noise BandWidth with unit of bins</param>
    public static Memory<double> HFT90D(int width, out double optimumOverlap, out double NENBW)
    {
        // Heinzel, G., Rüdiger, A.and Schilling, R. (2002).
        // Spectrum and spectral density estimation by the Discrete Fourier transform (DFT), including a comprehensive list of window functions and some new flat-top windows.
        // [online] Available at: https://holometer.fnal.gov/GH_FFT.pdf.

        // HFT90D
        // wj = 1 − 1.942604 cos(z) + 1.340318 cos(2z) − 0.440811 cos(3z) + 0.043097 cos(4z).
        optimumOverlap = 0.76;
        NENBW = 3.8832;
        Memory<double> window = new double[width];
        var windowSpan = window.Span;
        double angleIncrement = 2.0 * Math.PI / width;
        for (int i = 0; i < width; i++)
        {
            double z = angleIncrement * i;
            double wj = 1 - (1.942604 * Math.Cos(z)) + (1.340318 * Math.Cos(2 * z)) - (0.440811 * Math.Cos(3 * z)) + (0.043097 * Math.Cos(4 * z));
            windowSpan[i] = wj;
        }

        return window;
    }

    /// <param name="optimumOverlap">Optimum overlap in ratio of width, used for Welch method</param>
    /// <param name="NENBW">Normalized Equivalent Noise BandWidth with unit of bins</param>
    public static Memory<double> SFT3M(int width, out double optimumOverlap, out double NENBW)
    {
        // Heinzel, G., Rüdiger, A.and Schilling, R. (2002).
        // Spectrum and spectral density estimation by the Discrete Fourier transform (DFT), including a comprehensive list of window functions and some new flat-top windows.
        // [online] Available at: https://holometer.fnal.gov/GH_FFT.pdf.

        // SFT3M
        // wj = 0.28235 − 0.52105 cos(z) + 0.19659 cos(2z).
        optimumOverlap = 0.655;
        NENBW = 2.9452;
        Memory<double> window = new double[width];
        var windowSpan = window.Span;
        double angleIncrement = 2.0 * Math.PI / width;
        for (int i = 0; i < width; i++)
        {
            double z = angleIncrement * i;
            double wj = 0.28235 - (0.52105 * Math.Cos(z)) + (0.19659 * Math.Cos(2 * z));
            windowSpan[i] = wj;
        }
        return window;
    }
}
