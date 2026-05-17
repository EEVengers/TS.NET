using TS.NET.Sequencer;

namespace TS.NET.Sequences;

public class AcRmsStep : Step
{
    private readonly record struct FrontendConfiguration(ThunderscopeTermination Termination, PgaPreampGain PgaPreampGain, int PgaLadder, ThunderscopeBandwidth Bandwidth);

    public double MinLimit { get; set; } = double.MinValue;
    public double MaxLimit { get; set; } = double.MaxValue;
    public int Averages { get; set; } = 10;

    public AcRmsStep(string name, int channelIndex, uint rateHz, CommonVariables variables) : base(name)
    {
        Action = (CancellationToken cancellationToken) =>
        {
            TestbenchException.TestbenchExceptionIfTrimDacZeroNotCalibrated(variables);
            TestbenchException.TestbenchExceptionIfTrimDacScaleNotCalibrated(variables);
            TestbenchException.TestbenchExceptionIfBufferInputVppNotCalibrated(variables);
            TestbenchException.TestbenchExceptionIfAdcBranchGainsNotCalibrated(variables);

            Instruments.Instance.SetThunderscopeChannel([channelIndex]);
            Instruments.Instance.SetThunderscopeResolution(AdcResolution.EightBit);
            Instruments.Instance.SetThunderscopeRate(rateHz);

            var combinations = BuildMeasurementCombinations();
            var rows = new List<string[]>();
            var rowByKey = new Dictionary<(ThunderscopeTermination Termination, PgaPreampGain PgaPreampGain, int PgaLadder), string[]>();
            var passed = 0;

            foreach (var combination in combinations)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var stdDevV = RunMeasurement(channelIndex, combination, rateHz, variables, cancellationToken);
                var key = (combination.Termination, combination.PgaPreampGain, combination.PgaLadder);
                if (!rowByKey.TryGetValue(key, out var row))
                {
                    row = [combination.Termination == ThunderscopeTermination.FiftyOhm ? "50R" : "1M", $"{combination.PgaPreampGain}", $"{combination.PgaLadder}", "", "", "", "", ""];
                    rowByKey[key] = row;
                    rows.Add(row);
                }

                row[GetBandwidthColumnIndex(combination.Bandwidth)] = $"{stdDevV * 1e6:F0}";
                Logger.Instance.Log(LogLevel.Information, Index, $"{combination.Termination}, {combination.PgaPreampGain}, {combination.PgaLadder}, {combination.Bandwidth}, {stdDevV * 1e6:F0} uVrms");

                if (stdDevV >= MinLimit && stdDevV <= MaxLimit)
                    passed++;
            }

            Result!.Metadata!.Add(new ResultMetadataTable
            {
                ShowInReport = true,
                Name = "AC RMS (bandwidth: 10kHz to PGA filter cutoff)",
                Headers = ["Termination", "PGA gain", "PGA ladder", "20MHz (uVrms)", "100MHz (uVrms)", "200MHz (uVrms)", "350MHz (uVrms)", "Full (uVrms)"],
                Rows = [.. rows]
            });

            return (passed == combinations.Length) ? Status.Passed : Status.Failed;
        };
    }

    private static FrontendConfiguration[] BuildMeasurementCombinations()
    {
        var terminations = Enum.GetValues<ThunderscopeTermination>();
        PgaPreampGain[] pgaPreampGains = [PgaPreampGain.High];
        ThunderscopeBandwidth[] bandwidths = [ThunderscopeBandwidth.Bw20M, ThunderscopeBandwidth.Bw100M, ThunderscopeBandwidth.Bw200M, ThunderscopeBandwidth.Bw350M, ThunderscopeBandwidth.BwFull];

        var combinations = new List<FrontendConfiguration>();

        foreach (var termination in terminations)
        {
            foreach (var pgaPreampGain in pgaPreampGains)
            {
                // Noise isn't large enough to get good readings in wider ranges
                for (int pgaLadder = 4; pgaLadder >= 0; pgaLadder--)
                {
                    foreach (var bandwidth in bandwidths)
                    {
                        combinations.Add(new FrontendConfiguration(termination, pgaPreampGain, pgaLadder, bandwidth));
                    }
                }
            }
        }

        return [.. combinations];
    }

    private static int GetBandwidthColumnIndex(ThunderscopeBandwidth bandwidth)
    {
        return bandwidth switch
        {
            ThunderscopeBandwidth.Bw20M => 3,
            ThunderscopeBandwidth.Bw100M => 4,
            ThunderscopeBandwidth.Bw200M => 5,
            ThunderscopeBandwidth.Bw350M => 6,
            ThunderscopeBandwidth.BwFull => 7,
            _ => throw new ArgumentOutOfRangeException(nameof(bandwidth), bandwidth, null)
        };
    }

    private double RunMeasurement(int channelIndex, FrontendConfiguration combination, uint rateHz, CommonVariables variables, CancellationToken cancellationToken)
    {
        var pathCalibration = Utility.GetChannelPathCalibration(channelIndex, combination.PgaPreampGain, combination.PgaLadder, variables);
        var temperature = Instruments.Instance.GetThunderscopeFpgaTemp();
        var trimDacZero = Frontend.GetTrimDacZero(temperature, pathCalibration.TrimDacZeroM, pathCalibration.TrimDacZeroC);

        var branchGains = Frontend.GetAdcBranchGain(variables.Calibration, [channelIndex], rateHz);
        Instruments.Instance.SetThunderscopeBranchGains(branchGains);
        //Instruments.Instance.SetThunderscopeBranchGains([0, 0, 0, 0, 0, 0, 0, 0]);

        switch (combination.Termination)
        {
            case ThunderscopeTermination.FiftyOhm:
                Instruments.Instance.SetThunderscopeCalManual50R(channelIndex, false, trimDacZero, pathCalibration.TrimDPot, pathCalibration.PgaPreampGain, pathCalibration.PgaLadder, combination.Bandwidth, 0);
                break;
            case ThunderscopeTermination.OneMegaohm:
                Instruments.Instance.SetThunderscopeCalManual1M(channelIndex, false, trimDacZero, pathCalibration.TrimDPot, pathCalibration.PgaPreampGain, pathCalibration.PgaLadder, combination.Bandwidth, 0);
                break;
        }

        double stdDevV = 0;
        for (int i = 0; i < Averages; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var stdDev = Instruments.Instance.GetThunderscopePopulationStdDev(channelIndex, sampleCount: 100_000);
            stdDevV += stdDev * (pathCalibration.BufferInputVpp / 256.0);
            //stdDevV += stdDev * (pathCalibration.BufferInputVpp / 4096.0);
        }
        stdDevV /= Averages;
        return stdDevV;
    }
}
