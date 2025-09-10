# Custom Player Action Manager

This readme will give you a brief introduction on how to add rebindable key mappings to 7 days to die. I will assume you have adequate knowledge of C# and know exactly what you are doing and what you need to achieve, since you'll need them to make use of your custom controls anyway.

### How the game handles input

7 days to die manages player input by InControl, with 5 PlayerActionSet and an **ActionSetManager**. Those sets are: **PlayerActionsLocal**, **PlayerActionsGlobal**, **PlayerActionsGUI**, **PlayerActionsPermanent** and **PlayerActionsVehicle**, which are all implementation of another abstraction on PlayerActionSet: **PlayerActionsBase**.

**PlayerActionsBase** handles a lot listening options for you, so you only need to take care of the initialization of your action set. Basically deriving from it and override **CreateActions**, **CreateDefaultJoystickBindings** and **CreateDefaultKeyboardBindings** is all you need for add a set.

A PlayerActionSet consists of a bunch of PlayerAction. You can take above action sets for example on how to create actions. Note that each action comes with an **ActionGroup**, and each action group comes with an **ActionTab**. Vanilla tabs and groups can be found in **PlayerActionData**, you can add your own group with static fields, but adding new tabs will break the control option page header, as the column count is fixed and it's hard to dynamically add and remove with unknown count.

**ActionSetManager** disables all action sets pushed into its stack but the top one. You can manage action set enable states with it if you need exclusive input handling, but since this mostly happens with vehicle and GUI, it's not a necessary part. For example, **PlayerActionsPermanent** is never pushed into it, thus it can be handled with other action sets on the top.

You can add **-debuginput=verbose** to launch arguments to enable console debug info on **ActionSetManager**.

### Why is this mod needed

If a modder wants to add a custom PlayerActionSet, he will need to create it manually at some point after mods loaded, patch XUiC_OptionsControls to insert the actions into the panels, inject into pref save and handle control reset. When this happens in multiple mods, different ways of dealing with things might conflict with each other.

With this mod, you only need to create your actions with default key mapping, and it will get all the other chores done.

It introduces a derived type of **PlayerActionsBase** named **CustomPlayerActionVersionBase**, and searches for all the subclasses in loaded mod main assemblies, creating an instance and keep them in a dictionary.

Since XUiC_OptionsControls create the setting pages only when you open it for the first time, it is possible to insert all the custom action sets into it at once.

Then it saves all mod action sets in a save file, each one with its name and custom version, so that when one set is removed it won't interfere the data loading of other sets.

Moreover, it patches the log method of **ActionSetManager** to include enable states of custom action sets.

### How to make use of it

**VehicleWeapon** makes use of this mod, you can take that for reference.

1. Derive from  **CustomPlayerActionVersionBase**, create your PlayerAction there.
2. In the constructor, set its **Name** and **Version**. It's also recommended to add a static Instance field and set the instance in constructor for convenient access.
3. Add conflicts with other action sets when necessary. this is done by `UserData = new PlayerActionData.ActionSetUserData(new PlayerActionsBase[] { other sets });`. Note this won't add your set to the conflict list of other sets, and the created user data is stored in a readonly collection, which does not support dynamic insertion and removal, so I add 2 extension methods to replace the user data of an action set with a new one that contains your set: `AddUniConflict` add the parameter to the caller's user data if it does not already exist, `AddBiConflict` add the param and the caller to each other's user data.
4. Adding conflicts with possibly loaded action sets if necessary. This is done by override `InitActionSetRelations`and call `CustomPlayerActionManager.TryGetCustomActionSetByName`, then add conflict with above extension methods if they exist. Note you should not access other custom action sets in constructor as their initialization order is undefined.
5. Always change the Version number when you remove actions. This will reset the key mapping without loading the save file, and update the save version on next save operation. I may be wrong, but this prevents potential BinaryReader exceptions during parsing obsolete data. This is not required for adding actions.

