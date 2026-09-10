using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace XRMultiplayer
{
    [DisallowMultipleComponent]
    public class ElevenLabsTTS : MonoBehaviour
    {
        private static ElevenLabsTTS _instance;
        public static ElevenLabsTTS Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                _instance = FindFirstObjectByType<ElevenLabsTTS>();
                if (_instance != null)
                    return _instance;

                _instance = FindObjectOfType<ElevenLabsTTS>();
                if (_instance != null)
                    return _instance;

                var go = new GameObject("ElevenLabsTTS");
                _instance = go.AddComponent<ElevenLabsTTS>();
                DontDestroyOnLoad(go);
                return _instance;
            }
        }

        [Tooltip("ElevenLabs API key for text-to-speech requests.")]
        public string apiKey = "your API key here";

        [Tooltip("Model ID used for ElevenLabs text-to-speech.")]
        public string modelId = "eleven_flash_v2_5";

        public async Task<AudioClip> GenerateSpeechAsync(string text, string voiceId)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(voiceId))
                return null;

            if (string.IsNullOrWhiteSpace(apiKey) || apiKey == "YOUR_ELEVENLABS_API_KEY")
            {
                Debug.LogWarning("ElevenLabsTTS: API key is missing or set to default placeholder in Inspector.");
                return null;
            }

            // mp3_44100_128 is supported on all ElevenLabs tiers (Free & Paid)
            var requestUri = $"https://api.elevenlabs.io/v1/text-to-speech/{voiceId}?output_format=mp3_44100_128";
            var payload = new ElevenLabsRequest
            {
                text = text,
                model_id = modelId
            };

            var json = JsonUtility.ToJson(payload);
            using var request = new UnityWebRequest(requestUri, "POST");
            var bodyRaw = Encoding.UTF8.GetBytes(json);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);

            var audioHandler = new DownloadHandlerAudioClip(requestUri, AudioType.MPEG);
            request.downloadHandler = audioHandler;

            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("xi-api-key", apiKey.Trim());

            var asyncOp = request.SendWebRequest();
            while (!asyncOp.isDone)
                await Task.Yield();

            if (request.result != UnityWebRequest.Result.Success)
            {
                // Safely log status codes without reading downloadHandler.text
                switch (request.responseCode)
                {
                    case 401:
                        Debug.LogWarning("ElevenLabsTTS: HTTP 401 Unauthorized. Check your API key in the Unity Inspector.");
                        break;
                    case 402:
                        Debug.LogWarning("ElevenLabsTTS: HTTP 402 Payment Required. Out of credits.");
                        break;
                    case 403:
                        Debug.LogWarning("ElevenLabsTTS: HTTP 403 Forbidden. Key may be revoked or missing 'Text to Speech' scope.");
                        break;
                    case 422:
                        Debug.LogWarning($"ElevenLabsTTS: HTTP 422 Unprocessable Entity. Verify voiceId '{voiceId}' is correct.");
                        break;
                    default:
                        Debug.LogWarning($"ElevenLabsTTS: Request failed with HTTP {request.responseCode}: {request.error}");
                        break;
                }
                return null;
            }

            var clip = audioHandler.audioClip;
            if (clip == null)
            {
                Debug.LogWarning("ElevenLabsTTS: Downloaded audio clip is null.");
                return null;
            }

            return clip;
        }

        [Serializable]
        private class ElevenLabsRequest
        {
            public string text;
            public string model_id;
        }
    }
}