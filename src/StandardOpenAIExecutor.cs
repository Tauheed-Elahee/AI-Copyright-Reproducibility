using System;
using System.ClientModel;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using OpenAI;
using OpenAI.Chat;

namespace AICopyrightReproducibility
{
    public sealed class StandardOpenAIExecutor : IDeploymentExecutor
    {
        private readonly ChatClient _client;
        // Used by AzureModeApi to inject the Azure deployment name into the request body
        private readonly string? _injectModel;

        public StandardOpenAIExecutor(DeploymentConfig deployment)
        {
            string endpoint = deployment.Connection.Endpoint
                ?? throw new InvalidOperationException($"Deployment '{deployment.Label}' missing connection.endpoint.");
            string apiKey = deployment.Connection.ApiKey
                ?? throw new InvalidOperationException($"Deployment '{deployment.Label}' missing connection.api_key.");
            string model = deployment.Parameters.TryGetValue("model", out JsonElement mEl)
                ? (mEl.GetString() ?? "")
                : "";
            _client = new ChatClient(model, new ApiKeyCredential(apiKey),
                new OpenAIClientOptions { Endpoint = new Uri(endpoint) });
        }

        internal StandardOpenAIExecutor(ChatClient client, string? injectModel = null)
        {
            _client      = client;
            _injectModel = injectModel;
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

            Dictionary<string, object?> body = HarnessUtils.BuildRequestBody("messages", prompt, parameters, omitNullFields);
            if (_injectModel is not null) body["model"] = _injectModel;
            string requestJson = JsonSerializer.Serialize(body);
            rec.RawFile = rawFileName;

            Stopwatch sw = Stopwatch.StartNew();
            string responseJson;
            int attempt = 0;
            while (true)
            {
                try
                {
                    using BinaryContent content = BinaryContent.Create(BinaryData.FromString(requestJson));
                    ClientResult result = await _client.CompleteChatAsync(content, options: null)
                                                       .ConfigureAwait(false);
                    sw.Stop();
                    PipelineResponse raw = result.GetRawResponse();
                    rec.Status          = raw.Status;
                    rec.ResponseHeaders = HarnessUtils.CaptureHeaders(raw);
                    responseJson        = raw.Content.ToString();
                    break;
                }
                catch (ClientResultException ex) when (ex.Status == 429 && attempt < retry.MaxAttempts)
                {
                    sw.Stop();
                    PipelineResponse? raw = ex.GetRawResponse();
                    string? retryAfter = null;
                    if (raw is not null)
                        foreach (KeyValuePair<string, string> h in raw.Headers)
                            if (string.Equals(h.Key, "Retry-After", StringComparison.OrdinalIgnoreCase))
                                { retryAfter = h.Value; break; }
                    TimeSpan delay = HarnessUtils.BackoffDelay(attempt, retryAfter, retry);
                    Console.WriteLine($"[{label}] 429 rate-limited, retry {attempt + 1}/{retry.MaxAttempts} after {delay.TotalSeconds:F1}s");
                    await Task.Delay(delay).ConfigureAwait(false);
                    attempt++;
                    rec.RetryCount++;
                    sw.Restart();
                }
                catch (ClientResultException ex)
                {
                    sw.Stop();
                    rec.Status = ex.Status;
                    rec.Error  = ex.Message;
                    PipelineResponse? raw = ex.GetRawResponse();
                    if (raw is not null) rec.ResponseHeaders = HarnessUtils.CaptureHeaders(raw);
                    responseJson = raw?.Content?.ToString()
                        ?? JsonSerializer.Serialize(new { error = ex.Message });
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
                HarnessUtils.ParseChatCompletionsResponse(responseJson, rec);
            }
            catch (Exception ex)
            {
                rec.Error = (rec.Error is null ? "" : rec.Error + " | ") + "parse: " + ex.Message;
            }

            return rec;
        }
    }
}
