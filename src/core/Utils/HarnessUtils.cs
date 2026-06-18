using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AICopyrightReproducibility.Config;

namespace AICopyrightReproducibility.Utils
{
    public static class HarnessUtils
    {
        public const string RawRunsDir = "runs";

        public static string Sha256Hex(string s)
        {
            byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(s));
            StringBuilder sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        public static Dictionary<string, string> CaptureHeaders(PipelineResponse response)
        {
            Dictionary<string, string> dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> h in response.Headers)
                dict[h.Key] = h.Value;
            return dict;
        }

        public static RunRecord CreateRecord(string label, int index) => new RunRecord
        {
            Deployment   = label,
            Index        = index,
            TimestampUtc = DateTimeOffset.UtcNow
        };

        public static Dictionary<string, object?> BuildRequestBody(
            string messageKey,
            QueryConfig prompt,
            Dictionary<string, JsonElement> parameters,
            bool omitNullFields)
        {
            List<Dictionary<string, string>> messages = new List<Dictionary<string, string>>();
            if (!string.IsNullOrEmpty(prompt.SystemMessage))
                messages.Add(new Dictionary<string, string> { ["role"] = "system", ["content"] = prompt.SystemMessage });
            messages.Add(new Dictionary<string, string> { ["role"] = "user", ["content"] = prompt.UserPrompt });

            Dictionary<string, object?> body = new Dictionary<string, object?> { [messageKey] = messages };
            foreach (KeyValuePair<string, JsonElement> kv in parameters)
            {
                if (omitNullFields && kv.Value.ValueKind == JsonValueKind.Null) continue;
                body[kv.Key] = kv.Value;
            }
            return body;
        }

