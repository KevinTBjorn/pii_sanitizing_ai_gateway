# C# AI Gateway (POC)

A Clean Architecture-based AI Gateway built in .NET. This service acts as a reverse proxy between client applications and Large Language Models (LLMs), providing document parsing, PII sanitization, and language detection before sending prompts to the LLM.

## Architecture & Tech Stack

*   **Framework:** .NET (C#) ASP.NET Core Web API
*   **Structure:** Clean Architecture (`AiGateway_Core` for domain logic/interfaces, `AiGateway_Infrastructure` External service implementations, `AiGateway_Proxy` for HTTP/Transport).
*   **PII Sanitization:** Microsoft Presidio (Containerized) + Custom C# masking
*   **Language Detection:** Lingua.
*   **LLM Provider:** Ollama (Local models).
*   **Telemetry:** Prometheus & Grafana.
*   **Infrastructure:** Docker Compose.

## Features

*   **Multipart Request Parsing:** Accepts prompts alongside documents (e.g., `.txt`, `.docx`, `.pdf`).
*   **PII Masking:** Detects sensitive data using Presidio and replaces it with tags (e.g., `[MASKED_NAME]`, `[MASKED_EMAIL]`).
*   **Language Fallback:** Automatically detects user prompt language, falling back to a default configuration when confidence is low.
*   **Observability:** Emits PII masking metrics and request telemetry.

---

## Getting Started

### Prerequisites
*   Docker & Docker Compose
*   .NET SDK (for local debugging)

> **Note:** Ensure your terminal's current working directory is the project root before running the commands below.

> **Note on LLM Output Quality:** The default model (`smollm:135m`) is constrained purely for low-latency testing. output quality is secondary, it exists purely to demonstrate the full end-to-end process.

### Overview of Docker Profiles

To provide flexibility during development, the `docker-compose.yml` architecture separates core background infrastructure from the application gateway using **Docker Compose Profiles**.

* **Default Mode (Infrastructure Only):** Runs supporting background services (Prometheus, Grafana, Presidio, and Ollama). This mode leaves out the Gateway so you can run and debug `AiGateway` directly in your local IDE.
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
### Testing the Gateway
Since the API runs on port 8080 in the withgateway profile, you can test it using Postman or cURL.

To test using a sample prompt and a document included in the repository:

1. Open a new terminal.
2. Navigate to the root directory of the cloned repository.
```bash
curl.exe -X POST http://localhost:8080/v1/chat/completions `
  -F "prompt=Hej, jeg hedder Sofie Hansen og bor i Aarhus. Skriv til sofie.hansen@example.com. Min IP er 192.168.1.1, mit Visa er 4000 0000 0000 0002 og jeg arbejder hos Novo Nordisk." `
  -F "model=smollm:135m" `
  -F "temperature=0.0" `
  -F "file=@DocumentTest\FileTest.docx"
```
---
### Viewing the Grafana Dashboard
The gateway emits Prometheus metrics to track request processing and PII entity statistics.

1. Ensure the stack is running via Docker Compose (`docker compose --profile withgateway up -d`).
2. Open the pre-configured PII & Request Telemetry dashboard:
   * **Dashboard URL:** [http://localhost:3000/d/aigateway-pii-telemetry/ai-gateway-pii-and-request-telemetry](http://localhost:3000/d/aigateway-pii-telemetry/ai-gateway-pii-and-request-telemetry)
3. Log in using default credentials (if prompted, click **Skip** for password change):
   * **Username:** `admin`
   * **Password:** `admin`
---
### Stopping and Cleanup
  Stop containers (preserves downloaded Ollama models and volume data):
  ```bash
  docker compose --profile withgateway down
  ```
  Complete cleanup (removes containers, networks, and persistent volumes):
  ```bash
  docker compose --profile withgateway down -v
  ```