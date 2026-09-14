import logging
from flask import Flask, request, jsonify
from presidio_analyzer import AnalyzerEngine, RecognizerRegistry, Pattern, PatternRecognizer
from presidio_analyzer.nlp_engine import NlpEngineProvider

logging.basicConfig(level=logging.INFO)
logger = logging.getLogger("presidio-custom")

app = Flask(__name__)

def init_engine():
    logger.info("Initializing NLP Engine and Recognizers...")
    
    # 1. Indlæs sprogmodeller fra default.yaml (da_core_news_lg & en_core_web_lg)
    provider = NlpEngineProvider(conf_file="/app/presidio_analyzer/conf/default.yaml")
    nlp_engine = provider.create_engine()

    # 2. Indlæs indbyggede recognizers
    registry = RecognizerRegistry()
    registry.load_predefined_recognizers(nlp_engine=nlp_engine)

    # 3. Dansk CPR
    cpr_pattern = Pattern(name="cpr_pattern", regex=r"\b(0[1-9]|[12][0-9]|3[01])(0[1-9]|1[0-2])[0-9]{2}[- ]?[0-9]{4}\b", score=0.85)
    registry.add_recognizer(PatternRecognizer(
        supported_entity="DK_CPR",
        supported_language="da",
        patterns=[cpr_pattern],
        context=["cpr", "cpr-nr", "personnummer"]
    ))

    # 4. Dansk CVR
    cvr_pattern = Pattern(name="cvr_pattern", regex=r"\b[1-9][0-9]{7}\b", score=0.7)
    registry.add_recognizer(PatternRecognizer(
        supported_entity="DK_CVR",
        supported_language="da",
        patterns=[cvr_pattern],
        context=["cvr", "cvr-nr", "virksomhedsnummer"]
    ))

    # 5. E-mail (da)
    email_pattern = Pattern(name="email_pattern", regex=r"(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b", score=1.0)
    registry.add_recognizer(PatternRecognizer(
        supported_entity="EMAIL_ADDRESS",
        supported_language="da",
        patterns=[email_pattern]
    ))

    # 6. OpenAI API Keys (all)
    openai_pattern = Pattern(name="openai_pattern", regex=r"sk-[a-zA-Z0-9T3BlbkFJ]{20,}|sk-proj-[a-zA-Z0-9_-]{40,}", score=1.0)
    registry.add_recognizer(PatternRecognizer(
        supported_entity="API_KEY",
        supported_language="all",
        patterns=[openai_pattern]
    ))

    logger.info("Engine initialization completed successfully!")
    return AnalyzerEngine(nlp_engine=nlp_engine, registry=registry)

# Initialiser motoren én gang ved opstart
analyzer = init_engine()

@app.route("/health", methods=["GET"])
@app.route("/healthz", methods=["GET"])
def health():
    return jsonify({"status": "healthy"}), 200

@app.route("/analyze", methods=["POST"])
def analyze():
    data = request.get_json() or {}
    text = data.get("text", "")
    language = data.get("language", "en")
    entities = data.get("entities", None)
    score_threshold = data.get("score_threshold", None)

    results = analyzer.analyze(
        text=text,
        language=language,
        entities=entities,
        score_threshold=score_threshold
    )

    return jsonify([res.to_dict() for res in results]), 200

if __name__ == "__main__":
    app.run(host="0.0.0.0", port=3000)