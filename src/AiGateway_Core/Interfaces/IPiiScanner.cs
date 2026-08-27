using AiGateway_Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.Interfaces
{
    public interface IPiiScanner
    {
        PiiScanResult ScanAndMask(string prompt);
    }
}
