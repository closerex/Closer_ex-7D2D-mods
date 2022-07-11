# Vehicle Weapon

This readme will guide you through the current conception of vehicle weapon, and explain each step in detail.

## ⭕How is this accomplished
There are 2 variants of weapons available: the particle weapon that simulates turret shooting explosive rounds and raycast weapon that serves as ordinary ranged gun.

For the raycast weapon, a lot things are handled by the code so it's mostly modding an item weapon and vehicle modifications with new syntaxes.

For the particle weapon, if you have worked with my **CustomParticleLoader** and its subsequent patches, you will find this one similar to the multi explosion stuff, which is the reason why it requires that patch. I recommend you refer to the [custom explosion tutorial](https://community.7daystodie.com/topic/27941-using-custom-explosion-particles-with-working-scripts-in-a20/ "custom explosion tutorial") before you continue, since you'll need custom explosion particles anyway.

To be short, what that multi explosion patch does is handling particle collision events and triggering explosions on collision position. It can also check for particle lifetime in every `FixedUpdate ` to trigger explosions on particle death. Particle weapon reuses that script, thus the main task here is attaching particle systems that emits projectile style particles to the vehicle, and add proper parts with necessary properties to your vehicle.

Check Vehicle tab in control option window for weapon controls.
## ⭕Prepare your vehicle

**VehicleWeaponTest** has a unity package inside with the tutorial vehicle it uses. You can tweak its xml to test with each property. Note that it requires **CustomParticleLoaderSpawnEntity**, **ParticleScriptTest** and **Kaboomstick** in addition since I reused their particles.

I actually don't make vehicles on my own, so I assume you are better at it than I am. The main concept of vehicle weapon is operating on transforms given in vehicles.xml, so for the unity part you'll need to add those transforms to the right location.

### 🔴For the particle weapon
As the name suggests, particle weapon shoot rounds with particle system. Thus a properly configured particle system is required. For the unity setup of the particle system, refer to the [reply](https://community.7daystodie.com/topic/27941-using-custom-explosion-particles-with-working-scripts-in-a20/?do=findComment&comment=482809 "reply") of the explosion tutorial.

Only notable issue here is the self collision problem. You'll need to reduce the collider size as much as possible to avoid self collision, add RigidBody to the root transform of all weapons and enable **Inherit Velocity** on the particle system with **Initial** and **Multiplier** set to 1, and move the particle system transforms forward in its shooting direction in case of vehicle veering.

Most importantly, **enable Collision module with *Send Collision Messages*** and set **Collision Quality** to high. For additional notes on particle system, refer to the sub explosion part of my custom explosion particle tutorial.

Muzzle flash and smoke is simulated with sub emitter of the weapon.

### 🔴For the raycast weapon
Although raycast took much more time for me to finish coding, it's super easy to configure: a shoot point transform to fire raycast shots, a muzzle transform to attach muzzle flash and smoke and you are done with unity. Both are empty transforms.

### 🔴Common notes
To make the weapon rotate with your mouse, a **rotator** is required for the weapon. It's mostly a xml part stuff, but since rotators works directly on what I call **rotation transforms**, you'll need them -- two of them, a vertical one and horizontal one -- facing Z forward and Y up with zero rotation. If somehow it's not working properly, check for rotation on parent transforms of the rotator transforms. If you are working with models in other softwares, google for how to export to unity with correct transform properties.

## ⭕Setup your xml
The whole weapon system is a combination of these custom vehicle parts: `vehicle weapon manager `, `vehicle weapon ` and `rotator `.

Changing property of these parts with vehicle modifications is also possible. I'll introduce those properties individually.

To override properties of a vehicle weapon with vehicle modifications, you need to make use of `item_property_overrides ` node. The name should be set to `lowercase vehicle name_weapon part name`, for example if the vehicle is named "Car" and the weapon part is named "Weapon", the node should be `<item_property_overrides name="car_Weapon"> `. It's also possible to override the properties of weapons with any name by using \* as name, but it's not recommended.

You can also add weapon specific effect_groups on the vehicle modification, so that one mod can contain multiple effect groups, each affecting different weapons. This is done by adding a `vehicle_weapon ` attribute to the `effect_group ` node with the above name as value. For example, an `effect_group ` affecting only the above weapon looks like this: `<effect_group tiered="false" vehicle_weapon="car_Weapon"> `.

Since cosmetics are also modifications, the said features can be applied to cosmetic mods too.

Properties with **Default ** values can be omitted if you don't need to change it, otherwise the property must present.

### 🔴WeaponManager
This part indicates this vehicle is armed with weapon, and handles a whole lot logic internally. For xml modders, you just add it to the vehicle.

The part name is hardcoded to be `<property class="vehicleWeaponManager"> `.
The class property is `<property name="class" value="WeaponManager,VehicleWeapon"/> `.

##### 🔻Moddable properties:
`cameraOffset[seat] `: A Vector3 that changes the camera position of player on `[seat] `. The actual property name will be cameraOffset0 and so on.

- **Default:** 0,2,0 so that player camera is moved up by 2 units for better aiming view.

### 🔴VehicleWeapon
As stated above, there are different type of weapons. They share some common properties, and have some exclusive properties.

The part name can be everything.
#### 🟥Common properties
These are properties shared by all weapon types.
##### 🔻Fixed properties:
`seat `: The seat this weapon is attached to. Only player on this seat can operate the weapon.

- **Default:** 0 (byte)

`slot `: The sort key of this weapon. Properties in xml are read into a disordered dictionary so the part does not appear in the order you put them in xml. This property is used to sort all the weapons on this seat.

Note that the value assigned in xml is not always the real value in game: they are reassigned with sorted order. For example you have 3 weapons with slot properties set to `1, 2 ,4 `, after sorting the value is reassigned to `0, 1, 2 `.

The sorted slot value defines the shortcut to operate this weapon individually: toggle activation state or firing from this weapon alone.

- **Default:** int.MaxValue. The order will be random if all weapons are set to the same value, but for a single weapon with default value, it's always sorted to the end of the list.

`enableTransform `: Works in pair with `enabled ` property, controls the visual transform to hide/show on weapon enable state change. Can be used to "install" a weapon on the vehicle.

- **Default:** empty string. Does not affect anything when not set.

`rotator `: The part name of the bound rotator.

- **Default:** none. When rotator is not set, the weapon won't rotate.
#####  🔻Moddable properties:
`enabled `: This controls the enable state of the vehicle.

- **Default:** true

`activationSound `: The sound to play when you activate the weapon.

- **Default:** empty string

`deactivationSound `: The sound to play when you deactivate the weapon.

- **Default:** empty string

`notReadySound `: The sound to play when the weapon is not ready, ie trying to fire a non-fullauto weapon when current burst cycle is not finished.

- **Default:** empty string

`fireWhen `: The enumeration controlling the timing when this weapon can be fired. Possible values are `Anytime `, `FromSlotKey ` and `OnTarget `. Combine multiple values by comma(,).

- **Default:** Anytime

`notOnTargetSound `: The sound to play when player is trying to fire the weapon when the rotator is not pointing target direction.

- **Default:** none

`burstCount `: Consider this as the pellet count of a shotgun shell.

- **Default:** 1 (int)

`burstRepeat `: The shots to fire on a single fire command. Each shot consumes 1 ammo. Combining with `burstInterval ` to produce a "burst mode" weapon.

- **Default:** 1 (int)

`burstInterval `: The interval between `burstRepeat` in seconds. When set to 0, all `burstRepeat ` is fired in the same frame.

- **Default:** 0 (float)

`fullauto `: If this weapon is fullauto. A fullauto weapon will always try to fire when you hold fire key or slot key. Fullauto burst mode is also possible.

- **Default:** false

`ammo `: The item to consume on each `burstRepeat `. When not set or the specified item is not found, the weapon does not consume ammo.

- **Default:** empty string

`emptySound `: The sound to play when trying to fire without enough ammo in player bag.

- **Default:** empty string

`fireSound `: The sound to play on each `burstRepeat `.

- **Default:** empty string

`endSound `: The sound to play when releasing trigger.

- **Default:** empty string

There is a hidden `repeatInterval ` field, which is handled differently by particle weapons and raycast weapons. It will be discussed in following sections.

#### 🟥ParticleWeapon
The class property is `<property name="class" value="ParticleWeapon,VehicleWeapon"/> `.

#####  🔻Fixed properties:
`particle_transform `: Particle weapon disables the emission module of the particle system found on `particle_transform ` automatically, and emit particles dynamically according to above properties.

#####  🔻Moddable properties:
`reloadTime `: The said `repeatInterval` field. When all `burstRepeat ` finishes, a "reload" is triggered for particle weapons.

- **Default:** 1 (float)

`reloadSound `: The sound to play when reload is triggered.

- **Default:** empty string

`particleIndex `: The custom particle index of the explosion particle. The particle is bound with an item, and passive effects and triggers are taken from that item.

`explodeOnCollision `: Should explosion be triggered on particle collision?

- **Default:** true

`explodeOnDeath `: Should explosion be triggered on particle lifetime depletion?

- **Default:** false

#### 🟥RaycastWeapon
The class property is `<property name="class" value="RaycastWeapon,VehicleWeapon"/> `.

#####  🔻Fixed properties:
`raycastTransform `: The transform where the raycast shot should origin from.

`muzzleTransform `: The transform where the muzzle flash and smoke particle is attached to.

- **Default:** empty string

`AADebug `: Show the aim assist cylinder. Refer to `AASize ` property.

- **Default:** false

##### Moddable properties:
`itemName `: The bound item of this weapon. The reason for taking a bound item is to calculate passive effects and fire trigger events from the item.

Bound item can be affected by player entity class, progression , buffs and modifications installed on the vehicle according to its tag.

`damageType `: The damage type of dealt shots. Refer to vanilla buffs.xml for all damage types.

- **Default:** Piercing, which is the default bullet damage type.

`material `: The bullet material. This determines the particle to spawn on raycast hits.

- **Default:** bullet

`AASize `: The aim assist cylinder radius. The weapon is locked on the targeted enemy in range, until player camera moves away from it. Turn `AADebug ` on to get a visible cylinder.

- **Default:** 0.75 (float)

`muzzleFlash `: The muzzle flash to spawn on each `burstRepeat `. Both vanilla particle name and custom bundle path is accepted.

- **Default:** empty string

`muzzleSmoke `: The muzzle smoke to spawn on each `burstRepeat `. Both vanilla particle name and custom bundle path is accepted.

- **Default:** empty string

For custom muzzle particles, quote from my [Custom Muzzle Flash mod](https://www.nexusmods.com/7daystodie/mods/2063 "Custom Muzzle Flash mod") (the mod is not required):

- Note how vanilla muzzle flash works:
Game spawns your particle, attach it to muzzle, then force emitting 10 particles immediately. The particle manager of the action checks all active particle objects on update, and destroys those without an alive particle component. This means your particle system component needs to be acyclic, short lived, and self destructive (or make sure it does not emit particles itself by disable emission module).

`buffs `: The buff to add on hiting alive entities. `BuffProcChance ` passive is calculated for each buff.

Note to modify this property with modifications, you need to use `buffAppend` and `buffRemove` to add or remove buffs from the weapon buff list. Adding existing buffs or removing nonexistent buffs does nothing and won't cause exception.
`BuffProcChance ` is defaulted to 1 and calculated by passive effects.

- **Default:** empty string

`repeatInterval ` of raycast weapon is affected by both `burstInterval ` property and `RoundsPerMinute ` passive effect.

#### 🟥DummyWeapon
One weapon accepts one rotator at most. However, imagine a turret with multiple barrels attached to one basement: apparently you need the basement rotate horizontally and the turret rotate vertically, but having bidirectional rotator on each turret will surely interfere with each other.

That's why we have this dummy weapon. It does not shoot, ignoring all the shooting related properties, and only updates the assigned rotator.

The class property is `<property name="class" value="DummyWeapon,VehicleWeapon"/> `.

Only common properties are accepted.

#### 🟥CycleFireWeapon
Cycle fire weapon works as a sub weapon manager: it takes control of the nested weapons, firing them in a cycle.

Nested weapons are placed within the node of a CycleFireWeapon. They share the same slot as the CycleFireWeapon, careless of their types. Setting `slot ` property of nested weapons affects the cycle order.

CycleFireWeapon itself does not shoot, but it takes the `fullauto ` property to decide whether you can hold fire key to cycle or must release and press for each weapon. However, `fullauto ` property of nested weapons has no effect, as CycleFireWeapon simulates input by pressing and releasing each weapon (which also causes `endSound ` being played).

CycleFireWeapon can also take a rotator and update it like DummyWeapon.

The class property is `<property name="class" value="CycleFireWeapon,VehicleWeapon"/> `.

#####  🔻Moddable properties:
`cycleInterval `: The interval in seconds when cycling through weapons. 
When set to 0, all weapons currently ready to fire are fired in the same frame, otherwise they are cycled with a delay.

- **Default:** 0 (float)

### 🔴VehicleWeaponRotator
Rotators can be categorized into 2 types: ballistic and directional. They both take a Ray as user input, but react differently.

Ballistic rotator performs a raycast with the input ray, take the hit position as projectile landing point, and calculate the angles of both rotation transforms.

Directional rotator takes the direction of the input ray as the final look direction of both rotation transforms.

The part name can be everything.
#### 🟥Common properties
These are properties shared by all kinds of rotators.

#####  🔻Fixed properties:
`transform `: The root transform of this rotator. Root transform serves as an anchor to calculate rotations which should always face towards vehicle front and never rotate.

`horRotationTransform `: The horizontal rotation transform. Rotations on Y axis is applied to this transform.

- **Default:** null

`verRotationTransform `: The vertical rotation transform. Rotation on X axis is applied to this transform.

- **Default:** null

#####  🔻Moddable properties:
`verticleMaxRotation `: The maximum rotation of vertical transform. Positive value for upwards, negative for downwards.

- **Default:** 45 (float)

`verticleMinRotation `: The minimum rotation of vertical transform. Positive value for upwards, negative for downwards.

- **Default:** 0 (float)

The property name is an acient typo but there are already some vehicle mods in use so I'll let it be until A21 break this mod.

`verticleRotationSpeed `: Degrees the vertical transform can rotate per second.

- **Default:** 360 (float)

`horizontalMaxRotation `: The maximum rotation of horizontal transform. Positive value for clockwise, negative for counterclockwise.

- **Default:** 180 (float)

`horizontalMinRotation `: The minimum rotation of horizontal transform. Positive value for clockwise, negative for counterclockwise.

- **Default:** -180 (float)

`horizontalRotationSpeed `: Degrees the horizontal transform can rotate per second.

- **Default:** 360 (float)

`indicatorTransform `: The transform that plays as an indicator of current aiming. Behaviour depends on rotator type: 

- Ballistic rotators move the transform to the hit position;
- Directional rotators simply enable the transform.

- **Default:** null

`indicatorColorOnTarget `: The indicator color override when rotator is on target.

- **Default:** clear

`indicatorColorAiming `: The indicator color override when rotator is not on target.

- **Default:** clear

Color string is parsed by [ColorUtility](https://docs.unity3d.com/ScriptReference/ColorUtility.TryParseHtmlString.html "ColorUtility") .

`indicatorColorProperty `: The property name of the color to override in your shader.
Check your shader property for the accurate name.
Must be set when `indicatorColorOnTarget ` and `indicatorColorAiming ` are set.

#### 🟥ParticleWeaponRotator
A ballistic rotator that only works with ParticleWeapon.

The class property is `<property name="class" value="ParticleWeaponRotator,VehicleWeapon"/> `.

#####  🔻Fixed properties:
`projectileSpeed `: The projectile speed used for angle calculation. Should be set to the velocity of particles.

`gravity `: The gravity modifier used for angle calculation. Should be set to the gravity modifier of particles.

`hitRaycastTransform `: The origin of the input ray is move to this transform. If not set, the origin is move to root transform with a 2 unit offset on Y axis.

The purpose is to avoid hitting vehicle collider with the input ray, so you get a faraway hit position most of the times. Thus if your indicator is always on the vehicle itself, consider adding an empty transform as `hitRaycastTransform `, and move it higher than all the vehicle colliders (or lower if your weapon is attached to the bottom of the vehicle).

#####  🔻Moddable properties:
`indicatorOffsetY `: When moving the indicator to the target position, move it up by this amount. Useful when projector is used as the indicator.

- **Default:** 0 (float)

`previewTypeEntity `: Create a primitive object at target position as a simple indicator. This should reflect the entity damage radius.

- **Default:** Sphere

`previewTypeBlock `: Create a primitive object at target position as a simple indicator.
This should reflect the block damage radius.

- **Default:** Sphere

For supported primitive types, refer to [Unity document](https://docs.unity3d.com/Manual/PrimitiveObjects.html "Unity document") .

`previewColorEntityOnTarget `: The color of entity preview object when rotator is on target.

- **Default:** clear

`previewColorEntityAiming `: The color of entity preview object when rotator is aiming.

- **Default:** clear

`previewColorBlockOnTarget `: The color of block preview object when rotator is on target.

- **Default:** clear

`previewColorBlockAiming `: The color of block preview object when rotator is aiming.

- **Default:** clear

`previewScaleEntity `: The scale of entity preview.

- **Default:** 0 (float)

`previewScaleBlock `: The scale of block preview.

- **Default:** 0 (float)

This simple preview is created for the purpose of auto generated indicator. When alpha value of OnTarget color or preview scale equals 0, the preview is not created. Does not take place of the indicator transform.

#### 🟥DirectionalWeaponRotator, HorizontalWeaponRotator and VerticalWeaponRotator
These are directional rotators.

The class properties are
`<property name="class" value="DirectionalWeaponRotator,VehicleWeapon"/> `,
`<property name="class" value="HorizontalWeaponRotator,VehicleWeapon"/> ` and
`<property name="class" value="VerticalWeaponRotator,VehicleWeapon"/> `.

While DirectionalWeaponRotator accepts rotation transforms on both axis, the other 2 rotators only rotates on one axis, thus only rotation transform and properties on that axis is needed.

DirectionalWeaponRotator only accept common properties. The unidirectional ones however, has a fixed `horizontalRotator `/`verticalRotator ` property according to its direction, to reference another unidirectional rotator to form a bidirectional rotator. The reason for this is as follows:

For the situation discribed in DummyWeapon, you should always reference the shared rotator. Consider one barbette with 3 turret, barbette rotates horizontally and those turrets rotate vertically, you should reference the horizontal rotator in all 3 vertical rotators.

To reduce network pressure, rotation is synced on clients only when the transform is rotated by more than 1 degrees, resulting in a small angular tolerance on other clients.

To eleminate this tolerance, a forced sync of current rotation is sent on firing a weapon. The problem is that a dummy weapon does not fire, so such forced sync never happens for the barbette. Referencing the barbette rotator in turret rotator will tell the turret to sync the rotation of the referenced rotator on firing.

Moreover, the referenced rotator also contributes to the OnTarget check.
