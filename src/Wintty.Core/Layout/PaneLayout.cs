namespace Wintty.Core.Layout;

/// <summary>Identifies one pane (one terminal session) within a window.</summary>
/// <param name="Value">The number; unique within a <see cref="TabSet"/>.</param>
public readonly record struct PaneId(int Value);

/// <summary>Which way a split divides a pane.</summary>
public enum SplitDirection
{
    /// <summary>Two panes side by side; the new one is on the right.</summary>
    Right = 0,

    /// <summary>Two panes stacked; the new one is below.</summary>
    Down = 1,
}

/// <summary>A direction to move focus between panes.</summary>
public enum FocusDirection
{
    /// <summary>Towards the left edge.</summary>
    Left = 0,

    /// <summary>Towards the right edge.</summary>
    Right = 1,

    /// <summary>Towards the top edge.</summary>
    Up = 2,

    /// <summary>Towards the bottom edge.</summary>
    Down = 3,
}

/// <summary>A rectangle in layout units, which the caller maps to pixels.</summary>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="Width">Width.</param>
/// <param name="Height">Height.</param>
public readonly record struct PaneRect(double X, double Y, double Width, double Height)
{
    /// <summary>The right edge.</summary>
    public double Right => X + Width;

    /// <summary>The bottom edge.</summary>
    public double Bottom => Y + Height;
}

/// <summary>A node of the tree that arranges the panes of one tab: either a single pane or a split.</summary>
public abstract record PaneNode
{
    private protected PaneNode()
    {
    }
}

/// <summary>A single pane.</summary>
/// <param name="Id">The pane.</param>
public sealed record PaneLeaf(PaneId Id) : PaneNode;

/// <summary>Two nodes shown side by side or stacked.</summary>
/// <param name="Direction">How the space is divided.</param>
/// <param name="Ratio">The share of the space the first node gets, between 0 and 1 exclusive.</param>
/// <param name="First">The left or top node.</param>
/// <param name="Second">The right or bottom node.</param>
public sealed record PaneSplit(SplitDirection Direction, double Ratio, PaneNode First, PaneNode Second) : PaneNode;

/// <summary>Operations on a tree of panes. Every operation returns a new tree; trees are never changed in place.</summary>
public static class PaneLayout
{
    /// <summary>Lists the panes of a tree from left to right and top to bottom.</summary>
    /// <param name="root">The tree.</param>
    /// <returns>The pane ids in layout order.</returns>
    public static IEnumerable<PaneId> Leaves(PaneNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return root switch
        {
            PaneLeaf leaf => [leaf.Id],
            PaneSplit split => Leaves(split.First).Concat(Leaves(split.Second)),
            _ => [],
        };
    }

    /// <summary>Splits a pane in two, giving the original half the space.</summary>
    /// <param name="root">The tree.</param>
    /// <param name="target">The pane to split.</param>
    /// <param name="direction">Which way to split.</param>
    /// <param name="created">Id for the new pane.</param>
    /// <returns>The new tree, or <paramref name="root"/> unchanged when <paramref name="target"/> is not in it.</returns>
    public static PaneNode Split(PaneNode root, PaneId target, SplitDirection direction, PaneId created)
    {
        ArgumentNullException.ThrowIfNull(root);
        return root switch
        {
            PaneLeaf leaf when leaf.Id == target =>
                new PaneSplit(direction, 0.5, leaf, new PaneLeaf(created)),
            PaneSplit split => SplitChildren(split, target, direction, created),
            _ => root,
        };
    }

    // Returns the same node when nothing below it changed, so callers can tell nothing happened.
    private static PaneNode SplitChildren(PaneSplit split, PaneId target, SplitDirection direction, PaneId created)
    {
        var first = Split(split.First, target, direction, created);
        var second = Split(split.Second, target, direction, created);
        return ReferenceEquals(first, split.First) && ReferenceEquals(second, split.Second)
            ? split
            : split with { First = first, Second = second };
    }

    /// <summary>Removes a pane; its neighbour takes over the space.</summary>
    /// <param name="root">The tree.</param>
    /// <param name="target">The pane to remove.</param>
    /// <returns>The new tree, or <see langword="null"/> when the last pane was removed.</returns>
    public static PaneNode? Close(PaneNode root, PaneId target)
    {
        ArgumentNullException.ThrowIfNull(root);
        switch (root)
        {
            case PaneLeaf leaf:
                return leaf.Id == target ? null : root;
            case PaneSplit split:
                var first = Close(split.First, target);
                var second = Close(split.Second, target);
                if (first is null)
                {
                    return second;
                }

                return second is null ? first : split with { First = first, Second = second };
            default:
                return root;
        }
    }

    /// <summary>The smallest share of a split either side may be given.</summary>
    public const double MinimumRatio = 0.1;

