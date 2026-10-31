using System.Text;
using Wintty.Core.Abstractions;
using Wintty.Ghostty;
using Xunit;

namespace Wintty.Tests.Ghostty;

/// <summary>Checks the replies the real libghostty engine produces for terminal queries.</summary>
[Trait("Category", "Integration")]
[Collection("NativeEnvironment")]
public class GhosttyQueryResponseTests
{
    private static readonly GhosttyGateway Gateway = new();

    private static (ITerminalEngine Engine, List<string> Replies) Create(bool setCellSize = true)
    {
        var engine = Gateway.Create(new TerminalEngineOptions(80, 24));
        if (setCellSize)
        {
            engine.SetCellSize(9, 19);
        }

        var replies = new List<string>();
        engine.ResponseHandler = data => replies.Add(Encoding.UTF8.GetString(data));
        return (engine, replies);
    }

    private static string Ask(ITerminalEngine engine, List<string> replies, string query)
    {
        replies.Clear();
        engine.Write(Encoding.UTF8.GetBytes(query));
        return string.Concat(replies).Replace("\u001b", "<ESC>");
    }

    [Fact]
    public void DeviceAttributes_AreReported()
    {
        var (engine, replies) = Create();
        using (engine)
        {
            Assert.Equal("<ESC>[?62;22c", Ask(engine, replies, "\u001b[c"));
            Assert.Equal("<ESC>[>1;0;0c", Ask(engine, replies, "\u001b[>c"));
            Assert.StartsWith("<ESC>P!|", Ask(engine, replies, "\u001b[=c"));
        }
    }

    [Fact]
    public void StatusAndCursorPosition_AreReported()
    {
        var (engine, replies) = Create();
        using (engine)
        {
            Assert.Equal("<ESC>[0n", Ask(engine, replies, "\u001b[5n"));
            Assert.Equal("<ESC>[1;1R", Ask(engine, replies, "\u001b[6n"));

            engine.Write("hello\r\nab"u8);

            Assert.Equal("<ESC>[2;3R", Ask(engine, replies, "\u001b[6n"));
        }
    }

    [Fact]
    public void TerminalVersion_IsReported()
    {
        var (engine, replies) = Create();
        using (engine)
        {
            Assert.Equal("<ESC>P>|libghostty<ESC>\\", Ask(engine, replies, "\u001b[>q"));
        }
    }

    [Fact]
    public void ModeQueries_ReportTheCurrentState()
    {
        var (engine, replies) = Create();
        using (engine)
        {
            Assert.Equal("<ESC>[?1;2$y", Ask(engine, replies, "\u001b[?1$p"));

            engine.Write("\u001b[?1h"u8);

            Assert.Equal("<ESC>[?1;1$y", Ask(engine, replies, "\u001b[?1$p"));
            Assert.Equal("<ESC>[?2004;2$y", Ask(engine, replies, "\u001b[?2004$p"));
        }
    }

    [Fact]
    public void KittyKeyboardFlagsQuery_ReportsTheActiveFlags()
    {
        var (engine, replies) = Create();
        using (engine)
        {
            Assert.Equal("<ESC>[?0u", Ask(engine, replies, "\u001b[?u"));

            engine.Write("\u001b[>5u"u8);

            Assert.Equal("<ESC>[?5u", Ask(engine, replies, "\u001b[?u"));
        }
    }

    [Fact]
    public void SizeQueries_UseTheCellPixelSize()
    {
        var (engine, replies) = Create();
        using (engine)
        {
            Assert.Equal("<ESC>[4;456;720t", Ask(engine, replies, "\u001b[14t"));
            Assert.Equal("<ESC>[8;24;80t", Ask(engine, replies, "\u001b[18t"));
            Assert.Equal("<ESC>[6;19;9t", Ask(engine, replies, "\u001b[16t"));
        }
    }

    [Fact]
    public void SizeQueries_FollowResizesAndNewCellSizes()
    {
        var (engine, replies) = Create();
        using (engine)
        {
            engine.Resize(100, 30);
            Assert.Equal("<ESC>[4;570;900t", Ask(engine, replies, "\u001b[14t"));

            engine.SetCellSize(10, 20);
            Assert.Equal("<ESC>[4;600;1000t", Ask(engine, replies, "\u001b[14t"));
        }
    }

    [Fact]
    public void BeforeACellSizeIsKnown_CharacterSizeIsAnsweredAndPixelsReportZero()
    {
        var (engine, replies) = Create(setCellSize: false);
        using (engine)
        {
            Assert.Equal("<ESC>[8;24;80t", Ask(engine, replies, "\u001b[18t"));
            Assert.Equal("<ESC>[4;0;0t", Ask(engine, replies, "\u001b[14t"));
        }
    }

    [Fact]
    public void SetCellSize_RejectsNonPositiveValues()
    {
        var (engine, _) = Create(setCellSize: false);
        using (engine)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => engine.SetCellSize(0, 19));
            Assert.Throws<ArgumentOutOfRangeException>(() => engine.SetCellSize(9, 0));
        }
    }

    [Fact]
    public void WithoutAHandler_QueriesAreIgnoredSafely()
    {
        using var engine = Gateway.Create(new TerminalEngineOptions(80, 24));

        engine.Write("\u001b[c\u001b[6n\u001b[>q"u8);

        Assert.Null(engine.ResponseHandler);
    }

    [Fact]
    public void AFailingHandler_DoesNotCrashOrStopTheTerminal()
    {
        using var engine = Gateway.Create(new TerminalEngineOptions(80, 24));
        engine.ResponseHandler = _ => throw new InvalidOperationException("boom");

        engine.Write("\u001b[c"u8);
        engine.Write("still alive"u8);

        Assert.Equal("still alive", engine.CreateSnapshot().GetRowText(0));
    }

    [Fact]
    public void ConfiguredDefaultColours_AreUsedForDrawing()
    {
        using var engine = Gateway.Create(new TerminalEngineOptions(80, 24, Foreground: new TerminalColor(10, 20, 30), Background: new TerminalColor(1, 2, 3)));

        var snapshot = engine.CreateSnapshot();

        Assert.Equal(new TerminalColor(10, 20, 30), snapshot.DefaultForeground);
        Assert.Equal(new TerminalColor(1, 2, 3), snapshot.DefaultBackground);
    }
}
