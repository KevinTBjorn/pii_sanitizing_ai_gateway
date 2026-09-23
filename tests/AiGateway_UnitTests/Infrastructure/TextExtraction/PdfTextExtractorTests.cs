using AiGateway_Infrastructure.TextExtraction;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace AiGateway_UnitTests.Infrastructure.TextExtraction
{
    public class PdfTextExtractorTests
    {
        private readonly ITestOutputHelper _output;

        // xUnit automatically injects ITestOutputHelper here
        public PdfTextExtractorTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ExtractTextAsync_LocalFile_ExtractsTextCorrectly()
        {
            // Arrange
            var extractor = new PdfTextExtractor();
            var filePath = @"C:\repos\AiGateway\DocumentTest\pdfTest.pdf"; // Sti til en lokal PDF

            using var stream = File.OpenRead(filePath);

            // Act
            var text = await extractor.ExtractTextAsync(stream, TestContext.Current.CancellationToken);
            _output.WriteLine("=== EXTRACTED TEXT START ===");
            _output.WriteLine(text);
            _output.WriteLine("=== EXTRACTED TEXT END ===");

            // Assert
            Assert.NotNull(text);
            Assert.NotEmpty(text);
        }
    }
}
