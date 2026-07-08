# ServiceDeskLite — AI Roadmap

ServiceDeskLite is evolving into an **AI-first ticketing system** — one that pairs genuine agent autonomy with real product value and a robust, production-grade architecture.

This roadmap builds directly on the systems already in place:

- **Agentic chat assistant** — Anthropic, tool-calling, streaming
- **RAG semantic search** — Voyage embeddings + pgvector
- **Domain-validated tools** — the model decides *what*, the domain decides *whether*
- **ADR-documented decisions** — every architectural choice is written down
- **C# / .NET backend** — clean, layered, hexagonal

The through-line across every phase: **the model proposes, the domain disposes.** Autonomy grows, but never at the expense of correctness or safety.

---

## Phase 1 — Deepen Agent Autonomy

Extend the existing agentic loop so it can carry out multi-step work on its own.

### 1. Agent Memory
- **Short-term:** persist conversation state in the backend
- **Long-term:** user profiles, ticket history, preferences
- Backed by Postgres + embeddings
- Memory retrieval exposed as its own tool

### 2. Multi-Tool Orchestration
- The agent plans tool sequences autonomously
- Example flow: `find_similar_tickets` → `create_ticket` → `assign_ticket`
- Built-in error handling and retry strategies

### 3. Self-Critique & Self-Correction
- Error classification and confidence scoring
- Tool fallbacks when the primary path fails
- Tighter correction loops inside the agentic cycle

---

## Phase 2 — Grow the RAG System

Turn today's semantic search into a full knowledge system.

### 4. Knowledge-Base RAG
- Dedicated `/KnowledgeBase` corpus
- Embeddings for articles, FAQs, and internal docs
- The agent draws on RAG for solution suggestions
- Streamed answers **with source citations**

### 5. Hybrid RAG
- Blend semantic + keyword + metadata retrieval
- Prioritization by ticket type
- Confidence-based ranking

### 6. RAG Evaluation
- Hallucination checks — answer vs. source comparison
- Automated scoring
- The agent corrects itself when grounding is weak

---

## Phase 3 — Ship Product Features

Wire the AI directly into the workflows that matter.

### 7. AI Auto-Routing
- Automatic prioritization and category detection
- Agent assignment
- Workflow status derived from ticket content

### 8. AI Ticket Summaries (Streaming)
- Summary, next steps, risks, and missing information
- Live-streamed for a responsive UX

### 9. AI Dashboard
- Ticket volume and automation rate
- Duplicate rate and RAG confidence
- Tool-calling statistics and token usage

---

## Phase 4 — Architectural Maturity

Make the AI features production-ready.

### 10. Agent Sandbox
- Isolated tool execution with domain validation
- Logging, rate limits, and safety checks
- Clean error propagation back into the agentic loop

### 11. Agent Evaluation Pipeline
- Test prompts and regression tests
- Tool-calling, RAG, and streaming test suites
- Deterministic test fixtures

### 12. Observability
- Prometheus metrics
- Tool latency, error rates, token usage
- RAG confidence and agent-decision tracing

---

## Phase 5 — Autonomous Ticket Worker

A background process that works tickets end to end.

### 13. Autonomous Worker
- Scans open tickets and detects missing information
- Asks follow-up questions
- Generates solution proposals
- Closes tickets automatically — and opens new ones when needed
- Combines RAG + tool-calling + streaming
- Operates within defined policies

---

## Milestones Overview

Each phase maps to a GitHub milestone, and every roadmap item is tracked as its own issue.

| Milestone | Phase | Focus                  | Issues    | Status  |
|:---------:|:-----:|------------------------|-----------|---------|
| **M4**    | 1     | Agent Autonomy         | #153–#155 | Planned |
| **M5**    | 2     | RAG Expansion          | #156–#158 | Planned |
| **M6**    | 3     | Product Features       | #159–#161 | Planned |
| **M7**    | 4     | Architectural Maturity | #162–#164 | Planned |
| **M8**    | 5     | Autonomous Worker      | #165      | Planned |

### M4 — Agent Autonomy
- **#153** Agent memory — persistent short/long-term state + retrieval tool
- **#154** Autonomous multi-tool orchestration with retry strategies
- **#155** Self-critique & self-correction in the agentic loop

### M5 — RAG Expansion
- **#156** Knowledge-base RAG with cited streaming answers
- **#157** Hybrid retrieval — semantic + keyword + metadata ranking
- **#158** RAG evaluation — hallucination checks & grounding scores

### M6 — Product Features
- **#159** AI auto-routing — priority, category, assignment, status
- **#160** Streaming AI ticket summaries
- **#161** AI dashboard — automation, RAG confidence & token-usage metrics

### M7 — Architectural Maturity
- **#162** Agent sandbox — isolated tool execution, rate limits, safety checks
- **#163** Agent evaluation pipeline — regression, tool-calling, RAG, streaming
- **#164** Observability — Prometheus metrics, latency, token usage, decision tracing

### M8 — Autonomous Worker
- **#165** Autonomous ticket worker — background loop scanning & resolving tickets
