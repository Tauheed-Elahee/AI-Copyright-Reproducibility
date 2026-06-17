using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using AICopyrightReproducibility.Config;
using AICopyrightReproducibility.Executors;
using AICopyrightReproducibility.Utils;

namespace AICopyrightReproducibility.Executors.Azure
{
    public sealed class AzureAgentApiExecutor : IDeploymentExecutor
    {
        private readonly HttpClient _http;
        private readonly DefaultAzureCredential _credential;
        private readonly string _agentEndpoint;
        private readonly string _agentApiVersion;
        private readonly string _scope;
        private readonly Logger _logger;

        internal AzureAgentApiExecutor(HttpClient http, DefaultAzureCredential credential, DeploymentConfig deployment, string fallbackScope, Logger logger)
        {
            _http = http;
            _credential = credential;
            AgentConnectionConfig agent = deployment.Connection.Agent
                ?? throw new InvalidOperationException($"Deployment '{deployment.Label}' missing connection.agent.");
            _agentEndpoint = agent.Endpoint
                ?? throw new InvalidOperationException($"Deployment '{deployment.Label}' missing connection.agent.endpoint.");
            _agentApiVersion = agent.ApiVersion
                ?? throw new InvalidOperationException($"Deployment '{deployment.Label}' missing connection.agent.api_version.");
            _scope  = deployment.Connection.TokenScope ?? fallbackScope;
            _logger = logger;
        }

        public async Task<RunRecord> ExecuteAsync(
            QueryConfig prompt,
            string label,
            Dictionary<string, JsonElement> parameters,
            bool omitNullFields,
            int index,
            string outDir,
            string rawFileName,
            RetryConfig retry)
        {
            RunRecord rec = HarnessUtils.CreateRecord(label, index);

            string requestJson = JsonSerializer.Serialize(
                HarnessUtils.BuildRequestBody("input", prompt, parameters, omitNullFields));
            rec.RawFile = rawFileName;

            Stopwatch sw = Stopwatch.StartNew();
            string responseJson = "";
            string agentUrl = _agentEndpoint +
                (_agentEndpoint.Contains('?') ? "&" : "?") +
                "api-version=" + Uri.EscapeDataString(_agentApiVersion);
            int attempt = 0;
            while (true)
            {
                try
                {
                    AccessToken token = await _credential.GetTokenAsync(
                        new TokenRequestContext(new[] { _scope }),
                        CancellationToken.None).ConfigureAwait(false);

                    using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, agentUrl);
                    request.Headers.Authorization =
                        new AuthenticationHeaderValue("Bearer", token.Token);
                    request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                    using HttpResponseMessage response =
                        await _http.SendAsync(request).ConfigureAwait(false);
                    sw.Stop();
                    rec.Status = (int)response.StatusCode;

                    if (rec.Status == 429 && attempt < retry.MaxAttempts)
                    {
                        string? retryAfter = response.Headers.RetryAfter?.Delta is TimeSpan d
                            ? d.TotalSeconds.ToString("F0") : null;
                        TimeSpan delay = HarnessUtils.BackoffDelay(attempt, retryAfter, retry);
                        _logger.Warn($"[{label}] 429 rate-limited, retry {attempt + 1}/{retry.MaxAttempts} after {delay.TotalSeconds:F1}s");
                        await Task.Delay(delay).ConfigureAwait(false);
                        attempt++;
                        rec.RetryCount++;
                        sw.Restart();
                        continue;
                    }

                    responseJson = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    break;
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    rec.Status   = -1;
                    rec.Error    = ex.Message;
                    responseJson = JsonSerializer.Serialize(new { error = ex.Message });
                    break;
                }
            }
            rec.DurationMs = sw.ElapsedMilliseconds;

            await File.WriteAllTextAsync(Path.Combine(outDir, rawFileName), responseJson).ConfigureAwait(false);
            rec.RawJson = responseJson;

            try
            {
                using JsonDocument doc  = JsonDocument.Parse(responseJson);
                JsonElement        root = doc.RootElement;

                if (root.TryGetProperty("id", out JsonElement id)) rec.ResponseId = id.GetString();

                if (root.TryGetProperty("usage", out JsonElement u) && u.ValueKind == JsonValueKind.Object)
                {
                    if (u.TryGetProperty("input_tokens",  out JsonElement it)) rec.PromptTokens     = it.GetInt32();
                    if (u.TryGetProperty("output_tokens", out JsonElement ot)) rec.CompletionTokens = ot.GetInt32();
                    if (u.TryGetProperty("total_tokens",  out JsonElement tt)) rec.TotalTokens      = tt.GetInt32();
                }

                if (root.TryGetProperty("output", out JsonElement output) &&
                    output.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in output.EnumerateArray())
                    {
                        if (!item.TryGetProperty("type", out JsonElement typeEl)) continue;
                        if (typeEl.GetString() != "message") continue;
                        if (!item.TryGetProperty("content", out JsonElement contentArr)) continue;
                        foreach (JsonElement block in contentArr.EnumerateArray())
                        {
                            if (!block.TryGetProperty("type", out JsonElement bt)) continue;
                            if (bt.GetString() != "output_text") continue;
                            if (!block.TryGetProperty("text", out JsonElement txt)) continue;
                            string text = txt.GetString() ?? "";
                            string hash = HarnessUtils.Sha256Hex(text);
                            rec.ContentText        = text;
                            rec.ContentSha256      = hash;
                            rec.ContentSha256Short = hash[..12];
                            rec.Lists = HarnessUtils.ExtractListItems(text);
                            break;
                        }
                        if (rec.ContentSha256 is not null) break;
                    }
                }

                if (rec.Status != 200)
                    rec.Error ??= HarnessUtils.ExtractErrorMessage(root);
            }
            catch (Exception ex)
            {
                rec.Error = (rec.Error is null ? "" : rec.Error + " | ") + "parse: " + ex.Message;
            }

            return rec;
        }
    }
}
