using System.Xml.Linq;
using FeedCord.Common;
using FeedCord.Services.Interfaces;
using HtmlAgilityPack;
using System.Collections.Concurrent;

namespace FeedCord.Infrastructure.Parsers
{
  public class ImageParserService : IImageParserService
  {
    private const int MaxPageImageCacheEntries = 2000;
    private static readonly TimeSpan PageImageCacheTtl = TimeSpan.FromMinutes(15);
    private readonly ConcurrentDictionary<string, CachedImageLookup> _pageImageCache = new(StringComparer.Ordinal);

    private readonly ICustomHttpClient _httpClient;
    private readonly ILogger<ImageParserService> _logger;

    public ImageParserService(ICustomHttpClient httpClient, ILogger<ImageParserService> logger)
    {
      _httpClient = httpClient;
      _logger = logger;
    }

    public async Task<string?> TryExtractImageLink(string pageUrl, string xmlSource, ImageFetchMode imageFetchMode)
    {
      if (imageFetchMode == ImageFetchMode.PageOnly)
      {
        return await ScrapeImageFromWebpage(pageUrl);
      }

      if (string.IsNullOrWhiteSpace(xmlSource))
      {
        return imageFetchMode == ImageFetchMode.FeedOnly
            ? string.Empty
            : await ScrapeImageFromWebpage(pageUrl);
      }

      var feedImageUrl = ExtractImageFromFeedXml(xmlSource);
      var resolvedFeedImageUrl = ResolveAndValidateImageUrl(pageUrl, feedImageUrl);

      if (string.IsNullOrWhiteSpace(resolvedFeedImageUrl))
        return imageFetchMode == ImageFetchMode.FeedOnly
            ? string.Empty
            : await ScrapeImageFromWebpage(pageUrl);

      return resolvedFeedImageUrl;
    }

    private static string ExtractImageFromFeedXml(string xmlSource)
    {
      try
      {
        var xdoc = XDocument.Parse(xmlSource);

        var enclosureImage = xdoc.Descendants("enclosure")
            .FirstOrDefault(e => e.Attribute("type") != null &&
                                 e.Attribute("type")!.Value.StartsWith("image/", StringComparison.OrdinalIgnoreCase));
        if (enclosureImage != null)
        {
          var url = enclosureImage.Attribute("url")?.Value;
          if (!string.IsNullOrWhiteSpace(url)) return url;
        }

        var mediaContent = xdoc.Descendants()
            .FirstOrDefault(el =>
                (el.Name.LocalName == "content" || el.Name.LocalName == "thumbnail") &&
                el.Attributes("url").Any() &&
                (el.Attribute("type")?.Value.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? true)
            );
        if (mediaContent != null)
        {
          var url = mediaContent.Attribute("url")!.Value;
          if (!string.IsNullOrWhiteSpace(url)) return url;
        }

        var itunesImage = xdoc.Descendants().FirstOrDefault(el => el.Name.LocalName == "image" &&
                                                                  el.Name.NamespaceName.Contains("itunes") &&
                                                                  el.Attribute("href") != null);
        if (itunesImage != null)
        {
          var url = itunesImage.Attribute("href")!.Value;
          if (!string.IsNullOrWhiteSpace(url)) return url;
        }

        var descNode = xdoc.Descendants("description").FirstOrDefault();
        var contentNode = xdoc.Descendants().FirstOrDefault(n => n.Name.LocalName == "encoded");

        var descHtml = descNode?.Value ?? string.Empty;
        var contentHtml = contentNode?.Value ?? string.Empty;

        var fromDesc = ExtractImgFromHtml(descHtml);
        if (!string.IsNullOrEmpty(fromDesc)) return fromDesc;

        var fromContent = ExtractImgFromHtml(contentHtml);
        if (!string.IsNullOrEmpty(fromContent)) return fromContent;
      }
      catch (Exception)
      {
        // Silent recovery in static utility - errors are logged at caller level
      }
      return string.Empty;
    }

    private static string ExtractImgFromHtml(string html)
    {
      if (string.IsNullOrWhiteSpace(html)) return string.Empty;

      var doc = new HtmlDocument();
      doc.LoadHtml(html);

      var imgNode = doc.DocumentNode.SelectSingleNode("//img[@src]");
      if (imgNode == null)
        return string.Empty;

      var src = imgNode.Attributes["src"]!.Value;

      return !string.IsNullOrWhiteSpace(src) ? src : string.Empty;
    }

    private async Task<string?> ScrapeImageFromWebpage(string pageUrl)
    {
      if (string.IsNullOrWhiteSpace(pageUrl))
        return string.Empty;

      if (TryGetCachedPageImage(pageUrl, out var cachedImageUrl))
      {
        return cachedImageUrl;
      }

      try
      {
        // Download HTML
        var response = await _httpClient.GetAsyncWithFallback(pageUrl);

        if (response is null) return string.Empty;

        response.EnsureSuccessStatusCode();
        var htmlContent = await response.Content.ReadAsStringAsync();

        // Existing logic
        var doc = new HtmlDocument();
        doc.LoadHtml(htmlContent);

        var imageUrl = ExtractImageFromDocument(doc);
        if (!string.IsNullOrEmpty(imageUrl))
        {
          var resolvedImageUrl = ResolveAndValidateImageUrl(pageUrl, imageUrl);
          if (!string.IsNullOrWhiteSpace(resolvedImageUrl))
          {
            SetCachedPageImage(pageUrl, resolvedImageUrl);
            return resolvedImageUrl;
          }
        }

        SetCachedPageImage(pageUrl, string.Empty);
      }
      catch (Exception ex)
      {
        _logger.LogWarning(ex, "Error scraping image from webpage: {PageUrl}", pageUrl);
      }

      return string.Empty;
    }

