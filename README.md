# Overview

MAUI leaks like a toddler's sippy cup. It's messy, gross, and helplessly unavoidable.

MemoryToolkit.Maui offers three helpful features to help manage this problem:

- **Detects leaks** in MAUI views/pages, and notifies you when they occur at runtime.      
- **Compartmentalizes & prevents _some_ leaks** by breaking apart pages and views when they're no longer needed.
- **Prevents _some_ leaks and ensures native resources are cleaned up** by automatically calling `DisconnectHandler()`, `Dispose()`, and conducting other targeted cleanup measures on view/page handlers.

# Sample Demonstration
A sample MAUI project is included that demonstrates the severity of the issue, along with the toolkit's ability to detect and eliminate it. **The demonstration is meant to be run on iOS.**

## Observing a Leak
The sample is a Shell app with a simple page that shows a scrollable list of 100 random photos from https://picsum.photos. Two buttons allow you to either push a new instance of the page on the navigation stack, or pop the current page. The current (managed) heap size is also displayed:

<img src="https://github.com/AdamEssenmacher/MemoryToolkit.Maui/assets/8496021/9d5c8a60-5bc8-4b2a-aec5-25826e412bd7" height="200">

Right off the bat, the app consumes ~38 MB of managed memory. An empty MAUI app uses ~7-8MB (at least on iOS). The other 30 MB is artificial for the sake of demonstration. The `ListView`'s `ItemSource` property has been set to a collection of mocked-out 'view model' objects that each contain a 300KB byte array. Most view models probably won't be this big naturally, but it's definitely in the realm of possibility. Also, it's important to realize that the sample app is not reporting on memory used on the native side, which could easily be a couple 100 KB per item since we're showing images. The point here is that while the situation is contrived, it fairly demonstrates how available memory is quickly consumed by a MAUI app.

To demonstrate a leak for yourself, push & pop the page a few times. Each time you push, you'll see that our heap size increases by ~30 MB. This is expected given our contrived design--we need each page to stay in memory so we can return to it later via the 'Pop' button. These actions simulate a user navigating to and away from pages in your app.

After several push/pop cycles, the heap size will increase to several hundred MB. This is a _lot_ for a mobile app, and will eventually lead to the OS terminating it.

