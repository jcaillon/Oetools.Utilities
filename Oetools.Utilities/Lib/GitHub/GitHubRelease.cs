#region header

// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (GitHubRelease.cs) is part of Oetools.Utilities.
// 
// Oetools.Utilities is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Oetools.Utilities is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Oetools.Utilities. If not, see <http://www.gnu.org/licenses/>.
// ========================================================================

#endregion

using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Oetools.Utilities.Lib.GitHub {
    [DataContract]
    public class GitHubRelease {
        [DataMember(Name = "html_url")]
        public string HtmlUrl { get; set; }

        /// <summary>
        /// Release version
        /// </summary>
        [DataMember(Name = "tag_name")]
        public string TagName { get; set; }

        /// <summary>
        /// Targeted branch
        /// </summary>
        [DataMember(Name = "target_commitish")]
        public string TargetCommitish { get; set; }

        /// <summary>
        /// Release name
        /// </summary>
        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "prerelease")]
        public bool Prerelease { get; set; }

        [DataMember(Name = "published_at")]
        public string PublishedAt { get; set; }

        [DataMember(Name = "assets")]
        public List<GitHubAsset> Assets { get; set; }

        /// <summary>
        /// Url of the zip containing the source code
        /// </summary>
        [DataMember(Name = "zipball_url")]
        public string ZipballUrl { get; set; }

        /// <summary>
        /// content of the release text
        /// </summary>
        [DataMember(Name = "body")]
        public string Body { get; set; }
    }
}