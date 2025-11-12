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

        using var stream1 = Assembly.GetExecutingAssembly().GetManifestResourceStream("TS.NET.Sequencer.HTML.report.css");
        if (stream1 != null)
        {
            using var reader = new StreamReader(stream1);
            var content = reader.ReadToEnd();
            content = content.Replace("[@top-center content]", sequence.Name);
            content = content.Replace("[@top-right content]", "Device: TS0019");
            content = content.Replace("[@bottom-left content]", sequence.StartTimestamp.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.Append(content);
        }

        using var stream2 = Assembly.GetExecutingAssembly().GetManifestResourceStream("TS.NET.Sequencer.HTML.report-tailwind.css");
        if (stream2 != null)
        {
            using var reader = new StreamReader(stream2);
            sb.Append(reader.ReadToEnd());
        }

        sb.AppendLine("</style>");
        return sb.ToString();
    }

    public static string GetLibraryD3(bool include)
    {
        var sb = new StringBuilder();
        if (include)
        {
            using var stream1 = Assembly.GetExecutingAssembly().GetManifestResourceStream("TS.NET.Sequencer.HTML.ResultMetadataXYChart.css");
            if (stream1 != null)
            {
                sb.AppendLine("<style>");
                using var reader = new StreamReader(stream1);
                sb.Append(reader.ReadToEnd());
                sb.AppendLine("</style>");
            }

            using var stream2 = Assembly.GetExecutingAssembly().GetManifestResourceStream("TS.NET.Sequencer.HTML.d3.v7.min.js");
            if (stream2 != null)
            {
                using var reader = new StreamReader(stream2);
                sb.AppendLine("<script>");
                sb.Append(reader.ReadToEnd());
                sb.AppendLine("</script>");
            }

            using var stream3 = Assembly.GetExecutingAssembly().GetManifestResourceStream("TS.NET.Sequencer.HTML.ResultMetadataXYChart.js");
            if (stream3 != null)
            {
                sb.AppendLine("<script>");
                using var reader = new StreamReader(stream3);
                sb.Append(reader.ReadToEnd());
                sb.AppendLine("</script>");
            }
        }
        return sb.ToString();
    }
}
