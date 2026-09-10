# Autonomous 3D AI Agent Architecture

A high-performance hybrid AI framework implementing autonomous generative agents inside an interactive 3D simulation environment. Built on top of Stanford’s Generative Agent architecture, this system features dynamic cloud/local LLM fallback routing and a real-time text-to-speech (TTS) pipeline powered by **ElevenLabs** and Piper[cite: 2].

🎥 **[Watch the Showcase Video](https://www.youtube.com/watch?v=UzyQBu-9o9Q)**

---

## Key Architectural Features

* **Real-Time Voice Synthesis Pipeline**: Integrated ElevenLabs API for low-latency, natural voice generation, paired with Piper for lightweight offline fallbacks[cite: 2].
* **Hybrid LLM Orchestration Engine**: Python backend supporting dynamic runtime switching between locally hosted models (Qwen 3.5 9B) and cloud APIs (OpenAI, Gemini).


* **Context Engineering & Fallback Routing**: Robust prompt management, context window optimization, and automatic retry mechanisms to ensure reliable agent outputs across multi-turn interactions[cite: 1].
* **Generative Memory & Perception**: Unity C# client implementing agent state, reflection, and environmental perception mapped to backend decisions.



---

## Tech Stack

* **Backend & Orchestration**: Python, FastAPI / Flask, RESTful APIs.


* **Voice & Generative AI**: ElevenLabs TTS API, Piper TTS, OpenAI API, Google Gemini API, Qwen 3.5 9B.


* **Simulation Client**: C# / Unity 3D Engine.


* **Architecture**: Stanford Generative Agent Framework, Multi-Agent Orchestration.



---

## Project Structure

```text
├── backend/
│   ├── app.py                 # Core API gateway & routing engine
│   ├── llm_router.py          # Model switching logic (Qwen / OpenAI / Gemini)
│   ├── tts_pipeline.py        # ElevenLabs & Piper voice generation service
│   └── prompt_templates.py    # Structured agent prompts & memory schemas
├── unity_scripts/
│   ├── AgentController.cs     # C# state machine & backend HTTP wrapper
│   ├── MemoryManager.cs       # Client-side perception & event buffer
│   └── AudioStreamer.cs       # Audio buffer playback for real-time TTS
├── .env.example               # Template for API keys
└── README.md

```

---

## Getting Started

### Prerequisites

* Python 3.10+
* Unity 2021.3+
* ElevenLabs API Key, Gemini or OpenAI API Key

### Installation

1. **Clone the repository**:
```bash
git clone https://github.com/YOUR_USERNAME/autonomous-ai-agent-architecture.git
cd autonomous-ai-agent-architecture

```

2. **Copy files into your Unity Project**

3. **Configure Environment Variables**:
Copy `.env.example` to `.env` and insert your API credentials:
```bash
cp .env.example .env

```


```env
ELEVENLABS_API_KEY=your_elevenlabs_key_here
OPENAI_API_KEY=your_openai_key_here
GEMINI_API_KEY=your_gemini_key_here

```

---

## Technical Trade-offs & Next Steps

* **Current Architecture**: Focus placed on response reliability and sub-second TTS delivery over full async streaming concurrency.
* **Production Roadmap**:
* Implement WebSockets to replace long-polling HTTP requests between Unity and Python.
* Containerize backend services using Docker for local GPU deployment.
* Implement localized vector database (ChromaDB/FAISS) for long-term memory retrievability beyond context windows.
