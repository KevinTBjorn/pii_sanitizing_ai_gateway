using AiGateway_Core.Constants;
using AiGateway_Infrastructure.Services;
using AiGateway_Infrastructure.Telemetry;
using System.Diagnostics.Metrics;

namespace AiGateway_UnitTests.Infrastructure.Services
{
    public class PiiScannerServiceTests
    {
        private readonly PiiScannerService _sut = new();

        #region Empty & Null Inputs

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ScanAndMask_NullOrWhitespaceInput_ReturnsFalseAndUnchanged(string? input)
        {
            var result = _sut.ScanAndMask(input!);

            Assert.False(result.HasSensitiveData);
            Assert.Equal(input, result.CleanedPrompt);
            Assert.Empty(result.DetectedTypes);
        }

        #endregion

        #region Email Masking

        [Theory]
        [InlineData("Kontakt mig på user@domain.com for hjælp.", $"Kontakt mig på {PiiTokens.Email} for hjælp.")]
        [InlineData("Send til søren@ræv.dk tak.", $"Send til {PiiTokens.Email} tak.")]
        [InlineData("Skriv til <info+tag@firma.co.uk>.", $"Skriv til <{PiiTokens.Email}>.")]
        [InlineData("E-mail:bjørn.ægidius@ecg.dk", $"E-mail:{PiiTokens.Email}")]
        public void ScanAndMask_ShouldMaskEmailsCorrectly(string input, string expected)
        {
            var result = _sut.ScanAndMask(input);

            Assert.True(result.HasSensitiveData);
            Assert.Equal(expected, result.CleanedPrompt);
            Assert.Contains("Email", result.DetectedTypes);
        }

        [Theory]
        [InlineData("Dette er ikke en email: bruger snabel-a domæne.dk")]
        [InlineData("Skriv til user@domain")] // Mangler .com eller .dk
        [InlineData("Vi mødes @ cafeen")]      // Bare et snabel-a
        public void ScanAndMask_InvalidEmailFormat_ShouldNotMask(string input)
        {
            var result = _sut.ScanAndMask(input);

            Assert.False(result.HasSensitiveData);
            Assert.Equal(input, result.CleanedPrompt);
            Assert.DoesNotContain("Email", result.DetectedTypes);
        }

        #endregion

        #region CPR Masking

        [Theory]
        [InlineData("Mit CPR er 120395-1234.", $"Mit CPR er {PiiTokens.Cpr}.")]
        [InlineData("CPR u. bindestreg: 1203951234", $"CPR u. bindestreg: {PiiTokens.Cpr}")]
        public void ScanAndMask_ValidCpr_ShouldMaskCpr(string input, string expected)
        {
            var result = _sut.ScanAndMask(input);

            Assert.True(result.HasSensitiveData);
            Assert.Equal(expected, result.CleanedPrompt);
            Assert.Contains("Danish_CPR", result.DetectedTypes);
        }

        [Theory]
        [InlineData("Ordrenummer 1203951234567")] // 13 cifre -> Ikke CPR
        [InlineData("Faktura 12345678")]          // 8 cifre -> Ikke CPR
        [InlineData("Dato 320195-1234")]          // Ugyldig dag 32 -> Ikke CPR
        public void ScanAndMask_InvalidCprFormat_ShouldNotMask(string input)
        {
            var result = _sut.ScanAndMask(input);

            Assert.False(result.HasSensitiveData);
            Assert.Equal(input, result.CleanedPrompt);
            Assert.DoesNotContain("Danish_CPR", result.DetectedTypes);
        }

        #endregion

        #region OpenAI API Key Masking

        [Theory]
        [InlineData("Her er min nøgle: sk-proj-12345678901234567890123456789012", $"Her er min nøgle: {PiiTokens.ApiKey}")]
        [InlineData("thesk-admin-123456789012345678901234567890123456", $"the{PiiTokens.ApiKey}")]
        [InlineData("OPENAI_KEY=sk-12345678901234567890123456789012", $"OPENAI_KEY={PiiTokens.ApiKey}")]
        public void ScanAndMask_OpenAiKey_ShouldMaskKey(string input, string expected)
        {
            var result = _sut.ScanAndMask(input);

            Assert.True(result.HasSensitiveData);
            Assert.Equal(expected, result.CleanedPrompt);
            Assert.Contains("OpenAI_API_Key", result.DetectedTypes);
        }

