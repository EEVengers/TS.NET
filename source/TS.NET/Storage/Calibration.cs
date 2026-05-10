using System.Text.Json;
using System.Text.RegularExpressions;

namespace TS.NET;

public class Calibration
{
    public int Version { get; set; } = 1;
    public string Serial { get; set; } = "";
    public string Timestamp { get; set; } = "";
    public FrontendCalibration[] Frontend { get; set; } = [FrontendCalibration.Default(0), FrontendCalibration.Default(1), FrontendCalibration.Default(2), FrontendCalibration.Default(3)];
    public AdcCalibration Adc { get; set; } = AdcCalibration.Default();

    public static Calibration Default()
    {
        return new Calibration();
    }

    public static Calibration FromJsonFile(string file)
    {
        if (!File.Exists(file))
            throw new FileNotFoundException(file);

        return FromFileJson(File.ReadAllText(file)) ?? throw new ArgumentNullException();
    }

    public void ToJsonFile(string path)
    {
        File.WriteAllText(path, ToFileJson());
    }

    public string ToFileJson()
    {
        var json = JsonSerializer.Serialize(this, FileJsonSerializerContext.Default.Calibration);
        return FlattenNumericArrays(json);
    }

    public string ToDeviceJson()
    {
        return JsonSerializer.Serialize(this, DeviceJsonSerializerContext.Default.Calibration);
    }

    public static Calibration FromFileJson(string json)
    {
        return JsonSerializer.Deserialize(json, FileJsonSerializerContext.Default.Calibration) ?? throw new ArgumentNullException();
    }

    public static Calibration FromDeviceJson(string json)
    {
        return JsonSerializer.Deserialize(json, DeviceJsonSerializerContext.Default.Calibration) ?? throw new ArgumentNullException();
    }

    private static string FlattenNumericArrays(string json)
    {
        const string number = @"[-+]?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?";
        var pattern = $"\"(?<name>[^\"]+)\"\\s*:\\s*\\[(?<values>\\s*(?:{number}\\s*(?:,\\s*{number}\\s*)*)?)\\]";

        return Regex.Replace(json, pattern,
            static match =>
            {
                var propertyName = match.Groups["name"].Value;
                var values = match.Groups["values"].Value;

                values = Regex.Replace(values, @"\s*,\s*", ",");
                values = Regex.Replace(values, @"\s+", "").Trim();

                return $"\"{propertyName}\": [{values}]";
            },
            RegexOptions.Singleline);
    }
}

public class FrontendCalibration
{
    public int Channel { get; set; }
    public double AttenuatorScale { get; set; }
    public required FrontendPathCalibration[] Path { get; set; }

    // Set up any defaults that don't get set during calibration, such as TrimDPot.
    public static FrontendCalibration Default(int channel)
    {
        return new FrontendCalibration()
        {
            Channel = channel,
            Path =
            [
                new(){ PgaPreampGain = PgaPreampGain.High, PgaLadder = 0, TrimDPot = 20 },
                new(){ PgaPreampGain = PgaPreampGain.High, PgaLadder = 1, TrimDPot = 20 },
                new(){ PgaPreampGain = PgaPreampGain.High, PgaLadder = 2, TrimDPot = 20 },
                new(){ PgaPreampGain = PgaPreampGain.High, PgaLadder = 3, TrimDPot = 19 },
                new(){ PgaPreampGain = PgaPreampGain.High, PgaLadder = 4, TrimDPot = 19 },
                new(){ PgaPreampGain = PgaPreampGain.High, PgaLadder = 5, TrimDPot = 19 },
                new(){ PgaPreampGain = PgaPreampGain.High, PgaLadder = 6, TrimDPot = 18 },
                new(){ PgaPreampGain = PgaPreampGain.High, PgaLadder = 7, TrimDPot = 18 },
                new(){ PgaPreampGain = PgaPreampGain.High, PgaLadder = 8, TrimDPot = 12 },
                new(){ PgaPreampGain = PgaPreampGain.High, PgaLadder = 9, TrimDPot = 9 },
                new(){ PgaPreampGain = PgaPreampGain.High, PgaLadder = 10, TrimDPot = 7 },
                new(){ PgaPreampGain = PgaPreampGain.Low, PgaLadder = 0, TrimDPot = 7 },
                new(){ PgaPreampGain = PgaPreampGain.Low, PgaLadder = 1, TrimDPot = 5 },
                new(){ PgaPreampGain = PgaPreampGain.Low, PgaLadder = 2, TrimDPot = 4 },
                new(){ PgaPreampGain = PgaPreampGain.Low, PgaLadder = 3, TrimDPot = 4 },
                new(){ PgaPreampGain = PgaPreampGain.Low, PgaLadder = 4, TrimDPot = 4 },
                new(){ PgaPreampGain = PgaPreampGain.Low, PgaLadder = 5, TrimDPot = 4 },
                new(){ PgaPreampGain = PgaPreampGain.Low, PgaLadder = 6, TrimDPot = 4 },
                new(){ PgaPreampGain = PgaPreampGain.Low, PgaLadder = 7, TrimDPot = 4 },
                new(){ PgaPreampGain = PgaPreampGain.Low, PgaLadder = 8, TrimDPot = 4 },
                new(){ PgaPreampGain = PgaPreampGain.Low, PgaLadder = 9, TrimDPot = 4 },
                new(){ PgaPreampGain = PgaPreampGain.Low, PgaLadder = 10, TrimDPot = 4 },
            ]
        };
    }
}

