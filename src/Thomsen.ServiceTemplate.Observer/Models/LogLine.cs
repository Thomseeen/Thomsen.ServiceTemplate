using System.Text.RegularExpressions;
using System.Windows.Media;

namespace Thomsen.ServiceTemplate.Observer.Models;

record class LogLine {
    public DateTime? TimeStamp { get; init; } = null;

    public string Text { get; init; }

    public Brush Color { get; init; } = Brushes.Black;

    public LogLine(string text) {
        if (TryGetColorByLogLevel(text, out Brush color)) {
            Color = color;
        }

        if (TryGetTimeStamp(ref text, out DateTime timeStamp)) {
            TimeStamp = timeStamp;
        }

        Text = text;
    }

    public static bool TryGetColorByLogLevel(string text, out Brush color) {
        Dictionary<string, Brush> colorMappings = new() {
            { "Trace", Brushes.Gray },
            { "Debug", Brushes.Black },
            { "Info", Brushes.Green },
            { "Warn", Brushes.Orange },
            { "Error", Brushes.Coral },
            { "Crit", Brushes.Red },
            // Special words, match lower
            { "Fail", Brushes.Orange },
            { "Fehler", Brushes.Coral },
            { "Fatal", Brushes.Red },
        };

        color = Brushes.Black;

        foreach (KeyValuePair<string, Brush> entry in colorMappings) {
            if (text.ToLower().Contains(entry.Key.ToLower())) {
                color = entry.Value;
                return true;
            }
        }

        return false;
    }

    public static bool TryGetTimeStamp(ref string text, out DateTime timeStamp) {
        Regex[] regexes = new Regex[] {
            // 2018-01-04T05:52:34.123
            new Regex(@"\d{4}-[01]\d-[0-3]\dT[0-2]\d:[0-5]\d:[0-5]\d(\.\d+)?"),
            // 29.08.2022 12:58:35.345
            new Regex(@"[0-3]\d\.[01]\d\.\d{4}\s[0-2]\d:[0-5]\d:[0-5]\d(\.\d+)?")
        };

        timeStamp = default;

        foreach (Regex regex in regexes) {
            if (regex.IsMatch(text)) {
                string match = regex.Match(text).Value;
                if (DateTime.TryParse(match, out DateTime time)) {
                    text = text.Replace(match, "");
                    timeStamp = time;
                    return true;
                }
            }
        }

        return false;
    }
}