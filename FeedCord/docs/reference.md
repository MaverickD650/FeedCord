# appsettings reference

Your `appsettings.json` is a collection of `Instances`.

```json
{
  "Instances": []
}
```

YAML is also supported using `appsettings.yaml` or `appsettings.yml`.

```yaml
Instances: []
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
  "RssCheckIntervalSeconds": 180,
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
      "RssCheckIntervalSeconds": 180,
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
      "RssCheckIntervalSeconds": 180,
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
      "RssCheckIntervalSeconds": 900,
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

- `App.ConcurrentRequests` limits the whole application.
- Instance-level `ConcurrentRequests` limits that single instance.

Top-level app settings example:

```json
{
  "App": {
    "ConcurrentRequests": 5
  },
  "Instances": ["..."]
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
  "App": {
    "ConcurrentRequests": 40
  }
}
```

## HTTP Settings (Advanced / Optional)

`Http` is an optional top-level object for advanced HTTP tuning.

Example:

```json
{
  "Http": {
    "TimeoutSeconds": 30,
    "PostMinIntervalSeconds": 2,
    "DefaultUserAgent": "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.5112.79 Safari/537.36",
    "FallbackUserAgents": [
      "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.5112.79 Safari/537.36",
      "FeedFetcher-Google"
    ]
  }
}
```

## Observability Settings (Prometheus + Kubernetes)

`Observability` configures the management HTTP endpoints used for metrics and health probes.

Example:

```json
{
  "Observability": {
    "Urls": "http://0.0.0.0:9090",
    "MetricsPath": "/metrics",
    "LivenessPath": "/health/live",
    "ReadinessPath": "/health/ready"
  }
}
```

Defaults:

- `Urls`: `http://0.0.0.0:9090`
- `MetricsPath`: `/metrics`
- `LivenessPath`: `/health/live`
- `ReadinessPath`: `/health/ready`

Validation:

- Path values must start with `/`.

Kubernetes probe example:

```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 9090
  initialDelaySeconds: 20
  periodSeconds: 15

readinessProbe:
  httpGet:
    path: /health/ready
    port: 9090
  initialDelaySeconds: 5
  periodSeconds: 10
```

Prometheus scrape example:

```yaml
scrape_configs:
  - job_name: feedcord
    metrics_path: /metrics
    static_configs:
      - targets: ["feedcord.default.svc.cluster.local:9090"]

## Image Fetch Mode

`ImageFetchMode` controls how images are sourced for posts.

- `FeedOnly`: Only use image data from the feed XML.
- `FeedThenPage`: Use feed XML first, then scrape the linked page if missing.
- `PageOnly`: Always scrape the linked page for images.

Example:

```json
{
  "Instances": [
    {
      "Id": "Gaming News Channel",
      "ImageFetchMode": "FeedOnly"
    }
  ]
}
```

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
      "RssCheckIntervalSeconds": 900,
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
      "RssCheckIntervalSeconds": 900,
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
- **RssCheckIntervalSeconds**: Poll interval in seconds.
- **Color**: Embed color.
- **DescriptionLimit**: Maximum description length.
- **Forum**: `true` for forum channels, `false` for text channels.
- **MarkdownFormat**: `true` for markdown posts, `false` for embeds.
- **PersistenceOnShutdown**: Persist feed state on shutdown. State is written to `feed_dump.json`.

### Validation Ranges

- **RssCheckIntervalSeconds**: `1` to `86400`.
- **DescriptionLimit**: `1` to `4000`.
- **ConcurrentRequests (App.ConcurrentRequests)**: `1` to `200`.
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
- **ImageFetchMode**: Image sourcing mode (`FeedOnly`, `FeedThenPage`, `PageOnly`).
- **App**: Optional top-level app settings object.
- **Http**: Optional top-level HTTP settings object.
- **Observability**: Optional top-level metrics and health probe settings.
- **PostFilters**: Phrase filters applied to title/content.
