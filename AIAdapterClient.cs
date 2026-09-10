using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace XRMultiplayer
{
    [DisallowMultipleComponent]
    public class AIAdapterClient : MonoBehaviour
    {
        public static AIAdapterClient Instance { get; private set; }

        public enum AIServiceProvider{LocalCustom,GoogleCloud,OpenAI,Anthropic}
        public enum RequestFormat {SimpleChat,OpenAI,GoogleCloud}

        //General Configuration
        [Tooltip("Select the AI provider")] 
        public AIServiceProvider activeService = AIServiceProvider.GoogleCloud;
        [Tooltip("Request timeout in seconds(increase for local models)")]
        public int timeoutSeconds = 60;

        //Local Settings
        [Tooltip("Select the request format")]
        public RequestFormat requestFormat = RequestFormat.OpenAI;
        public string defaultModel = "your-model";
        public string baseUrl = "http://localhost:8080";
        public bool manualPathOverride = false;
        public string customPath = "";
        public string ActivePath { get { if (manualPathOverride && !string.IsNullOrEmpty(customPath)) return customPath; return requestFormat switch { RequestFormat.OpenAI => "/v1/chat/completions", RequestFormat.GoogleCloud => "/v1/models/gemini-pro:generateContent", _ => "/api/v1/chat" }; } }

        //Google Gemini Settings
        [HideInInspector]
        public string geminiApiKey = "your API key";
        [Tooltip("Exact model ID (e.g., gemini-3.1-flash-lite-preview)")]
        public string geminiModel = "gemini-3.1-flash-lite-preview";
        [Tooltip("Use v1beta for newer models like 3.1 Flash Lite")]
        public bool useBeta = true;

        //Custom Remote OpenAI Settings
        [Tooltip("OpenAI Remote API key for the remote service")]
        public string openAiApiKey = "";
        [Tooltip("OpenAI Remote Model for the remote service")]
        public string openAiModel = "";
        [Tooltip("OpenAI Remote API URL for the remote service")]
        public string openAiApiUrl = "";

        #region Serialization Classes
        // Gemini
        [Serializable] public class GeminiPart { public string text; }
        [Serializable] public class GeminiContent { public GeminiPart[] parts; }
        [Serializable] public class GeminiRequest { public GeminiContent[] contents; }
        [Serializable] public class GeminiCandidate { public GeminiContent content; }
        [Serializable] public class GeminiResponse { public GeminiCandidate[] candidates; }

        // OpenAI
        [Serializable] class OpenAIMessage { public string role; public string content; }
        [Serializable] class OpenAIChoice { public OpenAIMessage message; public string text; }
        [Serializable] class OpenAIResponse { public OpenAIChoice[] choices; }
        [Serializable] class OpenAIRequest { public string model; public OpenAIMessage[] messages; }

        // Simple Local
        [Serializable] class ChatRequest { public string session; public string input; }
        [Serializable] class ChatResponse { public string reply; }
        #endregion

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void PostChat(string sessionId, string input, Action<string> onSuccess = null, Action<string> onError = null)
        {
            StartCoroutine(PostChatCoroutine(sessionId ?? string.Empty, input ?? string.Empty, onSuccess, onError));
        }

        IEnumerator PostChatCoroutine(string sessionId, string input, Action<string> onSuccess, Action<string> onError)
        {
            string url = baseUrl.TrimEnd('/') + ActivePath;
            string apiKey = "";
            string json = "";

            // Consolidated Switch Logic: Sets both URL and Payload based on the Selected Provider
            switch (activeService)
            {
                case AIServiceProvider.OpenAI:
                    url = openAiApiUrl;
                    apiKey = openAiApiKey;
                    var openAiReq = new OpenAIRequest{ model = openAiModel, messages = new OpenAIMessage[] { new OpenAIMessage { role = "user", content = input } } };
                    json = JsonUtility.ToJson(openAiReq);
                break;
                case AIServiceProvider.GoogleCloud:
                    string apiVersion = useBeta ? "v1beta" : "v1";
                    url = $"https://generativelanguage.googleapis.com/{apiVersion}/models/{geminiModel}:generateContent?key={geminiApiKey}";
                    
                    var gemReq = new GeminiRequest {
                        contents = new GeminiContent[] {
                            new GeminiContent { parts = new GeminiPart[] { new GeminiPart { text = input } } }
                        }
                    };
                    json = JsonUtility.ToJson(gemReq);
                break;
                case AIServiceProvider.LocalCustom:
                    switch (requestFormat)
                    { 
                        case RequestFormat.OpenAI:
                            var openReq = new OpenAIRequest { model = defaultModel, messages = new OpenAIMessage[] { new OpenAIMessage { role = "user", content = input } } };
                            json = JsonUtility.ToJson(openReq);
                        break;

                        case RequestFormat.GoogleCloud:
                            var gemReqLocal = new GeminiRequest {contents = new GeminiContent[] { new GeminiContent { parts = new GeminiPart[] { new GeminiPart { text = input } } } } };
                            json = JsonUtility.ToJson(gemReqLocal);
                        break;

                        default:
                            var simpleReq = new ChatRequest { session = sessionId, input = input };
                            json = JsonUtility.ToJson(simpleReq);
                        break;
                    }
                break;
            }

            using (var uwr = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
                uwr.uploadHandler = new UploadHandlerRaw(bodyRaw);
                uwr.downloadHandler = new DownloadHandlerBuffer();
                uwr.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(apiKey)) { uwr.SetRequestHeader("Authorization", $"Bearer {apiKey}"); }
                uwr.timeout = timeoutSeconds;

                yield return uwr.SendWebRequest();

                if (uwr.result != UnityWebRequest.Result.Success)
                {
                    onError?.Invoke($"{uwr.error}: {uwr.downloadHandler?.text}");
                }
                else
                {
                    ParseResponse(uwr.downloadHandler.text, onSuccess, onError);
                }
            }
        }

        private void ParseResponse(string rawText, Action<string> onSuccess, Action<string> onError)
        {
            try
            {
                switch (activeService)
                {
                    case AIServiceProvider.OpenAI:
                        var openai = JsonUtility.FromJson<OpenAIResponse>(rawText);
                        if (openai?.choices != null && openai.choices.Length > 0)
                            onSuccess?.Invoke(openai.choices[0].message?.content ?? openai.choices[0].text);
                        else
                            onError?.Invoke($"OpenAI parse failure: {rawText}");
                    break;
                    case AIServiceProvider.GoogleCloud:
                        var gem = JsonUtility.FromJson<GeminiResponse>(rawText);
                        if (gem?.candidates != null && gem.candidates.Length > 0)
                            onSuccess?.Invoke(gem.candidates[0].content.parts[0].text);
                        else
                            onError?.Invoke($"Gemini parse failure: {rawText}");
                        break;

                    case AIServiceProvider.LocalCustom:
                        switch (requestFormat)
                        {
                            case RequestFormat.OpenAI:
                                var oai = JsonUtility.FromJson<OpenAIResponse>(rawText);
                                if (oai?.choices != null && oai.choices.Length > 0)
                                    onSuccess?.Invoke(oai.choices[0].message?.content ?? oai.choices[0].text);
                                else
                                    onError?.Invoke($"OpenAI parse failure: {rawText}");
                            break;

                            case RequestFormat.GoogleCloud:
                                var gemLocal = JsonUtility.FromJson<GeminiResponse>(rawText);
                                if (gemLocal?.candidates != null && gemLocal.candidates.Length > 0)
                                    onSuccess?.Invoke(gemLocal.candidates[0].content.parts[0].text);
                                else
                                    onError?.Invoke($"Gemini parse failure: {rawText}");
                            break;

                            default:
                                var simpleResp = JsonUtility.FromJson<ChatResponse>(rawText);
                                if (!string.IsNullOrEmpty(simpleResp?.reply))
                                    onSuccess?.Invoke(simpleResp.reply);
                                else
                                    onError?.Invoke($"Simple parse failure: {rawText}");
                            break;
                        }
                    break;
                }
            }
            catch (Exception e) { onError?.Invoke($"JSON Parse Error: {e.Message}"); }
        }
    }
}