namespace Greenmind.Application.Contracts.Api;

public interface IApiResponse
{
    bool Succeeded { get; }

    ApiError? Error { get; }
}
