using System;
using System.Collections.Generic;
using AICopyrightReproducibility.Config;
using AICopyrightReproducibility.Utils;
using Xunit;

namespace AICopyrightReproducibility.Tests;

public class HarnessUtilsTests
{
    // ── Sha256Hex ────────────────────────────────────────────────────────────

    [Fact]
    public void Sha256Hex_EmptyString()
    {
        string hash = HarnessUtils.Sha256Hex("");
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", hash);
    }

    [Fact]
    public void Sha256Hex_KnownInput()
    {
        string hash = HarnessUtils.Sha256Hex("abc");
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
    }

    // ── ExtractListItems ─────────────────────────────────────────────────────

    [Fact]
    public void ExtractListItems_Standard()
    {
        string[] items = HarnessUtils.ExtractListItems("- alpha\n- beta");
        Assert.Equal(new string[] { "alpha", "beta" }, items);
    }

    [Fact]
    public void ExtractListItems_IgnoresNonBullet()
    {
        string[] items = HarnessUtils.ExtractListItems("intro\n- item\ntrailer");
        Assert.Equal(new string[] { "item" }, items);
    }

    [Fact]
    public void ExtractListItems_Empty()
    {
        string[] items = HarnessUtils.ExtractListItems("");
        Assert.Empty(items);
    }

    [Fact]
    public void ExtractListItems_TrimsWhitespace()
    {
        string[] items = HarnessUtils.ExtractListItems("-   spaced  ");
        Assert.Equal(new string[] { "spaced" }, items);
    }

    // ── ResolvePrompt ────────────────────────────────────────────────────────

    [Fact]
    public void ResolvePrompt_TitlePlaceholder()
    {
        string result = HarnessUtils.ResolvePrompt("{title}", "My Book", [], 1, 0);
        Assert.Equal("My Book", result);
    }

    [Fact]
    public void ResolvePrompt_SectionsPlaceholder()
    {
        string result = HarnessUtils.ResolvePrompt("{sections}", "t", ["A", "B"], 1, 0);
        Assert.Contains("- A", result);
        Assert.Contains("- B", result);
    }

    [Fact]
    public void ResolvePrompt_ExtraPlaceholder()
    {
        var extras = new Dictionary<string, string> { ["edition"] = "13e" };
        string result = HarnessUtils.ResolvePrompt("{edition}", "t", [], 1, 0, extras);
        Assert.Equal("13e", result);
    }

    // ── BackoffDelay ─────────────────────────────────────────────────────────

    private static RetryConfig DefaultRetry => new RetryConfig
        { MaxAttempts = 3, InitialDelayS = 1.0, MaxDelayS = 60.0 };

    [Fact]
    public void BackoffDelay_RetryAfterHeader()
    {
        TimeSpan delay = HarnessUtils.BackoffDelay(0, "30", DefaultRetry);
        Assert.Equal(TimeSpan.FromSeconds(30), delay);
    }

    [Fact]
    public void BackoffDelay_ExponentialFallback_Attempt0()
    {
        TimeSpan delay = HarnessUtils.BackoffDelay(0, null, DefaultRetry);
        Assert.Equal(TimeSpan.FromSeconds(1.0), delay);
    }

    [Fact]
    public void BackoffDelay_ExponentialFallback_Attempt1()
    {
        TimeSpan delay = HarnessUtils.BackoffDelay(1, null, DefaultRetry);
        Assert.Equal(TimeSpan.FromSeconds(2.0), delay);
    }

    [Fact]
    public void BackoffDelay_CappedAtMaxDelay()
    {
        var retry = new RetryConfig { MaxAttempts = 10, InitialDelayS = 1.0, MaxDelayS = 5.0 };
        TimeSpan delay = HarnessUtils.BackoffDelay(10, null, retry);
        Assert.Equal(TimeSpan.FromSeconds(5.0), delay);
    }

    // ── BindPrompts ──────────────────────────────────────────────────────────

    [Fact]
    public void BindPrompts_Success()
    {
        var queries = new Dictionary<string, QueryConfig>
        {
            ["q1"] = new QueryConfig { Label = "q1", UserPrompt = "{title}", Types = [] }
        };
        var texts = new Dictionary<string, TextEntry>
        {
            ["t1"] = new TextEntry("Full", "Short", ["S1", "S2"],
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string> { ["full"] = "Full", ["short"] = "Short" })
        };
        var prompts = new[] { new PromptEntry { Text = "t1", Queries = ["q1"] } };

        List<BoundPrompt> bound = HarnessUtils.BindPrompts(queries, texts, prompts);

        Assert.Single(bound);
        Assert.Equal("q1", bound[0].QueryLabel);
        Assert.Equal("t1", bound[0].TextLabel);
        Assert.Equal(new string[] { "S1", "S2" }, bound[0].Sections);
    }

