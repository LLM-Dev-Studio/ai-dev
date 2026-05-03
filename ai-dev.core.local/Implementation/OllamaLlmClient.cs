using AiDev.Core.Local.Contracts;
using AiDev.Services;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiDev.Core.Local.Implementation;

internal sealed class OllamaLlmClient(
    IHttpClientFactory httpClientFactory,
    StudioSettingsService settingsService) : ILlmClient
{
    public string Provider => "ollama";

    public async Task<Result<string>> CompleteAsync(
        string prompt,
        string modelId,
        CancellationToken ct = default)
    {
        var baseUrl = settingsService.GetSettings().OllamaBaseUrl.TrimEnd('/');
        var requestUri = $"{baseUrl}/api/chat";

        var requestBody = new JsonObject
        {
            ["model"] = modelId,
            ["stream"] = false,
            ["messages"] = new JsonArray(
                new JsonObject { ["role"] = "user", ["content"] = prompt }),
        };

        try
        {
            var http = httpClientFactory.CreateClient("ollama-llm");
            using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(
                    requestBody.ToJsonString(),
                    System.Text.Encoding.UTF8,
                    "application/json"),
            };

            var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                return new Err<string>(new DomainError(
                    "OllamaLlmClient.HttpError",
                    $"Ollama returned HTTP {(int)response.StatusCode}."));

            var doc = await response.Content.ReadFromJsonAsync<JsonDocument>(ct);
            var content = doc?.RootElement
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (content is null)
                return new Err<string>(new DomainError(
                    "OllamaLlmClient.EmptyResponse",
                    "Ollama returned an empty message content."));

            return new Ok<string>(content);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return new Err<string>(new DomainError(
                "OllamaLlmClient.Exception",
                ex.Message));
        }
    }
}
