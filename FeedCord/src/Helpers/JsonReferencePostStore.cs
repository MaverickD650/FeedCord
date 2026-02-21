using FeedCord.Common;
using FeedCord.Services.Interfaces;
using System.Text.Json;

namespace FeedCord.Helpers
{
    public class JsonReferencePostStore : IReferencePostStore
    {
        private const int CurrentVersion = 1;
        private readonly string _filePath;

        public JsonReferencePostStore(string filePath)
        {
            _filePath = filePath;
        }

        public Dictionary<string, ReferencePost> LoadReferencePosts()
        {
            var dictionary = new Dictionary<string, ReferencePost>();

            if (!File.Exists(_filePath))
            {
                return dictionary;
            }

            try
            {
                var json = File.ReadAllText(_filePath);

                if (string.IsNullOrWhiteSpace(json))
                {
                    return dictionary;
                }

                var payload = JsonSerializer.Deserialize<ReferencePostPersistenceModel>(json);
                if (payload?.Entries is null)
                {
                    return dictionary;
                }

                foreach (var entry in payload.Entries)
                {
                    if (string.IsNullOrWhiteSpace(entry.Url))
                    {
                        continue;
                    }

                    dictionary[entry.Url.Trim()] = new ReferencePost
                    {
                        IsYoutube = entry.IsYoutube,
                        LastRunDate = entry.LastRunDate
                    };
                }
            }
            catch
            {
                return dictionary;
            }

            return dictionary;
        }
        public void SaveReferencePosts(IReadOnlyDictionary<string, FeedState> data)
        {
            var payload = new ReferencePostPersistenceModel
            {
                Version = CurrentVersion,
                Entries = data.Select(entry => new ReferencePostPersistenceEntry
                {
                    Url = entry.Key,
                    IsYoutube = entry.Value.IsYoutube,
                    LastRunDate = entry.Value.LastPublishDate
                }).ToList()
            };

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = $"{_filePath}.tmp";
            var content = JsonSerializer.Serialize(payload, options);
            File.WriteAllText(tempPath, content);
            File.Move(tempPath, _filePath, overwrite: true);
        }

        private class ReferencePostPersistenceModel
        {
            public int Version { get; set; }
            public List<ReferencePostPersistenceEntry> Entries { get; set; } = [];
        }

        private class ReferencePostPersistenceEntry
        {
            public string Url { get; set; } = string.Empty;
            public bool IsYoutube { get; set; }
            public DateTime LastRunDate { get; set; }
        }
    }
}
