using Microsoft.Testing.Platform.TestHost;
using Viscacha.Model.Test;

namespace Viscacha.TestRunner.Framework;

internal sealed class Session
{
    public SessionUid Uid { get; }

    public string FileName { get; }
    public Suite Suite { get; }

    public Session(SessionUid uid, string fileName, Suite suite)
    {
        Uid = uid;
        FileName = fileName;
        Suite = suite;
    }
}