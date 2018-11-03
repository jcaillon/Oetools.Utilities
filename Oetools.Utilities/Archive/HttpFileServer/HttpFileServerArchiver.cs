#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (FileSystemArchiver.cs) is part of Oetools.Utilities.
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Cache;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using Oetools.Utilities.Archive.Filesystem;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Extension;
using Oetools.Utilities.Lib.Http;

namespace Oetools.Utilities.Archive.HttpFileServer {
    
    /// <summary>
    /// In that case, a folder on the file system represents an archive.
    /// </summary>
    internal class HttpFileServerArchiver : ArchiverBase, IArchiver {
       

        /// <inheritdoc cref="IArchiver.SetCompressionLevel"/>
        public void SetCompressionLevel(ArchiveCompressionLevel archiveCompressionLevel) {
            // not applicable
        }

        /// <inheritdoc cref="IArchiver.OnProgress"/>
        public event EventHandler<ArchiverEventArgs> OnProgress;
        
        /// <inheritdoc cref="IArchiver.PackFileSet"/>
        public int PackFileSet(IEnumerable<IFileToArchive> filesToPack) {
                       
            var request = new HttpRequest("https://api.github.com/");
            request
                .UseAuthorizationHeader(HttpAuthorizationType.Basic, "M3BVc2VyOjkzZmViMmY5NGI4NGFhYjI5MzcwNTdlNjkxMzFiZDA1NWQyY2NiY2E=")
                .UseProxy("http://mylocalhost:8087", "jucai69d", "julien caillon");
            //var resp = request.GetJson("repos/jcaillon/3P/releases?page=1&per_page=10", out List<ReleaseInfo> releaseInfo);

            request.UseBaseUrl("http://mylocalhost:8084").UseBasicAuthorizationHeader("admin", "admin123");
            var resp2 = request.DownloadFile("SimpleStupidHttpFileServer.cs", @"C:\Users\Julien\Desktop\http\test_download.txt");

            var rest = request.PutFile("yolo Swag.mp4", @"E:\Download\Ed Earthling - You Will Never Look at Your Life in the Same Way Again.mp4");
            
            rest = request.DeleteFile("yolo Swag.mp4");
            
            // init request
            var _httpRequest = WebRequest.CreateHttp("https://bugzilla.xamarin.com/show_bug.cgi?id=20321");
            
            
            _httpRequest.Method = "PUT";
            _httpRequest.ReadWriteTimeout = int.MaxValue;
            //_httpRequest.Timeout = TimeOut;
            _httpRequest.UserAgent = $"{nameof(Oetools)}/{typeof(HttpFileServerArchiver).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion}";

            var proxyUri = new Uri("http://mylocalhost:8888");
            
            var proxyCredCache = new CredentialCache(); 
            proxyCredCache.Add(new Uri(proxyUri.AbsoluteUri), "Basic", new NetworkCredential("jucai69d", "julien caillon", null));
            proxyCredCache.Add(new Uri("http://mylocalhost:8084"), "Basic", new NetworkCredential("admin", "admin123", null));

            var derp = proxyCredCache.GetCredential("mylocalhost", 8084, "Basic");

            _httpRequest.PreAuthenticate = true;
            _httpRequest.UseDefaultCredentials = false;
            _httpRequest.Credentials = proxyCredCache;
            _httpRequest.Proxy = new WebProxy(new Uri("http://mylocalhost:8888")) {
                UseDefaultCredentials = false,
                BypassProxyOnLocal = false,
                Credentials = proxyCredCache
            };
            
            _httpRequest.Headers.Add("Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{"admin"}:{"admin123"}")));
            _httpRequest.Headers.Add("Proxy-Authorization", "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{"jucai69d"}:{"julien caillon"}")));
            
            
            
            //_httpRequest.Proxy = new WebProxy("http://127.0.0.1:8888", true, null, new NetworkCredential("my user", "my password"));
            //if (Proxy != null) {
            //    _httpRequest.Proxy = Proxy;
            //}
            //if (!string.IsNullOrEmpty(BasicAuthenticationToken)) {
            //    request.Headers.Add("Authorization", "Basic " + BasicAuthenticationToken);
            //}
            _httpRequest.Accept = "*/*";
            _httpRequest.CachePolicy = new HttpRequestCachePolicy(HttpRequestCacheLevel.NoCacheNoStore);
            _httpRequest.Expect = null;
            _httpRequest.KeepAlive = true;

            using (var fileStream = File.OpenRead(@"C:\Users\Julien\Desktop\http\myfile")) {
                using (Stream writer = _httpRequest.GetRequestStream()) {
                    byte[] buffer = new byte[1024];
                    int nbBytesRead;
                    while ((nbBytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0) {
                        writer.Write(buffer, 0, nbBytesRead);
                        writer.Flush();
                    }
                }
            }
            
            using (var httpWebResponse = _httpRequest.GetResponse() as HttpWebResponse) {
                if (httpWebResponse != null) {
                    var StatusCodeResponse = httpWebResponse.StatusCode;
                    using (var responseStream = httpWebResponse.GetResponseStream()) {
                        if (responseStream != null) {
                            
                        }
                    }
                }
            }

            return DoForFiles(filesToPack.Select(f => 
                new FsFile {
                    ArchivePath = f.ArchivePath,
                    RelativePathInArchive = f.RelativePathInArchive,
                    Source = f.SourcePath,
                    Target = Path.Combine(f.ArchivePath, f.RelativePathInArchive).ToCleanPath()
                }
            ).ToList(), ActionType.Copy);
        }

        /// <inheritdoc cref="IArchiver.ListFiles"/>
        public IEnumerable<IFileInArchive> ListFiles(string archivePath) {
            if (!Directory.Exists(archivePath)) {
                return Enumerable.Empty<IFileInArchive>();
            }
            var archivePathNormalized = archivePath.ToCleanPath();
            return Utils.EnumerateAllFiles(archivePath, SearchOption.AllDirectories, null, false, _cancelToken)
                .Select(path => {
                    var fileInfo = new FileInfo(path);
                    return new FileInFilesystem {
                        RelativePathInArchive = path.FromAbsolutePathToRelativePath(archivePathNormalized),
                        ArchivePath = archivePath,
                        SizeInBytes = (ulong) fileInfo.Length,
                        LastWriteTime = fileInfo.LastWriteTime
                    };
                });
        }

        /// <inheritdoc cref="IArchiver.ExtractFileSet"/>
        public int ExtractFileSet(IEnumerable<IFileInArchiveToExtract> filesToExtract) {
            return DoForFiles(filesToExtract.Select(f => 
                new FsFile {
                    ArchivePath = f.ArchivePath,
                    RelativePathInArchive = f.RelativePathInArchive,
                    Source = Path.Combine(f.ArchivePath, f.RelativePathInArchive).ToCleanPath(),
                    Target = f.ExtractionPath
                }
            ).ToList(), ActionType.Copy);
        }

        /// <inheritdoc cref="IArchiver.DeleteFileSet"/>
        public int DeleteFileSet(IEnumerable<IFileInArchiveToDelete> filesToDelete) {
            return DoForFiles(filesToDelete.Select(f => 
                new FsFile {
                    ArchivePath = f.ArchivePath,
                    RelativePathInArchive = f.RelativePathInArchive,
                    Source = Path.Combine(f.ArchivePath, f.RelativePathInArchive).ToCleanPath()
                }
            ).ToList(), ActionType.Delete);
        }

        /// <inheritdoc cref="IArchiver.MoveFileSet"/>
        public int MoveFileSet(IEnumerable<IFileInArchiveToMove> filesToMove) {
            return DoForFiles(filesToMove.Select(f => 
                new FsFile {
                    ArchivePath = f.ArchivePath,
                    RelativePathInArchive = f.RelativePathInArchive,
                    Source = Path.Combine(f.ArchivePath, f.RelativePathInArchive).ToCleanPath(),
                    Target = Path.Combine(f.ArchivePath, f.NewRelativePathInArchive).ToCleanPath(),
                }
            ).ToList(), ActionType.Move);
        }
        
        private int DoForFiles(List<FsFile> files, ActionType actionType) {
            var totalFiles = files.Count;
            var totalFilesDone = 0;

            foreach (var archiveGroupedFiles in files.GroupBy(f => f.ArchivePath)) {
                try {
                    if (actionType != ActionType.Delete) {
                        // create all necessary target folders
                        foreach (var dirGroupedFiles in archiveGroupedFiles.GroupBy(f => Path.GetDirectoryName(f.Target))) {
                            if (!Directory.Exists(dirGroupedFiles.Key)) {
                                Directory.CreateDirectory(dirGroupedFiles.Key);
                            }
                        }
                    }

                    foreach (var file in archiveGroupedFiles) {
                        _cancelToken?.ThrowIfCancellationRequested();
                        if (!File.Exists(file.Source)) {
                            continue;
                        }
                        try {
                            switch (actionType) {
                                case ActionType.Copy:
                                    if (!file.Source.PathEquals(file.Target)) {
                                        if (File.Exists(file.Target)) {
                                            File.Delete(file.Target);
                                        }
                                        try {
                                            var buffer = new byte[1024 * 1024];
                                            using (var source = File.OpenRead(file.Source)) {
                                                long fileLength = source.Length;
                                                using (var dest = File.OpenWrite(file.Target)) {
                                                    long totalBytes = 0;
                                                    int currentBlockSize;
                                                    while ((currentBlockSize = source.Read(buffer, 0, buffer.Length)) > 0) {
                                                        totalBytes += currentBlockSize;
                                                        dest.Write(buffer, 0, currentBlockSize);
                                                        OnProgress?.Invoke(this, ArchiverEventArgs.NewProgress(archiveGroupedFiles.Key, file.RelativePathInArchive, Math.Round((totalFilesDone + (double) totalBytes / fileLength) / totalFiles * 100, 2)));
                                                        _cancelToken?.ThrowIfCancellationRequested();
                                                    }
                                                }
                                            }
                                        } catch (OperationCanceledException) {
                                            // cleanup the potentially unfinished file copy
                                            if (File.Exists(file.Target)) {
                                                File.Delete(file.Target);
                                            }
                                            throw;
                                        }
                                    }
                                    break;
                                case ActionType.Move:
                                    if (!file.Source.PathEquals(file.Target)) {
                                        if (File.Exists(file.Target)) {
                                            File.Delete(file.Target);
                                        }
                                        File.Move(file.Source, file.Target);
                                    }
                                    break;
                                case ActionType.Delete:
                                    File.Delete(file.Source);
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException(nameof(actionType), actionType, null);
                            }
                        } catch (OperationCanceledException) {
                            throw;
                        } catch (Exception e) {
                            throw new ArchiverException($"Failed to {actionType.ToString().ToLower()} {file.Source.PrettyQuote()}{(string.IsNullOrEmpty(file.Target) ? "" : $" in {file.Target.PrettyQuote()}")}.", e);
                        }
                        
                        totalFilesDone++;
                        OnProgress?.Invoke(this, ArchiverEventArgs.NewProcessedFile(archiveGroupedFiles.Key, file.RelativePathInArchive));
                        OnProgress?.Invoke(this, ArchiverEventArgs.NewProgress(archiveGroupedFiles.Key, file.RelativePathInArchive, Math.Round((double) totalFilesDone / totalFiles * 100, 2)));
                    }

                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception e) {
                    throw new ArchiverException($"Failed to {actionType.ToString().ToLower()} files{(string.IsNullOrEmpty(archiveGroupedFiles.Key) ? "" : $" in {archiveGroupedFiles.Key.PrettyQuote()}")}.", e);
                }

                OnProgress?.Invoke(this, ArchiverEventArgs.NewArchiveCompleted(archiveGroupedFiles.Key));
            }

            return totalFilesDone;
        }

        private struct FsFile {
            public string ArchivePath { get; set; }
            public string RelativePathInArchive { get; set; }
            public string Source { get; set; }
            public string Target { get; set; }
        }

        private enum ActionType {
            Copy,
            Move,
            Delete
        }
    }
}