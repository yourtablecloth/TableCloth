using TableCloth.ManagedAi;

namespace TableCloth.ViewModels;

public partial class OptionsWindowViewModel
{
    private readonly IManagedAiSkillManager? _skillsManager;
    private readonly IManagedAiSkillMarketplace? _skillMarketplace;
    private readonly IManagedAiSkillRuntimeManager? _skillRuntimes;
    private readonly IManagedAiSkillRuntimeDiagnostics? _skillRuntimeDiagnostics;

    public bool CanOpenSkillMarketplace => _skillsManager is not null && _skillMarketplace is not null;

    public SkillMarketplaceWindowViewModel? CreateSkillMarketplaceViewModel() =>
        _skillsManager is not null && _skillMarketplace is not null
            ? new(_skillsManager, _skillMarketplace, _appMessageBox, _skillRuntimes, _skillRuntimeDiagnostics) : null;
}
