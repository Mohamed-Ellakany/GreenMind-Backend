namespace Greenmind.Application.Contracts.Regulatory;

public sealed class RegulatoryImportValidationException(string message) : Exception(message);
