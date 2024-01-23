# MemoryToolkit.Maui

## Features

- Detects memory leaks in MAUI views/pages, and notifies you when they occur at runtime.
- Compartmentalizes leaks by de-constructing views/pages when they're no longer needed.
- Automatically calls `DisconnectHandler()` on MAUI controls to help prevent leaks.

## Leaks discovered using this toolkit
- :white_square_button: https://github.com/dotnet/maui/issues/20094 Page-level leak when using modal navigation in iOS.
- :white_square_button: https://github.com/dotnet/maui/issues/20119 Navigation page leaks on iOS unless DisconnectHandler() is called
- :white_square_button: https://github.com/mono/SkiaSharp.Extended/issues/250 SKLottieView captures window Dispatcher as long as InAnimationEnabled is true
- :white_check_mark: https://github.com/roubachof/Sharpnado.CollectionView/issues/110 Strong event subscription in renderer causes control to leak, cascading to the page.
- :white_check_mark: https://github.com/roubachof/Sharpnado.CollectionView/pull/112 Explicit cleanup required on iOS to avoid ref counting leak, cascading to the page.

## Platform Support

I'm only testing this on Android/iOS. Please let me know if you have any issues on other platforms.

## Overview

Anyone paying attention knows that MAUI leaks like a toddler's sippy cup. Aside from these leaks being messy and gross, they also feel helplessly unavoidable. The root causes of this leakiness are so deeply ingrained in MAUI's architecture that we realistically have to accept it as just part of MAUI development.

MemoryToolkit.Maui assumes this is not a problem we can totally fix, and so instead aims to make this a problem we can at least manage. It addresses three key areas: leak detection, leak compartmentalization, and leak prevention.

### 1) Leak Detection

