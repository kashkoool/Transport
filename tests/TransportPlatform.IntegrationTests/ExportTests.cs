using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using TransportPlatform.Application.Abstractions;
using TransportPlatform.Domain.Companies;
using TransportPlatform.Infrastructure.Persistence;

namespace TransportPlatform.IntegrationTests;

/// <summary>The trips report exports as CSV, XLSX and PDF with correct content types + signatures.</summary>
public sealed class ExportTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private const string Password = "Str0ng!Passw0rd";

    [Fact]
    public async Task Trip_report_exports_as_csv_xlsx_and_pdf()
    {
        var manager = await SeedCompanyManagerWithTripAsync();

        var csv = await manager.GetAsync("/api/vendor/reports/trips/export?format=csv");
        csv.StatusCode.Should().Be(HttpStatusCode.OK);
        csv.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        (await csv.Content.ReadAsStringAsync()).Should().Contain("Origin");

        var xlsx = await manager.GetAsync("/api/vendor/reports/trips/export?format=xlsx");
        xlsx.StatusCode.Should().Be(HttpStatusCode.OK);
        xlsx.Content.Headers.ContentType!.MediaType
            .Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var xlsxBytes = await xlsx.Content.ReadAsByteArrayAsync();
        xlsxBytes.Should().StartWith(new byte[] { 0x50, 0x4B }); // "PK" — zip/xlsx signature

        var pdf = await manager.GetAsync("/api/vendor/reports/trips/export?format=pdf");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        var pdfBytes = await pdf.Content.ReadAsByteArrayAsync();
        Encoding.ASCII.GetString(pdfBytes, 0, 4).Should().Be("%PDF");
    }

    private async Task<HttpClient> SeedCompanyManagerWithTripAsync()
    {
        Guid companyId;
        var managerEmail = $"mgr-{Guid.NewGuid():N}@example.com";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var company = new Company("Export Lines", $"v-{Guid.NewGuid():N}@example.com", null);
            company.Activate();
            db.Companies.Add(company);
            await db.SaveChangesAsync();
            companyId = company.Id;
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
            await identity.RegisterVendorManagerAsync(managerEmail, Password, "Mgr", companyId);
        }

        var client = factory.CreateClient();
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email = managerEmail, password = Password });
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var auth = await login.Content.ReadFromJsonAsync<AuthDto>(Json);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        var bus = await (await PostJson(client, "/api/vendor/buses",
            new { busNumber = $"X-{Guid.NewGuid():N}".Substring(0, 8), seatCount = 40, type = 0, model = "Bus" }))
            .Content.ReadFromJsonAsync<IdDto>(Json);
        await PostJson(client, "/api/vendor/trips", new
        {
            busId = bus!.Id,
            origin = "Damascus",
            destination = "Aleppo",
            departureUtc = DateTimeOffset.UtcNow.AddDays(3),
            arrivalUtc = DateTimeOffset.UtcNow.AddDays(3).AddHours(5),
            price = 70_000m,
            currency = "SYP",
        });
        return client;
    }

    private static async Task<HttpResponseMessage> PostJson(HttpClient client, string url, object body)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        return await client.SendAsync(req);
    }

    private sealed record AuthDto(string AccessToken, string RefreshToken, string Email);
    private sealed record IdDto(Guid Id);
}
