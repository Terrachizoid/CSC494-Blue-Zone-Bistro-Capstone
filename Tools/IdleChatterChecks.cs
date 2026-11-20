// Regression checks for idle chatter (IdleChatterService): CSV parsing, kind/place filtering, conditions, weights
// and no quick repeats. Also checks the shipped sheet parses and uses only known conditions.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public static class IdleChatterChecks
{
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }

    public static string Run(string projectRoot)
    {
        string csv = "id,kind,places,condition,weight,seconds,text\n" +
            "A,click,City;Store,,1,,\"Hello, \"\"captain\"\"\"\n" +
            "B,idle,Service,,1,4,Chop chop\n" +
            "C,click,Any,money_low,1,,Money is tight\n" +
            "D,click,Any,not_a_condition,1,,Never\n" +
            "E,nonsense,Any,,1,,Skipped row\n";
        var lines = IdleChatterService.Parse(csv);
        Check(lines.Count == 4 && lines[0].text == "Hello, \"captain\"" && lines[0].places.Contains("Store") && lines[1].DisplaySeconds == 4, "Idle CSV not parsed");
        var service = new IdleChatterService(lines);
        var rich = new IdleContext { state = new GameSessionState { money = 50 } };
        var poor = new IdleContext { state = new GameSessionState { money = 1 } };
        var random = new Random(3);
        Check(service.Pick(IdleKind.Idle, "Service", rich, random)?.id == "B" && service.Pick(IdleKind.Idle, "City", rich, random) == null, "Kind/place filter failed");
        for (int i = 0; i < 10; i++) Check(service.Pick(IdleKind.Click, "Sorting", rich, random) == null, "Condition or unknown condition not respected");
        Check(service.Pick(IdleKind.Click, "Sorting", poor, random)?.id == "C", "Condition line missing when it holds");
        var first = service.Pick(IdleKind.Click, "City", poor, random); var second = service.Pick(IdleKind.Click, "City", poor, random);
        Check(first != null && second != null && first.id != second.id, "Line repeated while another was available");
        string path = Path.Combine(projectRoot, "Assets", "Resources", "Idle", "IdleChatter.csv");
        var shipped = IdleChatterService.Parse(File.ReadAllText(path));
        Check(shipped.Count >= 90 && shipped.All(l => IdleConditions.Known(l.condition)) && shipped.Select(l => l.id).Distinct().Count() == shipped.Count,
            "Shipped idle sheet has too few lines, an unknown condition, or duplicate ids");
        return $"PASS: idle chatter parsing, filters, conditions, no quick repeats, and {shipped.Count} shipped lines.";
    }
}
