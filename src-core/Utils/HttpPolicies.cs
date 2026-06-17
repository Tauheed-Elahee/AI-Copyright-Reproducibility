using System;
using System.ClientModel.Primitives;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AICopyrightReproducibility.Utils
{
    public sealed class ApiVersionPolicy : PipelinePolicy
    {
        private readonly string _apiVersion;
        public ApiVersionPolicy(string apiVersion) => _apiVersion = apiVersion;

        public override void Process(PipelineMessage message,
            IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            AddQuery(message);
            ProcessNext(message, pipeline, currentIndex);
        }

        public override async ValueTask ProcessAsync(PipelineMessage message,
            IReadOnlyList<PipelinePolicy> pipeline, int currentIndex)
        {
            AddQuery(message);
            await ProcessNextAsync(message, pipeline, currentIndex).ConfigureAwait(false);
        }

        private void AddQuery(PipelineMessage message)
        {
            Uri? uri = message.Request?.Uri;
            if (uri is null) return;
            UriBuilder builder = new UriBuilder(uri);
            string add = "api-version=" + Uri.EscapeDataString(_apiVersion);
            string existing = builder.Query.TrimStart('?');
            builder.Query = string.IsNullOrEmpty(existing) ? add : existing + "&" + add;
            message.Request!.Uri = builder.Uri;
        }
    }
}
