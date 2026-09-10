using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace XRMultiplayer
{
    /// <summary>
    /// Component that loads a character profile from a markdown file and exposes the prompt text.
    /// Place .md files under Assets/AssetCustom/Scripts/AI/Data/Characters and set <see cref="characterFileName"/> to the file name.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public class AICharacter : MonoBehaviour
    {
        [Tooltip("File name of the character profile located under Assets/AssetCustom/Scripts/AI/Data/Characters (e.g. guard_garret.md)")]
        public string characterFileName;

        [TextArea(4, 12), Tooltip("Cached prompt content loaded from the character file.")]
        public string characterPrompt;

        [Tooltip("Animator on this NPC; used to trigger a talking animation when the AI replies.")]
        public Animator animator;

        [Tooltip("Trigger parameter name on the Animator to fire for talking.")]
        public string talkingTriggerName = "Talk";

        [Tooltip("If true, the talking trigger will fire whenever the AI sends a reply.")]
        public bool playTalkingAnimationOnReply = true;

        public enum TTSProvider
        {
            Piper,
            ElevenLabs
        }

        [Tooltip("AudioSource on this NPC used for TTS playback.")]
        public AudioSource audioSource;

        [Tooltip("Select which text-to-speech provider to use for this NPC.")]
        public TTSProvider ttsProvider = TTSProvider.Piper;

        [Tooltip("Voice model filename stored under StreamingAssets/Piper/Voices/")]
        public string voiceFileName = "steve.onnx";

        [Tooltip("ElevenLabs voice ID used for remote TTS.")]
        public string elevenLabsVoiceId = "";

        [Range(0.5f, 2.0f), Tooltip("Playback pitch for the NPC voice.")]
        public float voicePitch = 1.0f;

        void Awake()
        {
            if (animator == null)
                animator = GetComponent<Animator>();
            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();
            if (audioSource != null)
            {
                audioSource.spatialBlend = 1.0f;
                audioSource.playOnAwake = false;
            }

            LoadCharacterFile();
        }

        /// <summary>
        /// Loads the character markdown file into <see cref="characterPrompt"/>.
        /// </summary>
        public void LoadCharacterFile()
        {
            if (string.IsNullOrEmpty(characterFileName))
                return;

            try
            {
                var resourceName = Path.GetFileNameWithoutExtension(characterFileName);
                var resourcePath = Path.Combine("AI", "Characters", resourceName).Replace("\\", "/");
                var textAsset = Resources.Load<TextAsset>(resourcePath);
                if (textAsset == null)
                {
                    Debug.LogWarning($"AICharacter: character resource not found: Resources/{resourcePath}.md");
                    return;
                }

                characterPrompt = textAsset.text;

                var persistentPath = GetPersistentCharacterPath();
                if (File.Exists(persistentPath))
                {
                    characterPrompt = File.ReadAllText(persistentPath);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"AICharacter: failed to load character file '{characterFileName}': {e.Message}");
            }
        }

        string GetPersistentCharacterPath()
        {
            var folder = Path.Combine(Application.persistentDataPath, "AI", "Characters");
            return Path.Combine(folder, characterFileName);
        }

        /// <summary>
        /// Triggers the configured talking animation on the NPC animator.
        /// </summary>
        public void TriggerTalkingAnimation()
        {
            if (!playTalkingAnimationOnReply)
                return;

            if (animator == null)
                animator = GetComponent<Animator>();

            if (animator != null && !string.IsNullOrWhiteSpace(talkingTriggerName))
            {
                animator.ResetTrigger(talkingTriggerName);
                animator.SetTrigger(talkingTriggerName);
            }
        }

        /// <summary>
        /// Synthesizes and plays the provided text with the local Piper backend.
        /// </summary>
        public async Task SpeakAsync(string dialogueText)
        {
            if (string.IsNullOrWhiteSpace(dialogueText))
                return;

            if (audioSource == null)
                audioSource = GetComponent<AudioSource>();

            AudioClip clip = null;
            try
            {
                switch (ttsProvider)
                {
                    case TTSProvider.Piper:
                        clip = await LocalPiperTTS.Instance.GenerateSpeechAsync(dialogueText, voiceFileName);
                        break;
                    case TTSProvider.ElevenLabs:
                        clip = await ElevenLabsTTS.Instance.GenerateSpeechAsync(dialogueText, elevenLabsVoiceId);
                        break;
                }

                if (clip == null)
                    return;

                // Trigger talking animation when starting to speak
                try { TriggerTalkingAnimation(); } catch { }

                if (audioSource != null)
                {
                    audioSource.pitch = voicePitch;
                    audioSource.PlayOneShot(clip);
                    var duration = (clip.length / Mathf.Max(0.1f, voicePitch)) + 0.2f;
                    Destroy(clip, duration);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"AICharacter: failed to speak dialogue: {e.Message}");
            }
        }

        /// <summary>
        /// Returns the loaded character prompt (may be empty).
        /// </summary>
        public string GetPrompt()
        {
            return characterPrompt ?? string.Empty;
        }
    }
}
