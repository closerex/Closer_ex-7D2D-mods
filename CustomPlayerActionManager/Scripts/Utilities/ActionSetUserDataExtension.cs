using System.Collections.Generic;

public static class ActionSetUserDataExtension
{
    public static void AddUniConflict(this PlayerActionsBase self, PlayerActionsBase other)
    {
        List<PlayerActionsBase> list = new List<PlayerActionsBase>((self.UserData as PlayerActionData.ActionSetUserData).bindingsConflictWithSet);
        if(!list.Contains(other))
        {
            list.Add(other);
            self.UserData = new PlayerActionData.ActionSetUserData(list.ToArray());
        }
    }

    public static void AddBiConflict(this PlayerActionsBase self, PlayerActionsBase other)
    {
        self.AddUniConflict(other);
        other.AddUniConflict(self);
    }
}

