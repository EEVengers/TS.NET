namespace TS.NET.Sequencer
{
    using System.Reflection;
    using System.Text;
    using static System.Runtime.InteropServices.JavaScript.JSType;

    public class HtmlReportGenerator
    {
        public void Render(Sequence sequence, string outputFilePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset=\"UTF-8\">");
            sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine($"    <title>Report: {sequence.Name}</title>");
            sb.Append(GetStyles(sequence));
            sb.AppendLine("</head>");
            sb.AppendLine("<body class=\"bg-gray-200 py-4 print:p-0 print:bg-white\">");

            sb.AppendLine($"<div class=\"max-w-4xl mx-auto bg-white px-10 py-15 print:p-0\">");
            sb.AppendLine($"    <h1 class=\"text-4xl text-black p-2 mb-4 text-center bg-gray-100 border-2 border-gray-900\">{sequence.Name}</h1>");
            sb.AppendLine($"    <h1 class=\"text-4xl {StatusTextColour(sequence.Status)} p-2 mb-8 text-center bg-gray-100 border-2 border-gray-900\">{sequence.Status}</h1>");

            sb.AppendLine("    <div class=\"flex flex-col gap-2 mb-8 text-sm text-black\">");
            sb.AppendLine($"        <div>Sequence name: {sequence.Name}</div>");
            sb.AppendLine($"        <div>Start timestamp: {sequence.StartTimestamp:F} ({sequence.TzId})</div>");
            sb.AppendLine($"        <div>Duration: {HumanDuration(sequence.Duration)}</div>");
            sb.AppendLine("    </div>");

            sb.AppendLine("    <table class=\"min-w-full bg-white\">");
            sb.AppendLine("        <thead class=\"bg-gray-200 text-black text-sm leading-normal\">");
            sb.AppendLine("            <tr>");
            sb.AppendLine("                <th class=\"p-1 text-left\">#</th>");
            sb.AppendLine("                <th class=\"p-1 text-left\">Step name</th>");
            sb.AppendLine("                <th class=\"p-1 text-left\">Duration</th>");
            sb.AppendLine("                <th class=\"p-1 text-left\">Summary</th>");
            sb.AppendLine("                <th class=\"p-1 text-left\">Status</th>");
            sb.AppendLine("            </tr>");
            sb.AppendLine("        </thead>");
            sb.AppendLine("        <tbody class=\"text-black text-sm \">");

            if (sequence.Steps != null)
            {
                foreach (var step in sequence.Steps)
                {
                    var status = step.Result?.Status ?? Status.Skipped;
                    sb.AppendLine($"            <tr class=\"border-b border-gray-200\">");
                    sb.AppendLine($"                <td class=\"p-1 text-left whitespace-nowrap\">{step.Index}</td>");
                    sb.AppendLine($"                <td class=\"p-1 text-left\">{step.Name}</td>");
                    sb.AppendLine($"                <td class=\"p-1 text-left\">{HumanDuration(step.Result?.Duration)}</td>");
                    sb.AppendLine($"                <td class=\"p-1 text-left\">{step.Result?.Summary ?? "-"}</td>");
                    sb.AppendLine($"                <td class=\"p-1 text-left {StatusTextColour(step.Result?.Status)}\">{step.Result?.Status.ToString() ?? "-"}</td>");
                    sb.AppendLine("            </tr>");

                    if (step.Result?.Exception != null || (step.Result?.Metadata != null && step.Result.Metadata.Count > 0))
                    {
                        sb.AppendLine("            <tr class=\"bg-gray-50\">");
                        sb.AppendLine("                <td colspan=\"5\" class=\"p-1\">");
                        sb.AppendLine("                    <div class=\"text-sm\">");

                        if (step.Result.Exception != null)
                        {
                            sb.AppendLine("                        <strong class=\"text-red-500\">Exception:</strong>");
                            sb.AppendLine($"                        <pre class=\"bg-red-100 text-red-700 p-2 mt-1 whitespace-pre-wrap\">{step.Result.Exception}</pre>");
                        }

                        if (step.Result.Metadata != null)
                        {
                            foreach (var meta in step.Result.Metadata)
                            {
                                switch (meta)
                                {
                                    case StepResultChart chart:
                                        sb.AppendLine("                        <em class=\"text-gray-500\">Chart metadata not yet implemented in report.</em>");
                                        break;
                                    default:
                                        sb.AppendLine("                        <em class=\"text-gray-500\">Metadata type not implemented in report generator.</em>");
                                        break;
                                }
                            }
                        }

                        sb.AppendLine("                    </div>");
                        sb.AppendLine("                </td>");
                        sb.AppendLine("            </tr>");
                    }
                }
            }

            sb.AppendLine("        </tbody>");
            sb.AppendLine("    </table>");
            sb.AppendLine("</div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            File.WriteAllText(outputFilePath, sb.ToString());
        }

        private static string HumanDuration(TimeSpan? duration)
        {
            if (duration == null)
                return "-";
            if (duration?.TotalHours > 1)
                return $"{duration?.Hours}h {duration?.Minutes}m {duration?.Seconds}s";
            else if (duration?.TotalMinutes > 1)
                return $"{duration?.Minutes}m {duration?.Seconds}s";
            else
                return $"{duration?.Seconds}.{duration?.Milliseconds:D3}s";
        }

        private static string StatusTextColour(Status? status)
        {
            switch(status)
            {
                case Status.Running:
                case Status.Cancelled:
                    return "text-yellow-600";
                case Status.Passed:
                case Status.Done:
                    return "text-green-600";
                case Status.Failed:
                case Status.Error:
                    return "text-red-600";
                case Status.Skipped:
                case null:
                    return "text-black";
                default:
                    throw new NotImplementedException();
            }    
        }

        private static string GetStyles(Sequence sequence)
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
                        content = content.Replace("[@top-right content]", "Device: TS00007");
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
}
