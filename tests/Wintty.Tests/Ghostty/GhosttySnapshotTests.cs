using Wintty.Core.Abstractions;
using Wintty.Ghostty;
using Xunit;

namespace Wintty.Tests.Ghostty;

/// <summary>Verifies screen contents read back from the real libghostty render state.</summary>
[Trait("Category", "Integration")]
[Collection("NativeEnvironment")]
public class GhosttySnapshotTests
{
    private static readonly GhosttyGateway Gateway = new();

    private static ITerminalEngine Engine(int columns = 20, int rows = 5)
    {
        var engine = Gateway.Create(new TerminalEngineOptions(columns, rows));
        return engine;
    }

    private static void Feed(ITerminalEngine engine, string text) =>
        engine.Write(System.Text.Encoding.UTF8.GetBytes(text));

    [Fact]
    public void BlankScreen_HasRequestedSizeAndEmptyRows()
    {
        using var engine = Engine(12, 4);

        var snapshot = engine.CreateSnapshot();

        Assert.Equal(12, snapshot.Columns);
        Assert.Equal(4, snapshot.Rows);
        for (var row = 0; row < snapshot.Rows; row++)
        {
            Assert.Equal(string.Empty, snapshot.GetRowText(row));
        }
    }

    [Fact]
    public void PlainText_AppearsOnFirstRowAndMovesCursor()
    {
        using var engine = Engine();

        Feed(engine, "hello");
        var snapshot = engine.CreateSnapshot();

        Assert.Equal("hello", snapshot.GetRowText(0));
        Assert.Equal(5, snapshot.Cursor.Column);
        Assert.Equal(0, snapshot.Cursor.Row);
        Assert.True(snapshot.Cursor.Visible);
    }

    [Fact]
    public void NewlinesMoveToFollowingRows()
    {
        using var engine = Engine();

        Feed(engine, "one\r\ntwo");
        var snapshot = engine.CreateSnapshot();

        Assert.Equal("one", snapshot.GetRowText(0));
        Assert.Equal("two", snapshot.GetRowText(1));
        Assert.Equal(1, snapshot.Cursor.Row);
    }

    [Fact]
    public void ForegroundColour_IsResolvedToRgb()
    {
        using var engine = Engine();

        Feed(engine, "\u001b[38;2;10;20;30mA\u001b[0mB");
        var snapshot = engine.CreateSnapshot();

        Assert.Equal(new TerminalColor(10, 20, 30), snapshot[0, 0].Foreground);
        Assert.Equal(snapshot.DefaultForeground, snapshot[1, 0].Foreground);
    }

    [Fact]
    public void PaletteColour_IsResolvedToRgb()
    {
        using var engine = Engine();

        Feed(engine, "\u001b[31mR");
        var snapshot = engine.CreateSnapshot();

        Assert.NotEqual(snapshot.DefaultForeground, snapshot[0, 0].Foreground);
        Assert.True(snapshot[0, 0].Foreground.Red > snapshot[0, 0].Foreground.Green);
    }

    [Fact]
    public void BackgroundColour_IsReported()
    {
        using var engine = Engine();

        Feed(engine, "\u001b[48;2;1;2;3mX");
        var snapshot = engine.CreateSnapshot();

        Assert.Equal(new TerminalColor(1, 2, 3), snapshot[0, 0].Background);
        Assert.Equal(snapshot.DefaultBackground, snapshot[1, 0].Background);
    }

    [Fact]
    public void Attributes_AreReported()
    {
        using var engine = Engine();

        Feed(engine, "\u001b[1;3;4;7;9mX\u001b[0mY");
        var snapshot = engine.CreateSnapshot();

        var styled = snapshot[0, 0];
        Assert.True(styled.Attributes.HasFlag(CellAttributes.Bold));
        Assert.True(styled.Attributes.HasFlag(CellAttributes.Italic));
        Assert.True(styled.Attributes.HasFlag(CellAttributes.Inverse));
        Assert.True(styled.Attributes.HasFlag(CellAttributes.Strikethrough));
        Assert.Equal(UnderlineStyle.Single, styled.Underline);
        Assert.Equal(CellAttributes.None, snapshot[1, 0].Attributes);
        Assert.Equal(UnderlineStyle.None, snapshot[1, 0].Underline);
    }

    [Fact]
    public void WideCharacter_OccupiesTwoCells()
    {
        using var engine = Engine();

        Feed(engine, "中x");
        var snapshot = engine.CreateSnapshot();

        Assert.Equal("中", snapshot[0, 0].Text);
        Assert.Equal(2, snapshot[0, 0].Width);
        Assert.Equal(0, snapshot[1, 0].Width);
        Assert.Equal("x", snapshot[2, 0].Text);
        Assert.Equal("中x", snapshot.GetRowText(0));
    }

    [Fact]
    public void Emoji_IsKeptAsOneGrapheme()
    {
        using var engine = Engine();

        Feed(engine, "\U0001F600");
        var snapshot = engine.CreateSnapshot();

        Assert.Equal("\U0001F600", snapshot[0, 0].Text);
    }

    [Fact]
    public void HiddenCursor_IsReportedInvisible()
    {
        using var engine = Engine();

        Feed(engine, "\u001b[?25l");

        Assert.False(engine.CreateSnapshot().Cursor.Visible);
    }

    [Theory]
    [InlineData(2, CursorStyle.Block)]
    [InlineData(4, CursorStyle.Underline)]
    [InlineData(6, CursorStyle.Bar)]
    public void CursorShape_FollowsDecscusr(int code, CursorStyle expected)
    {
        using var engine = Engine();

        Feed(engine, $"\u001b[{code} q");

        Assert.Equal(expected, engine.CreateSnapshot().Cursor.Style);
    }

    [Fact]
    public void Snapshot_IsImmutableAfterFurtherWrites()
    {
        using var engine = Engine();
        Feed(engine, "first");
        var before = engine.CreateSnapshot();

        Feed(engine, "\r\u001b[2Ksecond");

        Assert.Equal("first", before.GetRowText(0));
        Assert.Equal("second", engine.CreateSnapshot().GetRowText(0));
    }

    [Fact]
    public void Resize_ChangesSnapshotSize()
    {
        using var engine = Engine(10, 3);

        engine.Resize(30, 8);
        var snapshot = engine.CreateSnapshot();

        Assert.Equal(30, snapshot.Columns);
        Assert.Equal(8, snapshot.Rows);
    }

    [Fact]
    public void AfterDispose_CreateSnapshotThrows()
    {
        var engine = Engine();
        engine.Dispose();

        Assert.Throws<ObjectDisposedException>(() => engine.CreateSnapshot());
    }
}
