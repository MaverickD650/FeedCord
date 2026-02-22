namespace FeedCord;

public class Program
{
  internal static Action<string[]> StartupEntryPoint { get; set; } = Startup.Initialize;

  public static void Main(string[] args)
  {
    StartupEntryPoint(args);
  }
}
