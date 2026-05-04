We have an LLM player (ollama - https://ollama.com/) installed with reasonable but basic resources that serve the students in their projects.

This player operates in API mode, supporting a wide range of models. You can get an impression of potential models from the following link - https://huggingface.co/models?sort=trending&search=gpt

Here are some example use cases with a variety of LLM models of the API (you can see more examples in the following link - https://docs.ollama.com/api/introduction)

1. Basic Text Generation (/api/generate)
Used for "one-shot" tasks where you don't need the model to remember previous messages. Ideal for summarization or data extraction.
Bash
curl -k https://<OLLAMA_HOST>/api/generate -d '{
 "model": "gpt-oss:20b",
 "prompt": "Summarize the benefits of local LLMs in three bullet points.",
 "stream": false
}'
2. Conversational Chat (/api/chat)
Designed for interactive applications. You provide the messages array to maintain context.
Bash
curl -k https://<OLLAMA_HOST>/api/chat -d '{
 "model": "llama4",
 "messages": [
   { "role": "user", "content": "What is the capital of France?" },
   { "role": "assistant", "content": "The capital of France is Paris." },
   { "role": "user", "content": "What is its most famous tower?" }
 ],
 "stream": false
}'
3. Structured Data Extraction (JSON Mode)
Forces the model to output a valid JSON object. This is essential for building apps where the AI's output needs to be parsed by code.
Bash
curl -k https://<OLLAMA_HOST>/api/generate -d '{
 "model": "deepseek-v3",
 "prompt": "Extract the name and age from this text: John is a 30-year-old engineer.",
 "format": "json",
 "stream": false
}'
4. Vision & Multimodal Analysis
If you are running a vision-capable model (like llava or qwen3-omni), you can send an image for analysis. Note that the image must be a base64 encoded string.
Bash
curl -k https://<OLLAMA_HOST>/api/generate -d '{
 "model": "llava",
 "prompt": "What is in this image?",
 "images": ["<base64_string_here>"],
 "stream": false
}'
5. Model Inventory
Retrieve a list of all models currently installed and ready to be played.
Bash
curl -k https://<OLLAMA_HOST>/api/tags
6. Model Load Status
Retrieve a list of all models currently loaded into memory and are and ready for immediate service.
Bash
curl -k https://<OLLAMA_HOST>/api/ps

Let us know if a specific model, that is not installed, would be needed to be installed.

Regards,
CIS Helpdesk