## Detecting Individual Leaks
You might have noticed in the previous test that the on-screen "Leaks Detected" counter remains at 0. This is because we haven't enabled the leak detection feature of the toolkit yet. Open `MainPage.xaml` and change the value of the attached property `mtk:GCMonitorBehavior.Cascade="False"` to 'True' and re-run your test. You'll notice that each time you push a new page, no new leaks are detected. However, each time you pop a page, several dozen leaks will be detected after a short delay (even more if you've scrolled around a bit).

If you check out your debug output, you'll also see that each leaked Element / Handler has been logged as a warning:

<img src="https://github.com/AdamEssenmacher/MemoryToolkit.Maui/assets/8496021/2765c2e3-075a-4426-bbb4-6135a9ff5dc4" height="400">

## Fixing the Leaks
Now for the ✨magic✨! Open `MainPage.xaml` again and change the value of the attached property `mtk:AutoDisconnectBehavior.Cascade="False"` to 'True'. Re-run your test, and you'll find that **0** leaks are detected. Event better: each time a page is popped, the memory it was using is reclaimed. You can push & pop all you want--the app will always return to its 'baseline' state of using ~38MB of managed memory.

I suspect most devs will be curious about where the offending leak was, and how `AutoDisconnectBehavior` fixed it. There are actually a handful of leaks in this example. `ListView` leaks. `Border` leaks. Global styles leak. `ViewCell` leaks. There might even be more. `AutoDisconnectBehavior` applies a broad approach that seems to 'just work' most of the time. Read on for details.

# Understanding the Underlying Problem
There are two core architectural issues behind MAUI's systemic memory problem.

## Problem 1: Poor leak compartmentalization
Memory leaks spread through MAUI pages like a zombie virus. Out of the box, they'll typically compartmentalize at the Page level. Meaning, **a leak of any size will grow to consume its entire host page**. This is _bad news_... particularly for `NavigationPages`! Naval vessels are built with compartmentalized designs to prevent a minor leak from becoming a catastrophic one. MAUI's design makes no attempt to contain leaks when they happen.

## Problem 2: Poor component lifecycle management
Individual controls (e.g. `ListView`, `Border`, `Entry`, etc.) may be implemented in such a way that they **require** explicit cleanup (i.e. via calls to `DisconnectHandler()` and/or `Dispose()` to avoid memory leaks. (This is particularly true on Apple platforms where cyclic references are not handled by the garbage collector.) It is _critical_ that these cleanup methods are called, but MAUI provides no mechanism (such as a standard component lifecycle) to do so for you. They'll say (even proudly) that this is "by design". The justification is that MAUI should not make any assumptions about when the developer is 'done with' a given element. For example, a view might be cached or getting moved between pages. Even if this argument held any water, MAUI _still_ doesn't offer developers a standard mechanism to easily and intentionally manage this problem. Enter MemoryToolkit.Maui!

### Defining 'done with'

MemoryToolkit.Maui makes the _daring_ assumption that developers are usually 'done with' an `Element` when it is no longer reachable in the current navigation stack. This condition is considered met when any of the following are true:

- The `Element`'s `Page` (or itself, if the `Element` is a `Page`) was just popped off the navigation stack.
- The `Element` has been unloaded and is not (or no longer) hosted within a `Page` (e.g. a `ControlTemplate` that was just swapped out).
- The `Element` is hosted within a `NavigationPage` that has been unloaded (this can be temporarily ignored; see the 'Advanced Use' section below).

Out of the box, MemoryToolkit.Maui uses this definition to automatically apply leak monitoring, prevention, and compartmentalization features.

This definition is likely incomplete (we probably need to consider things like nested modal navigation and tabbed pages), but I think it's a good starting point. In cases where this definition doesn't apply (e.g. cached pages), MemoryToolkit.Maui still offers tools so developers can take direct control over monitoring and managing component lifecycles.

# How to use

## Installation
This is still an early project and I'm iterating on it a lot (especially AutoDisconnectHandler). I'll publish a nuget when it's more stable. Until then, clone the repo and check back often :)

## Automatic Leak Detection
The toolkit includes an `Application` subclass `GCMonitoredApplication`, which is used to instrument automatic leak logging/alerting. This library may move away from an `Application` base class in the future, but for now, it's a holdover from earlier iterations where GC monitoring relied on watching for navigation events.

## Warning!
While leak prevention & compartmentalization features are intended to be safe for production use, you might not want to use leak detection in release builds due to excessive `GC.Collect()` calls needed to get the GC to behave deterministically.

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
When set to 'True', this attached behavior will respond to the `VisualElement`'s `Unloaded` event. It does some work to determine if you're 'done with' the element (see the definition above) and, if so, walks the `Element`'s visual tree (via `GetVisualChildren()`), registering each `Element` (and its `Handler`) that it finds for expected garbage collection. It will then force some GC runs to get the garbage collector to behave deterministically, check if our monitored objects were collected or not, and finally report the results.

That's it! You can be sure GC monitoring is hooked up correctly by watching out for Trace logs:

<img src="https://github.com/AdamEssenmacher/MemoryToolkit.Maui/assets/8496021/913d4ee4-029e-482e-836d-c43df41ead87" height="200">

If you're unlucky enough to have discovered a leak, you'll see at least one error dialog:
<img src="https://github.com/AdamEssenmacher/MemoryToolkit.Maui/assets/8496021/6815c761-d5c6-4948-94ad-49bc446ba081" height="200">

Note that iOS limits the number of concurrent UI alerts, so be sure to check your Warn logs for a complete picture.

**!IMPORTANT!** Since this behavior walks the visual tree on Unload, it will **not** catch subviews that may have been dynamically removed from the parent view. In these cases, consider adding another `GCMonitorBehavior.Cascade` property to the subview, or manually monitor the view using `GCCollectionMonitor` directly.

### Suppressing GCMonitorBehavior.Cascade
When walking the visual tree, `GCMonitorBehavior.Cascade` will skip any view (and its subviews) if that view has the attached property `GCMonitorBehavior.Suppress` set to 'true'. You may wish to do this if you're already aware of a leak and wish to suppress further warnings. Or, in more advanced scenarios, you may not actually expect that view to be collectible according to our definition of 'done with' (for example, for view caching). In these situations, manually monitor the view with `GCCollectionMonitor` directly.

## Compartmentalize & prevent leaks with AutoDisconnectBehavior.Cascade
When `GCMonitorBehavior` finds leaks, you will likely be alarmed by _how many_ it finds. You're very likely to discover whole pages where **nothing** is being collected at all. You might even think this toolkit is reporting false positives. Surely _everything_ isn't leaking... right? 😳

The attached behavior `AutoDisconnectBehavior.Cascade` is surprisingly effective at preventing leaks (for reasons I'll explain later). When it can't prevent leaks, it will at least compartmentalize them--which both prevents them from infecting their host pages and helps isolate the offending control for further analysis.

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

Also, just like with `GCMonitorBehavior`, `AutoDisconnectBehavior` will _not_ get a chance to run on views that may have been removed from the host view dynamically. In these cases, add another `AutoDisconnectBehavior.Cascade` property on the subview. In the future, I intend to break out the code responsible for view deconstruction so developers can run this process manually as needed.

### How does AutoDisconnectBehavior work?
While quite effective, `AutoDisconnectBehavior.Cascade` is an extremely destructive double-edged blade. As such, it's important that you understand what it does. If it runs prematurely, it _will_ bork your app.

#### Phase 1) Compartmentalization
**The first edge:** The behavior does its best to remove any references each view has to other views. It does this by setting certain properties to null (such as `ItemsSource`, `Content`, and `Parent`), calling `ClearLogicalChildren()`, and finally clearing the `BindingContext`. If this step fails to remove references to other objects, the leak will spread. I expect that this process will improve as MemoryToolkit.Maui matures.

#### Phase 2) BindingContext/Reset
`AutoDisconnectBehavior` doesn't actually do anything active during this phase. With the MAUI view having been basically reset and its BindingContext cleared, its (still connected) Handler will (or at least, _should_) restore the underlying native platform control to a near-default state where leaks are least likely to happen.

#### Phase 3) Cleanup
**The second edge:** After giving the platform handlers their chance to react to a now-empty view, `AutoDisconnectHandler` calls `Dispose()` (if applicable) and then `DisconnectHandler()` on the view's Handler. Other targeted cleanup measures are also applied to address known leaks in MAUI.

#### Advanced Use

##### Custom Deconstruction Hook
In some cases, known leaks may be worked around by whacking the control into a safe state when we're done with it. For example, an `SKLottieView` from SkiaSharp is known to leak as long as its `IsAnimationEnabled` property is True. The `AutoDisconnectBehavior` class offers a static event `OnDisconnectingHandler` that is invoked immediately before each call to `DisconnectHandler()`. You may use this hook to examine the view and change its state (for example, to set an `SKLottieView`'s `IsAnimationEnabled` property to 'false').

##### Temporarily Unloaded NavigationPages
There are a few common-enough scenarios where you'll expect a `NavigationPage` to be unloaded only temporarily. For example, calling `Browser.OpenAsync(..)`. In these cases, you can temporarily set the 'Suppress' properties on the `NavigationPage` itself, which will cause all behaviors within the page to be ignored. Here's an example handler method:
```csharp
private void OnTapped(object? sender, TappedEventArgs e)
{
    var navigationPage = Utilities.GetFirstSelfOrParentOfType<NavigationPage>(this);
    if (navigationPage == null)
        return;

    GCMonitorBehavior.SetSuppress(navigationPage, true);
    AutoDisconnectBehavior.SetSuppress(navigationPage, true);
}
```
Be sure to set the 'Suppress' properties back to false later. The view's `Loaded` event is probably a good place for that.

## ControlTemplates
A common use of the `ControlTemplate` is to change the appearance of a control at run time. For example, https://github.com/roubachof/Sharpnado.TaskLoaderView uses different control templates to show different views based on some loading state (e.g. loading, loaded, error). Whenever ControlTemplates are being used in this way, it's a good idea to use the above attached properties on a per-template basis.

# Platform Notes

I'm only testing this on Android/iOS. Please let me know if you have any issues on other platforms.

Also, the nature of leaks in MAUI often makes them platform-specific, so be sure to test all of your target platforms separately.

# Scoreboard
:white_square_button:: Discovered and/or isolated, but not mitigated

:wrench:: Compartmentalized with MemoryToolkit.Maui

:muscle:: Fixed with `AutoDisconnectBehavior` from MemoryToolkit.Maui

:white_check_mark:: Since fixed in source!


- :wrench: https://github.com/dotnet/maui/issues/20094 Page-level leak when using modal navigation in iOS.
- :muscle: https://github.com/dotnet/maui/issues/20119 Navigation page leaks on iOS unless DisconnectHandler() is called.
- :muscle: https://github.com/dotnet/maui/issues/20163 Border leaks when StrokeShape is set via global style (as the OOTB MAUI template does), cascading to the page.
- :muscle: https://github.com/dotnet/maui/issues/20195 ViewCells on iOS need Dispose() called on handler platform views to prevent leaks, which cascade to the page.
- :muscle: https://github.com/dotnet/maui/issues/20025 ListView on iOS needs Dispose() called on handler to prevent leak, which cascades to the page.
- :muscle: https://github.com/mono/SkiaSharp.Extended/issues/250 SKLottieView captures window Dispatcher as long as InAnimationEnabled is true, cascading to the page (* fix requires custom deconstruction hook).
- :muscle: https://github.com/dotnet/maui/issues/20121 iOS Border leaks when StrokeShape is set, cascading to the page.
- :white_check_mark: https://github.com/roubachof/Sharpnado.CollectionView/issues/110 Strong event subscription in renderer causes control to leak, cascading to the page.
- :white_check_mark: https://github.com/roubachof/Sharpnado.CollectionView/pull/112 Explicit cleanup required on iOS to avoid ref counting leak, cascading to the page.
- :white_check_mark: https://github.com/roubachof/Sharpnado.CollectionView/pull/113 Native resources disposed in wrong order causes leak cascading to the page.

# More resources
https://github.com/dotnet/maui/wiki/Memory-Leaks
