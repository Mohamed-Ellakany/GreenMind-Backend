using Greenmind.Application.Contracts.Api;

namespace Greenmind.Application.Tests.Contracts.Api;

public class ApiResponseFactoryTests
{
    [Fact]
    public void Success_ReturnsConsistentSuccessShape()
    {
        var response = ApiResponseFactory.Success(
            new { Name = "Crop" },
            traceId: "trace-1");

        Assert.True(response.Succeeded);
        Assert.NotNull(response.Data);
        Assert.Null(response.Error);
        Assert.Equal("trace-1", response.TraceId);
        Assert.Equal("Request completed successfully.", response.Message);
    }

    [Fact]
    public void ValidationFailure_ReturnsBadRequestWithFieldLevelDetails()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["name"] = ["Name is required."],
            ["plantingDate"] = ["Planting date is invalid."]
        };

        var response = ApiResponseFactory.ValidationFailure<object?>(errors, "trace-2");

        Assert.False(response.Succeeded);
        Assert.Null(response.Data);
        Assert.Equal("trace-2", response.TraceId);
        Assert.NotNull(response.Error);
        Assert.Equal(ApiErrorCodes.ValidationFailed, response.Error.Code);
        Assert.Equal(400, response.Error.StatusCode);
        Assert.Equal(errors, response.Error.ValidationErrors);
    }

    [Theory]
    [InlineData(401, ApiErrorCodes.Unauthorized)]
    [InlineData(403, ApiErrorCodes.Forbidden)]
    [InlineData(404, ApiErrorCodes.NotFound)]
    [InlineData(409, ApiErrorCodes.Conflict)]
    public void FromStatusCode_MapsKnownStatusesToStableCodes(int statusCode, string expectedCode)
    {
        var response = ApiResponseFactory.FromStatusCode<object?>(statusCode, "trace-3");

        Assert.False(response.Succeeded);
        Assert.NotNull(response.Error);
        Assert.Equal(statusCode, response.Error.StatusCode);
        Assert.Equal(expectedCode, response.Error.Code);
    }

    [Fact]
    public void FromException_DoesNotLeakExceptionDetails()
    {
        var exception = new InvalidOperationException("Sensitive database connection details.");

        var response = ApiResponseFactory.FromException<object?>(exception, "trace-4");

        Assert.False(response.Succeeded);
        Assert.NotNull(response.Error);
        Assert.Equal(500, response.Error.StatusCode);
        Assert.Equal(ApiErrorCodes.UnhandledException, response.Error.Code);
        Assert.DoesNotContain("Sensitive", response.Error.Message);
        Assert.Equal("An unexpected error occurred.", response.Error.Message);
    }
}
