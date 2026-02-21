# appsettings.json reference

Your `appsettings.json` is a collection of `Instances`.

```json
{
  "Instances": []
}
```

Each Discord channel is one instance and has one webhook.

```json
{
  "Id": "Gaming News Channel",
  "RssUrls": [
    "https://examplesrssfeed1.com/rss",
    "https://examplesrssfeed2.com/rss",
    "https://examplesrssfeed3.com/rss"
  ],
  "YoutubeUrls": [""],
  "DiscordWebhookUrl": "https://discord.com/api/webhooks/...",
  "RssCheckIntervalMinutes": 3,
  "EnableAutoRemove": true,
  "Color": 8411391,
  "DescriptionLimit": 200,
  "Forum": true,
  "MarkdownFormat": false,
  "PersistenceOnShutdown": false
}
```

Example with two configured channels:

```json
{
  "Instances": [
    {
      "Id": "Gaming News Channel",
      "Username": "Gaming News",
      "RssUrls": [
        "https://examplesrssfeed1.com/rss",
        "https://examplesrssfeed2.com/rss",
        "https://examplesrssfeed3.com/rss"
      ],
      "YoutubeUrls": [""],
      "DiscordWebhookUrl": "https://discord.com/api/webhooks/...",
      "RssCheckIntervalMinutes": 3,
      "EnableAutoRemove": true,
      "Color": 8411391,
      "DescriptionLimit": 200,
      "Forum": true,
      "MarkdownFormat": false,
      "PersistenceOnShutdown": false
    },
    {
      "Id": "Tech News Channel",
      "Username": "Tech News",
      "RssUrls": [
        "https://examplesrssfeed4.com/rss",
        "https://examplesrssfeed5.com/rss",
        "https://examplesrssfeed6.com/rss"
      ],
      "YoutubeUrls": [""],
      "DiscordWebhookUrl": "https://discord.com/api/webhooks/...",
      "RssCheckIntervalMinutes": 3,
      "EnableAutoRemove": true,
      "Color": 8411391,
      "DescriptionLimit": 200,
      "Forum": true,
      "MarkdownFormat": false,
      "PersistenceOnShutdown": false
    }
  ],
  "ConcurrentRequests": 40
}
```

A minimal working file:

```json
{
  "Instances": [
    {
      "Id": "Runescape Feed",
      "YoutubeUrls": [""],
      "RssUrls": [
        "https://github.com/qolors/FeedCord/releases.atom"
      ],
      "DiscordWebhookUrl": "https://discord.com/api/webhooks/...",
      "RssCheckIntervalMinutes": 15,
      "Color": 8411391,
      "DescriptionLimit": 500,
      "Forum": true,
      "MarkdownFormat": false,
      "PersistenceOnShutdown": true
    }
  ],
  "ConcurrentRequests": 40
}
```

## Concurrent Requests

You can configure concurrency globally and per instance.

- Top-level `ConcurrentRequests` limits the whole application.
- Instance-level `ConcurrentRequests` limits that single instance.

Top-level example:

```json
{
  "Instances": ["..."],
  "ConcurrentRequests": 5
}
```

Instance-level example:

```json
{
  "Instances": [
    {
      "Id": "Gaming",
      "ConcurrentRequests": 1
    }
  ],
  "ConcurrentRequests": 40
}
```

## HTTP Fallback User-Agents (Advanced / Optional)

`HttpFallbackUserAgents` is an optional top-level array for advanced troubleshooting. Most setups should leave this unset and use defaults.

## Post Filters

`PostFilters` is an array of objects with `Url` and `Filters` fields.

Per-URL filters:

```json
{
  "Instances": [
    {
      "Id": "FeedCord",
      "YoutubeUrls": [""],
      "RssUrls": [
        "https://github.com/qolors/FeedCord/releases.atom",
        "https://github.com/qolors/Clam-Shell/releases.atom"
      ],
      "DiscordWebhookUrl": "https://discord.com/api/webhooks/...",
      "RssCheckIntervalMinutes": 15,
      "Color": 8411391,
      "DescriptionLimit": 500,
      "Forum": true,
      "MarkdownFormat": false,
      "PersistenceOnShutdown": true,
      "ConcurrentRequests": 10,
      "PostFilters": [
        {
          "Url": "https://github.com/qolors/FeedCord/releases.atom",
          "Filters": ["release", "new feature"]
        },
        {
          "Url": "https://github.com/qolors/Clam-Shell/releases.atom",
          "Filters": ["phishing"]
        }
      ]
    }
  ],
  "ConcurrentRequests": 40
}
```

Filter all feeds with `"Url": "all"`:

```json
{
  "Instances": [
    {
      "Id": "FeedCord",
      "YoutubeUrls": [""],
      "RssUrls": [
        "https://github.com/qolors/FeedCord/releases.atom",
        "https://github.com/qolors/Clam-Shell/releases.atom"
      ],
      "DiscordWebhookUrl": "https://discord.com/api/webhooks/...",
      "RssCheckIntervalMinutes": 15,
      "Color": 8411391,
      "DescriptionLimit": 500,
      "Forum": true,
      "MarkdownFormat": false,
      "PersistenceOnShutdown": true,
      "ConcurrentRequests": 10,
      "PostFilters": [
        {
          "Url": "all",
          "Filters": ["release", "new feature", "phishing"]
        }
      ]
    }
  ],
  "ConcurrentRequests": 40
}
```

---

## Property References

### Required

- **Id**: Unique name of the feed service instance.
- **RssUrls**: RSS feeds to read.
- **YoutubeUrls**: YouTube feeds/channels to read.
- **DiscordWebhookUrl**: Discord webhook URL for posting.
- **RssCheckIntervalMinutes**: Poll interval in minutes.
- **Color**: Embed color.
- **DescriptionLimit**: Maximum description length.
- **Forum**: `true` for forum channels, `false` for text channels.
- **MarkdownFormat**: `true` for markdown posts, `false` for embeds.
- **PersistenceOnShutdown**: Persist feed state on shutdown. State is written to `feed_dump.json`.

### Validation Ranges

- **RssCheckIntervalMinutes**: `1` to `1440`.
- **DescriptionLimit**: `1` to `4000`.
- **ConcurrentRequests (Top-level)**: `1` to `200`.
- **ConcurrentRequests (Instance)**: `1` to `200`.

### Optional

- **Username**: Bot display name.
- **EnableAutoRemove**: Remove URL after repeated failures.
- **AvatarUrl**: Bot avatar image.
- **AuthorIcon**: Embed author icon.
- **AuthorName**: Embed author display name.
- **AuthorUrl**: Link when clicking author name.
- **FallbackImage**: Backup image if metadata parsing fails.
- **ConcurrentRequests**: Per-instance request limit.
- **HttpFallbackUserAgents**: Optional top-level fallback user-agent list.
- **PostFilters**: Phrase filters applied to title/content.