        public static void ParseChatCompletionsResponse(string json, RunRecord rec)
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            if (root.TryGetProperty("model",              out JsonElement m))  rec.Model             = m.GetString();
            if (root.TryGetProperty("system_fingerprint", out JsonElement sf) && sf.ValueKind == JsonValueKind.String)
                rec.SystemFingerprint = sf.GetString();
            if (root.TryGetProperty("id",                 out JsonElement id)) rec.ResponseId        = id.GetString();
            if (root.TryGetProperty("created",            out JsonElement cr) && cr.ValueKind == JsonValueKind.Number)
                rec.Created = cr.GetInt64();
            if (root.TryGetProperty("usage", out JsonElement u) && u.ValueKind == JsonValueKind.Object)
            {
                if (u.TryGetProperty("prompt_tokens",     out JsonElement pt)) rec.PromptTokens     = pt.GetInt32();
                if (u.TryGetProperty("completion_tokens", out JsonElement ct)) rec.CompletionTokens = ct.GetInt32();
                if (u.TryGetProperty("total_tokens",      out JsonElement tt)) rec.TotalTokens      = tt.GetInt32();
            }
            if (root.TryGetProperty("choices", out JsonElement ch) && ch.ValueKind == JsonValueKind.Array
                && ch.GetArrayLength() > 0)
            {
                JsonElement c0 = ch[0];
                if (c0.TryGetProperty("finish_reason", out JsonElement fr)) rec.FinishReason = fr.GetString();
                if (c0.TryGetProperty("message", out JsonElement msg)
                    && msg.TryGetProperty("content", out JsonElement cont)
                    && cont.ValueKind == JsonValueKind.String)
                {
                    string text = cont.GetString() ?? "";
                    string hash = Sha256Hex(text);
                    rec.ContentText        = text;
                    rec.ContentSha256      = hash;
                    rec.ContentSha256Short = hash[..12];
                    rec.Lists = ExtractListItems(text);
                }
            }
            if (rec.Status != 200 && rec.Error is null)
                rec.Error = ExtractErrorMessage(root);
        }

        public static string? ExtractErrorMessage(JsonElement root)
        {
            if (root.TryGetProperty("error", out JsonElement err) &&
                err.TryGetProperty("message", out JsonElement msg))
                return msg.GetString();
            return null;
        }

        public static string[] ExtractListItems(string text) =>
            text.Split('\n')
                .Where(l => l.StartsWith("- "))
                .Select(l => l[2..].Trim())
                .ToArray();

        public static TimeSpan BackoffDelay(int attempt, string? retryAfterHeader, RetryConfig retry)
        {
            if (retryAfterHeader is not null && double.TryParse(retryAfterHeader, out double secs))
                return TimeSpan.FromSeconds(Math.Min(secs, retry.MaxDelayS));
            return TimeSpan.FromSeconds(
                Math.Min(retry.InitialDelayS * Math.Pow(2, attempt), retry.MaxDelayS));
        }

        public static string ResolvePrompt(string template, string title,
            string[] sections, int globalRep, int contentSeed,
            Dictionary<string, string>? extras = null)
        {
            static string Bulleted(IEnumerable<string> items) =>
                string.Join("\n", items.Select(s => "- " + s));

            string[] shuffled = sections.ToArray();
            new Random(contentSeed + globalRep).Shuffle(shuffled);

            string result = template
                .Replace("{title}", title)
                .Replace("{sections}", Bulleted(sections))
                .Replace("{sections_shuffled}", Bulleted(shuffled));

            if (extras is not null)
                foreach (var (k, v) in extras)
                    result = result.Replace("{" + k + "}", v);

            return result;
        }

        public static ResolvedConnectionConfig ResolveConnection(
            DeploymentConnectionConfig connection,
            EndpointsConfig endpoints,
            SecretsConfig secrets,
            string label)
        {
            string epName = connection.Endpoint
                ?? throw new InvalidOperationException($"Deployment '{label}' missing connection.endpoint.");

            if (!endpoints.Endpoints.TryGetValue(epName, out EndpointConfig? ep))
                throw new InvalidOperationException($"Deployment '{label}': endpoint '{epName}' not found in endpoints.");

            foreach (string f in ep.Fields)
                if (!connection.Fields.ContainsKey(f))
                    throw new InvalidOperationException(
                        $"Deployment '{label}': endpoint '{epName}' requires field '{f}'.");

            string url = Regex.Replace(ep.Url, @"\$\{([^}]+)\}", m =>
            {
                string key = m.Groups[1].Value;
                return connection.Fields.TryGetValue(key, out string? v) ? v
                    : throw new InvalidOperationException(
                        $"Deployment '{label}': no value for URL token '${{{key}}}'.");
            });

            string? apiKey = null;
            if (ep.Auth.Type == "api_key" && ep.Auth.Key is not null)
            {
                if (!secrets.Keys.TryGetValue(ep.Auth.Key, out apiKey))
                    throw new InvalidOperationException(
                        $"Deployment '{label}': key '{ep.Auth.Key}' not found in secrets.");
            }

            return new ResolvedConnectionConfig
            {
                Url        = url,
                AuthType   = ep.Auth.Type,
                TokenScope = ep.Auth.Scope,
                ApiKey     = apiKey,
                AuthHeader = ep.Auth.Header,
                AuthScheme = ep.Auth.Scheme,
                Fields     = connection.Fields,
            };
        }

        private static void ValidateTemplate(string queryLabel, string textLabel,
            string template, Dictionary<string, string> titleExtras)
        {
            var resolvable = new HashSet<string>(
                new[] { "title", "sections", "sections_shuffled" }.Concat(titleExtras.Keys),
                StringComparer.OrdinalIgnoreCase);
            List<string> unresolved = Regex.Matches(template, @"\{([^}]+)\}")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(m => m.Groups[1].Value)
                .Where(k => !resolvable.Contains(k))
                .Distinct()
                .ToList();
            if (unresolved.Count > 0)
                throw new InvalidOperationException(
                    $"Query '{queryLabel}' bound to text '{textLabel}' has unresolvable placeholder(s): " +
                    string.Join(", ", unresolved.Select(k => "{" + k + "}")));
        }

        public static List<BoundPrompt> BindPrompts(
            Dictionary<string, QueryConfig> queries,
            Dictionary<string, TextEntry>   texts,
            IEnumerable<PromptEntry>        prompts)
        {
            var result = new List<BoundPrompt>();
            foreach (PromptEntry entry in prompts)
            {
                if (!texts.TryGetValue(entry.Text, out TextEntry? tc))
                    throw new InvalidOperationException($"Prompt entry references unknown text label '{entry.Text}'.");
                foreach (string qLabel in entry.Queries)
                {
                    if (!queries.TryGetValue(qLabel, out QueryConfig? q))
                        throw new InvalidOperationException($"Prompt entry references unknown query label '{qLabel}' for text '{entry.Text}'.");

                    ValidateTemplate(q.Label, entry.Text, q.UserPrompt, tc.TitleExtras);

                    result.Add(new BoundPrompt
                    {
                        QueryLabel         = q.Label,
                        ListTask           = q.ListTask,
                        OrderTask          = q.OrderTask,
                        SystemMessage      = q.SystemMessage,
                        UserPromptTemplate = q.UserPrompt,
                        TextLabel          = entry.Text,
                        TitleFull          = tc.TitleFull,
                        TitleShort         = tc.TitleShort,
                        Sections           = tc.Sections,
                        Aliases            = tc.Aliases,
                        TitleExtras        = tc.TitleExtras,
                    });
                }
            }
            return result;
        }
    }
}
