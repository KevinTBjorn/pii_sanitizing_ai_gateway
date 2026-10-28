using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.RequestParser
{
    public record InputFile(Stream fileStream, string extension);
}
