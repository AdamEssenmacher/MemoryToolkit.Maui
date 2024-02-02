namespace MemoryToolkit.Maui;

public static class Utilities
{
    public static T? GetFirstSelfOrParentOfType<T>(Element element) where T : class
    {
        if (element is T selfResult)
            return selfResult;

        Element? parent = element.Parent;
        while (parent is not null)
        {
            if (parent is T parentResult)
                return parentResult;

            parent = parent.Parent;
        }

        return null;
    }
}