using UnityEngine;

namespace TapTapGameJam.Types
{
    /// <summary>
    /// 需要定义的公共类型都放在这里，方便统一管理。
    /// </summary>



    // 整个游戏的三个阶段：白天、购物、夜晚
    public enum GamePhase
    {
        Day,
        Shopping,
        Night
    }

    // 面板小游戏的稳定身份。底层数值只用于兼容旧场景序列化，业务代码不应依赖数字。
    public enum MiniGameKind
    {
        Clean = 0,
        PourDogFood = 1,
        SortFile = 2,
        StockTrade = 3,
        MakeCoffee = 4,
        PrinterRepair = 5
    }

}
