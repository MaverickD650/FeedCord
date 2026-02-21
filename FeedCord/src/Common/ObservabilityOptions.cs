using System.ComponentModel.DataAnnotations;

namespace FeedCord.Common
{
    public class ObservabilityOptions
    {
        public const string SectionName = "Observability";

        [Required(ErrorMessage = "Observability.Urls is required.")]
        public string Urls { get; set; } = "http://0.0.0.0:9090";

        [Required(ErrorMessage = "Observability.MetricsPath is required.")]
        [RegularExpression("^/.*", ErrorMessage = "Observability.MetricsPath must start with '/'.")]
        public string MetricsPath { get; set; } = "/metrics";

        [Required(ErrorMessage = "Observability.LivenessPath is required.")]
        [RegularExpression("^/.*", ErrorMessage = "Observability.LivenessPath must start with '/'.")]
        public string LivenessPath { get; set; } = "/health/live";

        [Required(ErrorMessage = "Observability.ReadinessPath is required.")]
        [RegularExpression("^/.*", ErrorMessage = "Observability.ReadinessPath must start with '/'.")]
        public string ReadinessPath { get; set; } = "/health/ready";
    }
}