        [Theory]
        [InlineData("sk-proj-12345")] // Nøglen er for kort (vores regex kræver mindst 32 tegn)
        [InlineData("Jeg bruger en task-manager i dag")] // Indeholder 'sk-', men er et almindeligt ord
        [InlineData("Husk-at købe ind")] // Indeholder 'sk-' midt i ord/sætning
        public void ScanAndMask_InvalidOpenAiKey_ShouldNotMask(string input)
        {
            var result = _sut.ScanAndMask(input);

            Assert.False(result.HasSensitiveData);
            Assert.Equal(input, result.CleanedPrompt);
            Assert.DoesNotContain("OpenAI_API_Key", result.DetectedTypes);
        }

        #endregion

        #region Generic Key Assignment Masking

        [Theory]
        [InlineData("api_key = \"123456789012345678\"", $"{PiiTokens.ApiKey}")]
        [InlineData("access_token: 'abcdefghijklmnopqrstuvwxyz123'", $"{PiiTokens.ApiKey}")]
        [InlineData("auth-token=12345678901234567890", $"{PiiTokens.ApiKey}")]
        public void ScanAndMask_GenericSecretAssignment_ShouldMaskEntireAssignment(string input, string expected)
        {
            var result = _sut.ScanAndMask(input);

            Assert.True(result.HasSensitiveData);
            Assert.Equal(expected, result.CleanedPrompt);
            Assert.Contains("Generic_Secret", result.DetectedTypes);
        }

        [Theory]
        [InlineData("Hvad er en api_key egentlig for noget?")] // Ordet er der, men ingen værdi tildeles
        [InlineData("api_key = 123")] // Værdien er for kort (kræver mindst 16 tegn)
        [InlineData("secret_key findes i manualen")] // Igen, ingen tildeling
        public void ScanAndMask_InvalidGenericSecret_ShouldNotMask(string input)
        {
            var result = _sut.ScanAndMask(input);

            Assert.False(result.HasSensitiveData);
            Assert.Equal(input, result.CleanedPrompt);
            Assert.DoesNotContain("Generic_Secret", result.DetectedTypes);
        }

        #endregion

        #region Multiple PII Types & Clean Input

        [Fact]
        public void ScanAndMask_MultiplePiiInSinglePrompt_ShouldMaskAllAndReturnAllTypes()
        {
            string prompt = "Bruger søren@ræv.dk har CPR 120395-1234 og key sk-proj-12345678901234567890123456789012";

            var result = _sut.ScanAndMask(prompt);

            Assert.True(result.HasSensitiveData);
            Assert.Equal($"Bruger {PiiTokens.Email} har CPR {PiiTokens.Cpr} og key {PiiTokens.ApiKey}", result.CleanedPrompt);
            Assert.Equal(3, result.DetectedTypes.Count);
            Assert.Contains("Email", result.DetectedTypes);
            Assert.Contains("Danish_CPR", result.DetectedTypes);
            Assert.Contains("OpenAI_API_Key", result.DetectedTypes);
            Assert.DoesNotContain("søren@ræv.dk", result.CleanedPrompt);
            Assert.DoesNotContain("120395-1234", result.CleanedPrompt);
            Assert.DoesNotContain("sk-proj-12345678901234567890123456789012", result.CleanedPrompt);
        }

        [Fact]
        public void ScanAndMask_CleanPrompt_ShouldReturnUnchanged()
        {
            string prompt = "Hvad er hovedstaden i Danmark og hvordan virker fotosyntese?";

            var result = _sut.ScanAndMask(prompt);

            Assert.False(result.HasSensitiveData);
            Assert.Equal(prompt, result.CleanedPrompt);
            Assert.Empty(result.DetectedTypes);
        }

        #endregion

        #region Metrics Counter Increment Test
        [Fact]
        public void ScanAndMask_ShouldIncrementPiiMetricsCounter()
        {
            // Arrange: Opsæt en MeterListener til at lytte på vore static PiiMetrics
            long recordedValue = 0;
            string? recordedPiiType = null;

            using var listener = new MeterListener();
            listener.InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == PiiMetrics.MeterName && instrument.Name == PiiMetrics.PiiMaskedCounterName)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            };

            listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
            {
                recordedValue += measurement;
                foreach (var tag in tags)
                {
                    if (tag.Key == "pii_type") recordedPiiType = tag.Value?.ToString();
                }
            });

            listener.Start();

            // Act
            _sut.ScanAndMask("Send til mail1@test.dk og mail2@test.dk");

            // Assert
            Assert.Equal(2, recordedValue); // Forventer 2 e-mails talt op
            Assert.Equal("masked_email", recordedPiiType);
        }
    }
        #endregion
}
