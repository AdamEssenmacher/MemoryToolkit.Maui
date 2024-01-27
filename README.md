# Overview

Anyone paying attention knows that MAUI leaks like a toddler's sippy cup. It's messy, gross, and feels helplessly unavoidable.

MemoryToolkit.Maui assumes this is not a problem we can totally fix, and so instead aims to make this a problem we can at least manage. It offers three helpful features:

- **Detects leaks** in MAUI views/pages, and notifies you when they occur at runtime.
- **Compartmentalizes & prevents _some_ leaks** by breaking apart pages and views when they're no longer needed.
- **Prevents _some_ leaks and ensures native resources are cleaned up** by automatically calling `DisconnectHandler()` on view/page handlers.

## Warning!
While leak prevention & compartmentalization features are intended to be safe for production use, it may not be advisable to use leak detection in release builds due to excessive GC.Collect() calls needed to get the GC to behave deterministically.

# Platform Notes

I'm only testing this on Android/iOS. Please let me know if you have any issues on other platforms.

Also, the nature of leaks in MAUI often makes them platform-specific, so be sure to test all of your target platforms separately.

# Scoreboard
:white_square_button:: Discovered and/or isolated, but not mitigated

:wrench:: Compartmentalized with MemoryToolkit.Maui

:muscle:: Fixed with `AutoDisconnectBehavior` from MemoryToolkit.Maui

:white_check_mark:: Since fixed in source!


- :wrench: https://github.com/dotnet/maui/issues/20094 Page-level leak when using modal navigation in iOS.
- :wrench: https://github.com/dotnet/maui/issues/20119 Navigation page leaks on iOS unless DisconnectHandler() is called.
- :muscle: https://github.com/dotnet/maui/issues/20163 Border leaks when StrokeShape is set via global style (as the OOTB MAUI template does), cascading to the page.
- :muscle: https://github.com/dotnet/maui/issues/20195 ViewCells on iOS need Dispose() called on handler platform views to prevent leaks, which cascade to the page.
- :muscle: https://github.com/dotnet/maui/issues/20025 ListView on iOS needs Dispose() called on handler to prevent leak, which cascades to the page.
- :muscle: https://github.com/mono/SkiaSharp.Extended/issues/250 SKLottieView captures window Dispatcher as long as InAnimationEnabled is true, cascading to the page (* fix requires custom deconstruction hook).
- :muscle: https://github.com/dotnet/maui/issues/20121 iOS Border leaks when StrokeShape is set, cascading to the page.
- :white_check_mark: https://github.com/roubachof/Sharpnado.CollectionView/issues/110 Strong event subscription in renderer causes control to leak, cascading to the page.
- :white_check_mark: https://github.com/roubachof/Sharpnado.CollectionView/pull/112 Explicit cleanup required on iOS to avoid ref counting leak, cascading to the page.
- :white_check_mark: https://github.com/roubachof/Sharpnado.CollectionView/pull/113 Native resources disposed in wrong order causes leak cascading to the page.

# How to use

## Installation
This is still an early project and I'm iterating on it a lot (especially AutoDisconnectHandler). I'll publish a nuget when it's more stable. Until then, clone the repo and check back often :)

## Using GCMonitoredApplication for automatic leak detection instrumentation

The toolkit includes an Application subclass `GCMonitoredApplication`, which is used to instrument automatic leak logging/alerting. This library will probably move away from an Application base class in the future, but for now, it's a holdover from earlier iterations where GC monitoring relied on watching for navigation events.

To use, modify your App.xaml like:

```xaml
<mtk:GCMonitoredApplication xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
                            xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
                            xmlns:mtk="clr-namespace:MemoryToolkit.Maui;assembly=MemoryToolkit.Maui"
                            x:Class="Scavos.Maui.App"
                            ShowMemToolkitAlerts="True">
```
By default, a `GCMonitoredApplication` logs the GC status of collected objects at the Trace level. When monitored objects fail to be collected as expected, a message is logged at the Warn level, and a runtime UI alert is shown.

### Customizing GCMonitoredApplication
You may set the BindableProperty `ShowMemToolkitAlerts` to false (true is the default) to disable runtime UI alerts when leaks are detected.

You may also take total control of collection/leak event callbacks by subclassing `GCMonitoredApplication` and overriding `OnLeaked` or `OnCollected`.

