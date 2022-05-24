# Vehicle Weapon

This readme will guide you through the current conception of vehicle weapon, and explain each step in detail.

### How is this accomplished

If you have worked with my **CustomParticleLoader** and its subsequent patches, you will find this one similar to the multi explosion stuff, and that's the reason it requires that patch. I recommend you refer to the custom explosion tutorial before you continue, since you'll need custom explosion particles anyway.

To be short, what that multi explosion patch does is handling particle collision events and triggering explosions on collision position. It can also check for particle lifetime in every `FixedUpdate ` to trigger explosions on particle death. The vehicle weapon system available now reuses that script, and add a set of controls on the particle system, which requires **CustomPlayerActionManager**. Thus, the main task here is attaching particle systems that emits projectile style particles to the vehicle, and add proper parts with necessary properties to your vehicle.

### Prepare your vehicle

**VehicleWeaponTest** has a unity package inside with the tutorial vehicle it uses. You can tweak its xml to test with each property. Note that it requires **CustomParticleLoaderSpawnEntity**, **ParticleScriptTest** and **Kaboomstick** in addition since I reused their particles.

I actually don't make vehicles on my own, so I assume you are better at it than I am. Only notable issue here is the self collision problem. You'll need to reduce the collider size as much as possible, add RigidBody to the weapon transform and enable **Inherit Velocity** on the particle system with **Initial** and **Multiplier** set to 1, and move the particle system transforms forward in its shooting direction in case of vehicle veering.

Most importantly, **enable Collision module with *Send Collision Messages*** and set **Collision Quality** to high. For additional notes on particle system, refer to the sub explosion part of my custom explosion particle tutorial.

### Setup your xml

**VehicleWeaponTest** has all properties with comment in vehicles.xml.

I may add some more supplemental instructions here if people find them vague, so feel free to bug me in discord.



