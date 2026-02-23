using System.Text;
using FeedCord.Core.Interfaces;

namespace FeedCord.Core;

public class BatchLogger : IBatchLogger
{
  private readonly ILogger<BatchLogger> _logger;
  private readonly object _logLock = new();

  public BatchLogger(ILogger<BatchLogger> logger)
  {
    _logger = logger;
  }

  public Task ConsumeLogData(LogAggregator logItem)
  {
    lock (_logLock)
    {
      ProcessLogItem(logItem);
    }

    return Task.CompletedTask;
  }

  private void ProcessLogItem(LogAggregator logItem)
  {
    var batchSummary = new StringBuilder();
    batchSummary.AppendLine($"> Batch Run for {logItem.InstanceId} finished:");
    batchSummary.AppendLine($"> Started At: {logItem.StartTime} | Finished At: {logItem.EndTime}");

    if (!logItem.UrlStatuses.IsEmpty)
    {
      int totalUrls = logItem.UrlStatuses.Count;
      List<KeyValuePair<string, int>> failedResponses = new();

      foreach (var entry in logItem.UrlStatuses)
      {
        if (entry.Value != 200)
        {
          failedResponses.Add(entry);
        }
      }

      int failedCount = failedResponses.Count;
      batchSummary.AppendLine($"> {totalUrls} URLs tested with {failedCount} failed responses.");

      if (failedCount > 0)
      {
        batchSummary.AppendLine("> The following URLs had bad responses:");
        foreach (var issue in failedResponses)
        {
          var statusText = issue.Value == -99 ? "Request Timed Out" : issue.Value.ToString();
          batchSummary.AppendLine($"> Url: {issue.Key}, Response Status: {statusText}");
        }
      }
    }

    if (logItem.NewPostCount == 0)
    {
      batchSummary.AppendLine("> No new posts found. Posts extracted from feeds:");
      foreach (var (url, post) in logItem.LatestPosts)
      {
        batchSummary.AppendLine($"> Url: {url} | Title: {post?.Title} | Publish Date: {post?.PublishDate}");
      }
    }
    else
    {
      batchSummary.AppendLine($"> {logItem.NewPostCount} new posts found in the feed - posting to Discord Hook..");
    }

    _logger.LogInformation(batchSummary.ToString());

    logItem.Reset();
  }


}
