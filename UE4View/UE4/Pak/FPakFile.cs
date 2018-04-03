﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace UE4View.UE4.Pak
{
    public class FPakFile : IDisposable
    {
        public FPakFile(string FileName)
        {
            if (File.Exists(FileName))
            {
                CreationTime = File.GetCreationTime(FileName);
                AccessTime = File.GetLastAccessTime(FileName);
                WriteTime = File.GetLastWriteTime(FileName);
                OriginalFileName = Path.GetFileName(FileName);

                stream = new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.Read);

                if(stream != null)
                    Initialize();
            }
        }

        private byte[] ReadStreamData(long Offset, long Size)
        {
            if (stream != null)
            {
                var data = new byte[Size];
                int streamoffset = 0;
                stream.Seek(Offset, Offset < 0 ? SeekOrigin.End : SeekOrigin.Begin);
                do
                {
                    streamoffset += stream.Read(data, streamoffset, (int)Size);
                    if (streamoffset == 0)
                        break;
                } while (streamoffset != Size);

                return data;
            }
            return null;
        }

        private void Initialize()
        {
            var InfoSize = FPakInfo.GetSerializedSize();
            var InfoData = ReadStreamData(-InfoSize, InfoSize);
            Info.Serialize(InfoData);

            if (IsPakMagic())
                LoadIndex();
        }
        private void LoadIndex()
        {
            var IndexData = ReadStreamData(Info.IndexOffset, Info.IndexSize);
            if (CanDecrypt())
                DecryptData(ref IndexData, GetDecryptionKey());

            using (var reader = new FArchive(IndexData))
            {
                reader.Version = Info.Version;
                MountPoint = reader.ToFString().Replace("../../../", "");
                int NumEntries = reader.ToInt32();
                for (int i = 0; i < NumEntries; i++)
                {
                    string FileName = reader.ToFString();
                    var entry = new FPakEntry();
                    entry.Serialize(reader);
                    AbsoluteIndex.Add(FileName, entry);
                }
            }
        }

        private void DecryptData(ref byte[] data, string key)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (key == null)
                return;

            using (var rijndael = new RijndaelManaged())
            {
                rijndael.Mode = CipherMode.ECB;
                rijndael.Padding = PaddingMode.Zeros;
                rijndael.IV = new byte[16];
                rijndael.BlockSize = 128;

                rijndael.Key = key.ToCharArray().Select(c => (byte)c).Take(32).ToArray();
                using (var crypt = rijndael.CreateDecryptor())
                {
                    using (MemoryStream msDecrypt = new MemoryStream(data))
                    {
                        using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, crypt, CryptoStreamMode.Read))
                        {
                            using (MemoryStream outData = new MemoryStream(data))
                            {
                                csDecrypt.CopyTo(outData);
                            }
                        }
                    }
                }
            }
        }
        private byte[] UnpackEntry(in FPakEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var decompressed = new byte[entry.UncompressedSize];
            int Index = 0;
            int offset = 0;
            foreach (var block in entry.CompressionBlocks)
            {
                var CompressedBlockSize = block.CompressionEnd - block.CompressionStart;
                var UncompressedBlockSize = Math.Min(entry.UncompressedSize - entry.CompressionBlockSize * Index, entry.CompressionBlockSize);
                var data = ReadStreamData(block.CompressionStart, CompressedBlockSize);

                if (entry.CompressionMethod == FPakEntry.ECompressionFlags.COMPRESS_ZLIB)
                {
                    using (var compressed = new MemoryStream(data, 2, data.Length - 2)) // skip 2 bytes for zlib specification
                    {
                        using (var decstream = new DeflateStream(compressed, CompressionMode.Decompress))
                        {
                            while(offset < entry.UncompressedSize)
                                offset += decstream.Read(decompressed, offset, (int)UncompressedBlockSize);
                        }
                    }
                }
                // TODO: Test if GZip decompresses fine.
                else if (entry.CompressionMethod == FPakEntry.ECompressionFlags.COMPRESS_GZIP)
                {
                    System.Diagnostics.Debugger.Break();
                    using (var compressed = new MemoryStream(data))
                    {
                        using (var decstream = new GZipStream(compressed, CompressionMode.Decompress))
                        {
                            offset += decstream.Read(decompressed, offset, (int)UncompressedBlockSize);
                        }
                    }
                }
                Index++;
            }
            return decompressed;

        }

        public bool IsValid() => IsPakMagic() && CanDecrypt();
        public bool IsPakMagic() => Info.Magic == FPakInfo.PakFile_Magic;
        public bool IsEncrypted() => Info.bEncryptedIndex != 0;
        public bool CanDecrypt() => IsEncrypted() && GetDecryptionKey() != null;
        // Make this into a dictionary 
        private string GetDecryptionKey() =>
            //"e3VqgSMhuaPw75fm0PdGZCN3ASwpVOk5Ij7iLf8VOEdqGL6aw05JeX0RHMgBvypd" // Darwin
            //"y298qjSb115NqQ3Agad30DWn2QYrTI8CT6aP05l2PBV9Qe92S94PdoVCCy06A38L" // Fortnite
            "\x71\x29\xD5\xE5\x78\xF0\xDC\x38\x21\xE3\xCD\x70\x4F\x01\xE5\x11\xF9\xA6\x03\x40\xCF\x5B\x4C\x85\x0F\x3B\x0B\x6B\x5E\x80\xD0\xB9"
            //"\xE5\xD5\x29\x71\x38\xDC\xF0\x78\x70\xCD\xE3\x21\x11\xE5\x01\x4F\x40\x03\xA6\xF9\x85\x4C\x5B\xCF\x6B\x0B\x3B\x0F\xB9\xD0\x80\x5E"
            //null
            ;

        public byte[] ReadEntry(object entry) => ReadEntry(entry as FPakEntry);
        public byte[] ReadEntry(FPakEntry entry)
        {
            if (entry != null)
            {
                if (entry.CompressionMethod == FPakEntry.ECompressionFlags.COMPRESS_None)
                    return ReadStreamData(entry.Offset, entry.Size);
                else
                    return UnpackEntry(in entry);
            }
            return null;
        }

        /// <summary>
        ///  Retrieves asset contents by its full path. Supports wildcard '*'.
        /// </summary>
        /// <param name="name">Path to asset</param>
        /// <returns>Requested asset content as byte array if found. Null otherwise</returns>
        public byte[] ReadEntryByName(string name)
        {
            var pathparts = name.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            return ReadEntry(RecursiveSearch(pathparts, Index));
        }
        public byte[] ReadEntryByAbsoluteName(string name)
        {
            if (AbsoluteIndex.ContainsKey(name))
                return ReadEntry(AbsoluteIndex[name]);
            else
                return null;
        }

        private FPakEntry RecursiveSearch(string[] parts, FFileIndex dir)
        {
            if (parts.Length == 1)
            {
                return dir.Files
                    .Where(kv => new System.Management.Automation.WildcardPattern(parts[0]).IsMatch(kv.Key))
                    .Select(kv => kv.Value)
                    .SingleOrDefault();
            }
            else
            {
                var dirs = dir.Directories
                    .Where(kv => new System.Management.Automation.WildcardPattern(parts[0]).IsMatch(kv.Key))
                    .Select(kv => kv.Value);

                foreach(var idx in dirs)
                {
                    var found = RecursiveSearch(parts.Skip(1).ToArray(), idx);
                    if (found != null)
                        return found;
                }
            }
            return null;
        }
        public void Close()
        {
            stream?.Dispose();
            stream = null;
        }

        public FPakInfo Info { get; } = new FPakInfo();
        public FFileIndex Index
        {
            get
            {
                if(_fileIndex == null)
                {
                    _fileIndex = new FFileIndex();
                    foreach(var kv in AbsoluteIndex)
                        _fileIndex.Add(kv.Key, kv.Value);
                }

                return _fileIndex;
            }
        }
        public Dictionary<string, FPakEntry> AbsoluteIndex { get; } = new Dictionary<string, FPakEntry>();
        public string MountPoint { get; private set; }
        public string OriginalFileName { get; private set; }

        public DateTime CreationTime { get; private set; }
        public DateTime AccessTime { get; private set; }
        public DateTime WriteTime { get; private set; }

        internal FileStream stream = null;
        internal FFileIndex _fileIndex = null;

        #region IDisposable Support
        private bool disposedValue = false;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Close();
                }

                disposedValue = true;
            }
        }
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
