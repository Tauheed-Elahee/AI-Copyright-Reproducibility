using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using AICopyrightReproducibility.Config;

namespace AICopyrightReproducibility.Executors
{
    public interface IDeploymentExecutor
    {
        Task<RunRecord> ExecuteAsync(
            QueryConfig prompt,
            string label,
            Dictionary<string, JsonElement> parameters,
            bool omitNullFields,
            int index,
            string outDir,
            string rawFileName,
            RetryConfig retry);
    }
}
