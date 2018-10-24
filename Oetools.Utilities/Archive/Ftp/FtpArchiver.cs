#region header
// ========================================================================
// Copyright (c) 2018 - Julien Caillon (julien.caillon@gmail.com)
// This file (FtpArchiver.cs) is part of Oetools.Utilities.
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
using System.Text;
using Oetools.Utilities.Archive.Ftp.Core;
using Oetools.Utilities.Lib.Extension;

namespace Oetools.Utilities.Archive.Ftp {
    
    internal class FtpArchiver : ArchiverBase, IArchiver {
        
        /// <inheritdoc cref="IArchiver.PackFileSet"/>
        public int PackFileSet(IEnumerable<IFileToArchive> filesToPackIn) {
            var filesToPack = filesToPackIn.ToList();
            int totalFiles = filesToPack.Count;
            int totalFilesDone = 0;
            foreach (var ftpGroupedFiles in filesToPack.GroupBy(f => f.ArchivePath)) {
                try {
                    if (!ftpGroupedFiles.Key.ParseFtpAddress(out var uri, out var userName, out var passWord, out var host, out var port, out _)) {
                        throw new ArchiverException($"The ftp uri is invalid, the typical format is ftp://user:pass@server:port/path. Input uri was : {uri.PrettyQuote()}.");
                    }
            
                    _cancelToken?.ThrowIfCancellationRequested();
                    
                    var ftp = FtpsClient.Instance.Get(uri);
                    ConnectOrReconnectFtp(ftp, userName, passWord, host, port);

                    foreach (var file in ftpGroupedFiles) {
                        if (!File.Exists(file.SourcePath)) {
                            continue;
                        }
                        _cancelToken?.ThrowIfCancellationRequested();
                        try {
                            var filesDone = totalFilesDone;
                            void TransferCallback(FtpsClient sender, ETransferActions action, string local, string remote, ulong done, ulong? total, ref bool cancel) {
                                OnProgress?.Invoke(this, ArchiverEventArgs.NewProgress(ftpGroupedFiles.Key, file.RelativePathInArchive, Math.Round((filesDone + (double) done / (total ?? 0)) / totalFiles * 100, 2)));
                            }
                            try {
                                ftp.PutFile(file.SourcePath, file.RelativePathInArchive, TransferCallback);
                            } catch (Exception) {
                                // try to create the directory and then push the file again
                                ftp.MakeDir(Path.GetDirectoryName(file.RelativePathInArchive) ?? "", true);
                                ftp.SetCurrentDirectory("/");
                                ftp.PutFile(file.SourcePath, file.RelativePathInArchive, TransferCallback);
                            }
                            totalFilesDone++;
                            OnProgress?.Invoke(this, ArchiverEventArgs.NewProcessedFile(ftpGroupedFiles.Key, file.RelativePathInArchive));
                        } catch (Exception e) {
                            throw new ArchiverException($"Failed to send {file.SourcePath.PrettyQuote()} to {file.ArchivePath.PrettyQuote()} and distant path {file.RelativePathInArchive.PrettyQuote()}.", e);
                        }
                    }
                    
                    FtpsClient.Instance.DisconnectFtp();
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception e) {
                    throw new ArchiverException($"Failed to send files to {ftpGroupedFiles.Key.PrettyQuote()}.", e);
                }
                OnProgress?.Invoke(this, ArchiverEventArgs.NewArchiveCompleted(ftpGroupedFiles.Key));
            }

            return totalFilesDone;
        }

        /// <summary>
        /// Not used for ftp archiver.
        /// </summary>
        /// <param name="archiveCompressionLevel"></param>
        public void SetCompressionLevel(ArchiveCompressionLevel archiveCompressionLevel) { }

        /// <inheritdoc cref="IArchiver.OnProgress"/>
        public event EventHandler<ArchiverEventArgs> OnProgress;

