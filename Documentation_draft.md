# AI Gateway — Technical Documentation

This documentation describes the architecture, technology stack, and instructions for controlling and extending the AI Gateway solution.

---

## 1. Technology Stack

| Component | Technology | Role / Function |
| :--- | :--- | :--- |
| **Reverse Proxy** | .NET 10 | Receives client requests, coordinates PII scanning, and forwards requests to the LLM. |
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

To provide flexibility during development, the `docker-compose.yml` architecture separates core background infrastructure from the application gateway using **Docker Compose Profiles**.

* **Default Mode (Infrastructure Only):** Runs supporting background services (Prometheus, Grafana, Presidio, and Ollama). This mode leaves ports open on `localhost` so you can run and debug `AiGateway_Proxy` directly in your local IDE.
* **Gateway Profile (`withgateway`):** Runs the complete containerized stack, including the `aigateway` proxy service mapped to port `8080`.

### CLI Commands

**Build and start the complete stack with Gateway:**
```bash
docker compose --profile withgateway up --build -d
```
**Stream Gateway logs to inspect PII masking:**
```bash
docker logs -f aigateway
```

---

# Configuration of AiGateway service appsettings

## 1. PII Options (`PiiOptions`)

Controls communication parameters with the Presidio Analyzer service.

| Key | Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| **`BaseUrl`** | `string` | `http://localhost:5001` | Base HTTP address of the Presidio Analyzer service. *(Overridden to `http://presidio-analyzer:3000` in Docker Compose)*. |
| **`TimeoutSeconds`** | `integer` | `13` | Maximum time (in seconds) to wait for Presidio to analyze a payload before throwing a timeout exception. |
---

## 2. Language Detection (`LanguageDetection`)

Configures supported languages and fallback logic for lingua langauge to detect so it can be passed to Presidio.

| Key | Type | Default Value | Description |
| :--- | :--- | :--- | :--- |
| **`SupportedLanguages`** | `array` | `["da", "en"]` | Array of ISO 639-1 language codes enabled for text scanning. |
| **`DefaultFallback`** | `string` | `"en"` | Default language code passed to Presidio if language detection is inconclusive or yields an unsupported language. |

---