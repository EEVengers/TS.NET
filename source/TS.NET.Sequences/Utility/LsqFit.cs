using System.Buffers;
using System.Numerics.Tensors;

namespace TS.NET.Sequences;

public readonly record struct SineWaveParameters(double FrequencyHz, double Amplitude, double PhaseDeg, double Offset);

public static class LsqFit
{
    private readonly record struct SineCoefficientFit(double CoefficientB, double CoefficientD, double Offset, double Rss);

    // signal(t) = amplitude * sin(2π * frequency * time + phaseRadians) + offset
    public static SineWaveParameters SineThreeParameter(ReadOnlySpan<double> data, double sampleRateHz, double frequencyHz)
    {
        var sampleCount = data.Length;
        var sinBuffer = ArrayPool<double>.Shared.Rent(sampleCount);
        var cosBuffer = ArrayPool<double>.Shared.Rent(sampleCount);
        SineCoefficientFit coefficients;
        try
        {
            coefficients = FitCoefficientsAtFrequency(sampleRateHz, data, frequencyHz, sinBuffer.AsSpan(0, sampleCount), cosBuffer.AsSpan(0, sampleCount));
        }
        finally
        {
            ArrayPool<double>.Shared.Return(sinBuffer, clearArray: false);
            ArrayPool<double>.Shared.Return(cosBuffer, clearArray: false);
        }

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

        var sampleCount = data.Length;
        var sinBuffer = ArrayPool<double>.Shared.Rent(sampleCount);
        var cosBuffer = ArrayPool<double>.Shared.Rent(sampleCount);
        try
        {
            var sinTerms = sinBuffer.AsSpan(0, sampleCount);
            var cosTerms = cosBuffer.AsSpan(0, sampleCount);

            double step = (upperFrequency - lowerFrequency) / coarseSteps;
            double bestFrequency = approximateFrequencyHz;
            double bestRss = double.PositiveInfinity;

            for (int i = 0; i <= coarseSteps; i++)
            {
                double candidateFrequency = lowerFrequency + (i * step);
                var coefficients = FitCoefficientsAtFrequency(sampleRateHz, data, candidateFrequency, sinTerms, cosTerms);
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
            double rssC = FitCoefficientsAtFrequency(sampleRateHz, data, c, sinTerms, cosTerms).Rss;
            double rssD = FitCoefficientsAtFrequency(sampleRateHz, data, d, sinTerms, cosTerms).Rss;

            for (int i = 0; i < refinementIterations; i++)
            {
                if (rssC < rssD)
                {
                    b = d;
                    d = c;
                    rssD = rssC;
                    c = b - ((b - a) * goldenRatio);
                    rssC = FitCoefficientsAtFrequency(sampleRateHz, data, c, sinTerms, cosTerms).Rss;
                }
                else
                {
                    a = c;
                    c = d;
                    rssC = rssD;
                    d = a + ((b - a) * goldenRatio);
                    rssD = FitCoefficientsAtFrequency(sampleRateHz, data, d, sinTerms, cosTerms).Rss;
                }
            }

            double fittedFrequency = (a + b) * 0.5;
            var fittedCoefficients = FitCoefficientsAtFrequency(sampleRateHz, data, fittedFrequency, sinTerms, cosTerms);

            double amplitude = Math.Sqrt((fittedCoefficients.CoefficientB * fittedCoefficients.CoefficientB) + (fittedCoefficients.CoefficientD * fittedCoefficients.CoefficientD));
            double phaseRadians = Math.Atan2(fittedCoefficients.CoefficientD, fittedCoefficients.CoefficientB);
            return new SineWaveParameters(fittedFrequency, amplitude, phaseRadians * (180.0 / Math.PI), fittedCoefficients.Offset);
        }
        finally
        {
            ArrayPool<double>.Shared.Return(sinBuffer, clearArray: false);
            ArrayPool<double>.Shared.Return(cosBuffer, clearArray: false);
        }
    }

    private static SineCoefficientFit FitCoefficientsAtFrequency(double sampleRateHz, ReadOnlySpan<double> sampleValues, double frequency, Span<double> sinTerms, Span<double> cosTerms)
    {
        int sampleCount = sampleValues.Length;
        if (sinTerms.Length < sampleCount || cosTerms.Length < sampleCount)
            throw new ArgumentException("Basis buffers must be at least sample length.");

        var sinBasis = sinTerms.Slice(0, sampleCount);
        var cosBasis = cosTerms.Slice(0, sampleCount);
        BuildSinCosBasis(sampleRateHz, frequency, sinBasis, cosBasis);

        double sumSin = TensorPrimitives.Sum(sinBasis);
        double sumCos = TensorPrimitives.Sum(cosBasis);
        double sumOne = sampleCount;
        double sumSinSquared = TensorPrimitives.Dot(sinBasis, sinBasis);
        double sumCosSquared = TensorPrimitives.Dot(cosBasis, cosBasis);
        double sumSinCos = TensorPrimitives.Dot(sinBasis, cosBasis);
        double sumValueSin = TensorPrimitives.Dot(sampleValues, sinBasis);
        double sumValueCos = TensorPrimitives.Dot(sampleValues, cosBasis);
        double sumValues = TensorPrimitives.Sum(sampleValues);
        double sumValueSquared = TensorPrimitives.Dot(sampleValues, sampleValues);

        Solve3x3(
            m11: sumSinSquared,
            m12: sumSinCos,
            m13: sumSin,
            m21: sumSinCos,
            m22: sumCosSquared,
            m23: sumCos,
            m31: sumSin,
            m32: sumCos,
            m33: sumOne,
            rhs1: sumValueSin,
            rhs2: sumValueCos,
            rhs3: sumValues,
            out double coefficientB,
            out double coefficientD,
            out double offset);

        // Residual sum of squares expanded in terms of the precomputed sums.
        double rss = sumValueSquared
            - (2.0 * ((coefficientB * sumValueSin) + (coefficientD * sumValueCos) + (offset * sumValues)))
            + ((coefficientB * coefficientB * sumSinSquared)
            + (coefficientD * coefficientD * sumCosSquared)
            + (offset * offset * sumOne)
            + (2.0 * coefficientB * coefficientD * sumSinCos)
            + (2.0 * coefficientB * offset * sumSin)
            + (2.0 * coefficientD * offset * sumCos));

        if (rss < 0 && rss > -1e-9)
            rss = 0;

        return new SineCoefficientFit(coefficientB, coefficientD, offset, rss);
    }

    private static void BuildSinCosBasis(double sampleRateHz, double frequency, Span<double> sinBasis, Span<double> cosBasis)
    {
        double angularStep = (2.0 * Math.PI * frequency) / sampleRateHz;
        double sinStep = Math.Sin(angularStep);
        double cosStep = Math.Cos(angularStep);

        double sinCurrent = 0.0;
        double cosCurrent = 1.0;

        for (int i = 0; i < sinBasis.Length; i++)
        {
            sinBasis[i] = sinCurrent;
            cosBasis[i] = cosCurrent;

            double nextSin = (sinCurrent * cosStep) + (cosCurrent * sinStep);
            double nextCos = (cosCurrent * cosStep) - (sinCurrent * sinStep);

            sinCurrent = nextSin;
            cosCurrent = nextCos;

            // Keep recurrence numerically stable for long vectors.
            if ((i & 1023) == 1023)
            {
                double magnitude = Math.Sqrt((sinCurrent * sinCurrent) + (cosCurrent * cosCurrent));
                if (magnitude != 0)
                {
                    double invMagnitude = 1.0 / magnitude;
                    sinCurrent *= invMagnitude;
                    cosCurrent *= invMagnitude;
                }
            }
        }
    }

    // Solves a 3x3 linear system using Cramer's Rule
    private static void Solve3x3(
        double m11, double m12, double m13,
        double m21, double m22, double m23,
        double m31, double m32, double m33,
        double rhs1, double rhs2, double rhs3,
        out double x1, out double x2, out double x3)
    {
        double determinant = (m11 * ((m22 * m33) - (m23 * m32))) - (m12 * ((m21 * m33) - (m23 * m31))) + (m13 * ((m21 * m32) - (m22 * m31)));

        if (Math.Abs(determinant) < 1e-24)
        {
            x1 = 0;
            x2 = 0;
            x3 = 0;
            return;
        }

        double determinantX1 = (rhs1 * ((m22 * m33) - (m23 * m32))) - (m12 * ((rhs2 * m33) - (m23 * rhs3))) + (m13 * ((rhs2 * m32) - (m22 * rhs3)));
        double determinantX2 = (m11 * ((rhs2 * m33) - (m23 * rhs3))) - (rhs1 * ((m21 * m33) - (m23 * m31))) + (m13 * ((m21 * rhs3) - (rhs2 * m31)));
        double determinantX3 = (m11 * ((m22 * rhs3) - (rhs2 * m32))) - (m12 * ((m21 * rhs3) - (rhs2 * m31))) + (rhs1 * ((m21 * m32) - (m22 * m31)));

        x1 = determinantX1 / determinant;
        x2 = determinantX2 / determinant;
        x3 = determinantX3 / determinant;
    }
}