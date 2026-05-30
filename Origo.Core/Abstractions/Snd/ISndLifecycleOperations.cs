namespace Origo.Core.Abstractions.Snd;

/// <summary>
///     提供存档相关的生命周期入口：继续游戏、初始存档、主菜单入口。
/// </summary>
public interface ISndLifecycleOperations
{
    /// <summary>是否存在可继续游戏的目标存档。</summary>
    bool HasContinueData();

    /// <summary>请求继续游戏（基于当前 continue 目标）。</summary>
    bool RequestContinueGame();

    /// <summary>请求加载初始存档模板。</summary>
    void RequestLoadInitialSave();

    /// <summary>按启动流程重新读取主菜单入口配置。</summary>
    void RequestLoadMainMenuEntrySave();
}