namespace TeachAndFight.Combat.Demo
{
    // #6 W1 데모 전용 - 실제 규칙 실행(#7 RuleEvaluator)이 나오기 전까지 쓰는 임시 하드코딩 규칙셋
    public interface IHardcodedBrain
    {
        ActionCommand Decide(FighterController self, FighterController enemy);
    }

    // 임시 규칙셋 1: 돌격형 - 스태미나 관리 없이 붙어서 강공격 위주
    public class RushBrain : IHardcodedBrain
    {
        public ActionCommand Decide(FighterController self, FighterController enemy)
        {
            if (self.UltGaugePct >= 100f)
                return ActionCommand.Ultimate();

            if (self.Distance > 1.2f)
                return ActionCommand.Approach();

            return self.StaminaPct >= 25f ? ActionCommand.HeavyAttack() : ActionCommand.LightAttack();
        }
    }

    // 임시 규칙셋 2: 거리유지형 - 스태미나 낮으면 휴식, 근접 시에만 견제
    public class TurtleBrain : IHardcodedBrain
    {
        public ActionCommand Decide(FighterController self, FighterController enemy)
        {
            if (self.UltGaugePct >= 100f)
                return ActionCommand.Ultimate();

            if (self.StaminaPct < 30f)
                return ActionCommand.Idle();

            if (self.Distance <= 1.5f)
                return ActionCommand.LightAttack();

            if (self.Distance < 3f)
                return ActionCommand.Retreat();

            return ActionCommand.Idle();
        }
    }
}