## Monitor for leaks with GCMonitorBehavior.Cascade
`GCMonitorBehavior.Cascade` is an [attached behavior](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/behaviors?view=net-maui-8.0#attached-behaviors) that works in tandem with `GCCollectionMonitor` to register your app's pages/views and their [handlers](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/handlers/?view=net-maui-8.0) for GC collection monitoring.

Monitoring collection of a page/view (and all its subviews) is as simple as adding the attached property:

```xaml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:mtk="clr-namespace:MemoryToolkit.Maui;assembly=MemoryToolkit.Maui"
             x:Class="My.App.Views.SamplePage"
             mtk:GCMonitorBehavior.Cascade="True">
```
When set to 'True", this attached behavior will respond to the view's `Unloaded` event by walking the visual tree (via `GetVisualChildren()`) registering each element (and its handler) that it finds for expected garbage collection.

That's it! You can be sure GC monitoring is hooked up correctly by watching out for Trace logs:

<img src="https://github.com/AdamEssenmacher/MemoryToolkit.Maui/assets/8496021/913d4ee4-029e-482e-836d-c43df41ead87" height="200">

If you're unlucky enough to have discovered a leak, you'll see at least one error dialog:
<img src="https://github.com/AdamEssenmacher/MemoryToolkit.Maui/assets/8496021/6815c761-d5c6-4948-94ad-49bc446ba081" height="200">

**!IMPORTANT!** Since this behavior walks the visual tree on Unload, it will **not** catch subviews that may have been dynamically removed from the parent view. In these cases, consider adding another `GCMonitorBehavior.Cascade` property to the subview, or otherwise manually manage using `GCCollectionMonitor`.

### Suppressing GCMonitorBehavior.Cascade
When walking the visual tree, `GCMonitorBehavior.Cascade` will skip any view (and its subviews) if that view has the attached property `GCMonitorBehavior.Suppress` set to 'true'. You may wish to do this if you're already aware of a leak and wish to suppress further warnings. Or, in more advanced scenarios, you may not actually expect that view to be collectible once it is unloaded (for example, for view caching). In this situations, manually register such views with `GCCollectionMonitor` when you are done with them.

## Compartmentalize & prevent leaks with AutoDisconnectBehavior.Cascade
When `GCMonitorBehavior` finds leaks, you will likely be alarmed by _how many_ it finds. You're very likely to discover whole pages where **nothing** is being collected at all. You might even think this toolkit is reporting false positives. Surely _everything_ isn't leaking... right?

Unfortunately, leaks spread through MAUI views like a zombie virus. Out of the box, they'll typically compartmentalize at the Page level. Meaning, a leak of any size will grow to consume its entire host page. This is **bad news**. Particularly for NavigationPages!

There's some good news, though! The attached behavior `AutoDisconnectBehavior.Cascade` is surprisingly effective at preventing leaks (for reasons I'll explain later). When it can't prevent leaks, it will at least compartmentalize them--which both prevents them from infecting their host pages and helps isolate the offending control for further analysis.

Attaching this behavior is just like with `GCMonitorBehavior.Cascade`:
```xaml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:mtk="clr-namespace:MemoryToolkit.Maui;assembly=MemoryToolkit.Maui"
             x:Class="My.App.Views.SamplePage"
             mtk:GCMonitorBehavior.Cascade="True"
             mtk:AutoDisconnectBehavior.Cascade="True">
```

**!IMPORTANT!** When attaching both GCMonitorBehavior and AutoDisconnectBehavior, order matters. `AutoDisconnectBehavior.Cascade` is quite destructive. It achieves compartmentalization by tearing the visual tree apart, which will circumvent `GCMonitorBehavior`'s attempts to gather views for monitoring. So, make sure `AutoDisconnectBehavior` always comes after `GCMonitorBehavior`.

Just like with `GCMonitorBehavior`, `AutoDisconnectBehavior` offers an opt-out attached property `AutoDisconnectBehavior.Suppress` that can be used to exclude any view (and its subviews) from the effects of `AutoDisconnectBehavior.Cascade`

Also, just like with `GCMonitorBehavior`, `AutoDisconnectBehavior` will _not_ get a chance to run on views that may have been removed from the host view dynamically. In these cases, add another `AutoDisconnectBehavior.Cascade` property on the subview.

### How does AutoDisconnectBehavior work?
While quite effective, `AutoDisconnectBehavior.Cascade` is an extremely destructive double-edged blade. As such, it's important that you understand what it does.

#### Phase 1) Compartmentalization
**The first edge:** The behavior does its best to remove any references each view has to other views. It does this by calling `ClearLogicalChildren()` and setting other properties to null (such as `ItemsSource`, `Content`, and `Parent`) before finally clearing the BindingContext. If this step fails to remove references to other objects, the leak will spread. I expect that this process will improve as MemoryToolkit.Maui matures.

#### Phase 2) BindingContext/Reset
`AutoDisconnectBehavior` doesn't actually do anything active during this phase. With the MAUI view having been basically reset and its BindingContext cleared, its (still connected) Handler will (or at least, _should_) restore the underlying native platform control to a near-default state where leaks are least likely to happen.

#### Phase 3) Cleanup
**The second edge:** After giving the platform handlers their chance to react to a now-empty view, `AutoDisconnectHandler` calls `Dispose()` (if applicable) and then `DisconnectHandler()` on the view's Handler. It's incredibly absurd that MAUI's design left this method to never be called automatically by the framework, instead expecting us developers to call it for each and every image, frame, label, and button in our apps. Using `AutoDisconnectBehavior.Cascade` effectively reverses this approach, making automatic view cleanup on Unload 'opt-out' instead of 'opt-in'.

#### Advanced Use: Custom deconstruction hook

In some cases, known leaks may be worked around by whacking the control into a safe state when we're done with it. For example, an `SKLottieView` from SkiaSharp is known to leak as long as its `IsAnimationEnabled` property is True. The `AutoDisconnectBehavior` class offers a static event `OnDisconnectingHandler` that is invoked immediately before each call to `DisconnectHandler()`. You may use this hook to examine the view and change its state.

## ControlTemplates
A common use of the `ControlTemplate` is to change the appearance of a control at run time. For example, https://github.com/roubachof/Sharpnado.TaskLoaderView uses different control templates to show different views based on some loading state (e.g. loading, loaded, error). Whenever ControlTemplates are being used in this way, it's a good idea to use the above attached properties on a per-template basis.

# More resources
https://github.com/dotnet/maui/wiki/Memory-Leaks
