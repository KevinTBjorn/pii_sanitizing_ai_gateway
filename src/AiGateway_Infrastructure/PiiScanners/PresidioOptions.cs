using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace AiGateway_Core.Options
{
    public class PresidioOptions
    {
        public const string SectionName = "PresidioOptions";

        [Required(ErrorMessage = "PresidioOptions:PresidioUrl is required in configuration.")]
        public required Uri PresidioUrl { get; set; }

        [Required]
        [Range(1, 60, ErrorMessage = "PresidioOptions:TimeoutSeconds must be between 1 and 60 seconds.")]
        public required int TimeoutSeconds { get; set; }
    }
}