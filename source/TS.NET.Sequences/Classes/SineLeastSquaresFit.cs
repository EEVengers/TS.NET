namespace TS.NET.Sequences;

public static class SineLeastSquaresFit
{
    // signal(t) = amplitude * sin(2π * frequency * time + phaseRadians) + offset
    public static (double amplitude, double phaseRadians, double offset) FitSineWave(double sampleRateHz, double[] sampleValues, double frequency)
    {
        double[] times = new double[sampleValues.Length];
        for (int i = 0; i < sampleValues.Length; i++)
        {
            times[i] = i / sampleRateHz;
        }

        int sampleCount = times.Length;
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
            double sineTerm = Math.Sin(angularFrequency * times[i]);
            double cosineTerm = Math.Cos(angularFrequency * times[i]);
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

        // Build the 3x3 system (normal equations)
        double[,] normalMatrix = {
        { sumSinSquared, sumSinCos, sumSin },
        { sumSinCos, sumCosSquared, sumCos },
        { sumSin, sumCos, sumOne }
    };

        double[] rightHandSide = { sumValueSin, sumValueCos, sumValues };

        // Solve for coefficients B, D, and offset C
        double[] coefficients = Solve3x3(normalMatrix, rightHandSide);
        double coefficientB = coefficients[0]; // for sin term
        double coefficientD = coefficients[1]; // for cos term
        double offset = coefficients[2];       // DC offset

        // Convert B and D to amplitude and phase
        double amplitude = Math.Sqrt(coefficientB * coefficientB + coefficientD * coefficientD);
        double phaseRadians = Math.Atan2(coefficientD, coefficientB);

        return (amplitude, phaseRadians, offset);
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
                matrixCopy[row, col] = rhs[row];

            double determinantCol =
                matrixCopy[0, 0] * (matrixCopy[1, 1] * matrixCopy[2, 2] - matrixCopy[1, 2] * matrixCopy[2, 1]) -
                matrixCopy[0, 1] * (matrixCopy[1, 0] * matrixCopy[2, 2] - matrixCopy[1, 2] * matrixCopy[2, 0]) +
                matrixCopy[0, 2] * (matrixCopy[1, 0] * matrixCopy[2, 1] - matrixCopy[1, 1] * matrixCopy[2, 0]);

            solution[col] = determinantCol / determinant;
        }

        return solution;
    }
}