        /// <inheritdoc cref="IArchiver.ListFiles"/>
        public IEnumerable<IFileInArchive> ListFiles(string ftpUri) {
            
            if (!ftpUri.ParseFtpAddress(out var uri, out var userName, out var passWord, out var host, out var port, out var relativePath)) {
                throw new ArchiverException($"The ftp uri is invalid, the typical format is ftp://user:pass@server:port/path. Input uri was : {uri.PrettyQuote()}.");
            }
            
            var ftp = FtpsClient.Instance.Get(uri);
            ConnectOrReconnectFtp(ftp, userName, passWord, host, port);
            
            var folderStack = new Stack<string>();
            folderStack.Push(relativePath);
            while (folderStack.Count > 0) {
                
                var folder = folderStack.Pop();
                ftp.SetCurrentDirectory(folder);

                foreach (var file in ftp.GetDirectoryList()) {
                    _cancelToken?.ThrowIfCancellationRequested();
                    if (file.IsDirectory) {
                        folderStack.Push(Path.Combine(folder, file.Name).Replace("\\", "/"));
                    } else {
                        yield return new FileInFtp {
                            RelativePathInArchive = Path.Combine(folder, file.Name).Replace("\\", "/").TrimStart('/'),
                            LastWriteTime = file.CreationTime,
                            SizeInBytes = file.Size,
                            ArchivePath = uri
                        };
                    }
                }
            }
            
            FtpsClient.Instance.DisconnectFtp();
        }

        /// <inheritdoc cref="IArchiver.ExtractFileSet"/>
        public int ExtractFileSet(IEnumerable<IFileInArchiveToExtract> filesToExtractIn) {
            var filesToExtract = filesToExtractIn.ToList();
            int totalFiles = filesToExtract.Count;
            int totalFilesDone = 0;
            foreach (var ftpGroupedFiles in filesToExtract.GroupBy(f => f.ArchivePath)) {
                try {
                    // create all necessary extraction folders
                    foreach (var extractDirGroupedFiles in ftpGroupedFiles.GroupBy(f => Path.GetDirectoryName(f.ExtractionPath))) {
                        if (!Directory.Exists(extractDirGroupedFiles.Key)) {
                            Directory.CreateDirectory(extractDirGroupedFiles.Key);
                        }
                    }
                    
                    if (!ftpGroupedFiles.Key.ParseFtpAddress(out var uri, out var userName, out var passWord, out var host, out var port, out _)) {
                        throw new ArchiverException($"The ftp uri is invalid, the typical format is ftp://user:pass@server:port/path. Input uri was : {uri.PrettyQuote()}.");
                    }
            
                    _cancelToken?.ThrowIfCancellationRequested();
                    
                    var ftp = FtpsClient.Instance.Get(uri);
                    ConnectOrReconnectFtp(ftp, userName, passWord, host, port);

                    foreach (var file in ftpGroupedFiles) {
                        _cancelToken?.ThrowIfCancellationRequested();
                        try {
                            try {
                                var filesDone = totalFilesDone;
                                void TransferCallback(FtpsClient sender, ETransferActions action, string local, string remote, ulong done, ulong? total, ref bool cancel) {
                                    OnProgress?.Invoke(this, ArchiverEventArgs.NewProgress(ftpGroupedFiles.Key, file.RelativePathInArchive, Math.Round((filesDone + (double) done / (total ?? 0)) / totalFiles * 100, 2)));
                                }
                                ftp.GetFile(file.RelativePathInArchive, file.ExtractionPath, TransferCallback);
                            } catch (FtpCommandException e) {
                                if (e.ErrorCode == 550) {
                                    // path does not exist
                                    continue;
                                }
                                throw;
                            }
                            totalFilesDone++;
                            OnProgress?.Invoke(this, ArchiverEventArgs.NewProcessedFile(ftpGroupedFiles.Key, file.RelativePathInArchive));
                        } catch (Exception e) {
                            throw new ArchiverException($"Failed to get {file.ExtractionPath.PrettyQuote()} from {file.ArchivePath.PrettyQuote()} and distant path {file.RelativePathInArchive.PrettyQuote()}.", e);
                        }
                    }
                    
                    FtpsClient.Instance.DisconnectFtp();
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception e) {
                    throw new ArchiverException($"Failed to get files from {ftpGroupedFiles.Key.PrettyQuote()}.", e);
                }
                OnProgress?.Invoke(this, ArchiverEventArgs.NewArchiveCompleted(ftpGroupedFiles.Key));
            }

            return totalFilesDone;
        }

