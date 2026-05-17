namespace TS.NET.Sequences;

public readonly record struct SineWaveParameters(double FrequencyHz, double Amplitude, double PhaseDeg, double Offset);

public static class LsqFit
{
    private readonly record struct SineCoefficientFit(double CoefficientB, double CoefficientD, double Offset, double Rss);

    // signal(t) = amplitude * sin(2π * frequency * time + phaseRadians) + offset
    public static SineWaveParameters SineThreeParameter(ReadOnlySpan<double> data, double sampleRateHz, double frequencyHz)
    {
        var coefficients = FitCoefficientsAtFrequency(sampleRateHz, data, frequencyHz);

        double amplitude = Math.Sqrt((coefficients.CoefficientB * coefficients.CoefficientB) + (coefficients.CoefficientD * coefficients.CoefficientD));
        double phaseRadians = Math.Atan2(coefficients.CoefficientD, coefficients.CoefficientB);
        return new SineWaveParameters(frequencyHz, amplitude, phaseRadians * (180.0 / Math.PI), coefficients.Offset);
    }

    public static SineWaveParameters SineFourParameter(ReadOnlySpan<double> data, double sampleRateHz, double approximateFrequencyHz, double searchHalfSpanHz, int coarseSteps = 64, int refinementIterations = 20)
    {
        if (data.Length < 4)
            throw new ArgumentException("At least 4 samples are required for four-parameter fitting.", nameof(data));

        double nyquist = sampleRateHz * 0.5;
        double minFrequency = 1e-12;
        double span = searchHalfSpanHz;

        if (span <= 0)
            throw new ArgumentOutOfRangeException(nameof(searchHalfSpanHz), "Search span must be positive.");

        double lowerFrequency = Math.Clamp(approximateFrequencyHz - span, minFrequency, nyquist);
        double upperFrequency = Math.Clamp(approximateFrequencyHz + span, minFrequency, nyquist);

        if (upperFrequency <= lowerFrequency)
            throw new ArgumentException("Frequency search range is invalid. Check approximate frequency, span, and sample rate.");

        double step = (upperFrequency - lowerFrequency) / coarseSteps;
        double bestFrequency = approximateFrequencyHz;
        double bestRss = double.PositiveInfinity;

        for (int i = 0; i <= coarseSteps; i++)
        {
            double candidateFrequency = lowerFrequency + (i * step);
            var coefficients = FitCoefficientsAtFrequency(sampleRateHz, data, candidateFrequency);
            double rss = coefficients.Rss;
            if (rss < bestRss)
            {
                bestRss = rss;
                bestFrequency = candidateFrequency;
            }
        }
        
        double a = Math.Max(lowerFrequency, bestFrequency - step);
        double b = Math.Min(upperFrequency, bestFrequency + step);

        const double goldenRatio = 0.6180339887498948;
        double c = b - ((b - a) * goldenRatio);
        double d = a + ((b - a) * goldenRatio);
        double rssC = FitCoefficientsAtFrequency(sampleRateHz, data, c).Rss;
        double rssD = FitCoefficientsAtFrequency(sampleRateHz, data, d).Rss;

        for (int i = 0; i < refinementIterations; i++)
        {
            if (rssC < rssD)
            {
                b = d;
                d = c;
                rssD = rssC;
                c = b - ((b - a) * goldenRatio);
                rssC = FitCoefficientsAtFrequency(sampleRateHz, data, c).Rss;
            }
            else
            {
                a = c;
                c = d;
                rssC = rssD;
                d = a + ((b - a) * goldenRatio);
                rssD = FitCoefficientsAtFrequency(sampleRateHz, data, d).Rss;
            }
        }

        double fittedFrequency = (a + b) * 0.5;
        var fittedCoefficients = FitCoefficientsAtFrequency(sampleRateHz, data, fittedFrequency);

        double amplitude = Math.Sqrt((fittedCoefficients.CoefficientB * fittedCoefficients.CoefficientB) + (fittedCoefficients.CoefficientD * fittedCoefficients.CoefficientD));
        double phaseRadians = Math.Atan2(fittedCoefficients.CoefficientD, fittedCoefficients.CoefficientB);
        return new SineWaveParameters(fittedFrequency, amplitude, phaseRadians * (180.0 / Math.PI), fittedCoefficients.Offset);
    }

