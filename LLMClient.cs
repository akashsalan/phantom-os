using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PhantomOS
{
    public class LLMClient
    {
        private static readonly HttpClient _http = new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
            MaxConnectionsPerServer = 4,
            EnableMultipleHttp2Connections = true
        })
        {
            Timeout = TimeSpan.FromSeconds(180)
        };

        private string GetSystemPrompt()
        {
            return ConfigManager.Current.SessionPreset switch
            {
                "interview" =>
                    "You are an elite real-time interview coach embedded in a stealth overlay. " +
                    "The user is currently IN a live interview and reading your responses in real-time while talking to their interviewer. " +
                    "Your responses must be optimized for quick scanning and immediate use.\n\n" +

                    "## RESPONSE FORMAT RULES:\n" +
                    "- **START with the direct answer** — no preamble, no greetings, no 'Great question'\n" +
                    "- Use **bold** for key terms the user should say out loud\n" +
                    "- Keep responses **under 200 words** unless a detailed technical answer is needed\n" +
                    "- Use bullet points and numbered lists — never write long paragraphs\n" +
                    "- Use `---` horizontal rules to separate distinct sections\n\n" +

                    "## QUESTION TYPE HANDLING:\n\n" +

                    "### Behavioral Questions (Tell me about a time...)\n" +
                    "Structure EVERY behavioral answer using STAR:\n" +
                    "- **Situation**: 1 sentence setting the scene\n" +
                    "- **Task**: What was your specific responsibility\n" +
                    "- **Action**: 2-3 bullet points of what YOU did (use 'I', not 'we')\n" +
                    "- **Result**: Quantified outcome with numbers/percentages if possible\n\n" +

                    "### Technical Questions (Explain concept X...)\n" +
                    "- **One-liner answer** first (what the interviewer wants to hear)\n" +
                    "- Then 2-3 bullets expanding with depth\n" +
                    "- Include a **real-world analogy** when helpful\n" +
                    "- Mention **trade-offs** to show senior-level thinking\n\n" +

                    "### Coding Questions (Write/debug code...)\n" +
                    "- State the **approach** in 1 sentence first\n" +
                    "- Mention **time and space complexity** upfront\n" +
                    "- Provide clean, commented code in a fenced code block\n" +
                    "- Add **edge cases** the user should mention to impress the interviewer\n\n" +

                    "### HR/Culture Questions (Why this company, strengths/weaknesses...)\n" +
                    "- Give a **sample answer** the user can adapt\n" +
                    "- Frame weaknesses as **growth areas with mitigation strategies**\n" +
                    "- Always tie answers back to the **role and company**\n\n" +

                    "### System Design Questions\n" +
                    "- Start with **requirements clarification** questions to suggest\n" +
                    "- Provide a **high-level architecture** with component names\n" +
                    "- Mention **scaling considerations** and **trade-offs**\n" +
                    "- Use numbered steps for the design walkthrough\n\n" +

                    "### Case/Estimation Questions\n" +
                    "- Break down into **clear assumptions** (numbered)\n" +
                    "- Show **step-by-step math**\n" +
                    "- Provide the final **ballpark answer** in bold\n\n" +

                    "## CRITICAL RULES:\n" +
                    "- NEVER say 'I see on your screen' or 'Based on what I can see'\n" +
                    "- NEVER use filler phrases — every word must add value\n" +
                    "- If the question is ambiguous, provide **2 interpretations** with answers for each\n" +
                    "- Add a 💡 **Pro tip** at the end when you have interviewer-impressing advice\n" +
                    "- Use markdown headers (### ) to organize multi-part answers\n" +
                    "- If you see an interview question on screen, answer it DIRECTLY as if the user asked it",

                "code" =>
                    "You are an elite programming assistant embedded in an invisible desktop overlay. " +
                    "Rules:\n" +
                    "- Analyze code, errors, stack traces, and documentation with precision.\n" +
                    "- Provide working code solutions with clear explanations.\n" +
                    "- If you see a bug, explain the root cause and give the fix.\n" +
                    "- Use fenced code blocks with language tags for all code.\n" +
                    "- Keep explanations practical — skip theory unless asked.\n" +
                    "- For complex problems, use step-by-step numbered lists.\n" +
                    "- Always specify the language/framework in your response.",

                "meeting" =>
                    "You are a professional meeting assistant embedded in an invisible overlay. " +
                    "Rules:\n" +
                    "- Summarize discussions into key points and action items.\n" +
                    "- Extract decisions, deadlines, and assigned owners.\n" +
                    "- Use bullet points and bold for important items.\n" +
                    "- Keep summaries concise — use headers to organize.\n" +
                    "- Highlight any risks or blockers mentioned.\n" +
                    "- Format action items as checkboxes: `- [ ] Action item`.",

                _ =>
                    "You are a highly capable AI assistant embedded in an invisible desktop overlay. " +
                    "Provide clear, concise, and actionable answers. " +
                    "Use markdown formatting — headers, bullets, code blocks, bold — for readability. " +
                    "Be direct. Skip preamble. Just answer."
            };
        }

        /// <summary>
        /// Non-streaming completion (fallback).
        /// </summary>
        public async Task<string> GetCompletion(string context, string prompt)
        {
            string apiKey = ConfigManager.GetApiKey();
            if (string.IsNullOrEmpty(apiKey))
                return "⚠ API key not configured.\nPress F12 to open Settings and enter your API key.";

            try
            {
                string userContent = string.IsNullOrWhiteSpace(context)
                    ? prompt
                    : $"[User's Screen/Audio Context]:\n{TrimContext(context)}\n\n[Instruction]:\n{prompt}";

                string provider = ConfigManager.Current.Provider;
                string requestUri;
                string jsonBody;
                var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost");

                switch (provider)
                {
                    case "anthropic":
                        requestUri = $"{ConfigManager.Current.BaseUrl.TrimEnd('/')}/messages";
                        request.Headers.Add("x-api-key", apiKey);
                        request.Headers.Add("anthropic-version", "2023-06-01");
                        var anthropicBody = new
                        {
                            model = ConfigManager.Current.Model,
                            system = GetSystemPrompt(),
                            messages = new[] { new { role = "user", content = userContent } },
                            max_tokens = 4096,
                            temperature = 0.3
                        };
                        jsonBody = JsonSerializer.Serialize(anthropicBody);
                        break;

                    case "google":
                        requestUri = $"{ConfigManager.Current.BaseUrl.TrimEnd('/')}/models/{ConfigManager.Current.Model}:generateContent?key={apiKey}";
                        var googleBody = new
                        {
                            systemInstruction = new { parts = new[] { new { text = GetSystemPrompt() } } },
                            contents = new[] { new { role = "user", parts = new[] { new { text = userContent } } } },
                            generationConfig = new { temperature = 0.3, maxOutputTokens = 4096 }
                        };
                        jsonBody = JsonSerializer.Serialize(googleBody);
                        break;

                    default:
                        requestUri = $"{ConfigManager.Current.BaseUrl.TrimEnd('/')}/chat/completions";
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                        var openaiBody = new
                        {
                            model = ConfigManager.Current.Model,
                            messages = new object[]
                            {
                                new { role = "system", content = GetSystemPrompt() },
                                new { role = "user", content = userContent }
                            },
                            max_tokens = 4096,
                            temperature = 0.3
                        };
                        jsonBody = JsonSerializer.Serialize(openaiBody);
                        break;
                }

                request.RequestUri = new Uri(requestUri);
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var response = await _http.SendAsync(request);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return $"⚠ API Error ({(int)response.StatusCode}):\n{ExtractErrorMessage(responseBody)}";

                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;
                string content = "[Empty response]";

                try
                {
                    switch (provider)
                    {
                        case "anthropic":
                            content = root.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
                            break;
                        case "google":
                            content = root.GetProperty("candidates")[0]
                                .GetProperty("content")
                                .GetProperty("parts")[0]
                                .GetProperty("text").GetString() ?? "";
                            break;
                        default:
                            content = root.GetProperty("choices")[0]
                                .GetProperty("message")
                                .GetProperty("content").GetString() ?? "";
                            break;
                    }
                }
                catch (Exception)
                {
                    content = $"[Failed to parse response format from {provider}.]";
                }

                return content.Trim();
            }
            catch (TaskCanceledException)
            {
                return "⚠ Request timed out. Check your network connection.";
            }
            catch (HttpRequestException ex)
            {
                return $"⚠ Network error: {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"⚠ Error: {ex.Message}";
            }
        }

        /// <summary>
        /// Streaming completion — calls onChunk for each SSE token received.
        /// Works with OpenAI, Anthropic, and Google providers.
        /// </summary>
        public async Task GetCompletionStreaming(string context, string prompt, Action<string> onChunk, Action<string> onComplete)
        {
            string apiKey = ConfigManager.GetApiKey();
            if (string.IsNullOrEmpty(apiKey))
            {
                onComplete("⚠ API key not configured.\nPress F12 to open Settings and enter your API key.");
                return;
            }

            try
            {
                string userContent = string.IsNullOrWhiteSpace(context)
                    ? prompt
                    : $"[User's Screen/Audio Context]:\n{TrimContext(context)}\n\n[Instruction]:\n{prompt}";

                string provider = ConfigManager.Current.Provider;
                string requestUri;
                string jsonBody;
                var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost");

                switch (provider)
                {
                    case "anthropic":
                        requestUri = $"{ConfigManager.Current.BaseUrl.TrimEnd('/')}/messages";
                        request.Headers.Add("x-api-key", apiKey);
                        request.Headers.Add("anthropic-version", "2023-06-01");
                        var anthropicBody = new
                        {
                            model = ConfigManager.Current.Model,
                            system = GetSystemPrompt(),
                            messages = new[] { new { role = "user", content = userContent } },
                            max_tokens = 4096,
                            temperature = 0.3,
                            stream = true
                        };
                        jsonBody = JsonSerializer.Serialize(anthropicBody);
                        break;

                    case "google":
                        requestUri = $"{ConfigManager.Current.BaseUrl.TrimEnd('/')}/models/{ConfigManager.Current.Model}:streamGenerateContent?alt=sse&key={apiKey}";
                        var googleBody = new
                        {
                            systemInstruction = new { parts = new[] { new { text = GetSystemPrompt() } } },
                            contents = new[] { new { role = "user", parts = new[] { new { text = userContent } } } },
                            generationConfig = new { temperature = 0.3, maxOutputTokens = 4096 }
                        };
                        jsonBody = JsonSerializer.Serialize(googleBody);
                        break;

                    default: // openai, custom, huggingface
                        requestUri = $"{ConfigManager.Current.BaseUrl.TrimEnd('/')}/chat/completions";
                        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                        var openaiBody = new
                        {
                            model = ConfigManager.Current.Model,
                            messages = new object[]
                            {
                                new { role = "system", content = GetSystemPrompt() },
                                new { role = "user", content = userContent }
                            },
                            max_tokens = 4096,
                            temperature = 0.3,
                            stream = true
                        };
                        jsonBody = JsonSerializer.Serialize(openaiBody);
                        break;
                }

                request.RequestUri = new Uri(requestUri);
                request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                if (!response.IsSuccessStatusCode)
                {
                    string errorBody = await response.Content.ReadAsStringAsync();
                    onComplete($"⚠ API Error ({(int)response.StatusCode}):\n{ExtractErrorMessage(errorBody)}");
                    return;
                }

                var fullResponse = new StringBuilder();
                using var stream = await response.Content.ReadAsStreamAsync();
                using var reader = new StreamReader(stream);

                while (!reader.EndOfStream)
                {
                    string? line = await reader.ReadLineAsync();
                    if (string.IsNullOrEmpty(line)) continue;

                    if (!line.StartsWith("data: ")) continue;
                    string data = line.Substring(6).Trim();

                    if (data == "[DONE]") break;

                    try
                    {
                        string? token = ExtractStreamToken(data, provider);
                        if (!string.IsNullOrEmpty(token))
                        {
                            fullResponse.Append(token);
                            onChunk(fullResponse.ToString());
                        }
                    }
                    catch { /* skip malformed chunks */ }
                }

                string finalText = fullResponse.ToString().Trim();
                onComplete(string.IsNullOrEmpty(finalText) ? "[Empty response]" : finalText);
            }
            catch (TaskCanceledException)
            {
                onComplete("⚠ Request timed out. Check your network connection.");
            }
            catch (HttpRequestException ex)
            {
                onComplete($"⚠ Network error: {ex.Message}");
            }
            catch (Exception ex)
            {
                onComplete($"⚠ Error: {ex.Message}");
            }
        }

        private string? ExtractStreamToken(string jsonData, string provider)
        {
            using var doc = JsonDocument.Parse(jsonData);
            var root = doc.RootElement;

            switch (provider)
            {
                case "anthropic":
                    // Anthropic SSE: {"type":"content_block_delta","delta":{"type":"text_delta","text":"..."}}
                    if (root.TryGetProperty("type", out var typeEl))
                    {
                        string? eventType = typeEl.GetString();
                        if (eventType == "content_block_delta" && root.TryGetProperty("delta", out var delta))
                        {
                            return delta.GetProperty("text").GetString();
                        }
                    }
                    return null;

                case "google":
                    // Google SSE: {"candidates":[{"content":{"parts":[{"text":"..."}]}}]}
                    if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
                    {
                        var parts = candidates[0].GetProperty("content").GetProperty("parts");
                        if (parts.GetArrayLength() > 0)
                            return parts[0].GetProperty("text").GetString();
                    }
                    return null;

                default: // openai
                    // OpenAI SSE: {"choices":[{"delta":{"content":"..."}}]}
                    if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                    {
                        var choice = choices[0];
                        if (choice.TryGetProperty("delta", out var oDelta) &&
                            oDelta.TryGetProperty("content", out var content))
                        {
                            return content.GetString();
                        }
                    }
                    return null;
            }
        }

        public async Task<string> TranscribeAudio(string audioPath)
        {
            string apiKey = ConfigManager.GetApiKey();
            if (string.IsNullOrEmpty(apiKey))
                return "[API key not configured]";

            if (string.IsNullOrEmpty(audioPath) || !File.Exists(audioPath))
                return "[No audio file found]";

            try
            {
                string provider = ConfigManager.Current.Provider;
                if (provider == "google" || provider == "anthropic")
                {
                    return "[Audio transcription requires an OpenAI-compatible Whisper endpoint. Configure an OpenAI-compatible provider for audio.]";
                }

                using var form = new MultipartFormDataContent();
                form.Add(new StringContent("whisper-1"), "model");
                form.Add(new StringContent("en"), "language");

                var fileBytes = await File.ReadAllBytesAsync(audioPath);
                var fileContent = new ByteArrayContent(fileBytes);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
                form.Add(fileContent, "file", "audio.wav");

                var request = new HttpRequestMessage(HttpMethod.Post,
                    $"{ConfigManager.Current.BaseUrl.TrimEnd('/')}/audio/transcriptions");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = form;

                var response = await _http.SendAsync(request);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return $"[Transcription failed: {ExtractErrorMessage(responseBody)}]";

                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    return doc.RootElement.GetProperty("text").GetString() ?? responseBody;
                }
                catch (JsonException)
                {
                    return responseBody.Trim();
                }
            }
            catch (Exception ex)
            {
                return $"[Transcription error: {ex.Message}]";
            }
            finally
            {
                try { if (File.Exists(audioPath)) File.Delete(audioPath); } catch { }
            }
        }

        /// <summary>
        /// Transcribe audio from raw WAV bytes (used for chunked streaming transcription).
        /// </summary>
        public async Task<string> TranscribeAudioChunk(byte[] wavData)
        {
            string apiKey = ConfigManager.GetApiKey();
            if (string.IsNullOrEmpty(apiKey))
                return "";

            string provider = ConfigManager.Current.Provider;
            if (provider == "google" || provider == "anthropic")
                return "";

            try
            {
                using var form = new MultipartFormDataContent();
                form.Add(new StringContent("whisper-1"), "model");
                form.Add(new StringContent("en"), "language");

                var fileContent = new ByteArrayContent(wavData);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
                form.Add(fileContent, "file", "chunk.wav");

                var request = new HttpRequestMessage(HttpMethod.Post,
                    $"{ConfigManager.Current.BaseUrl.TrimEnd('/')}/audio/transcriptions");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = form;

                var response = await _http.SendAsync(request);
                string responseBody = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return "";

                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    return doc.RootElement.GetProperty("text").GetString() ?? "";
                }
                catch
                {
                    return responseBody.Trim();
                }
            }
            catch
            {
                return "";
            }
        }

        private string TrimContext(string context)
        {
            const int maxChars = 12000;
            if (context.Length <= maxChars) return context;
            return "...[earlier context trimmed]...\n" +
                   context.Substring(context.Length - maxChars);
        }

        private string ExtractErrorMessage(string responseBody)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                if (doc.RootElement.TryGetProperty("error", out var error))
                {
                    if (error.TryGetProperty("message", out var msg))
                        return msg.GetString() ?? responseBody;
                    // Google format
                    if (error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var gmsg))
                        return gmsg.GetString() ?? responseBody;
                }
            }
            catch { }
            return responseBody.Length > 200 ? responseBody[..200] + "..." : responseBody;
        }
    }
}
