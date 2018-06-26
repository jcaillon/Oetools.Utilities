#region header
// ========================================================================
// Copyright (c) 2017 - Julien Caillon (julien.caillon@gmail.com)
// This file (ZipUnpacker.cs) is part of csdeployer.
// 
// csdeployer is a free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// csdeployer is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with csdeployer. If not, see <http://www.gnu.org/licenses/>.
// ========================================================================
#endregion
using System;
using System.Collections.Generic;
using System.IO;

namespace csdeployer.Lib.Compression.Zip {
    public partial class ZipEngine {
        /// <summary>
        /// Extracts files from a zip archive or archive chain.
        /// </summary>
        /// <param name="streamContext">A context interface to handle opening
        /// and closing of archive and file streams.</param>
        /// <param name="fileFilter">An optional predicate that can determine
        /// which files to process.</param>
        /// <exception cref="ArchiveException">The archive provided
        /// by the stream context is not valid.</exception>
        /// <remarks>
        /// The <paramref name="fileFilter"/> predicate takes an internal file
        /// path and returns true to include the file or false to exclude it.
        /// </remarks>
        public override void Unpack(
            IUnpackStreamContext streamContext,
            Predicate<string> fileFilter) {
            if (streamContext == null) {
                throw new ArgumentNullException("streamContext");
            }

            lock (this) {
                IList<ZipFileHeader> allHeaders = GetCentralDirectory(streamContext);
                if (allHeaders == null) {
                    throw new ZipException("Zip central directory not found.");
                }

                IList<ZipFileHeader> headers = new List<ZipFileHeader>(allHeaders.Count);
                foreach (ZipFileHeader header in allHeaders) {
                    if (!header.IsDirectory &&
                        (fileFilter == null || fileFilter(header.fileName))) {
                        headers.Add(header);
                    }
                }

                ResetProgressData();

                // Count the total number of files and bytes to be compressed.
                totalFiles = headers.Count;
                foreach (ZipFileHeader header in headers) {
                    long compressedSize;
                    long uncompressedSize;
                    long localHeaderOffset;
                    int archiveNumber;
                    uint crc;
                    header.GetZip64Fields(
                        out compressedSize,
                        out uncompressedSize,
                        out localHeaderOffset,
                        out archiveNumber,
                        out crc);

                    totalFileBytes += uncompressedSize;
                    if (archiveNumber >= totalArchives) {
                        totalArchives = (short) (archiveNumber + 1);
                    }
                }

                currentArchiveNumber = -1;
                currentFileNumber = -1;
                Stream archiveStream = null;
                try {
                    foreach (ZipFileHeader header in headers) {
                        currentFileNumber++;
                        UnpackOneFile(streamContext, header, ref archiveStream);
                    }
                } finally {
                    if (archiveStream != null) {
                        streamContext.CloseArchiveReadStream(
                            0, String.Empty, archiveStream);
                        currentArchiveNumber--;
                        OnProgress(ArchiveProgressType.FinishArchive);
                    }
                }
            }
        }

        /// <summary>
        /// Unpacks a single file from an archive or archive chain.
        /// </summary>
        private void UnpackOneFile(
            IUnpackStreamContext streamContext,
            ZipFileHeader header,
            ref Stream archiveStream) {
            ZipFileInfo fileInfo = null;
            Stream fileStream = null;
            try {
                Converter<Stream, Stream> compressionStreamCreator;
                if (!decompressionStreamCreators.TryGetValue(
                    header.compressionMethod, out compressionStreamCreator)) {
                    // Silently skip files of an unsupported compression method.
                    return;
                }

                long compressedSize;
                long uncompressedSize;
                long localHeaderOffset;
                int archiveNumber;
                uint crc;
                header.GetZip64Fields(
                    out compressedSize,
                    out uncompressedSize,
                    out localHeaderOffset,
                    out archiveNumber,
                    out crc);

                if (currentArchiveNumber != archiveNumber + 1) {
                    if (archiveStream != null) {
                        streamContext.CloseArchiveReadStream(
                            currentArchiveNumber,
                            String.Empty,
                            archiveStream);
                        archiveStream = null;

                        OnProgress(ArchiveProgressType.FinishArchive);
                        currentArchiveName = null;
                    }

                    currentArchiveNumber = (short) (archiveNumber + 1);
                    currentArchiveBytesProcessed = 0;
                    currentArchiveTotalBytes = 0;

                    archiveStream = OpenArchive(
                        streamContext, currentArchiveNumber);

                    FileStream archiveFileStream = archiveStream as FileStream;
                    currentArchiveName = (archiveFileStream != null ? Path.GetFileName(archiveFileStream.Name) : null);

                    currentArchiveTotalBytes = archiveStream.Length;
                    currentArchiveNumber--;
                    OnProgress(ArchiveProgressType.StartArchive);
                    currentArchiveNumber++;
                }

                archiveStream.Seek(localHeaderOffset, SeekOrigin.Begin);

                ZipFileHeader localHeader = new ZipFileHeader();
                if (!localHeader.Read(archiveStream, false) ||
                    !AreFilePathsEqual(localHeader.fileName, header.fileName)) {
                    string msg = "Could not read file: " + header.fileName;
                    throw new ZipException(msg);
                }

                fileInfo = header.ToZipFileInfo();

                fileStream = streamContext.OpenFileWriteStream(
                    fileInfo.FullName,
                    fileInfo.Length,
                    fileInfo.LastWriteTime);

                if (fileStream != null) {
                    currentFileName = header.fileName;
                    currentFileBytesProcessed = 0;
                    currentFileTotalBytes = fileInfo.Length;
                    currentArchiveNumber--;
                    OnProgress(ArchiveProgressType.StartFile);
                    currentArchiveNumber++;

                    UnpackFileBytes(
                        streamContext,
                        fileInfo.FullName,
                        fileInfo.CompressedLength,
                        fileInfo.Length,
                        header.crc32,
                        fileStream,
                        compressionStreamCreator,
                        ref archiveStream);
                }
            } finally {
                if (fileStream != null) {
                    streamContext.CloseFileWriteStream(
                        fileInfo.FullName,
                        fileStream,
                        fileInfo.Attributes,
                        fileInfo.LastWriteTime);

                    currentArchiveNumber--;
                    OnProgress(ArchiveProgressType.FinishFile);
                    currentArchiveNumber++;
                }
            }
        }

