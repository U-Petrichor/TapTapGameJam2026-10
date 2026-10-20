using System.Collections.Generic;

namespace TapTapGameJam.Tutorial
{
    /// <summary>
    /// 《新手教程弹出点》的默认内容。Editor 用它生成 TutorialSequence.asset；
    /// 资产缺失时 TutorialDirector 也会用它兜底。
    /// </summary>
    public static class TutorialDefaultContent
    {
        public static List<TutorialStep> CreateSteps()
        {
            return new List<TutorialStep>
            {
                Step(
                    TutorialStepIds.Day1Intro,
                    TutorialSignals.DayStart,
                    TutorialSpeaker.Self,
                    "悲伤的一天从上班开始……\n不过今天开始一切都会变得不一样。\n行动起来，为我们的大事业做准备吧。\nWASD 方向移动"),

                Step(
                    TutorialStepIds.Day1FirstMove,
                    TutorialSignals.MoveFirst,
                    TutorialSpeaker.Boss,
                    "先把那张地上的污渍擦了，别耽误。",
                    requires: TutorialStepIds.Day1Intro,
                    emitOnClose: TutorialSignals.TaskSpawnFirst),

                Step(
                    TutorialStepIds.Day1TaskGuide,
                    TutorialSignals.TaskAdded,
                    TutorialSpeaker.Self,
                    "这工作真是没完没了，我们的大事业必须要准备起来了。\n任务出现提示，产卵点出现提示。\n靠近按 E 键交互，Q 键切换任务。",
                    requires: TutorialStepIds.Day1FirstMove,
                    emitOnClose: TutorialSignals.TaskRelease),

                Step(
                    TutorialStepIds.Day1FirstTaskDone,
                    TutorialSignals.TaskFirstCompleted,
                    TutorialSpeaker.Self,
                    "钱到账就行，至少今天这单没白干。\n完成工作获得金币，可购买补给和装备。",
                    requires: TutorialStepIds.Day1TaskGuide),

                Highlight(
                    TutorialStepIds.Day1CoinsHighlight,
                    TutorialStepIds.Day1FirstTaskDone,
                    TutorialHighlightTarget.Coins,
                    "after:" + TutorialStepIds.Day1FirstTaskDone),

                Step(
                    TutorialStepIds.Day1CountdownConcept,
                    "after:" + TutorialStepIds.Day1CoinsHighlight,
                    TutorialSpeaker.Boss,
                    "在磨蹭什么呢，时间可不等人。",
                    requires: TutorialStepIds.Day1CoinsHighlight),

                Highlight(
                    TutorialStepIds.Day1CountdownHighlight,
                    TutorialStepIds.Day1CountdownConcept,
                    TutorialHighlightTarget.Countdown,
                    "after:" + TutorialStepIds.Day1CountdownConcept),

                Step(
                    TutorialStepIds.Day1ComplexTask,
                    TutorialSignals.TaskComplexCompleted,
                    TutorialSpeaker.Self,
                    "这种工作真是钱少事多有气受。\n但是复杂任务可以累计怒气值，怒气点用于解锁天赋。",
                    requires: TutorialStepIds.Day1CountdownHighlight),

                Step(
                    TutorialStepIds.Day1NestMarked,
                    TutorialSignals.NestFirstMarked,
                    TutorialSpeaker.Self,
                    "这箱子倒好，往桌底下一躲，谁也不管。\n白天登记过的纸箱子，夜晚可以前来产卵。",
                    requires: TutorialStepIds.Day1FirstTaskDone),

                Step(
                    TutorialStepIds.Day1Settlement,
                    TutorialSignals.DaySettled,
                    TutorialSpeaker.Self,
                    "每日工作结算固定获得怒气点。\n快回家准备一下我们的大事业吧。",
                    requires: TutorialStepIds.Day1Intro),

                Step(
                    TutorialStepIds.ShoppingHome,
                    TutorialSignals.HomeEntered,
                    TutorialSpeaker.Self,
                    "每一个伟大事业背后都缺少不了一个准备充分的堡垒基地。",
                    requires: TutorialStepIds.Day1Settlement),

                Step(
                    TutorialStepIds.ShoppingTalent,
                    "after:" + TutorialStepIds.ShoppingHome,
                    TutorialSpeaker.Self,
                    "自从觉醒的那一天开始，我的愤怒就是我能量的来源，这份能量正是我伟大事业的基石。",
                    requires: TutorialStepIds.ShoppingHome),

                Step(
                    TutorialStepIds.ShoppingTalentGuide,
                    "after:" + TutorialStepIds.ShoppingTalent,
                    TutorialSpeaker.Self,
                    "打开天赋树，可以进行怒气值的分配规划，获得相应的能力。",
                    requires: TutorialStepIds.ShoppingTalent),

                Step(
                    TutorialStepIds.ShoppingWarehouse,
                    TutorialSignals.WarehouseOpened,
                    TutorialSpeaker.Self,
                    "夜晚的收获都将在这里存储，每日出行需要合理分配包裹存储。",
                    requires: TutorialStepIds.ShoppingHome),

                Step(
                    TutorialStepIds.NightFirst,
                    TutorialSignals.NightStarted,
                    TutorialSpeaker.Self,
                    "真成蟑螂了。先找个能躲的地方。白天那个箱子，应该还在。\n产卵点出现，靠近按 E 键产卵，Q 键切换任务。",
                    requires: TutorialStepIds.ShoppingHome),

                Step(
                    TutorialStepIds.NightFirstEgg,
                    TutorialSignals.NestFirstEgg,
                    TutorialSpeaker.Self,
                    "有了卵，孵化可需要大量的营养，在附近找找食物吧。\n食物出现，搬运食物到产卵点放置获得产卵值。",
                    requires: TutorialStepIds.NightFirst),

                Step(
                    TutorialStepIds.NightFirstHatch,
                    TutorialSignals.NestFirstHatch,
                    TutorialSpeaker.Self,
                    "小蟑螂孵化好了，可以通过点击产卵点派遣小蟑螂自动觅食或者躲避守卫。\n点击产卵点切换状态。",
                    requires: TutorialStepIds.NightFirstEgg),

                Step(
                    TutorialStepIds.NightWorkflow,
                    TutorialSignals.NestFirstCommand,
                    TutorialSpeaker.Self,
                    "根据场地的食物和守卫分布分配蟑螂的状态，通过搬运调整位置，搭建我们的工作流。\n鼠标滚轮缩放查看公司俯视图。",
                    requires: TutorialStepIds.NightFirstHatch),

                Step(
                    TutorialStepIds.NightSearch,
                    TutorialSignals.BlackBoxTouched,
                    TutorialSpeaker.Self,
                    "夜晚公司总有一些遗留物品，或许大家莫名不见的东西就是被蟑螂捡走了呢。\n未知物品出现，靠近按 E 键交互，Q 键切换任务。",
                    requires: TutorialStepIds.NightFirst),
            };
        }

        private static TutorialStep Step(
            string id,
            string trigger,
            TutorialSpeaker speaker,
            string text,
            string requires = null,
            string emitOnClose = null)
        {
            TutorialStep step = new TutorialStep
            {
                id = id,
                trigger = trigger,
                speaker = speaker,
                text = text,
                presentation = TutorialPresentation.Modal,
                pauseGame = true,
                emitOnClose = emitOnClose
            };
            if (!string.IsNullOrEmpty(requires)) step.requires.Add(requires);
            return step;
        }

        private static TutorialStep Highlight(
            string id,
            string requiredStep,
            TutorialHighlightTarget target,
            string trigger)
        {
            TutorialStep step = new TutorialStep
            {
                id = id,
                trigger = trigger,
                speaker = TutorialSpeaker.System,
                text = string.Empty,
                presentation = TutorialPresentation.HighlightOnly,
                highlight = target,
                pauseGame = false,
                autoCloseSeconds = 2.5f
            };
            if (!string.IsNullOrEmpty(requiredStep)) step.requires.Add(requiredStep);
            return step;
        }
    }
}
