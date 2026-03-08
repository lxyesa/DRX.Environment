// Copyright (c) DRX SDK
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Drx.Sdk.Shared.JavaScript.Attributes;

namespace Drx.Sdk.Shared.JavaScript.Bridges
{
    /// <summary>
    /// File 的 JavaScript 桥接层，暴露常见文件操作。
    /// </summary>
    [ScriptExport("File", ScriptExportType.StaticClass)]
    public static class FileJSBridge
    {
        [ScriptExport]
        public static bool exists(string path)
            => File.Exists(path);

        [ScriptExport]
        public static string readAllText(string path, string? encodingName = null)
            => File.ReadAllText(path, ResolveEncoding(encodingName));

        [ScriptExport]
        public static Task<string> readAllTextAsync(string path, string? encodingName = null)
            => File.ReadAllTextAsync(path, ResolveEncoding(encodingName));

        [ScriptExport]
        public static void writeAllText(string path, string content, string? encodingName = null)
        {
            EnsureParentDirectory(path);
            File.WriteAllText(path, content ?? string.Empty, ResolveEncoding(encodingName));
        }

        [ScriptExport]
        public static Task writeAllTextAsync(string path, string content, string? encodingName = null)
        {
            EnsureParentDirectory(path);
            return File.WriteAllTextAsync(path, content ?? string.Empty, ResolveEncoding(encodingName));
        }

        [ScriptExport]
        public static void appendAllText(string path, string content, string? encodingName = null)
        {
            EnsureParentDirectory(path);
            File.AppendAllText(path, content ?? string.Empty, ResolveEncoding(encodingName));
        }

        [ScriptExport]
        public static Task appendAllTextAsync(string path, string content, string? encodingName = null)
        {
            EnsureParentDirectory(path);
            return File.AppendAllTextAsync(path, content ?? string.Empty, ResolveEncoding(encodingName));
        }

        [ScriptExport]
        public static byte[] readAllBytes(string path)
            => File.ReadAllBytes(path);

        [ScriptExport]
        public static Task<byte[]> readAllBytesAsync(string path)
            => File.ReadAllBytesAsync(path);

        [ScriptExport]
        public static void writeAllBytes(string path, byte[] data)
        {
            EnsureParentDirectory(path);
            File.WriteAllBytes(path, data ?? Array.Empty<byte>());
        }

        [ScriptExport]
        public static Task writeAllBytesAsync(string path, byte[] data)
        {
            EnsureParentDirectory(path);
            return File.WriteAllBytesAsync(path, data ?? Array.Empty<byte>());
        }

        [ScriptExport]
        public static void delete(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        [ScriptExport]
        public static Task deleteAsync(string path)
            => Task.Run(() => delete(path));

        [ScriptExport]
        public static void copy(string sourceFileName, string destFileName, bool overwrite = false)
        {
            EnsureParentDirectory(destFileName);
            File.Copy(sourceFileName, destFileName, overwrite);
        }

        [ScriptExport]
        public static async Task copyAsync(string sourceFileName, string destFileName, bool overwrite = false)
        {
            EnsureParentDirectory(destFileName);

            if (!overwrite && File.Exists(destFileName))
                throw new IOException($"The destination file '{destFileName}' already exists.");

            await using var source = File.Open(sourceFileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            await using var destination = File.Open(destFileName, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await source.CopyToAsync(destination);
        }

        [ScriptExport]
        public static void move(string sourceFileName, string destFileName, bool overwrite = false)
        {
            EnsureParentDirectory(destFileName);
            File.Move(sourceFileName, destFileName, overwrite);
        }

        [ScriptExport]
        public static Task moveAsync(string sourceFileName, string destFileName, bool overwrite = false)
            => Task.Run(() => move(sourceFileName, destFileName, overwrite));

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
