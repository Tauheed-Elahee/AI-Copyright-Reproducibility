using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Azure.Identity;
using OpenAI;
using OpenAI.Chat;
using AICopyrightReproducibility.Config;
using AICopyrightReproducibility.Executors;
using AICopyrightReproducibility.Executors.Standard;
using AICopyrightReproducibility.Utils;

namespace AICopyrightReproducibility.Executors.Azure
{
    public sealed class AzureModeApi : IDeploymentExecutor
    {
        private readonly StandardOpenAIExecutor _inner;

        public AzureModeApi(ResolvedConnectionConfig resolved, DefaultAzureCredential credential, Logger logger)
        {
            string modelDeployment = resolved.Fields.TryGetValue("deployment", out string? dep) ? dep
                : throw new InvalidOperationException("AzureModeApi requires a 'deployment' field.");
            BearerTokenPolicy tp = new BearerTokenPolicy(credential, resolved.TokenScope ?? "https://ai.azure.com/.default");
            OpenAIClientOptions opts = new OpenAIClientOptions { Endpoint = new Uri(resolved.Url) };
            ChatClient client = new ChatClient(modelDeployment, authenticationPolicy: tp, options: opts);
            _inner = new StandardOpenAIExecutor(client, logger, modelDeployment);
        }

        public Task<RunRecord> ExecuteAsync(
            QueryConfig prompt,
            string label,
            Dictionary<string, JsonElement> parameters,
            bool omitNullFields,
            int index,
            string outDir,
            string rawFileName,
            RetryConfig retry) =>
            _inner.ExecuteAsync(prompt, label, parameters, omitNullFields, index, outDir, rawFileName, retry);
    }
}