    private bool TryGetCachedPageImage(string pageUrl, out string cachedImageUrl)
    {
      cachedImageUrl = string.Empty;

      if (!_pageImageCache.TryGetValue(pageUrl, out var cachedLookup))
      {
        return false;
      }

      if (cachedLookup.ExpiresAtUtc < DateTime.UtcNow)
      {
        _ = _pageImageCache.TryRemove(pageUrl, out _);
        return false;
      }

      cachedImageUrl = cachedLookup.ImageUrl;
      return true;
    }

    private void SetCachedPageImage(string pageUrl, string imageUrl)
    {
      _pageImageCache[pageUrl] = new CachedImageLookup(imageUrl, DateTime.UtcNow.Add(PageImageCacheTtl));

      if (_pageImageCache.Count <= MaxPageImageCacheEntries)
      {
        return;
      }

      var nowUtc = DateTime.UtcNow;
      foreach (var cacheItem in _pageImageCache)
      {
        if (cacheItem.Value.ExpiresAtUtc < nowUtc)
        {
          _ = _pageImageCache.TryRemove(cacheItem.Key, out _);
        }
      }
    }

    private sealed record CachedImageLookup(string ImageUrl, DateTime ExpiresAtUtc);

    private static string ResolveAndValidateImageUrl(string pageUrl, string? foundUrl)
    {
      if (!IsValidImageUrl(foundUrl))
        return string.Empty;

      var resolvedUrl = MakeAbsoluteUrl(pageUrl, foundUrl);
      if (!Uri.TryCreate(resolvedUrl, UriKind.Absolute, out var parsedUri))
        return string.Empty;

      if (!parsedUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
          !parsedUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        return string.Empty;

      return parsedUri.ToString();
    }

    private static string? ExtractImageFromDocument(HtmlDocument doc)
    {
      var imageUrl = GetMetaTagContent(doc, "property", "og:image");
      if (IsValidImageUrl(imageUrl))
        return imageUrl;

      imageUrl = GetMetaTagContent(doc, "property", "og:image:secure_url");
      if (IsValidImageUrl(imageUrl))
        return imageUrl;

      imageUrl = GetMetaTagContent(doc, "name", "twitter:image");
      if (IsValidImageUrl(imageUrl))
        return imageUrl;

      imageUrl = GetLinkRel(doc, "image_src");
      if (IsValidImageUrl(imageUrl))
        return imageUrl;

      imageUrl = GetFirstImageWithAttribute(doc, "data-src");
      if (IsValidImageUrl(imageUrl))
        return imageUrl;

      imageUrl = GetElementById(doc, "post-image");
      if (IsValidImageUrl(imageUrl))
        return imageUrl;

      imageUrl = GetFirstImg(doc);
      if (IsValidImageUrl(imageUrl))
        return imageUrl;

      return string.Empty;
    }

    private static bool IsValidImageUrl(string? url)
    {
      if (string.IsNullOrWhiteSpace(url))
        return false;

      if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
          url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
        return false;

      if (!Uri.TryCreate(url, UriKind.Absolute, out var parsedUri))
        return true;

      return !parsedUri.Scheme.Equals("file", StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetMetaTagContent(HtmlDocument doc, string attributeKey, string attributeValue)
    {
      var metaNode = doc.DocumentNode.SelectSingleNode($"//meta[@{attributeKey}='{attributeValue}']");
      return metaNode?.Attributes["content"]?.Value;
    }

    private static string? GetLinkRel(HtmlDocument doc, string relValue)
    {
      var linkNode = doc.DocumentNode.SelectSingleNode($"//link[@rel='{relValue}']");
      return linkNode?.Attributes["href"]?.Value;
    }

    private static string? GetFirstImg(HtmlDocument doc)
    {
      var imgNode = doc.DocumentNode.SelectSingleNode("//img[@src]");
      return imgNode?.Attributes["src"]!.Value;
    }

    private static string? GetFirstImageWithAttribute(HtmlDocument doc, string attributeName)
    {
      var imgNode = doc.DocumentNode.SelectSingleNode($"//img[@{attributeName}]");
      return imgNode?.Attributes[attributeName]!.Value;
    }

    private static string? GetElementById(HtmlDocument doc, string elementId)
    {
      var node = doc.GetElementbyId(elementId);
      if (node != null)
      {
        var src = node.Attributes["src"]?.Value;
        if (!string.IsNullOrWhiteSpace(src))
          return src;
        src = node.Attributes["data-src"]?.Value;
        return src;
      }
      return null;
    }
    private static string? MakeAbsoluteUrl(string pageUrl, string? foundUrl)
    {
      if (Uri.TryCreate(foundUrl, UriKind.Absolute, out var absolute))
      {
        return absolute.ToString();
      }

      if (Uri.TryCreate(pageUrl, UriKind.Absolute, out var baseUri) &&
          Uri.TryCreate(baseUri, foundUrl, out var relativeUri))
      {
        return relativeUri.ToString();
      }

      return foundUrl;
    }
  }
}
