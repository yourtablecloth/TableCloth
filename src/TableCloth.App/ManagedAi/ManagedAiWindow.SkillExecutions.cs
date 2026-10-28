using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TableCloth.ManagedAi;

public sealed partial class ManagedAiWindow
{
    private void AddChatResponse(string author, AiChatResponse response)
    {
        AddMessage(author, response.Text);
        // AddMessage adds this bubble synchronously on the UI thread. Keep the
        // application's execution facts outside the model Markdown and copy text.
        var bubble = (Border)_transcript.Children[^1];
        var body = (StackPanel)bubble.Child!;
        body.Children.Add(CreateSkillExecutionSection(response));
    }

    private static Control CreateSkillExecutionSection(AiChatResponse response)
    {
        var heading = L("앱 실행 기록", "App execution records");
        var help = L("브로커가 보고한 실행 사실을 표시합니다. AppContainer의 독립 OS 검증이나 모델 답변의 모든 수치 검증을 뜻하지 않습니다.",
            "These facts come from the execution broker. They do not provide independent OS attestation or verify every statement or value in the model response.");
        var content = new StackPanel { Spacing = 6 };
        var title = Text(heading);
        title.FontWeight = FontWeight.SemiBold;
        AutomationProperties.SetAutomationId(title, "ManagedAiSkillExecutionHeading");
        content.Children.Add(title);
        var records = response.SkillExecutions;
        if (records.Count == 0)
        {
            var empty = Text(L("이 응답에서 확인한 AppContainer 스크립트 실행이 없습니다.",
                "No AppContainer script execution was verified for this response."));
            AutomationProperties.SetAutomationId(empty, "ManagedAiSkillExecutionNone");
            AutomationProperties.SetName(empty, empty.Text);
            content.Children.Add(empty);
        }
        else
        {
            foreach (var record in records)
            {
                var identity = SkillExecutionIdentity(record);
                var outcome = SkillExecutionOutcome(record);
                var row = new StackPanel { Spacing = 2 };
                var identityText = Text(identity);
                identityText.FontWeight = FontWeight.SemiBold;
                AutomationProperties.SetName(identityText, identity);
                row.Children.Add(identityText);
                var outcomeText = Text(outcome);
                AutomationProperties.SetName(outcomeText, outcome);
                row.Children.Add(outcomeText);
                var call = Text(L($"호출 ID: {record.CallId}", $"Call ID: {record.CallId}"));
                AutomationProperties.SetName(call, call.Text);
                row.Children.Add(call);
                AutomationProperties.SetAutomationId(row, "ManagedAiSkillExecutionRecord");
                AutomationProperties.SetName(row, $"{identity}. {outcome}. {call.Text}");
                content.Children.Add(row);
            }
        }
        if (response.SkillExecutionsTruncated)
        {
            var truncated = Text(L("실행 기록 한도로 일부 기록을 표시하지 않습니다.",
                "Some execution records are omitted because the record limit was reached."));
            AutomationProperties.SetAutomationId(truncated, "ManagedAiSkillExecutionsTruncated");
            AutomationProperties.SetName(truncated, truncated.Text);
            content.Children.Add(truncated);
        }
        var section = new Border
        {
            Child = content, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 8, 0, 0)
        };
        section.Bind(Border.BorderBrushProperty, section.GetResourceObservable("ControlStrokeColorDefaultBrush"));
        AutomationProperties.SetAutomationId(section, "ManagedAiSkillExecutionSection");
        AutomationProperties.SetName(section, heading);
        AutomationProperties.SetHelpText(section, help);
        ToolTip.SetTip(section, help);
        return section;
    }

    private static string SkillExecutionIdentity(AiSkillExecutionRecord record)
    {
        var skill = SafeSkillExecutionLabel(record.SkillName, L("이름 미확인", "Unknown skill"));
        return L($"스킬: {skill} / 도구: {record.ToolName} [{record.EntryPoint}]",
            $"Skill: {skill} / Tool: {record.ToolName} [{record.EntryPoint}]");
    }

    private static string SafeSkillExecutionLabel(string value, string fallback)
    {
        // Names are registration metadata, but must never become a host-path or
        // control-character display channel. Core separately validates tokens
        // and the bounded relative entrypoint before accepting response records.
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128 || value.Any(char.IsControl) ||
            value.Contains(':') || value.Contains('\\') || value.StartsWith('/')) return fallback;
        return SensitiveDataSanitizer.Sanitize(value);
    }

    private static string SkillExecutionStatusText(AiSkillExecutionRecord record)
        => record.Succeeded ? L("성공", "Succeeded") : record.Status switch
        {
            AiSkillExecutionStatus.Failed => L("실패", "Failed"),
            AiSkillExecutionStatus.Blocked => L("차단", "Blocked"),
            _ => L("검증하지 못함", "Unverified")
        };

    private static string SkillExecutionOutcome(AiSkillExecutionRecord record)
    {
        var parts = new List<string>
        {
            record.WasAppContainer ? L("AppContainer: 브로커 확인", "AppContainer: broker confirmed") : L("AppContainer: 미확인", "AppContainer: unverified"),
            SkillExecutionStatusText(record),
            record.ExitCode is { } exit ? L($"종료 코드: {exit}", $"Exit code: {exit}") : L("종료 코드 없음", "Exit code unavailable")
        };
        if (record.TimedOut) parts.Add(L("시간 초과", "Timed out"));
        if (record.OutputLimited) parts.Add(L("출력 한도 초과", "Output limit exceeded"));
        return string.Join(" / ", parts);
    }

    private static string SkillExecutionProgressText(AiSkillExecutionRecord record)
    {
        try { AiSkillExecutionHistory.Validate([record]); }
        catch (ManagedAiException) { return L("스킬 실행 기록을 검증하지 못했습니다.", "Could not verify the skill execution record."); }
        var skill = SafeSkillExecutionLabel(record.SkillName, L("이름 미확인", "Unknown skill"));
        return L($"스킬 {skill}: {SkillExecutionStatusText(record)}", $"Skill {skill}: {SkillExecutionStatusText(record)}");
    }
}
