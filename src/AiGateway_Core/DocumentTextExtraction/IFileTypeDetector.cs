using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.DocumentTextExtraction
{
    public interface IFileTypeDetector
    {
        Task<string> DetectTypeAsync(Stream fileStream, string fallbackExtension = "", CancellationToken cancellationToken = default);
    }
}
