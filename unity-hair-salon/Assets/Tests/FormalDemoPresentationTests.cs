using System.Reflection;
using NUnit.Framework;

/// <summary>
/// The formal playable entry must use the authored salon scene. The diagnostic
/// 2D greybox is still available, but only when explicitly requested so the
/// browser entry cannot silently diverge from the reusable Unity demo.
/// </summary>
public sealed class FormalDemoPresentationTests
{
    [Test]
    public void FormalPlayableEntryUsesAuthoredSceneByDefault()
    {
        MethodInfo method = typeof(SalonDemo).GetMethod(
            "ShouldUseSimple2DPresentation", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null,
            "Presentation selection must be explicit so the formal build cannot silently become a greybox.");

        bool defaultPresentation = (bool)method.Invoke(null, new object[]
        {
            "http://127.0.0.1:64859/WebGLDemo/"
        });
        bool diagnosticPresentation = (bool)method.Invoke(null, new object[]
        {
            "http://127.0.0.1:64859/WebGLDemo/?simple2D=1"
        });

        Assert.That(defaultPresentation, Is.False,
            "The single playable entry should reuse the authored 3D salon scene by default.");
        Assert.That(diagnosticPresentation, Is.True,
            "The greybox must remain opt-in for diagnostics rather than being a second playable entry.");
    }
}