    private static SineCoefficientFit FitCoefficientsAtFrequency(double sampleRateHz, ReadOnlySpan<double> sampleValues, double frequency)
    {
        int sampleCount = sampleValues.Length;
        double angularFrequency = 2.0 * Math.PI * frequency;

        double sumSin = 0;
        double sumCos = 0;
        double sumOne = 0;
        double sumSinSquared = 0;
        double sumCosSquared = 0;
        double sumSinCos = 0;
        double sumValueSin = 0;
        double sumValueCos = 0;
        double sumValues = 0;

        for (int i = 0; i < sampleCount; i++)
        {
            var time = i / sampleRateHz;
            double sineTerm = Math.Sin(angularFrequency * time);
            double cosineTerm = Math.Cos(angularFrequency * time);
            double signalValue = sampleValues[i];

            sumSin += sineTerm;
            sumCos += cosineTerm;
            sumOne += 1.0;
            sumSinSquared += sineTerm * sineTerm;
            sumCosSquared += cosineTerm * cosineTerm;
            sumSinCos += sineTerm * cosineTerm;
            sumValueSin += signalValue * sineTerm;
            sumValueCos += signalValue * cosineTerm;
            sumValues += signalValue;
        }

        double[,] normalMatrix = {
            { sumSinSquared, sumSinCos, sumSin },
            { sumSinCos, sumCosSquared, sumCos },
            { sumSin, sumCos, sumOne }
        };

        double[] rightHandSide = { sumValueSin, sumValueCos, sumValues };
        double[] coefficients = Solve3x3(normalMatrix, rightHandSide);
        double coefficientB = coefficients[0];
        double coefficientD = coefficients[1];
        double offset = coefficients[2];

        double rss = 0;
        for (int i = 0; i < sampleCount; i++)
        {
            var time = i / sampleRateHz;
            double modelValue = (coefficientB * Math.Sin(angularFrequency * time)) + (coefficientD * Math.Cos(angularFrequency * time)) + offset;
            double residual = sampleValues[i] - modelValue;
            rss += residual * residual;
        }

        return new SineCoefficientFit(coefficientB, coefficientD, offset, rss);
    }

    // Solves a 3x3 linear system using Cramer's Rule
    private static double[] Solve3x3(double[,] matrix, double[] rhs)
    {
        double determinant =
            matrix[0, 0] * (matrix[1, 1] * matrix[2, 2] - matrix[1, 2] * matrix[2, 1]) -
            matrix[0, 1] * (matrix[1, 0] * matrix[2, 2] - matrix[1, 2] * matrix[2, 0]) +
            matrix[0, 2] * (matrix[1, 0] * matrix[2, 1] - matrix[1, 1] * matrix[2, 0]);

        double[] solution = new double[3];

        for (int col = 0; col < 3; col++)
        {
            double[,] matrixCopy = (double[,])matrix.Clone();
            for (int row = 0; row < 3; row++)
            {
                matrixCopy[row, col] = rhs[row];

            }

            double determinantCol =
                matrixCopy[0, 0] * (matrixCopy[1, 1] * matrixCopy[2, 2] - matrixCopy[1, 2] * matrixCopy[2, 1]) -
                matrixCopy[0, 1] * (matrixCopy[1, 0] * matrixCopy[2, 2] - matrixCopy[1, 2] * matrixCopy[2, 0]) +
                matrixCopy[0, 2] * (matrixCopy[1, 0] * matrixCopy[2, 1] - matrixCopy[1, 1] * matrixCopy[2, 0]);

            solution[col] = determinantCol / determinant;
        }

        return solution;
    }
}