namespace TeachAndFight.Combat
{
    // 02장 상태 머신
    public enum FighterState
    {
        Idle,
        Move,
        Dash,
        AttackStartup,
        AttackActive,
        Recovery,
        HitStun,
        Down,
    }
}
