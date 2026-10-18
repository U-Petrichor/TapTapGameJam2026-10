namespace TalentTree.Effects
{
    /// <summary>
    /// 天赋等级的只读来源。
    ///
    /// 存在的意义只有一个：让 <see cref="TalentEffectResolver"/> 不再依赖 <c>PlayerTalentTree</c> 这个
    /// MonoBehaviour。解耦之后 resolver 变成纯 C#（可单测、可被 Buff 层直接复用），而「等级怎么来的」
    /// （图规则、Requirement、点数）仍然归 PlayerTalentTree / TalentGraphEvaluator 管。
    /// </summary>
    public interface ITalentLevelSource
    {
        /// <summary>天赋树资产。为 null 表示没有任何天赋，解析结果为空。</summary>
        TalentTreeAsset Asset { get; }

        /// <summary>该天赋当前等级。0 表示未解锁。</summary>
        int GetLevel(TalentData talent);
    }
}