    /// <summary>Moves the divider next to a pane, making one side larger and the other smaller.</summary>
    /// <param name="root">The tree.</param>
    /// <param name="target">The pane whose surrounding divider moves: the nearest split on the matching axis.</param>
    /// <param name="direction">Which way the divider moves.</param>
    /// <param name="amount">How far to move it, as a share of the split, between 0 and 1.</param>
    /// <returns>The new tree, or <paramref name="root"/> unchanged when there is no divider to move.</returns>
    public static PaneNode Resize(PaneNode root, PaneId target, FocusDirection direction, double amount)
    {
        ArgumentNullException.ThrowIfNull(root);
        var wanted = direction is FocusDirection.Left or FocusDirection.Right ? SplitDirection.Right : SplitDirection.Down;
        var sign = direction is FocusDirection.Right or FocusDirection.Down ? 1 : -1;
        return ResizeIn(root, target, wanted, sign * amount) ?? root;
    }

    /// <summary>Gives every split an equal share.</summary>
    /// <param name="root">The tree.</param>
    /// <returns>The new tree.</returns>
    public static PaneNode Equalize(PaneNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return root is PaneSplit split
            ? split with { Ratio = 0.5, First = Equalize(split.First), Second = Equalize(split.Second) }
            : root;
    }

    // Returns null when this subtree holds no suitable divider, so the caller can try further up.
    private static PaneNode? ResizeIn(PaneNode node, PaneId target, SplitDirection wanted, double delta)
    {
        if (node is not PaneSplit split || !Leaves(split).Contains(target))
        {
            return null;
        }

        // Prefer the innermost matching divider around the target.
        var inFirst = Leaves(split.First).Contains(target);
        var inner = ResizeIn(inFirst ? split.First : split.Second, target, wanted, delta);
        if (inner is not null)
        {
            return inFirst ? split with { First = inner } : split with { Second = inner };
        }

        if (split.Direction != wanted)
        {
            return null;
        }

        return split with { Ratio = Math.Clamp(split.Ratio + delta, MinimumRatio, 1 - MinimumRatio) };
    }

    /// <summary>Works out where each pane goes.</summary>
    /// <param name="root">The tree.</param>
    /// <param name="bounds">The area to fill.</param>
    /// <returns>The rectangle of every pane.</returns>
    public static IReadOnlyDictionary<PaneId, PaneRect> Arrange(PaneNode root, PaneRect bounds)
    {
        ArgumentNullException.ThrowIfNull(root);
        var result = new Dictionary<PaneId, PaneRect>();
        Place(root, bounds, result);
        return result;
    }

    /// <summary>Finds the pane that focus moves to from another pane.</summary>
    /// <param name="root">The tree.</param>
    /// <param name="bounds">The area the tree fills.</param>
    /// <param name="from">The focused pane.</param>
    /// <param name="direction">Where to move.</param>
    /// <returns>
    /// The nearest pane in that direction whose edge overlaps <paramref name="from"/>, or <see langword="null"/>
    /// when <paramref name="from"/> is at that edge.
    /// </returns>
    public static PaneId? Neighbour(PaneNode root, PaneRect bounds, PaneId from, FocusDirection direction)
    {
        var rects = Arrange(root, bounds);
        if (!rects.TryGetValue(from, out var origin))
        {
            return null;
        }

        PaneId? best = null;
        var bestGap = double.MaxValue;
        foreach (var (id, rect) in rects)
        {
            if (id == from || !IsBeyond(origin, rect, direction) || !Overlaps(origin, rect, direction))
            {
                continue;
            }

            var gap = Gap(origin, rect, direction);
            if (gap < bestGap)
            {
                bestGap = gap;
                best = id;
            }
        }

        return best;
    }

    private static void Place(PaneNode node, PaneRect area, Dictionary<PaneId, PaneRect> output)
    {
        switch (node)
        {
            case PaneLeaf leaf:
                output[leaf.Id] = area;
                break;
            case PaneSplit split when split.Direction == SplitDirection.Right:
                var width = area.Width * split.Ratio;
                Place(split.First, area with { Width = width }, output);
                Place(split.Second, new PaneRect(area.X + width, area.Y, area.Width - width, area.Height), output);
                break;
            case PaneSplit split:
                var height = area.Height * split.Ratio;
                Place(split.First, area with { Height = height }, output);
                Place(split.Second, new PaneRect(area.X, area.Y + height, area.Width, area.Height - height), output);
                break;
        }
    }

    private const double Tolerance = 1e-6;

    private static bool IsBeyond(PaneRect origin, PaneRect other, FocusDirection direction) => direction switch
    {
        FocusDirection.Left => other.Right <= origin.X + Tolerance,
        FocusDirection.Right => other.X >= origin.Right - Tolerance,
        FocusDirection.Up => other.Bottom <= origin.Y + Tolerance,
        _ => other.Y >= origin.Bottom - Tolerance,
    };

    // The panes must share some stretch of the edge they would be crossing from.
    private static bool Overlaps(PaneRect origin, PaneRect other, FocusDirection direction) =>
        direction is FocusDirection.Left or FocusDirection.Right
            ? other.Y < origin.Bottom - Tolerance && other.Bottom > origin.Y + Tolerance
            : other.X < origin.Right - Tolerance && other.Right > origin.X + Tolerance;

    private static double Gap(PaneRect origin, PaneRect other, FocusDirection direction) => direction switch
    {
        FocusDirection.Left => origin.X - other.Right,
        FocusDirection.Right => other.X - origin.Right,
        FocusDirection.Up => origin.Y - other.Bottom,
        _ => other.Y - origin.Bottom,
    };
}
