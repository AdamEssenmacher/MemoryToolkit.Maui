using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MemoryToolkit.Maui.E2ETests;

public static class E2ETestRunner
{
    public static async Task<E2ETestReport> RunAsync(E2ETestPage testPage)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var results = new List<E2ETestResult>
        {
            await RunLeakMonitorBehaviorFromUnloadedAsync(testPage),
            await RunTearDownBehaviorFromUnloadedAsync(testPage),
            await RunNavigationPopCollectionAsync(testPage)
        };

        return new E2ETestReport(
            startedAt,
            DateTimeOffset.UtcNow,
            DeviceInfo.Current.Platform.ToString(),
            DeviceInfo.Current.VersionString,
            E2ETestReport.ResolveOutputPath(),
            results);
    }

    private static async Task<E2ETestResult> RunLeakMonitorBehaviorFromUnloadedAsync(E2ETestPage testPage)
    {
        RecordingMonitor monitor = new();
        IGarbageCollectionMonitor previousMonitor = GarbageCollectionMonitor.Instance;
        GarbageCollectionMonitor.Instance = monitor;

        try
        {
            var label = new Label
            {
                Text = "Leak monitor target"
            };
            LeakMonitorBehavior.SetName(label, "e2e-monitor-target");
            LeakMonitorBehavior.SetCascade(label, true);

            testPage.AttachTestView(label);
            await testPage.WaitForUiAsync();

            testPage.ClearTestView();

            bool monitorCalled = await monitor.WaitForCallAsync(TimeSpan.FromSeconds(3));
            bool namedTargetCaptured = monitor.CapturedTargets.Any(target => target.Name == "e2e-monitor-target");

            GC.KeepAlive(label);

            return CreateResult(
                "leak-monitor-unloaded",
                "LeakMonitorBehavior runs from a real Unloaded event",
                monitorCalled && namedTargetCaptured,
                new Dictionary<string, string>
                {
                    ["monitorCalled"] = monitorCalled.ToString(),
                    ["targetCount"] = monitor.CapturedTargets.Count.ToString(),
                    ["targetNames"] = string.Join(",", monitor.CapturedTargets.Select(target => target.Name)),
                    ["namedTargetCaptured"] = namedTargetCaptured.ToString()
                });
        }
        finally
        {
            testPage.ClearTestView();
            GarbageCollectionMonitor.Instance = previousMonitor;
        }
    }

    private static async Task<E2ETestResult> RunTearDownBehaviorFromUnloadedAsync(E2ETestPage testPage)
    {
        var bindingContext = new ProbeBindingContext();
        var behavior = new TestLabelBehavior();
        var gestureRecognizer = new TapGestureRecognizer();
        var label = new Label
        {
            Text = "Tear down target",
            BindingContext = bindingContext
        };
        label.Behaviors.Add(behavior);
        label.GestureRecognizers.Add(gestureRecognizer);
        TearDownBehavior.SetCascade(label, true);
        TearDownBehavior.SetStrategy(label, TearDownStrategy.Compartmentalize);

        testPage.AttachTestView(label);
        await testPage.WaitForUiAsync();

        testPage.ClearTestView();
        await testPage.WaitForUiAsync(750);

        bool bindingContextCleared = label.BindingContext is null;
        bool behaviorsCleared = label.Behaviors.Count == 0;
        bool gesturesCleared = label.GestureRecognizers.Count == 0;

        GC.KeepAlive(bindingContext);

        return CreateResult(
            "teardown-unloaded-compartmentalize",
            "TearDownBehavior compartmentalizes from a real Unloaded event",
            bindingContextCleared && behaviorsCleared && gesturesCleared,
            new Dictionary<string, string>
            {
                ["bindingContextCleared"] = bindingContextCleared.ToString(),
                ["behaviorsCleared"] = behaviorsCleared.ToString(),
                ["gesturesCleared"] = gesturesCleared.ToString()
            });
    }

    private static async Task<E2ETestResult> RunNavigationPopCollectionAsync(E2ETestPage testPage)
    {
        var tearDownTargets = new List<string>();
        TearDownBehavior.OnTearDown = target => tearDownTargets.Add(target.GetType().Name);

        NavigationCollectionProbe probe;
        try
        {
            probe = await CreateNavigationPopProbeAsync(testPage);
        }
        finally
        {
            TearDownBehavior.OnTearDown = null;
        }

        await CollectAsync();

        bool viewsCollected = probe.References.AliveViews == 0;
        bool handlersCollected = probe.References.AliveHandlers == 0;
        bool platformViewsCollected = probe.References.PlatformViews.Count == 0 || probe.References.AlivePlatformViews == 0;
        bool bindingContextCollected = !probe.BindingContextReference.IsAlive;
        bool tearDownHookInvoked = tearDownTargets.Count > 0;

        Dictionary<string, string> observations = probe.References.ToObservations();
        observations["bindingContextAlive"] = probe.BindingContextReference.IsAlive.ToString();
        observations["tearDownHookInvoked"] = tearDownHookInvoked.ToString();
        observations["tearDownTargets"] = string.Join(",", tearDownTargets);

        return CreateResult(
            "navigation-pop-compartmentalize-collects",
            "Navigation pop with TearDownBehavior compartmentalizes and releases the target page graph",
            viewsCollected && handlersCollected && platformViewsCollected && bindingContextCollected && tearDownHookInvoked,
            observations);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<NavigationCollectionProbe> CreateNavigationPopProbeAsync(E2ETestPage testPage)
    {
        Window window = testPage.Window ??
                        Application.Current?.Windows.FirstOrDefault() ??
                        throw new InvalidOperationException("E2E tests require an active Window.");

        var rootPage = new ContentPage
        {
            Content = new Label
            {
                Text = "Navigation root"
            }
        };
        var navigationPage = new NavigationPage(rootPage);

        var bindingContext = new ProbeBindingContext();
        var targetPage = new ContentPage
        {
            Title = "Navigation target",
            BindingContext = bindingContext,
            Content = new VerticalStackLayout
            {
                BindingContext = bindingContext,
                Children =
                {
                    new Label
                    {
                        Text = "Navigation target child",
                        BindingContext = bindingContext
                    },
                    new Border
                    {
                        Content = new Label
                        {
                            Text = "Nested content"
                        }
                    }
                }
            }
        };

        TearDownBehavior.SetCascade(targetPage, true);
        TearDownBehavior.SetStrategy(targetPage, TearDownStrategy.Compartmentalize);

        window.Page = navigationPage;
        await testPage.WaitForUiAsync(300);

        await navigationPage.Navigation.PushAsync(targetPage, false);
        await testPage.WaitForUiAsync(500);

        CapturedReferenceSet references = CapturedReferenceSet.Capture(targetPage);
        WeakReference bindingContextReference = new(bindingContext);

        await navigationPage.Navigation.PopAsync(false);
        await testPage.WaitForUiAsync(750);

        window.Page = testPage;
        await testPage.WaitForUiAsync(500);

        return new NavigationCollectionProbe(references, bindingContextReference);
    }

    private static E2ETestResult CreateResult(
        string id,
        string name,
        bool passed,
        IReadOnlyDictionary<string, string> observations)
    {
        string? failure = passed
            ? null
            : string.Join(", ", observations.Select(observation => $"{observation.Key}={observation.Value}"));

        return new E2ETestResult(id, name, passed, observations, failure);
    }

    private static async Task CollectAsync(int passes = 10, int delayMilliseconds = 50)
    {
        for (var i = 0; i < passes; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            await Task.Delay(delayMilliseconds);
        }
    }

    private sealed class TestLabelBehavior : Behavior<Label>
    {
    }

    private sealed class ProbeBindingContext
    {
        public byte[] Payload { get; } = new byte[1024 * 300];
    }

    private sealed record NavigationCollectionProbe(
        CapturedReferenceSet References,
        WeakReference BindingContextReference);

    private sealed class RecordingMonitor : IGarbageCollectionMonitor
    {
        private readonly TaskCompletionSource _callReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Action<CollectionTarget>? OnCollected { get; set; }

        public Action<CollectionTarget>? OnLeaked { get; set; }

        public ILogger Logger { get; set; } = NullLogger.Instance;

        public IReadOnlyList<CollectionTarget> CapturedTargets { get; private set; } = [];

        public Task MonitorAndForceCollectionAsync(List<CollectionTarget> collectionItems)
        {
            CapturedTargets = collectionItems.ToArray();
            _callReceived.TrySetResult();
            return Task.CompletedTask;
        }

        public async Task<bool> WaitForCallAsync(TimeSpan timeout)
        {
            Task completedTask = await Task.WhenAny(_callReceived.Task, Task.Delay(timeout));
            return completedTask == _callReceived.Task;
        }
    }
}
