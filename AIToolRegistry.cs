using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace XRMultiplayer
{
    /// <summary>
    /// Simple tool registry for NPC AI agents. Allows the model to emit structured tool calls that the game runtime executes.
    /// </summary>
    [DisallowMultipleComponent]
    public class AIToolRegistry : MonoBehaviour
    {
        [Tooltip("Whether tool calling is enabled for this NPC bridge.")]
        public bool enableToolCalling = true;

        [Tooltip("Tool definitions exposed to the model. Defaults include SellItem and GenerateQuest.")]
        public List<AIToolDefinition> tools = new List<AIToolDefinition>();

        void Awake()
        {
            if (tools == null || tools.Count == 0)
                BuildDefaultTools();
        }

        public void BuildDefaultTools()
        {
            tools = new List<AIToolDefinition>
            {
                new AIToolDefinition
                {
                    name = "SellItem",
                    description = "Sell an item to the player and return a short merchant-style response.",
                    schemaJson = "{\"recipient_id\":\"string\",\"item_id\":\"number\",\"gold_value\":\"number\",\"message\":\"string\"}"
                },
                new AIToolDefinition
                {
                    name = "GenerateQuest",
                    description = "Create a simple quest for the player and return a short quest summary.",
                    schemaJson = "{\"recipient_id\":\"string\",\"title\":\"string\",\"description\":\"string\",\"reward\":\"number\"}"
                },
                new AIToolDefinition
                {
                    name = "RecordPlayerOpinion",
                    description = "Write a short sentence describing how the NPC feels about the player's latest interaction and save it into the character's Player History section.",
                    schemaJson = "{\"recipient_id\":\"string\",\"opinion\":\"string\"}"
                }
            };
        }

        public string BuildToolInstructionText()
        {
            if (!enableToolCalling || tools == null || tools.Count == 0)
                return string.Empty;

            var lines = new List<string>
            {
                "You may call tools by returning JSON in this exact form:",
                "{\"tool_calls\":[{\"name\":\"ToolName\",\"arguments\":{\"recipient_id\":\"NPC_name\",\"item_id\":1234,\"gold_value\":5,\"message\":\"Message to show the player\"}}]}",
                "Use RecordPlayerOpinion after a meaningful interaction to capture how the NPC feels about the player.",
                "Available tools:"
            };

            foreach (var tool in tools)
            {
                lines.Add($"- {tool.name}: {tool.description}");
            }

            lines.Add("If you use a tool, do not show the raw JSON to the player. Instead, narrate the result in character.");
            return string.Join("\n", lines);
        }

        public bool TryHandleToolCalls(string modelReply, out string visibleReply)
        {
            visibleReply = modelReply;
            if (!enableToolCalling || string.IsNullOrWhiteSpace(modelReply))
                return false;

            if (!TryExtractToolCalls(modelReply, out var calls) || calls.Count == 0)
                return false;

            foreach (var call in calls)
            {
                Debug.Log($"AIToolRegistry: Tool call detected -> Name: {call.name}, Arguments: {call.argumentsJson}");
            }

            var results = new List<string>();
            foreach (var call in calls)
            {
                var result = ExecuteTool(call);
                if (!string.IsNullOrEmpty(result.message))
                    results.Add(result.message);
            }

            visibleReply = results.Count > 0 ? string.Join(" ", results) : modelReply;
            return true;
        }

        public AIToolExecutionResult ExecuteTool(AIToolCall toolCall)
        {
            if (toolCall == null || string.IsNullOrWhiteSpace(toolCall.name))
                return new AIToolExecutionResult(false, "No tool was specified.");

            switch (toolCall.name)
            {
                case "SellItem":
                    return new AIToolExecutionResult(true, ExecuteSellItem(toolCall.argumentsJson));
                case "GenerateQuest":
                    return new AIToolExecutionResult(true, ExecuteGenerateQuest(toolCall.argumentsJson));
                case "RecordPlayerOpinion":
                    return new AIToolExecutionResult(true, ExecuteRecordPlayerOpinion(toolCall.argumentsJson));
                default:
                    return new AIToolExecutionResult(false, $"Unsupported tool: {toolCall.name}");
            }
        }

        bool TryExtractToolCalls(string text, out List<AIToolCall> calls)
        {
            calls = new List<AIToolCall>();
            var cleaned = StripCodeFences(text);
            var jsonObjects = ExtractJsonObjects(cleaned);
            foreach (var jsonObject in jsonObjects)
            {
                if (!jsonObject.Contains("tool_calls") && !jsonObject.Contains("\"name\""))
                    continue;

                var nameMatch = Regex.Match(jsonObject, "\"name\"\\s*:\\s*\"([^\"]+)\"");
                if (!nameMatch.Success)
                    continue;

                var argumentsString = string.Empty;
                var argsMatch = Regex.Match(jsonObject, "\"arguments\"\\s*:\\s*(\\{.*\\})", RegexOptions.Singleline);
                if (argsMatch.Success)
                {
                    var argsObject = ExtractBalancedObject(argsMatch.Groups[1].Value);
                    if (!string.IsNullOrEmpty(argsObject))
                        argumentsString = argsObject;
                }

                calls.Add(new AIToolCall
                {
                    name = nameMatch.Groups[1].Value,
                    argumentsJson = argumentsString
                });
            }

            return calls.Count > 0;
        }

        string ExecuteSellItem(string argumentsJson)
        {
            var recipientId = ExtractString(argumentsJson, "recipient_id");
            var itemId = ExtractInt(argumentsJson, "item_id");
            var goldValue = ExtractInt(argumentsJson, "gold_value");
            var message = ExtractString(argumentsJson, "message");

            var summary = string.IsNullOrWhiteSpace(message)
                ? $"{recipientId} sold item #{itemId} for {goldValue} gold."
                : message;

            Debug.Log($"AIToolRegistry: SellItem recipient={recipientId} item={itemId} gold={goldValue}");
            return summary;
        }

        string ExecuteGenerateQuest(string argumentsJson)
        {
            var recipientId = ExtractString(argumentsJson, "recipient_id");
            var title = ExtractString(argumentsJson, "title");
            var description = ExtractString(argumentsJson, "description");
            var reward = ExtractInt(argumentsJson, "reward");

            var questTitle = string.IsNullOrWhiteSpace(title) ? "A new bounty" : title;
            var questDescription = string.IsNullOrWhiteSpace(description) ? "The local board requests your aid." : description;
            var summary = $"{recipientId} posted a new quest: {questTitle} ({questDescription}) Reward: {reward} gold.";

            Debug.Log($"AIToolRegistry: GenerateQuest recipient={recipientId} title={questTitle} reward={reward}");
            return summary;
        }

        string ExecuteRecordPlayerOpinion(string argumentsJson)
        {
            var recipientId = ExtractString(argumentsJson, "recipient_id");
            var opinion = ExtractString(argumentsJson, "opinion");
            if (string.IsNullOrWhiteSpace(opinion))
                opinion = "The player left a strong impression.";

            var character = GetComponent<AICharacter>();
            var characterFileName = character != null ? character.characterFileName : string.Empty;
            if (string.IsNullOrWhiteSpace(characterFileName))
            {
                Debug.LogWarning("AIToolRegistry: no AICharacter assigned; cannot update player history file.");
                return $"{recipientId} recorded a new opinion about the player.";
            }

            var characterPath = GetPersistentCharacterPath(characterFileName);
            string content;

            if (File.Exists(characterPath))
            {
                content = File.ReadAllText(characterPath);
            }
            else
            {
                content = LoadCharacterResourceText(characterFileName);
                if (string.IsNullOrEmpty(content))
                {
                    Debug.LogWarning($"AIToolRegistry: character resource not found for {characterFileName}");
                    return $"{recipientId} recorded a new opinion about the player.";
                }
            }
            const string marker = "PLAYER HISTORY:";
            var historyMarkerIndex = content.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (historyMarkerIndex < 0)
            {
                content = content.TrimEnd() + "\n\n" + marker + "\n- " + opinion + "\n";
            }
            else
            {
                var lineEnd = content.IndexOf('\n', historyMarkerIndex);
                if (lineEnd < 0)
                    lineEnd = content.Length;

                var bodyStart = lineEnd < content.Length ? lineEnd + 1 : lineEnd;
                var bodyText = lineEnd < content.Length ? content.Substring(bodyStart) : string.Empty;
                var trimmedBody = bodyText.TrimStart('\n');
                var newEntry = $"- {opinion}";
                var updatedBody = string.IsNullOrWhiteSpace(trimmedBody) ? newEntry : newEntry + "\n" + trimmedBody;

                content = content.Substring(0, historyMarkerIndex) + marker + "\n" + updatedBody;
            }

            var directory = Path.GetDirectoryName(characterPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(characterPath, content);
            Debug.Log($"AIToolRegistry: Recorded opinion for {recipientId} in {characterFileName} (persistent copy)");
            return $"{recipientId} formed a new opinion about the player.";
        }

        string LoadCharacterResourceText(string characterFileName)
        {
            var resourceName = Path.GetFileNameWithoutExtension(characterFileName);
            var resourcePath = Path.Combine("AI", "Characters", resourceName).Replace("\\", "/");
            var textAsset = Resources.Load<TextAsset>(resourcePath);
            return textAsset != null ? textAsset.text : string.Empty;
        }

        string GetPersistentCharacterPath(string characterFileName)
        {
            var folder = Path.Combine(Application.persistentDataPath, "AI", "Characters");
            return Path.Combine(folder, characterFileName);
        }

        string StripCodeFences(string text)
        {
            return text.Replace("```json", string.Empty).Replace("```", string.Empty).Trim();
        }

        List<string> ExtractJsonObjects(string text)
        {
            var objects = new List<string>();
            var startIndex = 0;
            while (startIndex < text.Length)
            {
                var openIndex = text.IndexOf('{', startIndex);
                if (openIndex < 0)
                    break;

                var depth = 0;
                var endIndex = -1;
                for (var i = openIndex; i < text.Length; i++)
                {
                    if (text[i] == '{')
                        depth++;
                    else if (text[i] == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            endIndex = i;
                            break;
                        }
                    }
                }

                if (endIndex > openIndex)
                {
                    objects.Add(text.Substring(openIndex, endIndex - openIndex + 1));
                    startIndex = endIndex + 1;
                }
                else
                {
                    break;
                }
            }

            return objects;
        }

        string ExtractBalancedObject(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var trimmed = text.Trim();
            if (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
                return trimmed;

            var openIndex = trimmed.IndexOf('{');
            if (openIndex < 0)
                return string.Empty;

            var depth = 0;
            for (var i = openIndex; i < trimmed.Length; i++)
            {
                if (trimmed[i] == '{')
                    depth++;
                else if (trimmed[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return trimmed.Substring(openIndex, i - openIndex + 1);
                }
            }

            return string.Empty;
        }

        string ExtractString(string json, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(json))
                return string.Empty;

            var match = Regex.Match(json, $"\"{fieldName}\"\\s*:\\s*\"([^\"]*)\"");
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        int ExtractInt(string json, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(json))
                return 0;

            var match = Regex.Match(json, $"\"{fieldName}\"\\s*:\\s*(-?\\d+)");
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }
    }

    [Serializable]
    public class AIToolDefinition
    {
        public string name;
        public string description;
        public string schemaJson;
    }

    [Serializable]
    public class AIToolCall
    {
        public string name;
        public string argumentsJson;
    }

    [Serializable]
    public class AIToolExecutionResult
    {
        public bool success;
        public string message;

        public AIToolExecutionResult() { }

        public AIToolExecutionResult(bool success, string message)
        {
            this.success = success;
            this.message = message;
        }
    }
}