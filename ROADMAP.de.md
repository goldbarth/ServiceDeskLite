ROADMAP.md
🚀 ServiceDeskLite – AI Roadmap

Diese Roadmap beschreibt die geplanten Erweiterungen für die KI‑/LLM‑Funktionen von ServiceDeskLite.
Sie baut direkt auf den bestehenden Systemen auf:

    Agentic Chat-Assistent (Anthropic, Tool‑Calling, Streaming)

    RAG‑Semantiksuche (Voyage + pgvector)

    Domain‑validierte Tools

    ADR‑Dokumentation

    C#/.NET Backend

Ziel ist ein AI‑First Ticketing System, das echte Autonomie, Produktwert und robuste Architektur vereint.
🧠 Phase 1 — Agent‑Autonomie erhöhen

Der bestehende agentische Loop wird erweitert, um komplexere Aufgaben selbstständig auszuführen.
1. Agent Memory

   Kurzfristiges Memory: Conversation‑State im Backend speichern

   Langfristiges Memory: User‑Profile, Ticket‑Historie, Präferenzen

   Speicherung über Postgres + Embeddings

   Memory‑Retrieval als eigenes Tool

2. Multi‑Tool‑Orchestrierung

   Agent plant Tool‑Sequenzen autonom

   Beispiele: Duplicate‑Check → create_ticket → assign_ticket

   Fehler‑Handling + Retry‑Strategien

3. Self‑Critique / Self‑Correction

   Fehlerklassifikation

   Confidence‑Scores

   Tool‑Fallbacks

   Verbesserte Korrekturschleifen im agentischen Loop

🔍 Phase 2 — RAG‑System erweitern

Die bestehende semantische Suche wird zu einem vollwertigen Knowledge‑System ausgebaut.
4. Knowledge‑Base RAG

   Ordner /KnowledgeBase

   Embeddings für Artikel, FAQ, interne Dokumente

   Agent nutzt RAG für Lösungsvorschläge

   Streaming‑Antworten mit Quellenangabe

5. Hybrid‑RAG

   Kombination aus Semantik + Keyword + Metadata

   Priorisierung nach Ticket‑Typ

   Confidence‑Ranking

6. RAG‑Evaluation

   Halluzinations‑Check

   Vergleich zwischen Antwort und Quelle

   Score‑Berechnung

   Agent korrigiert sich selbst

⚙️ Phase 3 — Produkt‑Features

AI‑Funktionen werden direkt in produktrelevante Workflows integriert.
7. AI‑Ticket‑Auto‑Routing

   Automatische Priorisierung

   Kategorie‑Erkennung

   Mitarbeiter‑Zuweisung

   Workflow‑Status basierend auf Ticketinhalt

8. AI‑Ticket‑Summaries (Streaming)

   Zusammenfassung

   nächste Schritte

   Risiken

   benötigte Informationen

   Live‑Streaming für bessere UX

9. AI‑Dashboard

   Ticket‑Volumen

   Automatisierungsquote

   Duplicate‑Rate

   RAG‑Confidence

   Tool‑Calling‑Statistik

   Token‑Usage

🧩 Phase 4 — Architektur‑Reife

Die AI‑Features werden produktionsreif gemacht.
10. Agent Sandbox

    Tools laufen isoliert

    Domain‑Validierung

    Logging

    Rate‑Limits

    Safety‑Checks

    Fehler‑Propagation in agentischen Loop

11. Agent‑Evaluation Pipeline

    Test‑Prompts

    Regression‑Tests

    Tool‑Calling‑Tests

    RAG‑Tests

    Streaming‑Tests

    deterministische Test‑Fixtures

12. Observability

    Prometheus Metrics

    Tool‑Latency

    Error‑Rates

    Token‑Usage

    RAG‑Confidence

    Agent‑Decision‑Tracing

🤖 Phase 5 — Autonomous Ticket Worker

Ein autonomer Hintergrundprozess, der Tickets selbstständig bearbeitet.
13. Autonomous Worker

    scannt offene Tickets

    erkennt fehlende Informationen

    stellt Rückfragen

    generiert Lösungsvorschläge

    schließt Tickets automatisch

    legt neue Tickets an, wenn nötig

    nutzt RAG + Tool‑Calling + Streaming

    arbeitet nach definierten Policies

📅 Release‑Plan (High‑Level)
Phase	Fokus	Status
1	Agent‑Autonomie	geplant
2	RAG‑Erweiterung	geplant
3	Produkt‑Features	geplant
4	Architektur‑Reife	geplant
5	Autonomous Worker	geplant