        /// <inheritdoc cref="IArchiver.DeleteFileSet"/>
        public int DeleteFileSet(IEnumerable<IFileInArchiveToDelete> filesToDeleteIn) {         
            var filesToDelete = filesToDeleteIn.ToList();
            int totalFiles = filesToDelete.Count;
            int totalFilesDone = 0;
            foreach (var ftpGroupedFiles in filesToDelete.GroupBy(f => f.ArchivePath)) {
                try {
                    if (!ftpGroupedFiles.Key.ParseFtpAddress(out var uri, out var userName, out var passWord, out var host, out var port, out _)) {
                        throw new ArchiverException($"The ftp uri is invalid, the typical format is ftp://user:pass@server:port/path. Input uri was : {uri.PrettyQuote()}.");
                    }
            
                    _cancelToken?.ThrowIfCancellationRequested();
                    
                    var ftp = FtpsClient.Instance.Get(uri);
                    ConnectOrReconnectFtp(ftp, userName, passWord, host, port);

                    foreach (var file in ftpGroupedFiles) {
                        _cancelToken?.ThrowIfCancellationRequested();
                        try {
                            try {
                                ftp.DeleteFile(file.RelativePathInArchive);
                            } catch (FtpCommandException e) {
                                if (e.ErrorCode == 550) {
                                    // path does not exist
                                    continue;
                                }
                                throw;
                            }
                            totalFilesDone++;
                            OnProgress?.Invoke(this, ArchiverEventArgs.NewProcessedFile(ftpGroupedFiles.Key, file.RelativePathInArchive));
                            OnProgress?.Invoke(this, ArchiverEventArgs.NewProgress(ftpGroupedFiles.Key, file.RelativePathInArchive, Math.Round(totalFilesDone / (double) totalFiles * 100, 2)));
                        } catch (Exception e) {
                            throw new ArchiverException($"Failed to delete {file.RelativePathInArchive.PrettyQuote()} from {file.ArchivePath.PrettyQuote()}.", e);
                        }
                    }
                    
                    FtpsClient.Instance.DisconnectFtp();
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception e) {
                    throw new ArchiverException($"Failed to delete files from {ftpGroupedFiles.Key.PrettyQuote()}.", e);
                }
                OnProgress?.Invoke(this, ArchiverEventArgs.NewArchiveCompleted(ftpGroupedFiles.Key));
            }

            return totalFilesDone;
        }

