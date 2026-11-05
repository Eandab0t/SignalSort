namespace SmartFileClassifier.Models;

/// <summary>
/// Options for sorting file classifications in the preview grid.
/// </summary>
public enum SortOrder
{
    OriginalName,
    Category,
    Purpose,
    NewName
}

/// <summary>
/// Extension methods for SortOrder.
/// </summary>
public static class SortOrderExtensions
{
    public static string GetDisplayName(this SortOrder order)
    {
        return order switch
        {
            SortOrder.OriginalName => "Original Name",
            SortOrder.Category => "Category",
            SortOrder.Purpose => "Purpose",
            SortOrder.NewName => "New Name",
            _ => order.ToString()
        };
    }
}
