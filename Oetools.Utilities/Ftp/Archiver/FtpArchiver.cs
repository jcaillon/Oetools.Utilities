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
using Oetools.Utilities.Archive;
using Oetools.Utilities.Lib.Extension;

namespace Oetools.Utilities.Ftp.Archiver {
    
    public class FtpArchiver : Archive.Archiver, IArchiver {

        /// <summary>
        ///     Send files to a FTP server
        /// </summary>
        public void PackFileSet(List<IFileToArchive> files, CompressionLvl compressionLevel, EventHandler<ArchiveProgressionEventArgs> progressHandler) {
            foreach (var ftpGroupedFiles in files.GroupBy(f => f.ArchivePath)) {
                try {
                    ftpGroupedFiles.Key.ParseFtpAddress(out var uri, out var userName, out var passWord, out var host, out var port, out _);
            
                    var ftp = FtpsClient.Instance.Get(uri);
                    ConnectOrReconnectFtp(ftp, userName, passWord, host, port);

                    foreach (var file in ftpGroupedFiles) {
                        SendFile(file, ftp, progressHandler);
                    }
                } catch (Exception e) {
                    progressHandler?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishArchive, ftpGroupedFiles.Key, null, e));
                }
            }
        }
        
        public List<IFileArchived> ListFiles(string ftpUri) {
            ftpUri.ParseFtpAddress(out var uri, out var userName, out var passWord, out var host, out var port, out var relativePath);
            
            var ftp = FtpsClient.Instance.Get(uri);
            ConnectOrReconnectFtp(ftp, userName, passWord, host, port);
            
            ftp.SetCurrentDirectory(relativePath);
            return ftp.GetDirectoryList()
                .Select(f => new FtpFileArchived {
                    RelativePathInArchive = Path.Combine(relativePath, f.Name),
                    LastWriteTime = f.CreationTime,
                    SizeInBytes = f.Size,
                    ArchivePath = uri
                } as IFileArchived)
                .ToList();
        }

        private void SendFile(IFileToArchive file, FtpsClient ftp, EventHandler<ArchiveProgressionEventArgs> progressHandler) {
            try {
                try {
                    ftp.PutFile(file.SourcePath, file.RelativePathInArchive);
                } catch (Exception) {
                    // try to create the directory and then push the file again
                    ftp.MakeDir((Path.GetDirectoryName(file.RelativePathInArchive) ?? "").Replace('\\', '/'), true);
                    ftp.SetCurrentDirectory("/");
                    ftp.PutFile(file.SourcePath, file.RelativePathInArchive);
                }
                progressHandler?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishFile, file.ArchivePath, file.RelativePathInArchive, null));
            } catch (Exception e) {
                progressHandler?.Invoke(this, new ArchiveProgressionEventArgs(ArchiveProgressionType.FinishFile, file.ArchivePath, file.RelativePathInArchive, e));
            }
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

            ftp.DataConnectionMode = EDataConnectionMode.Passive;
            while (!ftp.Connected && ftp.DataConnectionMode == EDataConnectionMode.Passive) {
                foreach (var mode in modes.OrderByDescending(mode => mode))
                    try {
                        var curPort = port > 0 ? port : ((mode & EsslSupportMode.Implicit) == EsslSupportMode.Implicit ? 990 : 21);
                        ftp.Connect(host, curPort, credential, mode, 1800);
                        ftp.Connected = true;
                        break;
                    } catch (Exception) {
                        //ignored
                    }

                ftp.DataConnectionMode = EDataConnectionMode.Active;
            }

            // failed?
            if (!ftp.Connected) {
                throw new Exception($"Failed to connect to a FTP server with : Username : {userName ?? "none"}, Password : {passWord ?? "none"}, Host : {host}, Port : {(port == 0 ? 21 : port)}");
            }
        }

    }
}