using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace TwitterBookmarkDownload;

public class GraphModel
{
    public class Root
    {
        [JsonProperty("data")]
        public Data Data { get; set; }
    }

    public class Data
    {
        [JsonProperty("bookmark_timeline")]
        public BookmarkTimeline BookmarkTimeline { get; set; }
    }

    public class BookmarkTimeline
    {
        [JsonProperty("timeline")]
        public Timeline Timeline { get; set; }
    }

    public class Timeline
    {
        [JsonPropertyName("instructions")]
        public List<Instructions> Instructions { get; set; }
    }

    public class Instructions
    {
        [JsonProperty("entries")]
        public List<InstructionEntry> Entries { get; set; }
    }

    public class InstructionEntry
    {
        [JsonProperty("content")]
        public InstructionContent Content { get; set; }

        [JsonProperty("entryId")]
        public string EntryId { get; set; }
    }

    public class InstructionContent
    {
        [JsonProperty("itemContent")]
        public InstructionItemContent? ItemContent { get; set; } // cursor ones are null
    }

    public class InstructionItemContent
    {
        [JsonProperty("itemType")]
        public string ItemType { get; set; }

        [JsonProperty("tweet_results")]
        public TweetResults? TweetResults { get; set; }
    }

    public class TweetResults
    {
        [JsonProperty("result")]
        public TweetResult? Result { get; set; }
    }

    public class TweetResult
    {
        [JsonProperty("rest_id")]
        public string RestId { get; set; }

        [JsonProperty("legacy")]
        public TweetLegacy Legacy { get; set; }

        [JsonProperty("core")]
        public TweetCore Core { get; set; }
    }

    public class TweetCore
    {
        [JsonProperty("user_results")]
        public TweetCoreUserResults UserResults { get; set; }
    }

    public class TweetCoreUserResults
    {
        [JsonProperty("result")]
        public TweetCoreUserResult Result { get; set; }
    }

    public class TweetCoreUserResult
    {
        [JsonProperty("legacy")]
        public TweetCoreUserLegacy Legacy { get; set; }
    }

    public class TweetCoreUserLegacy
    {
        [JsonProperty("screen_name")]
        public string ScreenName { get; set; }
    }

    public class TweetLegacy
    {
        [JsonProperty("extended_entities")]
        public TweetExtendedEntities? ExtendedEntities { get; set; }

        //[JsonProperty("created_at")]
        //public DateTime CreatedAt { get; set; }
    }

    public class TweetExtendedEntities
    {
        [JsonProperty("media")]
        public List<TweetMedia> Media { get; set; }
    }

    public class TweetMedia
    {
        /// <summary>
        /// full-res image or video thumb
        /// </summary>
        [JsonProperty("media_url_https")]
        public string MediaUrlHttps { get; set; }

        /// <summary>
        /// t.co link
        /// </summary>
        [JsonProperty("url")]
        public string Url { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }
}