using System.Net;
using System.Text;
using System.Text.Json;
using Chronos.Agent.Conversation;
using Chronos.Agent.Extraction;
using Chronos.Agent.Configuration;
using Moq;

namespace Chronos.Tests.Agent;

public class ILlmAdapterContractTests
{
    [Fact]
    public async Task ChatAsync_WithValidMessages_ReturnsNonEmptyContent()
    {
        // Arrange — mock adapter returns a canned response
        var mockAdapter = new Mock<ILlmAdapter>();
        mockAdapter
            .Setup(a => a.ChatAsync(
                It.IsAny<IReadOnlyList<ChatMessage>>(),
                It.IsAny<LlmOptions?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResponse("Hello from the LLM!"));

        var messages = new List<ChatMessage>
        {
            new("user", "Hi there")
        };

        // Act
        var response = await mockAdapter.Object.ChatAsync(messages, null, CancellationToken.None);

        // Assert
        Assert.NotNull(response);
        Assert.False(string.IsNullOrWhiteSpace(response.Content));
    }

    [Fact]
    public async Task ChatAsync_PassesCancellationToken()
    {
        var mockAdapter = new Mock<ILlmAdapter>();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        mockAdapter
            .Setup(a => a.ChatAsync(
                It.IsAny<IReadOnlyList<ChatMessage>>(),
                It.IsAny<LlmOptions?>(),
                It.Is<CancellationToken>(t => t.IsCancellationRequested)))
            .ThrowsAsync(new OperationCanceledException());

        var messages = new List<ChatMessage> { new("user", "test") };

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => mockAdapter.Object.ChatAsync(messages, null, cts.Token));
    }
}

public class OllamaLlmAdapterTests
{
    private static HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string responseBody)
    {
        var handler = new MockHttpMessageHandler(statusCode, responseBody);
        return new HttpClient(handler);
    }

    [Fact]
    public async Task ChatAsync_SendsCorrectRequestBody()
    {
        // Arrange
        var expectedResponse = JsonSerializer.Serialize(new
        {
            message = new { role = "assistant", content = "Paris is the capital." }
        });

        var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, expectedResponse);
        var httpClient = new HttpClient(handler);
        var options = new OllamaOptions { BaseUrl = "https://localhost", Model = "llama4" };
        var adapter = new OllamaLlmAdapter(httpClient, options);

        var messages = new List<ChatMessage>
        {
            new("system", "You are helpful."),
            new("user", "What is the capital of France?")
        };

        // Act
        var response = await adapter.ChatAsync(messages, null, CancellationToken.None);

        // Assert — verify response parsed correctly
        Assert.Equal("Paris is the capital.", response.Content);

        // Assert — verify outbound request shape
        Assert.NotNull(handler.CapturedRequestBody);
        using var doc = JsonDocument.Parse(handler.CapturedRequestBody);
        var root = doc.RootElement;
        Assert.Equal("llama4", root.GetProperty("model").GetString());
        Assert.False(root.GetProperty("stream").GetBoolean());
        Assert.Equal(2, root.GetProperty("messages").GetArrayLength());
    }

    [Fact]
    public async Task ChatAsync_WithJsonMode_SetsFormat()
    {
        var expectedResponse = JsonSerializer.Serialize(new
        {
            message = new { role = "assistant", content = "{\"key\":\"value\"}" }
        });

        var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, expectedResponse);
        var httpClient = new HttpClient(handler);
        var options = new OllamaOptions { BaseUrl = "https://localhost", Model = "llama4" };
        var adapter = new OllamaLlmAdapter(httpClient, options);

        var messages = new List<ChatMessage> { new("user", "Extract data") };

        // Act
        await adapter.ChatAsync(messages, new LlmOptions { JsonMode = true }, CancellationToken.None);

        // Assert — format: "json" should be present
        using var doc = JsonDocument.Parse(handler.CapturedRequestBody!);
        Assert.Equal("json", doc.RootElement.GetProperty("format").GetString());
    }

    [Fact]
    public async Task ChatAsync_NonSuccess_ThrowsHttpRequestException()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.InternalServerError, "error");
        var options = new OllamaOptions { BaseUrl = "https://localhost", Model = "llama4" };
        var adapter = new OllamaLlmAdapter(httpClient, options);

        var messages = new List<ChatMessage> { new("user", "test") };

        await Assert.ThrowsAsync<HttpRequestException>(
            () => adapter.ChatAsync(messages, null, CancellationToken.None));
    }

    [Fact]
    public async Task ChatAsync_OverridesModel_WhenSpecifiedInOptions()
    {
        var expectedResponse = JsonSerializer.Serialize(new
        {
            message = new { role = "assistant", content = "ok" }
        });
        var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, expectedResponse);
        var httpClient = new HttpClient(handler);
        var options = new OllamaOptions { BaseUrl = "https://localhost", Model = "llama4" };
        var adapter = new OllamaLlmAdapter(httpClient, options);

        var messages = new List<ChatMessage> { new("user", "test") };

        await adapter.ChatAsync(messages, new LlmOptions { Model = "deepseek-v3" }, CancellationToken.None);

        using var doc = JsonDocument.Parse(handler.CapturedRequestBody!);
        Assert.Equal("deepseek-v3", doc.RootElement.GetProperty("model").GetString());
    }
}

