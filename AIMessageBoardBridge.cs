using System;
using UnityEngine;
using System.Text.RegularExpressions;

namespace XRMultiplayer
{
    [DisallowMultipleComponent]
    public class AIMessageBoardBridge : MonoBehaviour
    {
        [Tooltip("If true, sends local player messages to the AI adapter")]
        public bool autoRespondToLocalMessages = true;

        [Tooltip("Assistant name used when posting AI replies")]
        public string assistantName = "GM";

        [Tooltip("Include recent conversation history when sending to the adapter")]
        public bool includeHistory = false;

        [Tooltip("Optional AICharacter component that provides the character profile/prompt to include when sending messages to the adapter.")]
        public AICharacter characterProfile;

        [Tooltip("Reference to the NetworkMessageBoard this bridge should post replies to. Assign in the inspector.")]
        public NetworkMessageBoard messageBoard;

        [Tooltip("Optional tool registry that can execute game tools such as SellItem or GenerateQuest.")]
        public AIToolRegistry toolRegistry;

        void OnEnable()
        {
            NetworkMessageBoard.MessageReceived += OnMessageReceived;
            if (messageBoard == null)
                messageBoard = GetComponent<NetworkMessageBoard>();
            if (characterProfile == null)
                characterProfile = GetComponent<AICharacter>();
            if (toolRegistry == null)
                toolRegistry = GetComponent<AIToolRegistry>();
        }

        void OnDisable()
        {
            NetworkMessageBoard.MessageReceived -= OnMessageReceived;
        }

        void OnMessageReceived(NetworkMessageBoard sourceBoard, string html)
        {
            // If this bridge is tied to a specific board, ignore messages from other boards.
            if (messageBoard != null && sourceBoard != messageBoard)
                return;
            if (!autoRespondToLocalMessages) return;
            if (string.IsNullOrEmpty(html)) return;

            var author = ParseAuthor(html);
            if (string.IsNullOrEmpty(author)) return;

            // don't respond to assistant messages to avoid loops
            if (author.Equals(assistantName, StringComparison.OrdinalIgnoreCase)) return;

            // require local player
            if (XRINetworkPlayer.LocalPlayer == null) return;
            var localName = XRINetworkPlayer.LocalPlayer.playerName;
            if (!author.Equals(localName, StringComparison.Ordinal)) return;

            var content = ParseContent(html);
            if (string.IsNullOrEmpty(content)) return;

            // ignore commands
            if (content.StartsWith("/")) return;

            AISessionManager.Instance?.AddUserMessage(content);

            var sessionId = AISessionManager.Instance != null ? AISessionManager.Instance.sessionId : Guid.NewGuid().ToString();
            var input = includeHistory && AISessionManager.Instance != null ? AISessionManager.Instance.BuildCombinedHistory() + "\n" + content : content;
            var charPrompt = characterProfile != null ? characterProfile.GetPrompt() : string.Empty;
            if (!string.IsNullOrEmpty(charPrompt))
            {
                input = charPrompt + "\n\n" + input;
            }

            if (toolRegistry != null)
            {
                var toolInstruction = toolRegistry.BuildToolInstructionText();
                if (!string.IsNullOrEmpty(toolInstruction))
                    input = input + "\n\n" + toolInstruction;
            }

            AIAdapterClient.Instance?.PostChat(sessionId, input, (reply) =>
            {
                var visibleReply = reply;
                if (toolRegistry != null && toolRegistry.TryHandleToolCalls(reply, out var handledReply))
                    visibleReply = handledReply;

                characterProfile?.TriggerTalkingAnimation();
                _ = characterProfile?.SpeakAsync(visibleReply);
                AISessionManager.Instance?.AddAssistantMessage(visibleReply);
                if (messageBoard != null)
                {
                    messageBoard.SubmitRawMessage($"<b>{assistantName}</b>:<br><br>{visibleReply}");
                }
                else
                {
                    Debug.LogWarning("AIMessageBoardBridge: no NetworkMessageBoard assigned; cannot post AI reply to board.");
                    PlayerHudNotification.Instance?.ShowText($"AI: {visibleReply}");
                }
            }, (err) =>
            {
                Debug.LogWarning($"AIAdapterClient error: {err}");
                PlayerHudNotification.Instance?.ShowText($"AI error: {err}");
            });
        }

        string ParseAuthor(string html)
        {
            try
            {
                var match = Regex.Match(html, "<b>(.*?)</b>");
                if (match.Success)
                    return match.Groups[1].Value;
            }
            catch { }
            return string.Empty;
        }

        string ParseContent(string html)
        {
            var marker = "<br><br>";
            var idx = html.IndexOf(marker);
            if (idx >= 0) return html.Substring(idx + marker.Length);
            // strip tags
            try
            {
                return Regex.Replace(html, "<.*?>", string.Empty);
            }
            catch { return html; }
        }
    }
}