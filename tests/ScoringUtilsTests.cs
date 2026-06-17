using System.Collections.Generic;
using AICopyrightReproducibility.Config;
using AICopyrightReproducibility.Utils;
using Xunit;

namespace AICopyrightReproducibility.Tests;

public class ScoringUtilsTests
{
    // ── ComputeStats ────────────────────────────────────────────────────────

    [Fact]
    public void ComputeStats_OddCount()
    {
        var (mean, median, _) = ScoringUtils.ComputeStats([1f, 2f, 3f]);
        Assert.Equal(2f, mean);
        Assert.Equal(2f, median);
    }

    [Fact]
    public void ComputeStats_EvenCount()
    {
        var (mean, median, _) = ScoringUtils.ComputeStats([1f, 3f]);
        Assert.Equal(2f, mean);
        Assert.Equal(2f, median);
    }

    [Fact]
    public void ComputeStats_WithMode()
    {
        var (_, _, mode) = ScoringUtils.ComputeStats([1.0f, 1.0f, 2.0f]);
        Assert.NotNull(mode);
        Assert.Equal(1.0f, mode!.Value, precision: 1);
    }

    [Fact]
    public void ComputeStats_NoMode_WhenAllUnique()
    {
        var (_, _, mode) = ScoringUtils.ComputeStats([1.0f, 2.0f, 3.0f]);
        Assert.Null(mode);
    }

    // ── ScoreRecord — list task ─────────────────────────────────────────────

    [Fact]
    public void ScoreRecord_ListTask_PerfectMatch()
    {
        string[] sections = ["Alpha", "Beta", "Gamma"];
        RunRecord rec = MakeListRecord(["Alpha", "Beta", "Gamma"]);
        ScoringUtils.ScoreRecord(rec, MakeBound(sections, listTask: true));

        Assert.Equal(3, rec.ExactMatches);
        Assert.Equal(3, rec.Coverage);
        Assert.Equal(0, rec.Hallucinations);
        Assert.True(rec.Li1First);
    }

    [Fact]
    public void ScoreRecord_ListTask_PartialMatch()
    {
        string[] sections = ["Alpha", "Beta", "Gamma"];
        RunRecord rec = MakeListRecord(["Alpha", "Gamma"]);
        ScoringUtils.ScoreRecord(rec, MakeBound(sections, listTask: true));

        Assert.Equal(2, rec.ExactMatches);
        Assert.Equal(2, rec.Coverage);
        Assert.Equal(0, rec.Hallucinations);
    }

    [Fact]
    public void ScoreRecord_ListTask_Hallucinations()
    {
        string[] sections = ["Alpha", "Beta"];
        RunRecord rec = MakeListRecord(["Alpha", "Delta", "Epsilon"]);
        ScoringUtils.ScoreRecord(rec, MakeBound(sections, listTask: true));

        Assert.Equal(1, rec.ExactMatches);
        Assert.Equal(1, rec.Coverage);
        Assert.Equal(2, rec.Hallucinations);
    }

    [Fact]
    public void ScoreRecord_ListTask_AliasResolution()
    {
        string[] sections = ["Alpha"];
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            { ["a"] = "Alpha" };
        RunRecord rec = MakeListRecord(["A"]);
        ScoringUtils.ScoreRecord(rec, MakeBound(sections, listTask: true, aliases: aliases));

        Assert.Equal(1, rec.ExactMatches);
        Assert.Equal(1, rec.Coverage);
        Assert.Equal(0, rec.Hallucinations);
    }

    // ── ScoreRecord — order task ────────────────────────────────────────────

    [Fact]
    public void ScoreRecord_OrderTask_PerfectOrder()
    {
        string[] sections = ["A", "B", "C"];
        RunRecord rec = MakeListRecord(["A", "B", "C"]);
        ScoringUtils.ScoreRecord(rec, MakeBound(sections, listTask: true, orderTask: true));

        Assert.Equal(0, rec.MinMoves);
        Assert.Equal(100f, rec.OrderPct);
    }

    [Fact]
    public void ScoreRecord_OrderTask_Reversed()
    {
        string[] sections = ["A", "B", "C"];
        RunRecord rec = MakeListRecord(["C", "B", "A"]);
        ScoringUtils.ScoreRecord(rec, MakeBound(sections, listTask: true, orderTask: true));

        // LIS of [2,1,0] has length 1 → MinMoves = 3-1 = 2
        Assert.Equal(2, rec.MinMoves);
    }

    [Fact]
    public void ScoreRecord_OrderTask_OneMoved()
    {
        string[] sections = ["A", "B", "C", "D"];
        RunRecord rec = MakeListRecord(["B", "C", "D", "A"]);
        ScoringUtils.ScoreRecord(rec, MakeBound(sections, listTask: true, orderTask: true));

        // LIS of [1,2,3,0] is [1,2,3] length 3 → MinMoves = 4-3 = 1
        Assert.Equal(1, rec.MinMoves);
    }

    // ── ScoreRecord — title / textbook hit ─────────────────────────────────

    [Fact]
    public void ScoreRecord_TitleHit_CaseInsensitive()
    {
        RunRecord rec = MakeListRecord([]);
        rec.ContentText = "This response mentions TORONTO NOTES 2022 somewhere.";
        rec.Status = 200;
        ScoringUtils.ScoreRecord(rec, MakeBound([], titleShort: "Toronto Notes 2022"));

        Assert.True(rec.TitleHit);
    }

    [Fact]
    public void ScoreRecord_TextbookHit()
    {
        var extras = new Dictionary<string, string> { ["textbook.short"] = "Bates Guide" };
        RunRecord rec = MakeListRecord([]);
        rec.ContentText = "See Bates Guide for more.";
        rec.Status = 200;
        ScoringUtils.ScoreRecord(rec, MakeBound([], titleExtras: extras));

        Assert.True(rec.TextbookHit);
    }

    // ── ScoreRecord — semantic hash gating ─────────────────────────────────

    [Fact]
    public void ScoreRecord_SemanticSha256_SetWhenOk()
    {
        RunRecord rec = MakeListRecord(["Alpha"]);
        rec.Status = 200;
        ScoringUtils.ScoreRecord(rec, MakeBound(["Alpha"], listTask: true));

        Assert.NotNull(rec.SemanticSha256);
    }

    [Fact]
    public void ScoreRecord_SemanticSha256_NullWhenError()
    {
        RunRecord rec = MakeListRecord(["Alpha"]);
        rec.Status = 500;
        ScoringUtils.ScoreRecord(rec, MakeBound(["Alpha"], listTask: true));

        Assert.Null(rec.SemanticSha256);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static RunRecord MakeListRecord(string[] lists) => new RunRecord
    {
        Lists = lists,
        Status = 200
    };

    private static BoundPrompt MakeBound(
        string[] sections,
        bool listTask = false,
        bool orderTask = false,
        string titleShort = "title",
        Dictionary<string, string>? aliases = null,
        Dictionary<string, string>? titleExtras = null) => new BoundPrompt
    {
        QueryLabel         = "q",
        TextLabel          = "t",
        TitleFull          = "Full Title",
        TitleShort         = titleShort,
        Sections           = sections,
        ListTask           = listTask,
        OrderTask          = orderTask,
        Aliases            = aliases ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
        TitleExtras        = titleExtras ?? new Dictionary<string, string>(),
        SystemMessage      = "",
        UserPromptTemplate = ""
    };
}
