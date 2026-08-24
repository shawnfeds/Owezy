namespace Owezy.IntegrationTests;

/// <summary>
/// Used to skip a test when a runtime prerequisite (e.g. LocalDB) is unavailable.
/// </summary>
internal sealed class SkipException : Exception
{
    public SkipException(string reason) : base(reason) { }
}