While detecting leaks in MAUI isn't necessarily *difficult*, the reality is that the vast majority of leaks just go unnoticed. From .NET 6 to 8, over a dozen [basic standard MAUI controls](https://github.com/dotnet/maui/issues/18365) were leaking.
The tools we have right now to detect memory leaks are limited, clunky, and can be difficult to use and understand. Check out the [offical guidance](https://github.com/dotnet/maui/wiki/Memory-Leaks#tooling-to-find-leaks). How likely are you to work generating and analyzing `.gcdump` files into your everyday dev cycles?

The MAUI team is doing good work to fix leaks in standard controls. They even have automated tests in place to prevent regressions! However, even now, the Border control leaks on iOS if the `StrokeShape` property is set. This demonstrates that some leaks are only going to manifest themselves in certain scenarios, and that we cannot rely on MAUI's quality control to catch them all. We need to monitor our own apps for leaks.

Wouldn't it be nice if we could just get a notification as soon as a leak happens? Maybe log a warning, or even display a UI alert? MemoryToolkit.Maui does this! It even lets you know which component(s) have leaked so you don't have to resort to the typical binary chop method to isolate it.

### 2) Leak Compartmentalization

Leaks happen... but they don't have to be *catastrophic*. Ideally, small leaks here and there shouldn't be a big deal. The problem with MAUI is that small leaks are virtually guaranteed to become big leaks, spreading through entire pages and navigation stacks like a virus.

Ship builders know how important it is to compartmentalize their ship designs. If a ship takes on water, it's important to keep that water contained to a single compartment. If the ship is compartmentalized, it can take on water and still stay afloat. If it's not, it will sink.

MAUI's architecture makes leak compartmentalization about as effective as the _RMS Titanic_'s. MemoryToolkit.Maui works to improve the situation by de-constructing your pages and views when they're no longer needed. This deconstruction usually won't prevent a leak (though sometimes it will!), but it can at least prevent leaks from spreading like a zombie apocalypse.

### 3) Leak Prevention (i.e. `DisconnectHandler()`)

For those who don't know, MAUI is an abstraction over native UI frameworks. This means that MAUI controls are not actually the UI controls you see on the screen (at least, not directly). Instead, they're just a representation of native controls, which are mapped via a `Handler`. These handlers are really important in MAUI. They're the bridge between the platform-agnostic MAUI world and the native platform world. We don't have to dig too deep into how Handlers work, but you should know that they have two very important methods: `ConnectHandler()` and `DisconnectHandler()`

These two methods pretty much do what it sounds like they might. `ConnectHandler()` is where the handler sets up the native control, wires up event subscriptions, and establishes any other resources it needs. DisconnectHandler() reverses the process, cleaning up the native control, unsubscribing from events, and disposing any other resources.

`ConnectHandler()` is a protected method, so it's not something you will ever need to worry about or call yourself unless you're writing your own Handler class. `DisconnectHandler()` is different. This method is public, and--due to an intentional design decision--never called by the MAUI framework. Instead, *you* are expected to call it. MAUI's explanation is that the framework has no way of knowing when we'll want a control to be cleaned up. That's fair to a degree. But still completely bonkers. The MAUI framework has no problem assuming when we'd want a control *initialized*....

I will note that calling `DisconnectHandler()` may have no practical effect. Depending on the control/platform, it might prevent a leak. It might not. It might help compartmentalize a leak. It might not. The point is, we don't know. Control authors will create components that require its invocation and feel 'correct' in doing so since the docs say it's OK. Bonkers.

If manually calling `DisconnectHandler()` on every label, image, button, border, and frame in your MAUI app isn't your idea of fun, MemoryToolkit.Maui has some good news for you. As part of the automated de-construction process, it also calls `DisconnectHandler()` for you, preventing an entire class of leaks with a single attached property.

## Using MemoryToolkit.Maui

### Leak Detection

MemoryToolkit.Maui makes detecting your leaky pages and views *during development* easy by providing an attached behavior that can be applied to a `Page` or any other `VisualElement`, `GCMonitorBehavior.Cascade`. When set to 'true', the behavior will hook into the VisualElement's `Unload` event. When triggered, it walks the visual tree and uses the library's `GCCollectionMonitor` class to create and track `WeakReference`s for each `VisualElement` (and their Handlers). The `GCCollectionMonitor` class exposes a method `ForceCollectionAsync(..)`, which forces a series full GC runs (a quirk necessary to get mono-based GCs to behave deterministically). After each GC run, the monitor checks to see if each of its tracked weak references is still alive. If a weak reference is no longer alive, this means the GC has collected it and the object has not leaked, so the weak reference is removed and an optional callback is triggered (i.e., for logging). If a tracked object remains alive through all collection attempts, this means the object has mostly likely leaked and a different optional callback is triggered.

The ideal time to call `ForceCollectionAsync(..)` is immediately after a navigation event that causes your tracked page or view to be permanently removed. Navigation patterns in MAUI applications are not standardized, so figuring out where to call this method is left to the developer.

#### GCMonitoredApplication

As a matter of convenience (as well as to provide an example), this library also offers an `Application` subclass `GCMonitoredApplication` that monitors for common navigation events and calls `ForceCollectionAsync(..)` automatically. This implementation only works for MAUI apps that use IWindowCreator for their root navigation (e.g. Prism), or for apps that set `MainPage` directly. I'm open to PRs extending this to Shell apps. My Shell experience is limited, as the first thing I usually do with a MAUI app is remove it with prejudice.

This convenience class logs tracked collections to a standard Microsoft `ILogger` at the trace level and leaks at the warn level. In addition, a UI alert is displayed when a leak is detected, so it is only meant to be used in development.

#### GCMonitorBehavior.Suppress

There may be situations where you want to prevent `GCMonitorBehavior.Cascade` from monitoring a subview. This could be because you're aware of a leak and want to suppress further warnings, or because you're using a view that you do not expect to be collected on unload (such as a view that is cached or reused). When walking the visual tree, `GCMonitorBehavior.Cascade` will skip any node with the attached property `GCMonitorBehavior.Suppress` set to 'true'.

#### Limitations

`GCMonitorBehavior.Cascade` works by walking the visual tree **when the Unload event occurs.** This means that it will **not** catch leaks that may have occured in child views that were dynamically removed from the parent view. In these situations, you may want to add another `GCMonitorBehavior.Cascade` in the subview. In more advanced scenarios (say, where you're caching views or moving them to a different page), use the `GCCollectionMonitor` directly to monitor views when you're done with them.

#### Production Use

Using the `GCMonitorBehavior.Cascade` and `GCCollectionMonitor` in production may not be desirable, as it forces several repeated full GC runs each time `ForceCollectionAsync(..)` is called. This may cause performance issues. However, since leaks can appear based on app state, this may be the lesser of two evils. Use with caution; feature flags are your friend.

### Leak Compartmentalization & Prevention

MemoryToolkit.Maui delivers these features through another simple attached property, `AutoDisconnectBehavior.Cascade`. The approach used here is very similar to `GCMonitorBehavior.Cascade`. The behavior hooks into the view's `Unload` event and walks the visual tree when triggered. Using a depth-first approach, and working from the bottom-up, the behavior calls `DisconnectHandler()` on each view before setting its `Parent` property to `null`--thus compartmentalizing any potential leaks.

#### Advanced Use: Custom deconstruction hook

In some cases, known leaks may be worked around by whacking the control into a safe state. For example, an `SKLottieView` from SkiaSharp is known to leak as long as its `IsAnimationEnabled` property is True. The `AutoDisconnectBehavior` class offers a static event `OnDisconnectingHandler` that is invoked immediately before the call to `DisconnectHandler()`. You may use this hook to examine the view and change its state.

#### IMPORTANT!!

If you're using both `GCMonitorBehavior.Cascade` and `AutoDisconnectBehavior.Cascade` attached properties, order matters. Since both walk the visual tree in response to the `Unload` event, `GCMonitorBehavior.Cascade()` should be attached *before* `AutoDisconnectBehavior.Cascade()`. You want the monitor behavior running first--before the auto disconnect behavior deconstructs the page.

#### Limitations

Just like with `GCMonitorBehavior.Cascade`, `AutoDisconnectBehavior.Cascade` will not be able to reach views that have been dynamically removed from a parent view.
