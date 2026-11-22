using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FinanceControl.Bff.Notifications;

namespace FinanceControl.Bff.Tests;

public sealed class MobilePushDeviceApiTests(BffApplicationFactory factory)
    : IClassFixture<BffApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task MobilePushDevices_AreUserScopedUpsertedByInstallationAndPlatformAndRevocable()
    {
        var unauthorized = await _client.GetAsync("/api/v1/notifications/mobile-devices");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        var token = await AuthenticateAsync();
        const string installationId = "7d88b32d-130b-47f0-835f-79d871867d31";
        using var createRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/notifications/mobile-devices")
        {
            Content = JsonContent.Create(new
            {
                installationId,
                platform = "ANDROID",
                expoPushToken = "ExponentPushToken[mobile-token-1]",
                deviceName = "Pixel 9",
                appVersion = "1.0.0"
            })
        };
        createRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var createResponse = await _client.SendAsync(createRequest);
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<MobilePushDeviceResponse>();
        Assert.NotNull(created);
        Assert.Equal(installationId, created.InstallationId);
        Assert.Equal("android", created.Platform);
        Assert.Equal("Pixel 9", created.DeviceName);

        using var updateRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/notifications/mobile-devices")
        {
            Content = JsonContent.Create(new
            {
                installationId,
                platform = "android",
                expoPushToken = "ExponentPushToken[mobile-token-2]",
                deviceName = "Pixel 9 renamed",
                appVersion = "1.1.0"
            })
        };
        updateRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var updateResponse = await _client.SendAsync(updateRequest);
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<MobilePushDeviceResponse>();
        Assert.Equal(created?.Id, updated?.Id);
        Assert.Equal("Pixel 9 renamed", updated?.DeviceName);
        Assert.Equal("1.1.0", updated?.AppVersion);

        using var listRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/notifications/mobile-devices");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var listResponse = await _client.SendAsync(listRequest);
        listResponse.EnsureSuccessStatusCode();
        var devices = await listResponse.Content.ReadFromJsonAsync<IReadOnlyList<MobilePushDeviceResponse>>();
        Assert.Single(devices!);
        Assert.Equal(updated?.Id, devices![0].Id);

        var friendToken = await AuthenticateAsync("friend@test.local");
        using var friendListRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/v1/notifications/mobile-devices");
        friendListRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", friendToken);
        var friendListResponse = await _client.SendAsync(friendListRequest);
        friendListResponse.EnsureSuccessStatusCode();
        var friendDevices = await friendListResponse.Content
            .ReadFromJsonAsync<IReadOnlyList<MobilePushDeviceResponse>>();
        Assert.Empty(friendDevices!);

        using var deleteRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/v1/notifications/mobile-devices/{installationId}/android");
        deleteRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var deleteResponse = await _client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var invalidRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/notifications/mobile-devices")
        {
            Content = JsonContent.Create(new
            {
                installationId = "not-a-guid",
                platform = "web",
                expoPushToken = "ExponentPushToken[]",
            })
        };
        invalidRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var invalidResponse = await _client.SendAsync(invalidRequest);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        using var invalidPayload = JsonDocument.Parse(await invalidResponse.Content.ReadAsStringAsync());
        var errors = invalidPayload.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("installationId", out _));
        Assert.True(errors.TryGetProperty("platform", out _));
        Assert.True(errors.TryGetProperty("expoPushToken", out _));
    }

    [Fact]
    public async Task MobilePushTest_RequiresAuthenticationAndRejectsExternalRoutes()
    {
        var unauthorized = await _client.PostAsJsonAsync(
            "/api/v1/notifications/mobile-devices/test",
            new { title = "Test", body = "Test", route = "/(tabs)" });
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        var token = await AuthenticateAsync();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/notifications/mobile-devices/test")
        {
            Content = JsonContent.Create(new
            {
                title = "Test",
                body = "Test",
                route = "https://example.com"
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(payload.RootElement.GetProperty("errors").TryGetProperty("route", out _));
    }

    private async Task<string> AuthenticateAsync(string email = BffApplicationFactory.DemoEmail)
    {
        var loginResponse = await _client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email,
            password = BffApplicationFactory.DemoPassword
        });
        loginResponse.EnsureSuccessStatusCode();
        using var login = JsonDocument.Parse(await loginResponse.Content.ReadAsStringAsync());
        return login.RootElement.GetProperty("accessToken").GetString()!;
    }
}
