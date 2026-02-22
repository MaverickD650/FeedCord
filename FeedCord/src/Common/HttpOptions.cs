using System.ComponentModel.DataAnnotations;

namespace FeedCord.Common
{
  public class HttpOptions
  {
    public const string SectionName = "Http";

    [Range(1, 300, ErrorMessage = "Http.TimeoutSeconds must be between 1 and 300.")]
    public int TimeoutSeconds { get; set; } = 30;

    [Required(ErrorMessage = "Http.DefaultUserAgent is required.")]
    public string DefaultUserAgent { get; set; } =
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 " +
        "(KHTML, like Gecko) Chrome/104.0.5112.79 Safari/537.36";

    [Range(1, 120, ErrorMessage = "Http.PostMinIntervalSeconds must be between 1 and 120.")]
    public int PostMinIntervalSeconds { get; set; } = 2;

    public string[]? FallbackUserAgents { get; set; }
  }
}
