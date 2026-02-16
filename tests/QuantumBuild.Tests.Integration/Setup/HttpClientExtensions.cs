using System.Net.Http.Json;
using System.Text.Json;

namespace QuantumBuild.Tests.Integration.Setup;

/// <summary>
/// Extension methods for HttpClient to simplify common test operations.
/// </summary>
public static class HttpClientExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Performs a GET request and deserializes the response to the specified type.
    /// </summary>
    public static async Task<T?> GetFromJsonAsync<T>(this HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }

    /// <summary>
    /// Performs a GET request and returns both the response and deserialized content.
    /// </summary>
    public static async Task<(HttpResponseMessage Response, T? Content)> GetWithResponseAsync<T>(
        this HttpClient client,
        string url)
    {
        var response = await client.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            return (response, default);
        }
        var content = await response.Content.ReadFromJsonAsync<T>(JsonOptions);
        return (response, content);
    }

    /// <summary>
    /// Performs a POST request and returns the ID from the response (assumes response is a Guid).
    /// </summary>
    public static async Task<Guid> PostAndGetIdAsync<T>(this HttpClient client, string url, T data)
    {
        var response = await client.PostAsJsonAsync(url, data, JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(JsonOptions);
    }

    /// <summary>
    /// Performs a POST request and returns both the response and deserialized content.
    /// </summary>
    public static async Task<(HttpResponseMessage Response, TResponse? Content)> PostWithResponseAsync<TRequest, TResponse>(
        this HttpClient client,
        string url,
        TRequest data)
    {
        var response = await client.PostAsJsonAsync(url, data, JsonOptions);
        if (!response.IsSuccessStatusCode)
        {
            return (response, default);
        }
        var content = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions);
        return (response, content);
    }

    /// <summary>
    /// Performs a PUT request with JSON content.
    /// </summary>
    public static async Task<HttpResponseMessage> PutAsJsonAsync<T>(this HttpClient client, string url, T data)
    {
        return await client.PutAsync(url, JsonContent.Create(data, options: JsonOptions));
    }

    /// <summary>
    /// Performs a PUT request and returns both the response and deserialized content.
    /// </summary>
    public static async Task<(HttpResponseMessage Response, TResponse? Content)> PutWithResponseAsync<TRequest, TResponse>(
        this HttpClient client,
        string url,
        TRequest data)
    {
        var response = await client.PutAsync(url, JsonContent.Create(data, options: JsonOptions));
        if (!response.IsSuccessStatusCode)
        {
            return (response, default);
        }
        var content = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions);
        return (response, content);
    }

    /// <summary>
    /// Performs a DELETE request and returns the response.
    /// </summary>
    public static async Task<HttpResponseMessage> DeleteAndGetResponseAsync(this HttpClient client, string url)
    {
        return await client.DeleteAsync(url);
    }

    /// <summary>
    /// Reads the response content as a string for debugging purposes.
    /// </summary>
    public static async Task<string> ReadContentAsStringAsync(this HttpResponseMessage response)
    {
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Extracts error messages from a validation error response.
    /// </summary>
    public static async Task<Dictionary<string, string[]>?> ReadValidationErrorsAsync(this HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return null;
        }

        try
        {
            var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOptions);
            return problemDetails?.Errors;
        }
        catch
        {
            return null;
        }
    }

    private class ValidationProblemDetails
    {
        public string? Type { get; set; }
        public string? Title { get; set; }
        public int? Status { get; set; }
        public string? Detail { get; set; }
        public Dictionary<string, string[]>? Errors { get; set; }
    }
}