        /// <inheritdoc cref="IArchiver.MoveFileSet"/>
        public int MoveFileSet(IEnumerable<IFileInArchiveToMove> filesToMoveIn) {
            var filesToMove = filesToMoveIn.ToList();
            int totalFiles = filesToMove.Count;
            int totalFilesDone = 0;
            foreach (var ftpGroupedFiles in filesToMove.GroupBy(f => f.ArchivePath)) {
                try {
                    if (!ftpGroupedFiles.Key.ParseFtpAddress(out var uri, out var userName, out var passWord, out var host, out var port, out _)) {
                        throw new ArchiverException($"The ftp uri is invalid, the typical format is ftp://user:pass@server:port/path. Input uri was : {uri.PrettyQuote()}.");
                    }
            
                    _cancelToken?.ThrowIfCancellationRequested();
                    
                    var ftp = FtpsClient.Instance.Get(uri);
                    ConnectOrReconnectFtp(ftp, userName, passWord, host, port);

                    foreach (var file in ftpGroupedFiles) {
                        _cancelToken?.ThrowIfCancellationRequested();
                        try {
                            try {
                                ftp.RenameFile(file.RelativePathInArchive, file.NewRelativePathInArchive);
                            } catch (FtpCommandException e) {
                                if (e.ErrorCode == 550) {
                                    // path does not exist
                                    continue;
                                } 
                                if (e.ErrorCode == 553) {
                                    // target already exists
                                    ftp.DeleteFile(file.NewRelativePathInArchive);
                                    ftp.RenameFile(file.RelativePathInArchive, file.NewRelativePathInArchive);
                                } else {
                                    throw;
                                }
                            }
                            totalFilesDone++;
                            OnProgress?.Invoke(this, ArchiverEventArgs.NewProcessedFile(ftpGroupedFiles.Key, file.RelativePathInArchive));
                            OnProgress?.Invoke(this, ArchiverEventArgs.NewProgress(ftpGroupedFiles.Key, file.RelativePathInArchive, Math.Round(totalFilesDone / (double) totalFiles * 100, 2)));
                        } catch (Exception e) {
                            throw new ArchiverException($"Failed to move {file.RelativePathInArchive.PrettyQuote()} to {file.NewRelativePathInArchive.PrettyQuote()} in {file.ArchivePath.PrettyQuote()}.", e);
                        }
                    }
                    
                    FtpsClient.Instance.DisconnectFtp();
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception e) {
                    throw new ArchiverException($"Failed to move files from {ftpGroupedFiles.Key.PrettyQuote()}.", e);
                }
                OnProgress?.Invoke(this, ArchiverEventArgs.NewArchiveCompleted(ftpGroupedFiles.Key));
            }
            return totalFilesDone;
        }
        
        /// <summary>
        ///     Connects to a FTP server trying every methods
        /// </summary>
        private void ConnectOrReconnectFtp(FtpsClient ftp, string userName, string passWord, string host, int port) {
            // try to connect!
            if (!ftp.Connected) {
                ConnectFtp(ftp, userName, passWord, host, port);
            } else {
                try {
                    ftp.GetCurrentDirectory();
                } catch (Exception) {
                    ConnectFtp(ftp, userName, passWord, host, port);
                }
            }
        }

        /// <summary>
        ///     Connects to a FTP server trying every methods
        /// </summary>
        private void ConnectFtp(FtpsClient ftp, string userName, string passWord, string host, int port) {
            
            NetworkCredential credential = null;
            if (!string.IsNullOrEmpty(userName)) {
                credential = new NetworkCredential(userName, passWord ?? "");
            }

            var modes = new List<EsslSupportMode>();
            typeof(EsslSupportMode).ForEach<EsslSupportMode>((s, l) => {
                modes.Add((EsslSupportMode) l);
            });

            var sb = new StringBuilder();

            ftp.DataConnectionMode = EDataConnectionMode.Passive;
            while (!ftp.Connected && ftp.DataConnectionMode == EDataConnectionMode.Passive) {
                foreach (var mode in modes.OrderByDescending(mode => mode)) {
                    try {
                        var curPort = port > 0 ? port : (mode & EsslSupportMode.Implicit) == EsslSupportMode.Implicit ? 990 : 21;
                        ftp.Connect(host, curPort, credential, mode, 1800);
                        ftp.Connected = true;
                        if (!ftp.Connected) {
                            ftp.Close();
                        }
                        break;
                    } catch (Exception e) {
                        sb.AppendLine($"{mode} >> {e.Message}");
                    }
                }
                ftp.DataConnectionMode = EDataConnectionMode.Active;
            }

            // failed?
            if (!ftp.Connected) {
                throw new ArchiverException($"Failed to connect to a FTP server with : Username : {userName ?? "none"}, Password : {passWord ?? "none"}, Host : {host}, Port : {(port == 0 ? 21 : port)}", new Exception(sb.ToString()));
            }
        }

    }
}