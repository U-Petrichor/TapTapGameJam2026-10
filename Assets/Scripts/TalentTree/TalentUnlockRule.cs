namespace TalentTree
{
    public enum TalentUnlockRule
    {
        AnyPathFromRoot, // 任意一个前置天赋存在从起点到达的路径。
        AllConnectedTalents // 所有指向当前节点的前置天赋都必须满足，后续天赋不参与。
    }
}