public class PuterLlmAdapterTests
{
    [Fact]
    public async Task ChatAsync_SendsCorrectDriverCallBody()
    {
        var expectedResponse = JsonSerializer.Serialize(new
        {
            success = true,
            result = new { message = new { content = "Paris is the capital." } }
        });

        var handler = new CapturingHttpMessageHandler(HttpStatusCode.OK, expectedResponse);
        var httpClient = new HttpClient(handler);
        var options = new PuterOptions { BaseUrl = "https://api.puter.com", ApiToken = "test-token", Model = "gpt-4o-mini" };
        var adapter = new PuterLlmAdapter(httpClient, options);

        var messages = new List<ChatMessage>
        {
            new("user", "What is the capital of France?")
        };

        // Act
        var response = await adapter.ChatAsync(messages, null, CancellationToken.None);

        // Assert — response
        Assert.Equal("Paris is the capital.", response.Content);

        // Assert — request shape
        Assert.NotNull(handler.CapturedRequestBody);
        using var doc = JsonDocument.Parse(handler.CapturedRequestBody);
        var root = doc.RootElement;
        Assert.Equal("puter-chat-completion", root.GetProperty("interface").GetString());
        Assert.Equal("complete", root.GetProperty("method").GetString());

        // Assert — auth header
        Assert.NotNull(handler.CapturedRequestHeaders);
        Assert.Contains("Bearer test-token", handler.CapturedRequestHeaders["Authorization"].First());
    }

    [Fact]
    public async Task ChatAsync_NonSuccess_ThrowsHttpRequestException()
    {
        var httpClient = new HttpClient(new MockHttpMessageHandler(HttpStatusCode.InternalServerError, "error"));
        var options = new PuterOptions { BaseUrl = "https://api.puter.com", ApiToken = "test-token", Model = "gpt-4o-mini" };
        var adapter = new PuterLlmAdapter(httpClient, options);

        var messages = new List<ChatMessage> { new("user", "test") };

        await Assert.ThrowsAsync<HttpRequestException>(
            () => adapter.ChatAsync(messages, null, CancellationToken.None));
    }
}

#region Test Helpers

/// <summary>Simple mock handler that returns a fixed response.</summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _responseBody;

    public MockHttpMessageHandler(HttpStatusCode statusCode, string responseBody)
    {
        _statusCode = statusCode;
        _responseBody = responseBody;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
        });
    }
}

/// <summary>Mock handler that captures request body and headers for verification.</summary>
public class CapturingHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpStatusCode _statusCode;
    private readonly string _responseBody;

    public string? CapturedRequestBody { get; private set; }
    public Dictionary<string, IEnumerable<string>>? CapturedRequestHeaders { get; private set; }

    public CapturingHttpMessageHandler(HttpStatusCode statusCode, string responseBody)
    {
        _statusCode = statusCode;
        _responseBody = responseBody;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Content != null)
            CapturedRequestBody = await request.Content.ReadAsStringAsync(cancellationToken);

        CapturedRequestHeaders = request.Headers
            .ToDictionary(h => h.Key, h => h.Value);

        return new HttpResponseMessage(_statusCode)
        {
            Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
        };
    }
}

#endregion
