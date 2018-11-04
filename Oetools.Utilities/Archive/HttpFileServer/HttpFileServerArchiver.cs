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
using System.Threading;
using Oetools.Utilities.Lib;
using Oetools.Utilities.Lib.Http;

namespace Oetools.Utilities.Archive.HttpFileServer {
    
    /// <summary>
    /// In that case, a folder on the file system represents an archive.
    /// </summary>
    internal class HttpFileServerArchiver : IHttpFileServerArchiver {
        
        private HttpRequest _httpRequest;

        private HttpRequest HttpRequest => _httpRequest ?? (_httpRequest = new HttpRequest(""));
        
        /// <inheritdoc cref="IHttpFileServerArchiver.SetProxy"/>
        public void SetProxy(string proxyUrl, string userName = null, string userPassword = null) {
            HttpRequest.UseProxy(proxyUrl, userName, userPassword);
        }

        /// <inheritdoc cref="IHttpFileServerArchiver.SetBasicAuthentication"/>
        public void SetBasicAuthentication(string userName, string userPassword) {
            HttpRequest.UseBasicAuthorizationHeader(userName, userPassword);
        }

        /// <inheritdoc cref="IHttpFileServerArchiver.SetHeaders"/>
        public void SetHeaders(Dictionary<string, string> headersKeyValue) {
            HttpRequest.UseHeaders(headersKeyValue);
        }

        /// <inheritdoc cref="IArchiver.SetCancellationToken"/>
        public void SetCancellationToken(CancellationToken? cancelToken) {
            HttpRequest.UseCancellationToken(cancelToken);
        }

        /// <inheritdoc cref="IArchiver.SetCompressionLevel"/>
        public void SetCompressionLevel(ArchiveCompressionLevel archiveCompressionLevel) {
            // not applicable
        }

        /// <inheritdoc cref="IArchiver.OnProgress"/>
        public event EventHandler<ArchiverEventArgs> OnProgress;
        
        /// <inheritdoc cref="IArchiver.PackFileSet"/>
        public int PackFileSet(IEnumerable<IFileToArchive> filesToPack) {
            return DoAction(filesToPack.ToList(), Action.Upload);
        }

        /// <inheritdoc cref="IArchiver.ListFiles"/>
        public IEnumerable<IFileInArchive> ListFiles(string archivePath) {
            throw new NotImplementedException($"This method {nameof(ListFiles)} is invalid for {nameof(HttpFileServerArchiver)}.");
        }

        /// <inheritdoc cref="IArchiver.ExtractFileSet"/>
        public int ExtractFileSet(IEnumerable<IFileInArchiveToExtract> filesToExtract) {
            return DoAction(filesToExtract.ToList(), Action.Download);
        }

        /// <inheritdoc cref="IArchiver.DeleteFileSet"/>
        public int DeleteFileSet(IEnumerable<IFileInArchiveToDelete> filesToDelete) {
            return DoAction(filesToDelete.ToList(), Action.Delete);
        }

        /// <inheritdoc cref="IArchiver.MoveFileSet"/>
        public int MoveFileSet(IEnumerable<IFileInArchiveToMove> filesToMove) {
            throw new NotImplementedException($"This method {nameof(MoveFileSet)} is invalid for {nameof(HttpFileServerArchiver)}.");
        }
        
        private int DoAction(IEnumerable<IFileArchivedBase> filesIn, Action action) {

            var files = filesIn.ToList();
            
            // total size to handle
            long totalSizeDone = 0;
            long totalSize = 0;
                try {
                switch (action) {
                    case Action.Upload:
                        foreach (var file in files.OfType<IFileToArchive>()) {
                            if (File.Exists(file.SourcePath)) {
                                totalSize += new FileInfo(file.SourcePath).Length;
                            }
                        }
                        break;
                    case Action.Download:
                        foreach (var file in files.OfType<IFileInArchiveToExtract>()) {
                            var response = HttpRequest.GetFileSize(WebUtility.UrlEncode(file.RelativePathInArchive.ToCleanRelativePathUnix()), out long size);
                            if (response.Success) {
                                totalSize += size;
                            }
                        }
                        break;
                    case Action.Delete:
                        totalSize = files.Count;
                        break;
                }
            } catch (Exception e) {
                throw new ArchiverException($"Failed to assess the total file size to handle during {action.ToString().ToLower()}.", e);
            }

            int nbFilesProcessed = 0;
            foreach (var serverGroupedFiles in files.GroupBy(f => f.ArchivePath)) {
                try {
                    HttpRequest.UseBaseUrl(serverGroupedFiles.Key);
                    foreach (var file in files) {
                        bool requestOk;
                        HttpResponse response;
                        var fileRelativePath = WebUtility.UrlEncode(file.RelativePathInArchive.ToCleanRelativePathUnix());

                        switch (action) {
                            case Action.Upload:
                                if (!File.Exists(((IFileToArchive) file).SourcePath)) {
                                    // skip to next file
                                    continue;
                                }
                                response = HttpRequest.PutFile(fileRelativePath, ((IFileToArchive) file).SourcePath, progress => {
                                    totalSizeDone += progress.NumberOfBytesDone;
                                    OnProgress?.Invoke(this, ArchiverEventArgs.NewProgress(serverGroupedFiles.Key, fileRelativePath, Math.Round(totalSizeDone / (double) totalSize * 100, 2)));
                                });
                                requestOk = response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created;
                                break;
                            case Action.Download:
                                response = HttpRequest.DownloadFile(fileRelativePath, ((IFileInArchiveToExtract) file).ExtractionPath, progress => {
                                    totalSizeDone += progress.NumberOfBytesDone;
                                    OnProgress?.Invoke(this, ArchiverEventArgs.NewProgress(serverGroupedFiles.Key, fileRelativePath, Math.Round(totalSizeDone / (double) totalSize * 100, 2)));
                                });
                                requestOk = response.StatusCode == HttpStatusCode.OK;
                                if (response.StatusCode == HttpStatusCode.NotFound || response.Exception is WebException we && we.Status == WebExceptionStatus.NameResolutionFailure) {
                                    // skip to next file
                                    continue;
                                }
                                break;
                            case Action.Delete:
                                response = HttpRequest.DeleteFile(WebUtility.UrlEncode(fileRelativePath));
                                requestOk = response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.NoContent;
                                if (response.StatusCode == HttpStatusCode.NotFound || response.Exception is WebException we1 && we1.Status == WebExceptionStatus.NameResolutionFailure) {
                                    // skip to next file
                                    continue;
                                }
                                break;
                            default:
                                throw new ArgumentOutOfRangeException(nameof(action), action, null);
                        }

                        if (!requestOk) {
                            if (response.Exception != null) {
                                throw response.Exception;
                            }
                            throw new ArchiverException($"The server returned {response.StatusCode} : {response.StatusDescription} for {fileRelativePath}.");
                        }

                        nbFilesProcessed++;
                        OnProgress?.Invoke(this, ArchiverEventArgs.NewProcessedFile(serverGroupedFiles.Key, fileRelativePath));
                    }
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception e) {
                    throw new ArchiverException($"Failed to {action.ToString().ToLower()} to {serverGroupedFiles.Key}.", e);
                }
                OnProgress?.Invoke(this, ArchiverEventArgs.NewArchiveCompleted(serverGroupedFiles.Key));
            }
            return nbFilesProcessed;
        }
        
        private enum Action {
            Upload,
            Download,
            Delete
        }
    }
}