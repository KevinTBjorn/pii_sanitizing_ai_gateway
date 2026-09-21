# AI Gateway — Technical Documentation

This documentation describes the architecture, technology stack, and instructions for controlling and extending the AI Gateway solution.

---

## 1. Technology Stack

| Component | Technology | Role / Function |
| :--- | :--- | :--- |
| **Reverse Proxy** | .NET 10 / YARP | Receives client requests, coordinates PII scanning, and forwards requests to the LLM. |
| **PII Engine** | Presidio (Python / FastAPI) | Detects and masks sensitive information (e.g., CPR numbers, email addresses). |
| **LLM Service** | Ollama (`smollm:135m`) | Hosts and serves the local language model. |
| **Metrics & Monitoring** | Prometheus & Grafana | Collects metrics (`aigateway_pii_occurrences_total`) and visualizes dashboards. |
| **Orchestration** | Docker Compose | Container management split into core infrastructure services and gateway profiles. |

---

## 2. Configuration & Control

### Adding New PII Rules (Presidio)

PII detection logic is managed in `infra/custom_app.py`. You can add custom regular expression (regex) patterns.

* **Adding a Regex Pattern (e.g., internal employee ID):**

```python
# Define regex pattern
emp_pattern = Pattern(name="emp_id_pattern", regex=r"\bEMP-\d{6}\b", score=0.95)

    for lang in ["da", "en"]:
        # Adds pattern to multiple language
        registry.add_recognizer(PatternRecognizer(
            name=f"DkCprRecognizer_{lang}",
            supported_entity="DK_CPR",
            supported_language=lang,
            patterns=[cpr_pattern],
            context=["cpr", "cpr-nr", "personnummer", "cpr nr"]
        ))
```

# Changing or Adding Language Models in Presidio

Microsoft Presidio uses **spaCy** as its primary Natural Language Processing (NLP) engine for entity recognition and syntactic parsing. By default, it uses the lightweight English model (`en_core_web_sm`). 

Follow these steps to change model (e.g., `en_core_web_lg`) possible model: https://spacy.io/usage/models.

Change model_name in `infra/nlp_configuration`

```yaml
nlp_engine_name: spacy
models:
  - lang_code: da
    model_name: da_core_news_md
  - lang_code: en
    model_name: en_core_web_md
```
Change the name here what you set in nlp_configuration `infra/Dockerfile.presidio`
```yaml
RUN python -m spacy download da_core_news_md
RUN python -m spacy download en_core_web_md
```
# Operations & Lifecycle Management: Docker Profiles



## 1. Overview of Docker Profiles

To provide flexibility during development, the `docker-compose.yml` architecture separates background infrastructure from the application gateway using **Docker Compose Profiles**.

* **Default Mode (Infrastructure Only)**: Runs Prometheus, Grafana, Presidio, and Ollama.
* **Gateway Profile (`withgateway`)**: Runs the complete stack, including the containerized `aigateway` proxy service. This mode leaves port `8080` open so you can run and debug `AiGateway_Proxy`

To build with gateway in cli: docker compose --profile withgateway up --build -d
To see what masking happens there: docker logs -f aigateway

---

# Configuration Guide: PII & Language Detection

This document details the `PiiOptions` and `LanguageDetection` settings defined in `appsettings.json`. These parameters control Presidio integration, payload parsing rules, and multi-language fallback behavior.

---

## 1. PII Options (`PiiOptions`)

Controls communication parameters with the Presidio Analyzer service and specifies structural JSON keys to bypass during PII scanning.

| Key | Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| **`BaseUrl`** | `string` | `http://localhost:5001` | Base HTTP address of the Presidio Analyzer service. *(Overridden to `http://presidio-analyzer:3000` in Docker Compose)*. |
| **`TimeoutSeconds`** | `integer` | `13` | Maximum time (in seconds) to wait for Presidio to analyze a payload before throwing a timeout exception. |
| **`IgnoredKeys`** | `array` | *(See list below)* | JSON request/response object keys completely excluded from PII scanning. |

### Ignored Keys Rationale (`IgnoredKeys`)
Passing non-user text (LLM hyper-parameters, tool definitions, metadata) to Presidio increases latency and causes false positive detections. The following keys are skipped automatically during recursive JSON parsing:

* **LLM Hyper-parameters:** `model`, `temperature`, `top_p`, `top_k`, `max_tokens`, `max_completion_tokens`, `presence_penalty`, `frequency_penalty`, `repeat_penalty`, `num_predict`, `seed`, `stop`, `response_format`
* **Execution & Streaming:** `stream`, `keep_alive`
* **Function & Tool Calling:** `tools`, `tool_choice`, `functions`
* **Structural Metadata:** `role`, `type`, `id`, `object`, `created`, `usage`

---

## 2. Language Detection (`LanguageDetection`)

Configures supported languages and fallback logic for lingua langauge to detect so it can be passed to Presidio.

| Key | Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| **`SupportedLanguages`** | `array` | `["da", "en"]` | Array of ISO 639-1 language codes enabled for text scanning. |
| **`DefaultFallback`** | `string` | `"en"` | Default language code passed to Presidio if language detection is inconclusive or yields an unsupported language. |

---