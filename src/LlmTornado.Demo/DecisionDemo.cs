using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LlmTornado.Decision;
using LlmTornado.Decision.Models;

namespace LlmTornado.Demo;

public class DecisionDemo : DemoBase
{
    [TornadoTest]
    public static async Task ClassifySupportTicket()
    {
        DecisionRequest request = new DecisionRequest(DecisionModel.TypeSafe.Jev.Latest,
                "I have asked three times now. My payouts have been failing for 3 days. Can I please just talk to a real person?")
            .AddNoul("is_urgent", "Does this convey urgency?", "Explicitly time-sensitive", "No urgency expressed")
            .AddNoul("wants_human", "Is the customer asking for a human agent?")
            .AddChoice("department", "Which team should handle this?", new Dictionary<string, object?>
            {
                ["billing"] = "Payments, invoicing, refunds",
                ["technical"] = "Bugs, outages, integrations",
                ["sales"] = "Pricing, upgrades, new accounts"
            })
            .AddScore("frustration", "How frustrated is the customer?", "Calm", "Frustrated", "Very angry");

        DecisionResult? result = await Program.ConnectMulti().Decision.CreateDecision(request);

        Assert.That(result, Is.NotNull);
        Assert.That(result!.Answers.Count, Is.EqualTo(4));

        DecisionNoulAnswer? urgent = result.GetNoul("is_urgent");
        DecisionNoulAnswer? human = result.GetNoul("wants_human");
        DecisionChoiceAnswer? department = result.GetChoice("department");
        DecisionScoreAnswer? frustration = result.GetScore("frustration");

        Assert.That(urgent, Is.NotNull);
        Assert.That(human, Is.NotNull);
        Assert.That(department, Is.NotNull);
        Assert.That(frustration, Is.NotNull);

        Console.WriteLine($"Model: {result.Model}");
        Console.WriteLine($"Urgent: {urgent!.Noul:F2}, wants human: {human!.Noul:F2}");
        Console.WriteLine($"Department: {department!.Choice} (confidence {department.Confidence:F2})");

        foreach (KeyValuePair<string, double> option in department.Probabilities)
        {
            Console.WriteLine($"  - {option.Key}: {option.Value:F2}");
        }

        Console.WriteLine($"Frustration: {frustration!.Score:F2} / {frustration.Legend.Count - 1} (confidence {frustration.Confidence:F2})");
        Console.WriteLine($"Usage: {result.Usage?.InputTokens} in, {result.Usage?.OutputTokens} out");
    }

    [TornadoTest]
    public static async Task ClassifyStructuredState()
    {
        // state and instructions can be structured JSON; refer to fields in backticks
        var state = new
        {
            source_text = "Invoice #4471 issued March 3, 2026 to Beaver Dam Logistics for $12,840.00, net 30."
        };

        DecisionRequest request = new DecisionRequest(DecisionModel.TypeSafe.Jev.Latest, state)
            .AddNoul("invoice_number_is_correct", new
            {
                extracted_value = "4471",
                question = "Does `extracted_value` match the invoice number in `source_text`?"
            })
            .AddChoice("customer_name", "Which option is the customer name in `source_text`?",
                "Beaver Logistics", "Dam Logistics", "Beaver Dam Logistics")
            .AddScore("amount_due", "How large is the amount due in `source_text`?",
                "Under $1,000", "$1,000 to $10,000", "$10,000 to $100,000", "Over $100,000");

        DecisionResult? result = await Program.ConnectMulti().Decision.CreateDecision(request);

        Assert.That(result, Is.NotNull);
        Assert.That(result!.GetChoice("customer_name")!.Choice, Is.EqualTo("Beaver Dam Logistics"));
        Assert.That(result.GetNoul("invoice_number_is_correct")!.Noul, Is.GreaterThan(0.5));

        Console.WriteLine($"Customer: {result.GetChoice("customer_name")?.Choice}");
        Console.WriteLine($"Amount bucket: {result.GetScore("amount_due")?.Score:F2}");
    }

    [TornadoTest]
    public static async Task ConfidenceGatedRouting()
    {
        // act automatically only when the model is confident; otherwise hand off
        DecisionResult? result = await Program.ConnectMulti().Decision.CreateDecision(
            DecisionModel.TypeSafe.Jev.Latest,
            "Hi, I think I might want to change something about my plan? Or maybe not, not sure.",
            new Dictionary<string, DecisionQuestion>
            {
                ["action"] = new DecisionChoice("What is the user trying to do?", new Dictionary<string, object?>
                {
                    ["upgrade"] = "Move to a higher tier",
                    ["downgrade"] = "Move to a lower tier",
                    ["cancel"] = "End the subscription",
                    ["other"] = null
                })
            });

        DecisionChoiceAnswer? action = result?.GetChoice("action");
        Assert.That(action, Is.NotNull);

        const double threshold = 0.8;
        Console.WriteLine(action!.Confidence >= threshold
            ? $"Auto-route to '{action.Choice}' (confidence {action.Confidence:F2})"
            : $"Low confidence ({action.Confidence:F2}), best guess '{action.Choice}': escalate to a human");
    }
}
