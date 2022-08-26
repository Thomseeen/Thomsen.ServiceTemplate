using System.Windows.Media;

namespace Thomsen.ServiceTemplate.Observer.ViewModels {
    record class LogLine {
        public string Text { get; set; }

        public Brush Color { get; set; } = Brushes.Black;

        public LogLine(string text) {
            Text = text;

            if (Text.Contains("Trace")) {
                Color = Brushes.Gray;
            }

            if (Text.Contains("Debug")) {
                Color = Brushes.Black;
            }

            if (Text.Contains("Info")) {
                Color = Brushes.Green;
            }

            if (Text.Contains("Warn")) {
                Color = Brushes.Orange;
            }

            if (Text.Contains("Error")) {
                Color = Brushes.Coral;
            }

            if (Text.Contains("Crit")) {
                Color = Brushes.Red;
            }
        }
    }
}