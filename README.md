# ![FeedCord Banner](https://github.com/MaverickD650/FeedCord/blob/main/FeedCord/docs/images/FeedCord.png)

---

## FeedCord: Self-hosted RSS Reader for Discord

FeedCord is designed to be a 'turn key' automated RSS feed reader with the main focus on Discord Servers.

Use it for increasing community engagement and activity or just for your own personal use. The combination of FeedCord and Discord's Forum Channels can really shine to make a vibrant news feed featuring gallery-style display alongside custom threads, creating an engaging space for your private community discussions.

### An example of what FeedCord can bring to your server

---

![FeedCord Gallery 1](https://github.com/Qolors/FeedCord/blob/main/FeedCord/docs/images/gallery1.png)

![FeedCord Gallery 2](https://github.com/Qolors/FeedCord/blob/main/FeedCord/docs/images/gallery2.png)

A showing of one channel. Run as many of these as you want!

---

## FeedCord Setup

FeedCord is very simple to get up and running. It only takes a few steps:

- Create a Discord Webhook
- Create and Edit a local file or two

Provided below is a quick guide to get up and running.

## Quick Setup

### 1. Create a new folder with a new file named `appsettings.yaml` (or `appsettings.yml`) inside with the following content

```yaml
Instances:
  - Id: "My First News Feed"
    YoutubeUrls:
      - ""
    RssUrls:
      - ""
    Forum: false
    DiscordWebhookUrl: "..."
    RssCheckIntervalSeconds: 1500
    EnableAutoRemove: false
    Color: 8411391
    DescriptionLimit: 250
    MarkdownFormat: false
    PersistenceOnShutdown: true
App:
  ConcurrentRequests: 40
Http:
  TimeoutSeconds: 30
  PostMinIntervalSeconds: 2
  FallbackUserAgents:
    - "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/104.0.5112.79 Safari/537.36"
    - "FeedFetcher-Google"
Observability:
  Urls: "http://0.0.0.0:9090"
  MetricsPath: "/metrics"
  LivenessPath: "/health/live"
  ReadinessPath: "/health/ready"
```

There are currently 17 properties you can configure. You can read more in depth explanation of the file structure as well as view all properties and their purpose in the [reference documentation](https://github.com/MaverickD650/FeedCord/blob/main/FeedCord/docs/reference.md)

### Validation Notes

- Top-level `ConcurrentRequests` must be between `1` and `200`.
- Instance `ConcurrentRequests` must be between `1` and `200`.
- `RssCheckIntervalSeconds` must be between `1` and `86400`.
- `DescriptionLimit` must be between `1` and `4000`.

### Persistence Notes

- When `PersistenceOnShutdown` is `true`, FeedCord persists feed state to `feed_dump.json`.
- `feed_dump.json` is the persistence format.

### Metrics & Health Endpoints

- Prometheus scrape endpoint: `/metrics`
- Kubernetes liveness endpoint: `/health/live`
- Kubernetes readiness endpoint: `/health/ready`

Kubernetes probes example:

```yaml
livenessProbe:
  httpGet:
    path: /health/live
    port: 9090

readinessProbe:
  httpGet:
    path: /health/ready
    port: 9090
```

---

### 2. Create a new Webhook in Discord (Visual Steps Provided)

![Discord Webhook](https://github.com/MaverickD650/FeedCord/blob/main/FeedCord/docs/images/webhooks.png)

### Quick Note

Be sure to populate your `appsettings.yaml` *"DiscordWebhookUrl"* property with your newly created Webhook

#### Webhook Configuration Options

You have two ways to configure your Discord webhook URL:

##### Option 1: Direct URL (Hardcoded)

```json
{
  "Instances": [
    {
      "Id": "Gaming News",
      "DiscordWebhookUrl": "https://discord.com/api/webhooks/YOUR_WEBHOOK_ID/YOUR_WEBHOOK_TOKEN",
      ...
    }
  ]
}
```

##### Option 2: Environment Variable (Recommended for Docker)

You can reference an environment variable using the `env:` prefix. This is useful for keeping sensitive webhook URLs out of your config files:

```json
{
  "Instances": [
    {
      "Id": "Gaming News",
      "DiscordWebhookUrl": "env:FEEDCORD_WEBHOOK_GAMING",
      ...
    },
    {
      "Id": "Tech News",
      "DiscordWebhookUrl": "env:FEEDCORD_WEBHOOK_TECH",
      ...
    }
  ]
}
```

Then set the environment variables when running:

**Docker:**

```bash
docker run --name FeedCord \
  -v "/path/to/your/appsettings.yaml:/app/config/appsettings.yaml" \
  -e FEEDCORD_WEBHOOK_GAMING="https://discord.com/api/webhooks/..." \
  -e FEEDCORD_WEBHOOK_TECH="https://discord.com/api/webhooks/..." \
  qolors/feedcord:latest
```

**Docker Compose:**

```yaml
services:
  feedcord:
    image: qolors/feedcord:latest
    volumes:
      - /path/to/your/appsettings.yaml:/app/config/appsettings.yaml
    environment:
      FEEDCORD_WEBHOOK_GAMING: "https://discord.com/api/webhooks/..."
      FEEDCORD_WEBHOOK_TECH: "https://discord.com/api/webhooks/..."
```

**Local/Native:**

```bash
export FEEDCORD_WEBHOOK_GAMING="https://discord.com/api/webhooks/..."
export FEEDCORD_WEBHOOK_TECH="https://discord.com/api/webhooks/..."
dotnet run -- path/to/your/appsettings.yaml
```

## RSS Feeds

Before you actually run FeedCord, make sure you have populated your `appsettings.yaml` with RSS and YouTube feeds.

- For new users that aren't bringing their own list check out [awesome-rss-feeds](https://github.com/plenaryapp/awesome-rss-feeds) and add some that interest you
- Each url is entered by line seperating by comma. It should look like this in your `appsettings.yaml` file:

```json
"RssUrls": [
       "https://examplesrssfeed1.com/rss",
       "https://examplesrssfeed2.com/rss",
       "https://examplesrssfeed3.com/rss",
     ]
```

## YouTube Feeds

- You can bring your favorite YouTube channels as well to be notified of new uploads
- FeedCord parses from the channel's base url so simply navigate to the channel home page and use that url.
- Example here if I was interested in Unbox Therapy & Tyler1:

***NOTE***

If a YouTube link keeps failing at retrieving the RSS Link - Directly use the xml formatted YouTube link. It is more reliable.

The format for that looks like: `"https://www.youtube.com/feeds/videos.xml?channel_id={YOUR_CHANNEL_ID_HERE}"`

You can use online web tools like [tunepocket](https://www.tunepocket.com/youtube-channel-id-finder/?srsltid=AfmBOorSH1Ye9r1erCzY2qaqV_pUa23U8wG-DeAMAhGfGZ9dbMY5RE2j) to get the Id for the channel.

```json
"YoutubeUrls": [
       "https://www.youtube.com/@unboxtherapy",
       "https://www.youtube.com/@TYLER1LOL",
       "https://www.youtube.com/feeds/videos.xml?channel_id={YOUR_CHANNEL_ID_HERE}"
     ]
```

FeedCord supports direct YouTube feed URLs such as:

- `https://www.youtube.com/feeds/videos.xml?channel_id=...`
- `https://www.youtube.com/feeds/videos.xml?playlist_id=...`
- `https://www.youtube.com/feeds/videos.xml?user=...`

### Running FeedCord

Now that your file is set up, you have two ways to run FeedCord

### Docker (Recommended)

```bash
docker pull qolors/feedcord:latest
```

Be sure to update the volume path to your `appsettings.yaml`.

```bash
docker run --name FeedCord -v "/path/to/your/appsettings.yaml:/app/config/appsettings.yaml" qolors/feedcord:latest
```

### Build From Source

Install the [.NET SDK](dotnet.microsoft.com/download)

Clone this repo

```bash
git clone https://github.com/Qolors/FeedCord
```

Change Directory

```bash
cd FeedCord
```

Restore Dependencies

```bash
dotnet restore
```

Build

```bash
dotnet build
```

Run with your `appsettings.yaml` (or `.yml`) (provide your own path)

```bash
dotnet run -- path/to/your/appsettings.yaml
```

With the above steps completed, FeedCord should now be running and posting updates from your RSS feeds directly to your Discord channel.

## Testing & Coverage

FeedCord maintains a comprehensive test suite with automated coverage tracking. For detailed information about testing see **[TESTING.md](TESTING.md)**.

### Running Tests Locally

```bash
# Run all tests
dotnet test

# Run with coverage report
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Run specific test class
dotnet test --filter "ClassName=FeedCord.Tests.Services.FeedManagerTests"
```

## Changelog

All notable changes to this project will be documented in GitHub's release section. Each release will have a detailed list of changes, improvements, and bug fixes. This is a shift from the original project but allows for better organization and visibility of changes directly within GitHub's interface. It also allows us to leverage GitHub's release features, such as tagging and release notes, to provide a more comprehensive overview of each update.

This project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