    [Fact]
    public void BindPrompts_UnknownText_Throws()
    {
        var prompts = new[] { new PromptEntry { Text = "missing", Queries = ["q1"] } };
        Assert.Throws<InvalidOperationException>(() =>
            HarnessUtils.BindPrompts(new Dictionary<string, QueryConfig>(),
                                     new Dictionary<string, TextEntry>(),
                                     prompts));
    }

    [Fact]
    public void BindPrompts_UnknownQuery_Throws()
    {
        var texts = new Dictionary<string, TextEntry>
        {
            ["t1"] = new TextEntry("Full", "Short", [],
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string> { ["full"] = "Full", ["short"] = "Short" })
        };
        var prompts = new[] { new PromptEntry { Text = "t1", Queries = ["missing"] } };
        Assert.Throws<InvalidOperationException>(() =>
            HarnessUtils.BindPrompts(new Dictionary<string, QueryConfig>(), texts, prompts));
    }

    [Fact]
    public void BindPrompts_UnresolvablePlaceholder_Throws()
    {
        var queries = new Dictionary<string, QueryConfig>
        {
            ["q1"] = new QueryConfig { Label = "q1", UserPrompt = "{unknown_field}", Types = [] }
        };
        var texts = new Dictionary<string, TextEntry>
        {
            ["t1"] = new TextEntry("Full", "Short", [],
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, string> { ["full"] = "Full", ["short"] = "Short" })
        };
        var prompts = new[] { new PromptEntry { Text = "t1", Queries = ["q1"] } };
        Assert.Throws<InvalidOperationException>(() =>
            HarnessUtils.BindPrompts(queries, texts, prompts));
    }

    // ── ResolveConnection ────────────────────────────────────────────────────

    [Fact]
    public void ResolveConnection_ReplacesApiKey()
    {
        var (endpoints, secrets) = MakeConfig("my_endpoint", "https://example.com", "api_key", "my_key", "sk-abc");
        var connection = new DeploymentConnectionConfig { Endpoint = "my_endpoint" };
        ResolvedConnectionConfig resolved = HarnessUtils.ResolveConnection(connection, endpoints, secrets, "test");
        Assert.Equal("sk-abc", resolved.ApiKey);
        Assert.Equal("https://example.com", resolved.Url);
    }

    [Fact]
    public void ResolveConnection_SubstitutesUrlFields()
    {
        var (endpoints, secrets) = MakeConfig("my_endpoint", "https://example.com/${deployment}", "api_key", "my_key", "sk-abc",
            requiredFields: ["deployment"]);
        var connection = new DeploymentConnectionConfig
        {
            Endpoint = "my_endpoint",
            Fields   = new Dictionary<string, string> { ["deployment"] = "my-model" }
        };
        ResolvedConnectionConfig resolved = HarnessUtils.ResolveConnection(connection, endpoints, secrets, "test");
        Assert.Equal("https://example.com/my-model", resolved.Url);
    }

    [Fact]
    public void ResolveConnection_ThrowsOnMissingEndpoint()
    {
        var connection = new DeploymentConnectionConfig { Endpoint = "nonexistent" };
        Assert.Throws<InvalidOperationException>(() =>
            HarnessUtils.ResolveConnection(connection, new EndpointsConfig(), new SecretsConfig(), "test"));
    }

    [Fact]
    public void ResolveConnection_ThrowsOnMissingRequiredField()
    {
        var (endpoints, secrets) = MakeConfig("my_endpoint", "https://example.com/${deployment}", "api_key", "my_key", "sk-abc",
            requiredFields: ["deployment"]);
        var connection = new DeploymentConnectionConfig { Endpoint = "my_endpoint" };
        Assert.Throws<InvalidOperationException>(() =>
            HarnessUtils.ResolveConnection(connection, endpoints, secrets, "test"));
    }

    private static (EndpointsConfig, SecretsConfig) MakeConfig(
        string endpointName, string url, string authType, string keyName, string keyValue,
        string[]? requiredFields = null)
    {
        var endpoints = new EndpointsConfig
        {
            Endpoints = new Dictionary<string, EndpointConfig>
            {
                [endpointName] = new EndpointConfig
                {
                    Url    = url,
                    Auth   = new AuthConfig { Type = authType, Key = keyName, Header = "Authorization", Scheme = "Bearer" },
                    Fields = requiredFields is not null ? new List<string>(requiredFields) : new List<string>()
                }
            }
        };
        var secrets = new SecretsConfig
        {
            Keys = new Dictionary<string, string> { [keyName] = keyValue }
        };
        return (endpoints, secrets);
    }
}