public class FrontendPathCalibration     // LMH6518
{
    public required PgaPreampGain PgaPreampGain { get; set; }
    public required byte PgaLadder { get; set; }    // One of 11 values: 0 - 10
    public required byte TrimDPot { get; set; }     // 8 bit raw value. Step resistance: 390.625 (100k/256). Min value = 4 (1580/390.625) [1580 from sim to fit allowed PGA input CM range].
    public double TrimDacScale { get; set; }        // DAC LSB normalised to ADC fullscale
    public double TrimDacZeroM { get; set; }        // Part of Y=mx+c, where m is temperature, and Y is the trim DAC code.
    public double TrimDacZeroC { get; set; }        // Part of Y=mx+c, where m is temperature, and Y is the trim DAC code.
    public double BufferInputVpp { get; set; }      // PGA input volts peak-peak for full ADC range in single channel mode at maximum sample rate
}

public enum PgaPreampGain
{
    Low = 0,
    High = 1
}

public class LoadScaleCalibration
{
    public required int[] Channel { get; set; }
    public required RateLoadScaleCalibration[] RateScale { get; set; }
}

public class RateLoadScaleCalibration
{
    public required uint Rate { get; set; }
    public required double[] Scale { get; set; }
}

public class BranchGainCalibration
{
    public required int[] Channel { get; set; }
    public required RateBranchGainCalibration[] RateGain { get; set; }
}

public class RateBranchGainCalibration
{
    public required uint Rate { get; set; }
    public required int[] Gain { get; set; }
}

// ADC Calibration Data
public class AdcCalibration
{
    public required LoadScaleCalibration[] LoadScale { get; set; }
    public required BranchGainCalibration[] BranchGain { get; set; }

