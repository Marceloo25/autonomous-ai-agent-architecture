using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace XRMultiplayer
{
    [DisallowMultipleComponent]
    public class AISessionManager : MonoBehaviour
    {
        public static AISessionManager Instance { get; private set; }

        [Tooltip("Session id used when communicating with the adapter")]
        public string sessionId;

        [Tooltip("Maximum number of messages to keep in memory")]
        public int maxMessages = 20;

        [Serializable]
        public class Message
        {
            public string role;
            public string content;
            public Message(string role, string content) { this.role = role; this.content = content; }
        }

        List<Message> m_History = new List<Message>();

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (string.IsNullOrEmpty(sessionId))
                sessionId = Guid.NewGuid().ToString();
        }

        public void AddUserMessage(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            m_History.Add(new Message("user", text));
            Trim();
        }

        public void AddAssistantMessage(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            m_History.Add(new Message("assistant", text));
            Trim();
        }

        void Trim()
        {
            while (m_History.Count > maxMessages)
                m_History.RemoveAt(0);
        }

        public string BuildCombinedHistory()
        {
            var sb = new StringBuilder();
            foreach (var m in m_History)
            {
                sb.AppendLine($"{m.role}: {m.content}");
            }
            return sb.ToString();
        }
    }
}