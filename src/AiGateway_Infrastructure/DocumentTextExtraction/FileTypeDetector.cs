using AiGateway_Core.DocumentTextExtraction;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Text;

namespace AiGateway_Infrastructure.DocumentTextExtraction
{
    public class FileTypeDetector : IFileTypeDetector
    {
        private record FileSignature(byte[] Signature, string Extension);

        private static readonly List<FileSignature> BinarySignatures = new()
        {
            new(new byte[] { 0x25, 0x50, 0x44, 0x46 }, ".pdf"),
            new(new byte[] { 0x4D, 0x5A }, ".exe"),
            new(new byte[] { 0xFF, 0xD8, 0xFF }, ".jpg"),
            new(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, ".zip")
        };

        // Common Text Byte Order Marks (BOM)
        private static readonly List<FileSignature> TextBoms = new()
    {
        new(new byte[] { 0x00, 0x00, 0xFE, 0xFF }, ".txt"), // UTF-32 BE
        new(new byte[] { 0xFF, 0xFE, 0x00, 0x00 }, ".txt"), // UTF-32 LE
        new(new byte[] { 0xEF, 0xBB, 0xBF }, ".txt"),       // UTF-8 BOM
        new(new byte[] { 0xFE, 0xFF }, ".txt"),             // UTF-16 BE
        new(new byte[] { 0xFF, 0xFE }, ".txt")              // UTF-16 LE
    };

        private readonly int _maxSignatureLength;

        public FileTypeDetector()
        {
            _maxSignatureLength = Math.Max(
                BinarySignatures.Max(s => s.Signature.Length),
                TextBoms.Max(s => s.Signature.Length)
            );
        }

        public async Task<string> DetectTypeAsync(Stream fileStream, string fallbackExtension = "", CancellationToken cancellationToken = default)
        {
            if (fileStream == null) throw new ArgumentNullException(nameof(fileStream));
            if (!fileStream.CanRead) throw new ArgumentException("Stream must be readable.", nameof(fileStream));
            if (!fileStream.CanSeek) throw new ArgumentException("Stream must be seekable.", nameof(fileStream));

            long originalPosition = fileStream.Position;

            try
            {
                byte[] buffer = new byte[_maxSignatureLength];
                int bytesRead = await fileStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);

                if (bytesRead == 0) return fallbackExtension;

                ReadOnlySpan<byte> headerSpan = buffer.AsSpan(0, bytesRead);


                foreach (var sig in BinarySignatures)
                {
                    if (bytesRead >= sig.Signature.Length && headerSpan.Slice(0, sig.Signature.Length).SequenceEqual(sig.Signature))
                    {
                        if (sig.Extension == ".zip")
                        {
                            return IsDocxStream(fileStream, originalPosition) ? ".docx" : ".zip";
                        }
                        return sig.Extension;
                    }
                }

                foreach (var bom in TextBoms)
                {
                    if (bytesRead >= bom.Signature.Length && headerSpan.Slice(0, bom.Signature.Length).SequenceEqual(bom.Signature))
                    {
                        return IsTextExtension(fallbackExtension) ? fallbackExtension : ".txt";
                    }
                }

                if (IsTextContent(fileStream, originalPosition))
                {
                    return IsTextExtension(fallbackExtension) ? fallbackExtension : ".txt";
                }

                return fallbackExtension;
            }
            finally
            {
                fileStream.Position = originalPosition;
            }
        }

        private static bool IsTextExtension(string ext) =>
            ext.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
            ext.Equals(".csv", StringComparison.OrdinalIgnoreCase);

        private static bool IsDocxStream(Stream fileStream, long originalPosition)
        {
            try
            {
                fileStream.Position = originalPosition;
                using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: true);
                return archive.GetEntry("word/document.xml") != null;
            }
            catch
            {
                return false;
            }
        }

        private static bool IsTextContent(Stream fileStream, long originalPosition)
        {
            fileStream.Position = originalPosition;

            byte[] sampleBuffer = new byte[1024];
            int read = fileStream.Read(sampleBuffer, 0, sampleBuffer.Length);

            if (read == 0) return true;

            for (int i = 0; i < read; i++)
            {
                if (sampleBuffer[i] == 0x00)
                {
                    return false;
                }
            }

            return true;
        }
    }
}