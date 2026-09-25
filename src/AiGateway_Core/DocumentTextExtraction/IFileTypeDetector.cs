using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.DocumentTextExtraction
{
    public interface IFileTypeDetector
    {
        /// <summary>
        /// Inspects the stream's magic bytes to determine the actual file type.
        /// </summary>
        Task<string> DetectTypeAsync(Stream stream, string fallbackExtension = "", CancellationToken cancellationToken = default);
    }
}
