using System.ComponentModel.DataAnnotations;

namespace FeedCord.Common
{
  public class AppOptions
  {
    public const string SectionName = "App";

    [Range(1, 200, ErrorMessage = "App.ConcurrentRequests must be between 1 and 200.")]
    public int ConcurrentRequests { get; set; } = 20;
  }
}
