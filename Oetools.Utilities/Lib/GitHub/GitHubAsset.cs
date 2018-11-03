using System.Runtime.Serialization;

namespace Oetools.Utilities.Lib.GitHub {
    [DataContract]
    public class GitHubAsset {
        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "label")]
        public object Label { get; set; }

        [DataMember(Name = "content_type")]
        public string ContentType { get; set; }

        [DataMember(Name = "state")]
        public string State { get; set; }

        [DataMember(Name = "download_count")]
        public int DownloadCount { get; set; }

        [DataMember(Name = "created_at")]
        public string CreatedAt { get; set; }

        [DataMember(Name = "updated_at")]
        public string UpdatedAt { get; set; }

        [DataMember(Name = "browser_download_url")]
        public string BrowserDownloadUrl { get; set; }
    }
}