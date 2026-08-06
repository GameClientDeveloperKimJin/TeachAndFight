namespace TeachAndFight.Combat
{
    // 01장 어휘 사전의 do.action 과 1:1 대응
    public enum ActionType
    {
        Idle,
        Approach,
        Retreat,
        KeepDistance,
        Dash,
        LightAttack,
        HeavyAttack,
        Ultimate,
        // #26 Tier2
        CounterAttack,
        Feint,
    }

    public enum DashDirection
    {
        Toward,
        Away,
    }

    public struct ActionCommand
    {
        public ActionType Action;
        public DashDirection DashDirection;
        public float KeepDistanceRange;

        public static ActionCommand Idle() => new ActionCommand { Action = ActionType.Idle };
        public static ActionCommand Approach() => new ActionCommand { Action = ActionType.Approach };
        public static ActionCommand Retreat() => new ActionCommand { Action = ActionType.Retreat };
        public static ActionCommand KeepDistanceAt(float range) => new ActionCommand { Action = ActionType.KeepDistance, KeepDistanceRange = range };
        public static ActionCommand DashTo(DashDirection dir) => new ActionCommand { Action = ActionType.Dash, DashDirection = dir };
        public static ActionCommand LightAttack() => new ActionCommand { Action = ActionType.LightAttack };
        public static ActionCommand HeavyAttack() => new ActionCommand { Action = ActionType.HeavyAttack };
        public static ActionCommand Ultimate() => new ActionCommand { Action = ActionType.Ultimate };
        public static ActionCommand CounterAttack() => new ActionCommand { Action = ActionType.CounterAttack };
        public static ActionCommand Feint() => new ActionCommand { Action = ActionType.Feint };
    }
}
