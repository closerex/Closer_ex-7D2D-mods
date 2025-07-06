public class MinEventActionDecreaseProgressionLevelAndRefundSP : MinEventActionSetProgressionLevel
{
    public override void Execute(MinEventParams _params)
    {
        EntityPlayerLocal entityPlayerLocal = this.targets[0] as EntityPlayerLocal;
        if (this.targets != null)
        {
            ProgressionValue progressionValue = entityPlayerLocal.Progression.GetProgressionValue(this.progressionName);
            if (progressionValue != null)
            {
                if (this.level >= 0 && this.level < progressionValue.Level)
                {
                    ProgressionClass progressionClass = progressionValue.ProgressionClass;
                    int spcount = 0;
                    for (int i = this.level + 1; i <= progressionValue.Level; i++)
                        spcount += progressionClass.CalculatedCostForLevel(i);
                    progressionValue.Level = this.level;
                    entityPlayerLocal.Progression.SkillPoints += spcount;
                    entityPlayerLocal.Progression.bProgressionStatsChanged = true;
                    entityPlayerLocal.bPlayerStatsChanged = true;
                    //Log.Out($"[MinEventActionDecreaseProgressionLevelAndRefundSP] Decreased progression level of {this.progressionName} to {this.level} and refunded {spcount} SP. Current perk: {_params.ProgressionValue.Name} level {_params.ProgressionValue.Level}");
                }
            }
        }
    }
}
