using System;
using System.Threading;
using System.Threading.Tasks;
using PRISM.App.Services;
using Xunit;

namespace PRISM.Tests;

public class SingleInstanceServiceTests
{
    [Fact]
    public void SingleInstanceService_PrimaryAndSecondary_IPC_Communication_Succeeds()
    {
        string testMutex = $"Local\\PRISM_TestMutex_{Guid.NewGuid():N}";
        string testPipe = $"PRISM_TestPipe_{Guid.NewGuid():N}";

        string[]? receivedArgs = null;
        using var countdown = new ManualResetEventSlim(false);

        using var primary = new SingleInstanceService(args =>
        {
            receivedArgs = args;
            countdown.Set();
            return true;
        }, testMutex, testPipe);

        bool primaryInit = primary.Initialize(new[] { "primary_arg" });
        Assert.True(primaryInit, "Primary instance should successfully initialize as primary.");

        // Spawn a secondary instance that attempts to initialize
        using var secondary = new SingleInstanceService(_ => true, testMutex, testPipe);
        bool secondaryInit = secondary.Initialize(new[] { "--settings", "--test" });
        Assert.False(secondaryInit, "Secondary instance should return false indicating it signalled the primary.");

        // Wait for primary callback to be invoked with transmitted arguments
        bool signaled = countdown.Wait(3000);
        Assert.True(signaled, "Primary instance should receive IPC activation signal.");
        Assert.NotNull(receivedArgs);
        Assert.Contains("--settings", receivedArgs);
        Assert.Contains("--test", receivedArgs);
    }

    [Fact]
    public void SingleInstanceService_ActivationHandshake_ReturnsConfirmedStatus()
    {
        string testMutex = $"Local\\PRISM_TestMutex_{Guid.NewGuid():N}";
        string testPipe = $"PRISM_TestPipe_{Guid.NewGuid():N}";

        using var primary = new SingleInstanceService(args => true, testMutex, testPipe);
        Assert.True(primary.Initialize(new[] { "first" }));

        using var secondary = new SingleInstanceService(args => true, testMutex, testPipe);
        bool isSecondaryPrimary = secondary.Initialize(new[] { "--display" });
        Assert.False(isSecondaryPrimary, "Secondary instance should recognize running primary with active window.");
    }
}
