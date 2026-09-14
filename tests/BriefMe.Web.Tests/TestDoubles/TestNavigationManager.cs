using Microsoft.AspNetCore.Components;

namespace BriefMe.Web.Tests.TestDoubles;

public sealed class TestNavigationManager : NavigationManager
{
    public string? LastNavigationUri { get; private set; }

    public bool LastForceLoad { get; private set; }

    public TestNavigationManager() => Initialize("https://briefme.test/", "https://briefme.test/");

    protected override void NavigateToCore(string uri, NavigationOptions options)
    {
        LastNavigationUri = uri;
        LastForceLoad = options.ForceLoad;
    }
}
