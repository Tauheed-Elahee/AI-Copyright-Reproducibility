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

        internal AzureModeApi(DeploymentConfig deployment, DefaultAzureCredential credential, string fallbackScope, Logger logger)
        {
            string modelDeployment = deployment.Connection.Deployment
                ?? throw new InvalidOperationException($"Deployment '{deployment.Label}' missing connection.deployment.");
            BearerTokenPolicy tp = new BearerTokenPolicy(credential, deployment.Connection.TokenScope ?? fallbackScope);
            OpenAIClientOptions opts = new OpenAIClientOptions
            {
                Endpoint = new Uri(deployment.Connection.Endpoint
                    ?? throw new InvalidOperationException($"Deployment '{deployment.Label}' missing connection.endpoint."))
            };
            if (deployment.Connection.ApiVersionOverride is not null)
                opts.AddPolicy(new ApiVersionPolicy(deployment.Connection.ApiVersionOverride), PipelinePosition.PerCall);
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
