using System.Net;

namespace Greenmind.Application.Tests.Integration;

public sealed class SwaggerApiTests(GreenmindApiFactory factory)
    : IClassFixture<GreenmindApiFactory>
{
    [Fact]
    public async Task SwaggerDocument_IsGeneratedSuccessfully()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
