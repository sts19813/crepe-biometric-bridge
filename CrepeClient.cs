using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace CrepeBiometricBridge;

internal sealed class CrepeClient(BridgeSettings settings)
{
    private readonly HttpClient client = CreateClient(settings);

    public async Task<string> EnrollAsync(int employeeId, Guid templateId)
    {
        using var response = await client.PostAsJsonAsync("checks/biometrico/bridge/enroll", new
        {
            employee_id = employeeId,
            template_id = templateId,
        });

        return await MessageAsync(response, "La huella fue ligada al empleado en Crepe.");
    }

    public async Task<string> RegisterAsync(Guid templateId)
    {
        using var response = await client.PostAsJsonAsync("checks/biometrico/bridge/registrar", new
        {
            template_id = templateId,
        });

        return await MessageAsync(response, "Registro guardado.");
    }

    private static HttpClient CreateClient(BridgeSettings settings)
    {
        if (!Uri.TryCreate(settings.CrepeUrl.TrimEnd('/') + '/', UriKind.Absolute, out var address))
        {
            throw new InvalidOperationException("La dirección de Crepe no es válida.");
        }

        var client = new HttpClient { BaseAddress = address, Timeout = TimeSpan.FromSeconds(15) };

        if (!string.IsNullOrWhiteSpace(settings.BridgeKey))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", settings.BridgeKey);
        }

        return client;
    }

    private static async Task<string> MessageAsync(HttpResponseMessage response, string successMessage)
    {
        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadFromJsonAsync<BridgeResponse>();

            return payload?.Message ?? successMessage;
        }

        var detail = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"Crepe rechazó el registro ({(int)response.StatusCode}). {detail}");
    }

    private sealed record BridgeResponse(string? Message);
}
