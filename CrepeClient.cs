using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace CrepeBiometricBridge;

internal sealed class CrepeClient(BridgeSettings settings)
{
    private readonly HttpClient client = CreateClient(settings);

    public async Task<IReadOnlyList<EmployeeOption>> FindEmployeesAsync(string search, CancellationToken cancellationToken)
    {
        var response = await client.GetFromJsonAsync<EmployeesResponse>($"checks/biometrico/bridge/employees?search={Uri.EscapeDataString(search)}", cancellationToken);

        return response?.Employees ?? [];
    }

    public async Task<string> EnrollAsync(int employeeId, Guid templateId)
    {
        using var response = await client.PostAsJsonAsync("checks/biometrico/bridge/enroll", new
        {
            employee_id = employeeId,
            template_id = templateId,
        });

        return await MessageAsync(response, "La huella fue ligada al empleado en Crepe.");
    }

    public async Task<string> UnenrollAsync(int employeeId)
    {
        using var response = await client.PostAsJsonAsync("checks/biometrico/bridge/unenroll", new { employee_id = employeeId });

        return await MessageAsync(response, "Huella eliminada.");
    }

    public async Task<IReadOnlyList<AttendanceHistory>> HistoryAsync(CancellationToken cancellationToken)
    {
        var response = await client.GetFromJsonAsync<HistoryResponse>("checks/biometrico/bridge/history?limit=50", cancellationToken);

        return response?.Records ?? [];
    }

    public async Task<CheckRecord> RegisterAsync(Guid templateId)
    {
        using var response = await client.PostAsJsonAsync("checks/biometrico/bridge/registrar", new
        {
            template_id = templateId,
        });

        if (!response.IsSuccessStatusCode)
        {
            var errorPayload = await response.Content.ReadFromJsonAsync<BridgeResponse>();
            throw new CrepeBridgeException(errorPayload?.Message ?? "Crepe rechazó el registro.", (int)response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<CheckResponse>();

        return new CheckRecord(
            payload?.EmployeeName ?? "Colaborador",
            payload?.CheckedAt ?? DateTime.Now.ToString("HH:mm"),
            payload?.Status ?? "registered",
            payload?.Message ?? "Registro guardado.");
    }

    private static HttpClient CreateClient(BridgeSettings settings)
    {
        if (!Uri.TryCreate(settings.CrepeUrl.TrimEnd('/') + '/', UriKind.Absolute, out var address))
        {
            throw new InvalidOperationException("La dirección de Crepe no es válida.");
        }

        var client = new HttpClient { BaseAddress = address, Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

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

        var errorPayload = await response.Content.ReadFromJsonAsync<BridgeResponse>();
        throw new CrepeBridgeException(errorPayload?.Message ?? "Crepe rechazó el registro.", (int)response.StatusCode);
    }

    private sealed record BridgeResponse(string? Message);

    private sealed record CheckResponse(
        string? Message,
        string? Status,
        [property: JsonPropertyName("employee_name")] string? EmployeeName,
        [property: JsonPropertyName("checked_at")] string? CheckedAt);

    private sealed record EmployeesResponse(IReadOnlyList<EmployeeOption>? Employees);

    private sealed record HistoryResponse(IReadOnlyList<AttendanceHistory>? Records);
}

internal sealed class CrepeBridgeException(string message, int statusCode) : InvalidOperationException(message)
{
    public int StatusCode { get; } = statusCode;
}

internal sealed record CheckRecord(string EmployeeName, string CheckedAt, string Status, string Message);

internal sealed record AttendanceHistory(
    [property: JsonPropertyName("employee_name")] string EmployeeName,
    [property: JsonPropertyName("work_date")] string WorkDate,
    [property: JsonPropertyName("checked_in_at")] string? CheckedInAt,
    [property: JsonPropertyName("checked_out_at")] string? CheckedOutAt);

internal sealed record EmployeeOption(int Id, string Name, string? Position)
{
    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(Position) ? Name : $"{Name} · {Position}";
    }
}
