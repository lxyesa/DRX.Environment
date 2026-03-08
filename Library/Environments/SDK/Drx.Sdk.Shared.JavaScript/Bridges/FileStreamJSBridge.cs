// Copyright (c) DRX SDK
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Drx.Sdk.Shared.JavaScript.Attributes;

namespace Drx.Sdk.Shared.JavaScript.Bridges
{
    /// <summary>
    /// 文件流的 JavaScript 桥接层，暴露常见的流打开与读写能力。
    /// </summary>
    [ScriptExport("FileStream", ScriptExportType.StaticClass)]
    public static class FileStreamJSBridge
    {
        [ScriptExport]
        public static FileStream openRead(string path)
            => File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        [ScriptExport]
        public static Task<FileStream> openReadAsync(string path)
            => Task.FromResult(openRead(path));

        [ScriptExport]
        public static FileStream openWrite(string path)
        {
            EnsureParentDirectory(path);
            return File.Open(path, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
        }

        [ScriptExport]
        public static Task<FileStream> openWriteAsync(string path)
            => Task.FromResult(openWrite(path));

        [ScriptExport]
        public static FileStream openAppend(string path)
        {
            EnsureParentDirectory(path);
            return File.Open(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        }

        [ScriptExport]
        public static Task<FileStream> openAppendAsync(string path)
            => Task.FromResult(openAppend(path));

        [ScriptExport]
        public static byte[] readBytes(FileStream stream, int count)
        {
            ArgumentNullException.ThrowIfNull(stream);
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

            var buffer = new byte[count];
            var bytesRead = stream.Read(buffer, 0, count);
            if (bytesRead == count)
                return buffer;

            var result = new byte[bytesRead];
            Array.Copy(buffer, result, bytesRead);
            return result;
        }

        [ScriptExport]
        public static async Task<byte[]> readBytesAsync(FileStream stream, int count)
        {
            ArgumentNullException.ThrowIfNull(stream);
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

            var buffer = new byte[count];
            var bytesRead = await stream.ReadAsync(buffer, 0, count);
            if (bytesRead == count)
                return buffer;

            var result = new byte[bytesRead];
            Array.Copy(buffer, result, bytesRead);
            return result;
        }

        [ScriptExport]
        public static void writeBytes(FileStream stream, byte[] data)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(data);

            stream.Write(data, 0, data.Length);
        }

        [ScriptExport]
        public static Task writeBytesAsync(FileStream stream, byte[] data)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ArgumentNullException.ThrowIfNull(data);

            return stream.WriteAsync(data, 0, data.Length);
        }

        [ScriptExport]
        public static string readToEnd(FileStream stream, string? encodingName = null)
        {
            ArgumentNullException.ThrowIfNull(stream);

            using var reader = new StreamReader(stream, ResolveEncoding(encodingName), true, leaveOpen: true);
            return reader.ReadToEnd();
        }

        [ScriptExport]
        public static async Task<string> readToEndAsync(FileStream stream, string? encodingName = null)
        {
            ArgumentNullException.ThrowIfNull(stream);

            using var reader = new StreamReader(stream, ResolveEncoding(encodingName), true, leaveOpen: true);
            return await reader.ReadToEndAsync();
        }

        [ScriptExport]
        public static void writeText(FileStream stream, string content, string? encodingName = null)
        {
            ArgumentNullException.ThrowIfNull(stream);

            using var writer = new StreamWriter(stream, ResolveEncoding(encodingName), leaveOpen: true);
            writer.Write(content ?? string.Empty);
            writer.Flush();
        }

        [ScriptExport]
        public static async Task writeTextAsync(FileStream stream, string content, string? encodingName = null)
        {
            ArgumentNullException.ThrowIfNull(stream);

            using var writer = new StreamWriter(stream, ResolveEncoding(encodingName), leaveOpen: true);
            await writer.WriteAsync(content ?? string.Empty);
            await writer.FlushAsync();
        }

        [ScriptExport]
        public static long getPosition(FileStream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            return stream.Position;
        }

        [ScriptExport]
        public static void setPosition(FileStream stream, long position)
        {
            ArgumentNullException.ThrowIfNull(stream);
            stream.Position = position;
        }

        [ScriptExport]
        public static long getLength(FileStream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            return stream.Length;
        }

        [ScriptExport]
        public static void flush(FileStream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            stream.Flush();
        }

        [ScriptExport]
        public static Task flushAsync(FileStream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            return stream.FlushAsync();
        }

        [ScriptExport]
        public static void close(FileStream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            stream.Dispose();
        }

        [ScriptExport]
        public static Task closeAsync(FileStream stream)
        {
            ArgumentNullException.ThrowIfNull(stream);
            return stream.DisposeAsync().AsTask();
        }

        private static Encoding ResolveEncoding(string? encodingName)
        {
            if (string.IsNullOrWhiteSpace(encodingName))
                return Encoding.UTF8;

            return Encoding.GetEncoding(encodingName);
        }

        private static void EnsureParentDirectory(string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
        }
    }
}
