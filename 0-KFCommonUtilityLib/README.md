# Killing Floor 2 Utility Library
This mod adds ItemActions, MinEventActions, Requirements and MonoScripts that provide utilities inspired by Killing Floor 2. It's mainly a common lib for my future killing floor 2 style mods, but might also be useful for other modders.

This Readme will summarize the usage of these utilitties.

## ItemActions
There are 3 new actions available currently.
#### ItemActionHoldOpen
This action adds an **empty** state to the weapon, to easily enable guns to go into a hold open state when magazine is depleted. The state is synced on all clients, and retained on switching equipment.

To use this action on your weapon, replace
```xml
<property class="Action0">
	<property name="Class" value="Ranged"/>
```
with
```xml
<property class="Action0">
	<property name="Class" value="HoldOpen,KFCommonUtilityLib"/>
```
, then add an **empty** param to your animator.

Note this only works for Ranged weapons, not Launcher weapons.

#### ItemActionAltMode
This action derives from ItemActionHoldOpen, allows you to manage weapon mode with cvar easily. It adds following xml properties:

`Cvar_State_Switch`: The cvar that controls the mode. When the value is 0, original properties are used; when the value is greater than 0, the alt properties with that value as index is used instead.
`Alt_Sound_Start`: The start sounds of each mode, separated by comma(,). Replaces `Sound_start` when in alt mode.
`Alt_Sound_Empty`: The empty sounds of each mode, separated by comma(,). Replaces `Sound_empty` when in alt mode.
`Alt_Sound_Loop`: The loop sounds of each mode, separated by comma(,). Replaces `Sound_loop` when in alt mode.
`Alt_Sound_End`: The end sounds of each mode, separated by comma(,). Replaces `Sound_end` when in alt mode.
`Alt_InfiniteAmmo`: Whether each alt mode is infinite ammo, separated by comma(,). Valid values are **true** and **false**.

Sound properties should also work on item modifiers.

To use this action on your weapon, in addition to ItemActionHoldOpen, replace the class name with **AltMode,KFCommonUtilityLib**, and add your properties accordingly. 

Note that not all properties are required, the missing sounds are defaulted to no sound and infinite ammo is defaulted to false.

#### ItemActionRechargeable
This action derives from ItemActionAltMode, allows you to consume different cvars according to weapon mode on bursting shots easily. It adds following xml properties:

`Cvar_To_Consume`: The cvar "stock" to consume on fire shots, separated by comma(,).
`Cvar_Consumption`: The cvar "consumption" on each shot, separated by comma(,). When the stock value is less than consumption value, the corresponding empty sound of current mode is played.
`Cvar_No_Consumption_Burst_Count`: The shot count that does not consume the stock, separated by comma(,). This is decreased by 1 on each shot. Only in effect when greater than 0.

These properties only work when in alt modes.

To use this action on your weapon, in addition to ItemActionAltMode, replace the class name with **Rechargeable,KFCommonUtilityLib**, and add your properties accordingly.

Note that while `Cvar_No_Consumption_Burst_Count` is not required, the other 2 are a must.

## MinEventActions
There are currently 6 trigger actions avaliable .

#### MinEventActionAddBuffToTargetAndSelf
A simple addon to AddBuff that adds the same buff to the initiator as the targets.

Syntax is the same as AddBuff, replace `action` value with **AddBuffToTargetAndSelf,KFCommonUtilityLib**.

#### MinEventActionDecreaseProgressionLevelAndRefundSP
A simple addon to SetProgressionLevel that refunds all skill points spent on those decreased levels.

Syntax is the same as SetProgressionLevel, replace `action` value with **DecreaseProgressionLevelAndRefundSP,KFCommonUtilityLib**. `level` must be less than current progression level.

#### MinEventActionModifyCVarWithSelfRef
A simple addon to ModifyCVar that takes "@cvar" reference from the initiator instead of each target.

When using ModifyCVar with "@cvar", the actuall value is taken from each target. this action changes the behaviour to take the value from the initiator only.

Syntax is the same as ModifyCVar, replace `action` value with **ModifyCVarWithSelfRef,KFCommonUtilityLib**.

------------


### WeaponLabels
The following 3 actions controls the 3D Text gameobjects on your weapon, setting their text by string, cvar value or rounds in magazine. Moreover, they can change the color of certain materials.

Drag **KFUtilAttached** folder into your unity project and attach the script inside to the root transform of your weapon. Then create 3D Text objects and drag them onto the script. You can also drag mesh renderers onto the script to change its color on certain shader properties in xml.

By "slot", the attribute refers to the index of the objects you dragged onto the script.

**Text and colors are synced on all clients through NetPackages, thus you should avoid setting them frequently.**
#### MinEventActionSetStringOnWeaponLabel
This action changes the text of specified 3D Text object. The syntax is as follows:

```xml
<triggered_effect trigger="trigger name" action="SetStringOnWeaponLabel,KFCommonUtilityLib" cvar/text="text or cvar name" slot="index"/>
```

When `cvar` is presented, the text will be the cvar value; when `text` is presented, the text will be the string you put on it.

`slot` is defaulted to 0.

#### MinEventActionSetAmmoOnWeaponLabel
This action changed the text of specified 3D Text object to the round count in your magazine. The syntax is as follows:

```xml
<triggered_effect trigger="trigger name" action="SetAmmoOnWeaponLabel,KFCommonUtilityLib" slot="index"/>
```
`slot` is defaulted to 0.

#### MinEventActionSetWeaponLabelColor
This action changes the color of specified 3D Text object or the material of specified mesh renderer. The syntax is as follows:

```xml
<triggered_effect trigger="trigger name" action="SetWeaponLabelColor,KFCommonUtilityLib" color="color" is_text="true" slot0="index of 3D Text"/>
<triggered_effect trigger="trigger name" action="SetWeaponLabelColor,KFCommonUtilityLib" color="color" is_text="false" name="_EmissionColor" slot0="index of mesh renderer" slot1="index of material"/>
```

When `is_text` is set to true, only `slot0` is parsed, which represents the index of 3D Text object; when `is_text` is set to false, `slot0` represents the index of mesh renderers you dragged onto the script, and `slot1` represents the index of materials on that renderer, while `name` stands for the color property name of the shader.

For more information about property name and color format, refer to [Unity Doc](https://docs.unity3d.com/ScriptReference/Material.SetColor.html).

------------

## Requirements
There is currently only 1 requriement avaliable.
#### RoundsInHoldingItem
This requirement can be used to check rounds in magazine on `onSelfEquipStart`. I add this because vanilla `RoundsInMagazine` does not work properly when you start equipping a gun.

Syntax is the same as `RoundsInMagazine`, replace `name` value with **RoundsInHoldingItem,KFCommonUtilityLib**.

## Explosion Scripts
There is currently only 1 explosion script avaliable.

If you have no idea what this does, please refer to my custom explosion particle tutorial.
#### ExplosionAreaBuffTick
This script requires a trigger collider on the root transform, and add buffs specified in `Explosion.Buff` to all entities inside the collider every `Explosion.TickInterval` seconds. Moreover, it fires `onSelfAttackedOther` event from the item and initiator every tick.

This script takes following custom property:
`Explosion.TickInterval`: interval between each tick. Default value is 0.5.