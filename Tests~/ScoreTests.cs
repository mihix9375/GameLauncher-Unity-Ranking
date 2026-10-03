using System;
using System.Numerics;
using GameLauncher.Ranking;

public static class ScoreTests
{
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
    }
    public static void Main()
    {
        var a = RankingScore.Parse("100000000000000000000000000001");
        var b = RankingScore.Parse("100000000000000000000000000002");
        Check(a < b, "large integer lower digits remain exact");
        Check(a.ToBigInteger() == BigInteger.Parse("100000000000000000000000000001"), "BigInteger conversion");
        Check(((RankingScore)BigInteger.Pow(10, 5000)) == RankingScore.Parse("1e5000"), "large zero-filled BigInteger normalizes before digit limit");
        Check(RankingScore.Parse("1.2500") == RankingScore.Parse("125e-2"), "equivalent decimals");
        Check(RankingScore.Parse("-0") == default(RankingScore), "negative zero");
        Check(RankingScore.Parse("1e1000") > RankingScore.Parse("9e999"), "huge exponent comparison");
        Check(RankingScore.Parse("1e-1000") > 0L, "tiny exponent comparison");
        Check(RankingScore.Parse("-1e1000") < RankingScore.Parse("-9e999"), "negative exponent comparison");
        Check(RankingScore.Parse("9.99999e999").ToString() == "1e1000", "scientific rounding carry");
        Check(RankingScore.Parse("1.23456789e1000").ToString() == "1.235e1000", "scientific display");
        Check(RankingScore.Parse("1.23456789e1000").RawValue == "1.23456789e1000", "display does not change raw value");
        Check(((RankingScore)1.25m).RawValue == "1.25e0", "decimal input");
        Check(((RankingScore)1.25e100).RawValue == "1.25e100", "double input");
        foreach (var invalid in new[] { "NaN", "Infinity", "1e10001", "1e-10001", "1.2.3", "", new string('1', 1025) })
        {
            bool rejected = false;
            try { RankingScore.Parse(invalid); } catch (ArgumentException) { rejected = true; }
            Check(rejected, "reject invalid/unbounded score");
        }
        bool overflow = false;
        try { RankingScore.Parse("1e1000").ToDouble(); } catch (OverflowException) { overflow = true; }
        Check(overflow, "double overflow is not silently returned as Infinity");
    }
}