        /// <summary>
        /// Compares two internal file paths while ignoring case and slash differences.
        /// </summary>
        /// <param name="path1">The first path to compare.</param>
        /// <param name="path2">The second path to compare.</param>
        /// <returns>True if the paths are equivalent.</returns>
        private static bool AreFilePathsEqual(string path1, string path2) {
            path1 = path1.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            path2 = path2.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            return String.Compare(path1, path2, StringComparison.OrdinalIgnoreCase) == 0;
        }

        private Stream OpenArchive(IUnpackStreamContext streamContext, int archiveNumber) {
            Stream archiveStream = streamContext.OpenArchiveReadStream(
                archiveNumber, String.Empty, this);
            if (archiveStream == null && archiveNumber != 0) {
                archiveStream = streamContext.OpenArchiveReadStream(
                    0, String.Empty, this);
            }

            if (archiveStream == null) {
                throw new FileNotFoundException("Archive stream not provided.");
            }

            return archiveStream;
        }

        /// <summary>
        /// Decompresses bytes for one file from an archive or archive chain,
        /// checking the crc at the end.
        /// </summary>
        private void UnpackFileBytes(
            IUnpackStreamContext streamContext,
            string fileName,
            long compressedSize,
            long uncompressedSize,
            uint crc,
            Stream fileStream,
            Converter<Stream, Stream> compressionStreamCreator,
            ref Stream archiveStream) {
            CrcStream crcStream = new CrcStream(fileStream);

            ConcatStream concatStream = new ConcatStream(
                delegate(ConcatStream s) {
                    currentArchiveBytesProcessed = s.Source.Position;
                    streamContext.CloseArchiveReadStream(
                        currentArchiveNumber,
                        String.Empty,
                        s.Source);

                    currentArchiveNumber--;
                    OnProgress(ArchiveProgressType.FinishArchive);
                    currentArchiveNumber += 2;
                    currentArchiveName = null;
                    currentArchiveBytesProcessed = 0;
                    currentArchiveTotalBytes = 0;

                    s.Source = OpenArchive(streamContext, currentArchiveNumber);

                    FileStream archiveFileStream = s.Source as FileStream;
                    currentArchiveName = (archiveFileStream != null ? Path.GetFileName(archiveFileStream.Name) : null);

                    currentArchiveTotalBytes = s.Source.Length;
                    currentArchiveNumber--;
                    OnProgress(ArchiveProgressType.StartArchive);
                    currentArchiveNumber++;
                });

            concatStream.Source = archiveStream;
            concatStream.SetLength(compressedSize);

            Stream decompressionStream = compressionStreamCreator(concatStream);

            try {
                byte[] buf = new byte[4096];
                long bytesRemaining = uncompressedSize;
                int counter = 0;
                while (bytesRemaining > 0) {
                    int count = (int) Math.Min(buf.Length, bytesRemaining);
                    count = decompressionStream.Read(buf, 0, count);
                    crcStream.Write(buf, 0, count);
                    bytesRemaining -= count;

                    fileBytesProcessed += count;
                    currentFileBytesProcessed += count;
                    currentArchiveBytesProcessed = concatStream.Source.Position;

                    if (++counter % 16 == 0) // Report every 64K
                    {
                        currentArchiveNumber--;
                        OnProgress(ArchiveProgressType.PartialFile);
                        currentArchiveNumber++;
                    }
                }
            } finally {
                archiveStream = concatStream.Source;
            }

            crcStream.Flush();

            if (crcStream.Crc != crc) {
                throw new ZipException("CRC check failed for file: " + fileName);
            }
        }
    }
}