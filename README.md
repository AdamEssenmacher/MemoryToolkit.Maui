# Overview

MAUI memory behavior is much better than it was when this project started, but real apps can still leak views, handlers, binding contexts, and platform resources.

MemoryToolkit.Maui V2 is a .NET 10 rebaseline of the original toolkit. MAUI 9/10 introduced automatic handler disconnection and a `DisconnectHandlers()` API, so V2 defaults to a less destructive teardown path while keeping the original leak detection and containment tools available for apps that need them.

MemoryToolkit.Maui offers three primary features to help manage this problem:

- **Detects leaks** in MAUI views as they happen.
- **Requests cleanup for lifecycle-sensitive leaks** by using MAUI's handler disconnection path when views appear to be done.
- **Optionally compartmentalizes leaks** by clearing strong managed references such as `BindingContext`, MAUI behaviors, `Content`, `ItemsSource`, item templates, gesture recognizers, formatted text spans, and `Parent` when you need stronger fault containment.

If this project saves you time, money, or sanity, please consider [sponsoring me here on GitHub :heart:](https://github.com/sponsors/AdamEssenmacher)

# Quick Start
## Install

```nuget install AdamE.MemoryToolkit.Maui```

## Configure leak detection
Update `MauiProgram.cs`. Note, this is only required for leak detection and global V2 defaults. `TearDownBehavior` does not require configuration.
```c#
public static MauiApp CreateMauiApp()
{
    MauiAppBuilder builder = MauiApp.CreateBuilder();
    builder
        .UseMauiApp<App>();
    //...

#if DEBUG
    // Configure logging
    builder.Logging.AddDebug();
    
    // Ensure UseMemoryToolkit is called after logging has been configured!
    builder.UseMemoryToolkit(options =>
    {
        options.DefaultTearDownStrategy = TearDownStrategy.DisconnectHandlers;
        options.OnLeaked = collectionTarget =>
        {
            // This callback will run any time a leak is detected.
        };
    });
#endif

    return builder.Build();
}
```

## Detect leaks
Get started detecting leaks at runtime by adding the `LeakMonitorBehavior.Cascade` attached property to your views.
```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:mtk="clr-namespace:MemoryToolkit.Maui;assembly=MemoryToolkit.Maui"
             x:Class="ShellSample.MainPage"
             mtk:LeakMonitorBehavior.Cascade="True">
    <!-- All child views are now monitored for leaks. -->
</ContentPage>
```

If you're lucky enough to have no leaks, you should see info logs like:

<img src="https://github.com/AdamEssenmacher/MemoryToolkit.Maui/assets/8496021/913d4ee4-029e-482e-836d-c43df41ead87" height="200">

If you're not so lucky, you'll see warnings like:

<img src="https://github.com/AdamEssenmacher/MemoryToolkit.Maui/assets/8496021/2765c2e3-075a-4426-bbb4-6135a9ff5dc4" height="400">

If you've configured the callback as demonstrated above, you'll also see a runtime alert:

<img src="https://github.com/AdamEssenmacher/MemoryToolkit.Maui/assets/8496021/6815c761-d5c6-4948-94ad-49bc446ba081" height="200">

## Fix leaks
Once leaks have been detected, you can ask MemoryToolkit.Maui to apply an explicit teardown step when a view appears to be done. The V2 default uses MAUI's built-in `DisconnectHandlers()` path. If you also need to break managed references such as `BindingContext`, MAUI behaviors, `Content`, `ItemsSource`, item templates, gesture recognizers, formatted text spans, and `Parent`, opt into `Compartmentalize`.

```xml
<ContentPage xmlns="http://schemas.microsoft.com/dotnet/2021/maui"
             xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml"
             xmlns:mtk="clr-namespace:MemoryToolkit.Maui;assembly=MemoryToolkit.Maui"
             x:Class="ShellSample.MainPage"
             mtk:LeakMonitorBehavior.Cascade="True"
             mtk:TearDownBehavior.Cascade="True"
             mtk:TearDownBehavior.Strategy="DisconnectHandlers">
    <!-- Child views now request handler disconnection when they are done. -->
</ContentPage>
```
Note: When using both `LeakMonitorBehavior` and `TearDownBehavior`, make sure `TearDownBehavior` comes after `LeakMonitorBehavior` in the XAML.

## Warning!
While leak prevention & compartmentalization features are intended to be safe for production use, you might not want to use leak detection in release builds due to excessive `GC.Collect()` calls needed to get the GC to behave deterministically.

# User Guide

Warning: since the `LeakMonitorBehavior` works by walking the visual tree on Unload, it will **not** detect leaking subviews that may have been dynamically removed from the parent view. In these cases, consider adding another `LeakMonitorBehavior.Cascade` property to the subview.

## Suppressing Behaviors
Both `LeakMonitorBehavior` and `TearDownBehavior` offer an attached property `Suppress` that can be set to 'true' to exclude any view (and its subviews) from the effects of the behavior. This can be useful in cases where you're already aware of a leak and wish to suppress further warnings. Or perhaps you may not actually expect that view to be automatically monitored or torn down according to our definition of 'done with' (for example, for view caching).

## Custom Teardown Hook
In some cases, known leaks may be worked around by whacking the control into a safe state when we're done with it. For example, an `SKLottieView` from SkiaSharp once leaked as long as its `IsAnimationEnabled` property was set to True. The `TearDownBehavior` class offers a static `Action<object>` property `OnTearDown` that is invoked before the toolkit disconnects each element's handler in `Compartmentalize` mode. The default `DisconnectHandlers` strategy delegates directly to MAUI's tree-wide extension method and does not run this per-element hook.

## Teardown Strategies
V2 offers three teardown strategies:

- `DetectOnly`: do not tear down; useful for leak monitoring-only runs.
- `DisconnectHandlers`: call MAUI's built-in handler disconnection path. This is the V2 default.
- `Compartmentalize`: clear binding contexts, MAUI behaviors, parent/content references, item sources, item templates, gesture recognizers, formatted text spans, logical children, and resources before disconnecting handlers.

MAUI 9+ already disconnects handlers automatically in common cases, such as back navigation, and exposes `DisconnectHandlers()` for explicit cleanup. MemoryToolkit.Maui keeps `DisconnectHandlers` as the default because it gives the toolkit a low-destruction cleanup action when its lifecycle inference says a view is done. Use `Compartmentalize` when you are validating leak propagation or need fault containment.
`Compartmentalize` clears the `VisualElement.Behaviors` collection. MemoryToolkit's own `LeakMonitorBehavior` and `TearDownBehavior` hooks use attached properties, so they are not stored in that collection.
Managed-reference clearing is best-effort: if a control throws while one reference is being cleared, the exception is logged and teardown continues with the remaining cleanup steps.

## Temporarily Unloaded NavigationPages
There are a few common-enough scenarios where you'll expect a `NavigationPage` to be unloaded only temporarily. For example, calling `Browser.OpenAsync(..)`. In these cases, you can temporarily set the 'Suppress' properties on the `NavigationPage` itself, which will cause all behaviors within the page to be ignored. Here's an example handler method:
```csharp
private void OnTapped(object? sender, TappedEventArgs e)
{
    var navigationPage = Utilities.GetFirstSelfOrParentOfType<NavigationPage>(this);
    if (navigationPage == null)
        return;

    LeakMonitorBehavior.SetSuppress(navigationPage, true);
    TearDownBehavior.SetSuppress(navigationPage, true);
}
```
Be sure to set the 'Suppress' properties back to false later. The view's `Loaded` event is probably a good place for that.

## ControlTemplates
A common use of the `ControlTemplate` is to change the appearance of a control at run time. For example, https://github.com/roubachof/Sharpnado.TaskLoaderView uses different control templates to show different views based on some loading state (e.g. loading, loaded, error). Whenever ControlTemplates are being used in this way, it's a good idea to use the above attached properties on a per-template basis.

# How It Works

## Understanding the Underlying Problem
There are two core issues MemoryToolkit.Maui is designed around.

### Problem 1: Leak propagation through strong references
The V1/.NET 8-era failure mode was scary because small leaks could appear to retain much larger page graphs. Current MAUI is better: parent references are weak and handlers are disconnected automatically in common lifecycle cases, so a small leaked child is less likely to consume its whole host page.

That does not mean leak propagation is solved. A leaked control can still strongly retain its own `BindingContext`, `Content`, `ItemsSource`, item templates, resources, event subscriptions, commands, or platform objects. V2 keeps stronger graph clearing behind the opt-in `Compartmentalize` strategy for cases where you know a view is done and want to reduce the amount of state any remaining leak can keep alive.

### Problem 2: Poor component lifecycle management
Individual controls (e.g. `ListView`, `Border`, `Entry`, etc.) may be implemented in such a way that they need explicit handler cleanup to avoid memory leaks. This is particularly true on Apple platforms where cyclic references are not handled by the garbage collector. MAUI 9+ now disconnects handlers automatically in common cases and exposes `DisconnectHandlers()`, but apps can still need an intentional lifecycle signal when views are cached, moved, unloaded outside navigation, or need managed-reference containment.

## Defining 'done with'

MemoryToolkit.Maui's automatic behaviors are best-effort lifecycle inference. They run after a view unloads and make conservative guesses about when developers are usually 'done with' a view.

V2 currently treats a view as done in these cases:

- The view's host `Page` was popped from an active `NavigationPage`.
- The view has unloaded and is no longer hosted within a `Page` (e.g. a `ControlTemplate` was swapped out).
- The view is hosted within a `NavigationPage` that has itself unloaded and has not been suppressed.
- The view is not hosted within a `NavigationPage`, remains unloaded after a short delay, is no longer in a navigation/modal stack, and is not still hosted by Shell.

Automatic lifecycle inference skips `NavigationPage` containers themselves, because MAUI can keep navigation/modal stack state there while pages are temporarily unloaded. If you intentionally destroy a navigation container, call `TearDown()` directly.
It also skips views hosted by a `NavigationPage` while that navigation page still has active modal pages.

Out of the box, MemoryToolkit.Maui uses this definition to automatically apply leak monitoring and low-destruction handler disconnection. Compartmentalization is opt-in.

This is not comprehensive Shell, flyout, tab, or modal navigation tracking. It also cannot detect subviews that were dynamically removed before unload, and it should not be used as the only lifecycle signal for intentionally cached pages. In cases where this definition doesn't apply, MemoryToolkit.Maui still offers tools so developers can take direct control with `Monitor()` and `TearDown()`.

## How does TearDownBehavior work?
`TearDownBehavior.Cascade` runs when MemoryToolkit.Maui believes a view is done. The default strategy is intentionally low-destruction, but `Compartmentalize` is much more invasive and should only be used when you understand what it clears.

### Phase 1) Handler disconnection
By default, `TearDownBehavior` calls MAUI's built-in `DisconnectHandlers()` extension when a view appears to be done. This is the least destructive V2 strategy and does not clear managed references such as `BindingContext`, `Content`, or `Parent`.

### Phase 2) Optional compartmentalization
When `Strategy="Compartmentalize"` is set, the behavior also clears binding contexts and removes references each view has to other views. It does this by setting certain properties to null (such as `ItemsSource`, item templates, `Content`, and `Parent`), clearing MAUI behaviors, gesture recognizers, and formatted text spans, and calling `ClearLogicalChildren()`. If this step fails to remove references to other objects, the leak can still spread.

### Phase 3) Per-control cleanup hook
In `Compartmentalize` mode, `TearDownBehavior.OnTearDown` runs before each element's handler is disconnected. Use this only for targeted control state cleanup that is safe to run when the view is done.

# Sample App
A sample MAUI project is included that demonstrates runtime leak detection and opt-in containment with a deliberately leaky control. This is a contrived demo, not proof that current MAUI turns every small leak into a full-page leak. Current MAUI is better than that; the sample gives the toolkit a predictable leak to find. **The demonstration is meant to be run on iOS or Mac Catalyst.**

## Observe a Leak
The sample is a Shell app with a simple page that shows a scrollable list of 100 random photos from https://picsum.photos. Two buttons allow you to either push a new instance of the page on the navigation stack, or pop the current page. The current (managed) heap size is also displayed:

<img src="https://github.com/AdamEssenmacher/MemoryToolkit.Maui/assets/8496021/9d5c8a60-5bc8-4b2a-aec5-25826e412bd7" height="200">

Right off the bat, the app consumes ~38 MB of managed memory. An empty MAUI app uses ~7-8MB (at least on iOS). The other 30 MB is artificial for the sake of demonstration. The `CollectionView`'s `ItemsSource` property has been set to a collection of mocked-out 'view model' objects that each contain a 300KB byte array. Most view models probably won't be this big naturally, but it's definitely in the realm of possibility. Also, it's important to realize that the sample app is not reporting on memory used on the native side, which could easily be a couple 100 KB per item since we're showing images. The point here is that while the situation is contrived, it shows why retained view models or content graphs can matter.

To demonstrate a leak for yourself, push & pop the page a few times. Each time you push, you'll see that the heap size increases because a new page and its mocked data are now on the navigation stack. When you pop a page, the intentionally leaky label remains rooted by an app-level event subscription, so MemoryToolkit.Maui has a predictable leak to report.

In current MAUI, this single leaked label should not be assumed to retain the entire page and all of its data. The useful signal is whether the toolkit reports the leak and whether stronger cleanup reduces the amount of managed state a real leak can retain.

## Detect Leaks
`LeakMonitorBehavior.Cascade` is enabled in the sample by default. Each time you push a new page, no new leaks should be detected because the old page is still on the navigation stack. Each time you pop a page, the toolkit waits for the old page to become collectible and then reports anything still rooted after repeated garbage collections.

If you check out your debug output, you'll also see that each leaked Element / Handler has been logged as a warning.

## Contain Leaks
To try stronger containment, open `MainPage.xaml`, change `mtk:TearDownBehavior.Cascade="False"` to `True`, and set `mtk:TearDownBehavior.Strategy="Compartmentalize"`. This does not fix external roots such as the sample's intentionally leaked app-level event subscription, but it does clear managed references such as `BindingContext`, MAUI behaviors, `Content`, `ItemsSource`, item templates, gesture recognizers, formatted text spans, and `Parent` when the view is done.

Use the default `DisconnectHandlers` strategy when you want lower-destruction handler cleanup. Use `Compartmentalize` when you are validating propagation or trying to keep a known leak from retaining a larger managed object graph.
