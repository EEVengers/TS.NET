using System.Reflection;
using System.Text;

namespace TS.NET.Sequencer;

public static class ReportHelpers
{
    public static string StatusTextColour(Status? status)
    {
        return status switch
        {
            Status.Running or Status.Cancelled => "text-yellow-600",
            Status.Passed or Status.Done => "text-green-600",
            Status.Failed or Status.Error => "text-red-600",
            Status.Skipped or null => "text-black",
            _ => throw new NotImplementedException()
        };
    }

    public static string GetStyles(Sequence sequence)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<style>");

        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TS.NET.Sequencer.report.css"))
        {
            if (stream != null)
            {
                using (var reader = new StreamReader(stream))
                {
                    var content = reader.ReadToEnd();
                    content = content.Replace("[@top-center content]", sequence.Name);
                    content = content.Replace("[@top-right content]", "Device: TS0019");
                    content = content.Replace("[@bottom-left content]", sequence.StartTimestamp.ToString("yyyy-MM-dd HH:mm:ss"));
                    sb.Append(content);
                }
            }
        }

        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TS.NET.Sequencer.report-tailwind.css"))
        {
            if (stream != null)
            {
                using (var reader = new StreamReader(stream))
                {
                    sb.Append(reader.ReadToEnd());
                }
            }
        }

        sb.AppendLine("</style>");
        return sb.ToString();
    }
}
