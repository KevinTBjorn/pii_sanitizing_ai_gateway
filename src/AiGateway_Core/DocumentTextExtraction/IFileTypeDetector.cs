using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.DocumentTextExtraction
{
    public interface IFileTypeDetector
    {
        Task<string> DetectTypeAsync(Stream stream, string fallbackExtension = "", CancellationToken cancellationToken = default);
    }
}
