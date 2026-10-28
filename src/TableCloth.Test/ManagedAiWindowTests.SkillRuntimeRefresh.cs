using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using TableCloth.ManagedAi;

namespace TableCloth.Test;

public sealed partial class ManagedAiWindowTests
{
    [TestMethod]
    [DataRow("ManagedAiConnect")]
    [DataRow("ManagedAiUpdate")]
    [DataRow("ManagedAiDeviceLogin")]
    public Task RuntimeInstallationReplacesStoredSkillIdsWithNativeNames(string action)
        => WithPromptSkills(async (window, fixture) =>
        {
            Click(window, Find<Button>(window, "ManagedAiPromptAddSkill"));
            var picker = Find<ComboBox>(window, "ManagedAiPromptSkills");
            Assert.IsTrue(picker.Items.OfType<AiSkill>().Any(skill => skill.Name == "directory-alpha"));
            window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Find<Button>(window, action).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Ready(window);
            Assert.AreEqual(1, fixture.Runtime.Installs);
            Click(window, Find<Button>(window, "ManagedAiPromptAddSkill"));
            var native = picker.Items.OfType<AiSkill>().Single(skill => skill.Id == "directory-alpha");
            Assert.AreEqual("alpha-skill", native.Name);
            picker.SelectedItem = native;
            Dispatcher.UIThread.RunJobs();
            Assert.AreEqual("$alpha-skill ", Find<TextBox>(window, "ManagedAiChatInput").Text);
            Assert.HasCount(0, fixture.Chat.Requests);
        }, configure: fixture =>
        {
            fixture.Runtime.Installed = false;
            fixture.Auth.LoggedIn = false;
            fixture.Skills.ListItems = () => fixture.Runtime.Installed ? fixture.Skills.Items
                : fixture.Skills.Items.Select(skill => skill with { Name = skill.Id }).ToArray();
        });

    [TestMethod]
    public Task FailedNativeSkillRefreshDoesNotLeaveFallbackNamesSelectable()
        => WithPromptSkills(async (window, fixture) =>
        {
            Find<Button>(window, "ManagedAiUpdate").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Ready(window);
            Assert.AreEqual(1, fixture.Runtime.Installs);
            Click(window, Find<Button>(window, "ManagedAiPromptAddSkill"));
            Assert.HasCount(0, Find<ComboBox>(window, "ManagedAiPromptSkills").Items);
            Assert.HasCount(0, fixture.Chat.Requests);
        }, configure: fixture =>
        {
            fixture.Runtime.Installed = false;
            fixture.Auth.LoggedIn = false;
            fixture.Skills.ListItems = () => !fixture.Runtime.Installed
                ? fixture.Skills.Items.Select(skill => skill with { Name = skill.Id }).ToArray()
                : throw new ManagedAiException(AiFailureCode.InvalidPath);
        });
}
