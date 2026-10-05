using AiGateway_Core.Orchestration;
using AiGateway_Core.PiiScanners;
using AiGateway_Core.TextMasking;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_UnitTests.Core.TextMasking
{
    public class MaskingTransformerTests
    {
        private readonly MaskingTransformer _sut = new();

        [Fact]
        public async Task PiiTransformAsync_MultipleMatches_MasksAllEntitiesWithoutOffsetCorruption()
        {
            var text = "Alice sent email to alice@example.com today";
            var matches = new List<PiiMatch>
        {
            new(Start: 0, Length: 5, EntityType: "NAME", Score: 0.95, DetectedText: "Alice"),
            new(Start: 20, Length: 17, EntityType: "EMAIL", Score: 0.99, DetectedText: "alice@example.com")
        };

            var entry = new TextEntry
            {
                Content = text,
                Source = TextSourceType.UserPrompt,
                Matches = matches
            };

            var result = await _sut.PiiTransformAsync(new[] { entry }, TestContext.Current.CancellationToken);

            Assert.Single(result);
            Assert.Equal("[MASKED_NAME] sent email to [MASKED_EMAIL] today", result[0].Content);
            Assert.Empty(result[0].Matches);
        }


        [Fact]
        public async Task PiiTransformAsync_NoMatches_ReturnsOriginalEntryUnchanged()
        {
            var text = "Clean prompt with no sensitive data.";
            var entry = new TextEntry
            {
                Content = text,
                Source = TextSourceType.Document,
                Matches = Array.Empty<PiiMatch>()
            };

            var result = await _sut.PiiTransformAsync(new[] { entry }, TestContext.Current.CancellationToken);

            Assert.Single(result);
            Assert.Equal(text, result[0].Content);
            Assert.Same(entry, result[0]);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task PiiTransformAsync_NullOrEmptyContent_ReturnsOriginalContent(string? content)
        {
            var matches = new List<PiiMatch>
            {
                new(Start: 0, Length: 5, EntityType: "NAME", Score: 0.95, DetectedText: "Alice")
            };

            var entry = new TextEntry
            {
                Content = content!,
                Source = TextSourceType.UserPrompt,
                Matches = matches
            };

            var result = await _sut.PiiTransformAsync(new[] { entry }, TestContext.Current.CancellationToken);

            var transformed = Assert.Single(result);
            Assert.Equal(content, transformed.Content);
            Assert.Empty(transformed.Matches);
        }
    }
}
