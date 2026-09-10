using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace XRMultiplayer
{
    [DisallowMultipleComponent]
    public class LocalPiperTTS : MonoBehaviour
    {
        private static LocalPiperTTS _instance;
        public static LocalPiperTTS Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                _instance = FindObjectOfType<LocalPiperTTS>();
                if (_instance != null)
                    return _instance;

                var go = new GameObject("LocalPiperTTS");
                _instance = go.AddComponent<LocalPiperTTS>();
                DontDestroyOnLoad(go);
                return _instance;
            }
        }

        public async Task<AudioClip> GenerateSpeechAsync(string text, string voiceModelFileName)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            var piperExePath = Path.Combine(Application.streamingAssetsPath, "Piper", "piper.exe");
            var voiceModelPath = Path.Combine(Application.streamingAssetsPath, "Piper", "Voices", voiceModelFileName);

            if (!File.Exists(piperExePath))
            {
                UnityEngine.Debug.LogWarning($"LocalPiperTTS: Piper executable not found at {piperExePath}");
                return null;
            }

            if (!File.Exists(voiceModelPath))
            {
                UnityEngine.Debug.LogWarning($"LocalPiperTTS: Piper voice model not found at {voiceModelPath}");
                return null;
            }

            // Run the external Piper process on a background thread and capture raw PCM bytes.
            byte[] pcmBytes = null;
            try
            {
                pcmBytes = await Task.Run(() =>
                {
                    try
                    {
                        using var process = new Process();
                        process.StartInfo.FileName = piperExePath;
                        process.StartInfo.WorkingDirectory = Path.GetDirectoryName(piperExePath) ?? Application.streamingAssetsPath;
                        // request raw PCM output to avoid WAV header artifacts
                        process.StartInfo.Arguments = $"--model {Quote(voiceModelPath)} --output_raw";
                        process.StartInfo.RedirectStandardInput = true;
                        process.StartInfo.RedirectStandardOutput = true;
                        process.StartInfo.RedirectStandardError = true;
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.CreateNoWindow = true;

                        process.Start();
                        process.StandardInput.Write(text);
                        process.StandardInput.Close();

                        using var ms = new MemoryStream();
                        var buffer = new byte[4096];
                        int read;
                        while ((read = process.StandardOutput.BaseStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            ms.Write(buffer, 0, read);
                        }

                        process.WaitForExit();
                        if (process.ExitCode != 0)
                        {
                            var error = process.StandardError.ReadToEnd();
                            UnityEngine.Debug.LogWarning($"LocalPiperTTS: Piper exited with code {process.ExitCode}: {error}");
                            return null;
                        }

                        var bytes = ms.ToArray();
                        return bytes.Length > 0 ? bytes : null;
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogWarning($"LocalPiperTTS: failed to run Piper process: {e.Message}");
                        return null;
                    }
                });
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"LocalPiperTTS: background task failed: {e.Message}");
                return null;
            }

            if (pcmBytes == null || pcmBytes.Length == 0)
                return null;

            // Create the AudioClip on the Unity main thread.
            try
            {
                var clip = CreateAudioClipFromPcm(pcmBytes);
                return clip;
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogWarning($"LocalPiperTTS: failed to create AudioClip from PCM: {e.Message}");
                return null;
            }
        }

        private static AudioClip CreateAudioClipFromPcm(byte[] pcmBytes)
        {
            if (pcmBytes.Length < 2)
                return null;

            var sampleCount = pcmBytes.Length / 2;
            var data = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                short sample = (short)((pcmBytes[i * 2 + 1] << 8) | pcmBytes[i * 2]);
                data[i] = sample / 32768f;
            }

            var clip = AudioClip.Create("PiperSpeech", sampleCount, 1, 22050, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static string Quote(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? string.Empty : '"' + path + '"';
        }
    }
}
