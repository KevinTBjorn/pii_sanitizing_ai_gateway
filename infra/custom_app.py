import copy
import logging
from flask import Flask, request, jsonify
from presidio_analyzer import AnalyzerEngine, RecognizerRegistry, Pattern, PatternRecognizer
from presidio_analyzer.nlp_engine import NlpEngineProvider

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("presidio-custom")

app = Flask(__name__)

def build_custom_analyzer():
    logger.info("Initializing Custom Presidio Analyzer Engine...")
    
    provider = NlpEngineProvider(conf_file="/app/presidio_analyzer/conf/default.yaml")
    nlp_engine = provider.create_engine()

    registry = RecognizerRegistry()
    registry.load_predefined_recognizers(nlp_engine=nlp_engine)

    global_recognizer_names = [
        "CreditCardRecognizer", "IbanRecognizer", "IpRecognizer",
        "CryptoRecognizer", "UrlRecognizer",
        "MacAddressRecognizer", "PhoneRecognizer", "DateRecognizer"
    ]

    existing_recognizers = list(registry.recognizers)
    for rec in existing_recognizers:
        if rec.name in global_recognizer_names and rec.supported_language == "en":
            rec_da = copy.deepcopy(rec)
            rec_da.supported_language = "da"
            registry.add_recognizer(rec_da)

    # Define pattern
    cpr_pattern = Pattern(
        name="cpr_pattern",
        regex=r"(?<!\w)(?:0[1-9]|[12][0-9]|3[01])(?:0[1-9]|1[0-2])[0-9]{2}[- ]?[0-9]{4}(?!\w)",
        score=0.65
    )

    email_pattern = Pattern(
        name="email_pattern",
        regex=r"(?i)\b[a-zA-Z0-9æøåÆØÅ._%+-]+@[a-zA-Z0-9æøåÆØÅ.-]+\.[a-zA-ZæøåÆØÅ]{2,}\b",
        score=1.0
    )

    cvr_pattern = Pattern(
        name="cvr_pattern",
        regex=r"(?<!\d)[1-9][0-9]{7}(?!\d)",
        score=0.4
    )

    bank_pattern = Pattern(
        name="dk_bank_pattern",
        regex=r"(?<!\d)[0-9]{4}[- ]?[0-9]{6,10}(?!\d)",
        score=0.5
    )

    generic_key_pattern = Pattern(
        name="generic_api_key_pattern",
        regex=r"(?i)(?:api[_-]?key|access[_-]?token|secret[_-]?key|auth[_-]?token)\s*[:=]\s*[\"']?([a-zA-Z0-9_\-\.]{16,})[\"']?",
        score=1
    )

    for lang in ["da", "en"]:
        # Adds pattern to multiple language
        registry.add_recognizer(PatternRecognizer(
            name=f"DkCprRecognizer_{lang}",
            supported_entity="DK_CPR",
            supported_language=lang,
            patterns=[cpr_pattern],
            context=["cpr", "cpr-nr", "personnummer", "cpr nr"]
        ))
        
        registry.add_recognizer(PatternRecognizer(
            name=f"CustomEmailRecognizer_{lang}",
            supported_entity="EMAIL_ADDRESS",
            supported_language=lang,
            patterns=[email_pattern]
        ))

        registry.add_recognizer(PatternRecognizer(
            name=f"DkCvrRecognizer_{lang}",
            supported_entity="DK_CVR",
            supported_language=lang,
            patterns=[cvr_pattern],
            context=["cvr", "cvr-nr", "virksomhedsnummer", "cvr nr"]
        ))

        registry.add_recognizer(PatternRecognizer(
            name=f"DkBankAccountRecognizer_{lang}",
            supported_entity="DK_BANK_ACCOUNT",
            supported_language=lang,
            patterns=[bank_pattern],
            context=["reg", "reg.nr", "kontonr", "konto", "reg.nr.", "kontonummer", "bankkonto", "reg nr"]
        ))

        registry.add_recognizer(PatternRecognizer(
            name=f"GenericApiKeyRecognizer_{lang}",
            supported_entity="API_KEY",
            supported_language=lang,
            patterns=[generic_key_pattern],
            context=["key", "token", "secret", "auth", "bearer", "api"]
        ))

    logger.info("Custom Engine initialized successfully!")
    return AnalyzerEngine(nlp_engine=nlp_engine, registry=registry)

analyzer = build_custom_analyzer()

@app.route("/health", methods=["GET"])
@app.route("/healthz", methods=["GET"])
def health():
    return jsonify({"status": "healthy"}), 200

@app.route("/recognizers", methods=["GET"])
def get_recognizers():
    language = request.args.get("language")
    recognizers = analyzer.get_recognizers(language=language)
    return jsonify([r.name for r in recognizers]), 200

@app.route("/analyze", methods=["POST"])
def analyze():
    data = request.get_json() or {}
    results = analyzer.analyze(
        text=data.get("text"),
        language=data.get("language"),
        entities=data.get("entities"),
        score_threshold=data.get("score_threshold"),
        return_decision_process=data.get("return_decision_process", False)
    )
    return jsonify([res.to_dict() for res in results]), 200

if __name__ == "__main__":
    app.run(host="0.0.0.0", port=3000)