    public static AdcCalibration Default()
    {
        return new AdcCalibration()
        {
            LoadScale = [
                new() { Channel = [0], RateScale = [
                    new() { Rate = 1000000000, Scale = [1.0]},
                    new() { Rate = 660000000, Scale = [1.0]},
                    new() { Rate = 500000000, Scale = [1.0]},
                    new() { Rate = 330000000, Scale = [1.0]},
                    new() { Rate = 250000000, Scale = [1.0]},
                    new() { Rate = 165000000, Scale = [1.0]},
                    new() { Rate = 100000000, Scale = [1.0]},
                ] },
                new() { Channel = [1], RateScale = [
                    new() { Rate = 1000000000, Scale = [1.0]},
                    new() { Rate = 660000000, Scale = [1.0]},
                    new() { Rate = 500000000, Scale = [1.0]},
                    new() { Rate = 330000000, Scale = [1.0]},
                    new() { Rate = 250000000, Scale = [1.0]},
                    new() { Rate = 165000000, Scale = [1.0]},
                    new() { Rate = 100000000, Scale = [1.0]},
                ] },
                new() { Channel = [2], RateScale = [
                    new() { Rate = 1000000000, Scale = [1.0]},
                    new() { Rate = 660000000, Scale = [1.0]},
                    new() { Rate = 500000000, Scale = [1.0]},
                    new() { Rate = 330000000, Scale = [1.0]},
                    new() { Rate = 250000000, Scale = [1.0]},
                    new() { Rate = 165000000, Scale = [1.0]},
                    new() { Rate = 100000000, Scale = [1.0]},
                ] },
                new() { Channel = [3], RateScale = [
                    new() { Rate = 1000000000, Scale = [1.0]},
                    new() { Rate = 660000000, Scale = [1.0]},
                    new() { Rate = 500000000, Scale = [1.0]},
                    new() { Rate = 330000000, Scale = [1.0]},
                    new() { Rate = 250000000, Scale = [1.0]},
                    new() { Rate = 165000000, Scale = [1.0]},
                    new() { Rate = 100000000, Scale = [1.0]},
                ] },
                new() { Channel = [0,1], RateScale = [
                    new() { Rate = 500000000, Scale =[1.0, 1.0]},
                    new() { Rate = 330000000, Scale = [1.0, 1.0]},
                    new() { Rate = 250000000, Scale = [1.0, 1.0]},
                    new() { Rate = 165000000, Scale = [1.0, 1.0]},
                    new() { Rate = 100000000, Scale = [1.0, 1.0]},
                ] },
                new() { Channel = [1,2], RateScale = [
                    new() { Rate = 500000000, Scale = [1.0, 1.0]},
                    new() { Rate = 330000000, Scale = [1.0, 1.0]},
                    new() { Rate = 250000000, Scale = [1.0, 1.0]},
                    new() { Rate = 165000000, Scale = [1.0, 1.0]},
                    new() { Rate = 100000000, Scale = [1.0, 1.0]},
                ] },
                new() { Channel = [2,3], RateScale = [
                    new() { Rate = 500000000, Scale = [1.0, 1.0]},
                    new() { Rate = 330000000, Scale = [1.0, 1.0]},
                    new() { Rate = 250000000, Scale = [1.0, 1.0]},
                    new() { Rate = 165000000, Scale = [1.0, 1.0]},
                    new() { Rate = 100000000, Scale = [1.0, 1.0]},
                ] },
                new() { Channel = [0,2], RateScale = [
                    new() { Rate = 500000000, Scale = [1.0, 1.0]},
                    new() { Rate = 330000000, Scale = [1.0, 1.0]},
                    new() { Rate = 250000000, Scale = [1.0, 1.0]},
                    new() { Rate = 165000000, Scale = [1.0, 1.0]},
                    new() { Rate = 100000000, Scale = [1.0, 1.0]},
                ] },
                new() { Channel = [1,3], RateScale = [
                    new() { Rate = 500000000, Scale = [1.0, 1.0]},
                    new() { Rate = 330000000, Scale = [1.0, 1.0]},
                    new() { Rate = 250000000, Scale = [1.0, 1.0]},
                    new() { Rate = 165000000, Scale = [1.0, 1.0]},
                    new() { Rate = 100000000, Scale = [1.0, 1.0]},
                ] },
                new() { Channel = [0,3], RateScale = [
                    new() { Rate = 500000000, Scale = [1.0, 1.0]},
                    new() { Rate = 330000000, Scale = [1.0, 1.0]},
                    new() { Rate = 250000000, Scale = [1.0, 1.0]},
                    new() { Rate = 165000000, Scale = [1.0, 1.0]},
                    new() { Rate = 100000000, Scale = [1.0, 1.0]},
                ] },
                new() { Channel = [0,1,2,3], RateScale = [
                    new() { Rate = 250000000, Scale = [1.0, 1.0, 1.0, 1.0]},
                    new() { Rate = 165000000, Scale = [1.0, 1.0, 1.0, 1.0]},
                    new() { Rate = 100000000, Scale = [1.0, 1.0, 1.0, 1.0]},
                ] },
            ],
            BranchGain = [
                new() { Channel = [0], RateGain = [
                    new() { Rate = 1000000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 660000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 500000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 330000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 250000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 165000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 100000000, Gain = [0,0,0,0,0,0,0,0]},
                ] },
                new() { Channel = [1], RateGain = [
                    new() { Rate = 1000000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 660000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 500000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 330000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 250000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 165000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 100000000, Gain = [0,0,0,0,0,0,0,0]},
                ] },
                new() { Channel = [2], RateGain = [
                    new() { Rate = 1000000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 660000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 500000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 330000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 250000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 165000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 100000000, Gain = [0,0,0,0,0,0,0,0]},
                ] },
                new() { Channel = [3], RateGain = [
                    new() { Rate = 1000000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 660000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 500000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 330000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 250000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 165000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 100000000, Gain = [0,0,0,0,0,0,0,0]},
                ] },
                new() { Channel = [0,1], RateGain = [
                    new() { Rate = 500000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 330000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 250000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 165000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 100000000, Gain = [0,0,0,0,0,0,0,0]},
                ] },
                new() { Channel = [1,2], RateGain = [
                    new() { Rate = 500000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 330000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 250000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 165000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 100000000, Gain = [0,0,0,0,0,0,0,0]},
                ] },
                new() { Channel = [2,3], RateGain = [
                    new() { Rate = 500000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 330000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 250000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 165000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 100000000, Gain = [0,0,0,0,0,0,0,0]},
                ] },
                new() { Channel = [0,2], RateGain = [
                    new() { Rate = 500000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 330000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 250000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 165000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 100000000, Gain = [0,0,0,0,0,0,0,0]},
                ] },
                new() { Channel = [1,3], RateGain = [
                    new() { Rate = 500000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 330000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 250000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 165000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 100000000, Gain = [0,0,0,0,0,0,0,0]},
                ] },
                new() { Channel = [0,3], RateGain = [
                    new() { Rate = 500000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 330000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 250000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 165000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 100000000, Gain = [0,0,0,0,0,0,0,0]},
                ] },
                new() { Channel = [0,1,2,3], RateGain = [
                    new() { Rate = 250000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 165000000, Gain = [0,0,0,0,0,0,0,0]},
                    new() { Rate = 100000000, Gain = [0,0,0,0,0,0,0,0]},
                ] },
            ]
        };
    }
}

