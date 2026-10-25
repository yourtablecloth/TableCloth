using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace TableCloth.ManagedAi;

internal static class ManagedAiResponseActivityView
{
    public static Control Create(IReadOnlyList<AiResponseActivity> activities, bool evidenceIncomplete, bool responseCompleted)
    {
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 6, 0, 0) };
        AutomationProperties.SetAutomationId(panel, "ManagedAiResponseActivity");
        panel.Children.Add(new TextBlock
        {
            Text = L("스킬 및 도구 기록", "Skill and tool activity"), FontWeight = FontWeight.SemiBold,
            FontSize = 11, TextWrapping = TextWrapping.Wrap
        });
        if (!responseCompleted)
            panel.Children.Add(Text(L("응답을 완료하지 못했습니다. 아래 기록은 앱의 처리 내역만 나타냅니다.",
                "The response did not complete. These records show app operations only.")));
        if (activities.Count == 0)
            panel.Children.Add(Text(L("확인된 스킬 실행 기록이 없습니다.", "No skill execution was confirmed.")));
        else
        {
            var details = new StackPanel { Spacing = 3 };
            foreach (var activity in activities)
            {
                var name = Identifier(activity.Name);
                var identity = activity.Kind == AiResponseActivityKind.McpTool
                    ? $"{Identifier(activity.Server)} / {name}" : name;
                var row = Text($"{identity}: {OutcomeText(activity)}");
                AutomationProperties.SetAutomationId(row, "ManagedAiResponseActivityItem");
                details.Children.Add(row);
            }
            if (activities.Count > 3)
            {
                details.IsVisible = false;
                // The application's theme does not provide an Expander template.
                var disclosure = new Button
                {
                    Content = L($"기록 {activities.Count}개 보기", $"View {activities.Count} activity records"),
                    FontSize = 11, Padding = new Thickness(6, 2), MinHeight = 24,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
                };
                disclosure.Click += (_, _) =>
                {
                    details.IsVisible = !details.IsVisible;
                    disclosure.Content = details.IsVisible
                        ? L("기록 접기", "Hide activity records")
                        : L($"기록 {activities.Count}개 보기", $"View {activities.Count} activity records");
                };
                AutomationProperties.SetAutomationId(disclosure, "ManagedAiResponseActivityDetails");
                AutomationProperties.SetAutomationId(details, "ManagedAiResponseActivityItems");
                panel.Children.Add(disclosure);
                panel.Children.Add(details);
            }
            else panel.Children.Add(details);
        }
        if (evidenceIncomplete)
            panel.Children.Add(Text(L("일부 처리 기록을 표시하지 못했거나 완료 여부를 확인하지 못했습니다.",
                "Some activity records could not be displayed or their completion confirmed.")));
        var limit = L("스킬 지침 참조 여부와 응답 반영 여부는 확인할 수 없습니다.",
            "Skill instruction use and its effect on the answer cannot be verified.");
        if (activities.Any(activity => activity.Kind == AiResponseActivityKind.McpTool))
            limit += L(" MCP 기록만으로 스킬 사용을 확인할 수는 없습니다.", " MCP activity alone does not confirm skill use.");
        panel.Children.Add(Text(limit));
        return panel;
    }

    public static AiResponseActivityOutcome SandboxOutcome(SandboxCliRequest request, string report)
    {
        // Inspect only the app-owned status envelope. Output, paths, and identifiers never enter the activity UI.
        try
        {
            using var json = JsonDocument.Parse(report, new JsonDocumentOptions { MaxDepth = 8 });
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("Action", out var action) || action.ValueKind != JsonValueKind.String ||
                action.GetString() != request.Action.ToString().ToLowerInvariant() ||
                !root.TryGetProperty("Status", out var status) || status.ValueKind != JsonValueKind.String)
                return AiResponseActivityOutcome.Incomplete;
            return status.GetString() switch
            {
                "succeeded" when root.TryGetProperty("ExitCode", out var exitCode) && exitCode.TryGetInt32(out var code) && code == 0
                    => AiResponseActivityOutcome.Completed,
                "failed" or "unavailable" or "list-failed" => AiResponseActivityOutcome.Failed,
                _ => AiResponseActivityOutcome.Incomplete
            };
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        { return AiResponseActivityOutcome.Incomplete; }
    }

    private static string OutcomeText(AiResponseActivity activity) => (activity.Kind, activity.Outcome) switch
    {
        (AiResponseActivityKind.HostSkill, AiResponseActivityOutcome.Completed)
            => L("앱 처리 완료", "App operation completed"),
        (AiResponseActivityKind.HostSkill, AiResponseActivityOutcome.Failed)
            => L("앱 처리 실패", "App operation failed"),
        (AiResponseActivityKind.McpTool, AiResponseActivityOutcome.Completed)
            => L("MCP 응답 수신", "MCP response received"),
        (AiResponseActivityKind.McpTool, AiResponseActivityOutcome.Failed)
            => L("MCP 호출 실패", "MCP call failed"),
        _ => L("완료 확인 불가", "Completion unconfirmed")
    };

    private static string Identifier(string? value) => !string.IsNullOrEmpty(value) && value.Length <= 128 &&
        Array.TrueForAll(value.ToCharArray(), c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')
        ? value : L("식별 정보 없음", "Identity unavailable");

    private static TextBlock Text(string value) => new() { Text = value, TextWrapping = TextWrapping.Wrap, FontSize = 11 };
    private static string L(string korean, string english) => ManagedAiText.Select(korean, english);
}
