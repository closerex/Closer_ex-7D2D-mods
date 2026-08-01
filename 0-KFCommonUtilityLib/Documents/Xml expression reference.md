# ${\color{red}The \space Problem}$

`CVar` is a handy tool. It allows you to pass a random value instead of a magic number to a triggered effect or passive effect. It also supports simple calculation and comparison, allowing xml logic with certain degree of complexity.

But that's all about it. It has very limited access to the rest of the system, and takes several lines of instructions to achieve something simple as a capped increasement. It's also hardcoded to take the referenced value (a `@cvarname`) from the target entity, making it a pain when you want the value from the event initiator. Besides, the supported usage is defined per event: for example, `ModifyCVar` accepts not only a `@cvarname`, but also formatted `random`/`tierlist`/`progressionlist` etc; while many of the other events accepts only a `@cvarname`. This inconsistency is simply annoying.

# ${\color{aqua}The \space Prerequisite \space \color{lightblue}(skip \space if \space you \space don't \space code... \space manually)}$

Back in 2023, I was looking for a way to handle complex polymorphism on vanilla classes, specifically for `ItemAction`, in order to inject composition pattern support into the inheritance pattern. The idea is to define some `module` that contains patches on specific virtual methods and apply them during xml parsing, then create a new class that contains all the changes to replace the original type retrieved by reflection.

The system was first implemented with Mono.Cecil with some nasty harmony patch on HarmonyLib itself, with fully harmony patch compatibility. Recently I decided to rewrite it with Reflection.Emit which is much more flexible for type creation, also with the support of other types such as `ItemClass`. ~~Honestly I was just trying to deal with the incompatibility with `HarmonyLib`'s `CodeInstruction.CallClosure` which generates a runtime method that might present in different forms among which none can be saved to file with my created assembly. I'll talk about it in detail in the document about module system.~~

As a result, it enables me to write patches that are only applied to certain items, and combine them freely to create items with complex behaviors which otherwise would require an enormous metaclass or a long list of custom `ItemAction`s and `ItemClass`es. You must have already seen the system in action: just search `ItemClassModules` or `ItemActionModules` in the `items.xml` of any mod that requires kflib.

# ${\color{lightgreen}First \space Glance \space at \space the \space Solution}$

With the module system functioning, I'm now able to change how this `CVar` bullshit works completely. Some of you might have already used the `CVarExpression` effect which allows you to modify a `CVar` by a value calculated by an expression. It's a very powerful tool (still valid and also enpowered by the new system), but we can go further.

The design is to take a CVar as the placeholder that activates the module branch and also the default value, then hack the value handling by applying certain modules in following format:

```xml
<triggered_effect trigger="sometrigger" action="ModifyCVar" cvar="somecvar" operation="set" value="@defaultcvar">
    <mea_module module="ExpressionAsCVar,KFCommonUtilityLib" expr="some complex expression"/>
</triggered_effect>
```

This document should contain all the info you need to write your own cvar expression.

# ${\color{lime}The \space Solution}$

As you've already guessed, the trick here is that `mea_module` block. Let's dive into it without further delay.

I'll start with the big picture. If you find some part hard to understand, go through the rest of the document and review it again, or check out the examples in later sections.

## ${\color{gold}Module}$

A `module`, in this case a `mea_module`, stands for a class that contains a bunch of patches that gets applied to the original `action` class. The `mea` here stands for `MinEventAction` which is the vanilla prefix for the `action` class. The module we'll be discussing is designed to hijack the cvar retrieval code and run our expression calculation code to replace the original cvar value. For this purpose, currently 3 modules are created (add `,KFCommonUtilityLib ` after the name as above):

### ${\color{yellow}VariableAsCVar}$

The simplest implementation that replaces the cvar with another variable of one of the predefined variable types which I'm going to introduce later.

##### Attributes:

* `var`: The variable.
* `*`: A variable might contain its own additional properties. Unlike other modules that read the input from nested elements, `VariableAsCVar` reads them from the current element. You must add them after the `var` attribute though.

This module is good for simple variable replacing, for example access a metadata from holding item or perform a random roll.

### ${\color{yellow}ExpressionAsCVar}$

The standard expression implementation.

##### Attributes:

* `expr` or `expression`: The expression.

##### Child Elements:

This module accepts child elements with the name `var`, each of which stands for the additional properties of a **unique** variable. Use `name` attribute with the full variable content wrapped in the square bracket to specify the target variable.

Attributes are defined by each variable.

### ${\color{yellow}ComplexCondition}$

This module works as a requirement that filters current target entities by comparing 2 expressions. If the comparison yields false, the target is removed from the list.

Honestly this could also be implemented with a `Requirement`, but there's some major changes to the `Requirement` parsing format in v2.5 so I'm leaving the module system for `Requirement` to v3.0.

##### Attributes:

* `op` or `operation`: The compare operation, same as `CVarCompare`. FYI supported operations are as follows:
  * `=`: `E`, `EQ`, `Equals`
  * `!=`: `NE`, `NEQ`, `NotEquals`
  * `<`: `LT`, `Less`, `LessThan`
  * `>`: `GT`, `Greater`, `GreaterThan`
  * `<=`: `LTE`, `LessOrEqual`, `LessThanOrEqualTo`
  * `>=`: `GTE`, `GreaterOrEqual`, `GreaterThanOrEqualTo`
* `l_val`/`r_val` or `l_value`/`r_value`: The left/right side of the inequality as a single float value.
* `l_var`/`r_val` or `l_variable`/`r_variable`: The left/right side of the inequality as a variable.
* `l_expr`/`r_expr` or `l_expression`/`r_expression`: The left/right side of the inequality as an expression.

##### Child Elements:

Similar to `ExpressionAsCVar`, this module also takes child elements with the name `var` and leaves the additional attributes to the variables. In addition, it also takes a `side` attribute that defines the side of the inequality it should be applied to. Supported values are `Left`, `Right` and `Both`(default).

## ${\color{gold}Expression}$

An Expression is a collection of a bunch of variables and operators/built-in functions that returns a float value after evaluation. The supported form range from simple arithmetic operations to combined logical branches.

##### ${\color{yellow}Supported \space operators}$

* Mathematical operators: `+ - * / % ^`
* Logical operators: `= != < <= > >=`
* `NOT(v)`: Returns 1 if v is approximately 0, otherwise returns 0.
* `AND(v1, v2)`: Return v2 if v1 is not 0, otherwise returns v1.
* `OR(v1, v2)`: Returns v1 if v1 is not 0, otherwise returns v2.

##### ${\color{yellow}Supported \space functions} $

* `ROUND(v)`: Return `v` rounded to the nearest integer number.
* `CEILING(v)`: Return the smallest integer number greater than or equal to `v`.
* `FLOOR(v)`: Returns the greatest integer number smaller than or equal to `v`.
* `LOG(v, p)`: Returns the logarithm of a specified number `v` in a specified base `p`.
* `LOG(v)`: Returns the base 10 logarithm of a specified number `v`.
* `MIN(v1, v2)`: Return the smaller value between `v1` and `v2`.
* `MAX(v1, v2)`: Return the bigger value between `v1` and `v2`.
* `IF(cond1, v1[, cond2, v2...], default)`: Generates a if-elif-else branch. You must provide N pairs of condition and value (N >= 1) and a default value, and the conditions are checked from left to right, returning the paired value of the first valid one. The condition returns true if the value of `cond` is not 0.

##### ${\color{yellow}Alias \space Definition}$

When a complex variable appears multiple times in the expression, you can define an alias to simplify the content with following format:

```
$aliasname1:complex variable definition;
$aliasname2:complex variable definition;
[{aliasname1}] + [{aliasname2}]
```

All goes into the `expression` attribute string. An alias definition starts with `$`, and the alias name is separated from the expression by `:`; Then the definition ends with `;`. You can define them in multiple lines as shown above for clarity.

An alias definition can only contain one top level variable, which means you can't write `$alias:[var1] + [var2]`; To achieve this, use an expression variable instead: `$alias:expr=[var1] + [var2]`. When `alias1` is contained in `alias2`, it must be defined before `alias2`.

To refer an alias variable in expression, write `[{alias}]`.

### ${\color{orchid}Wait \space a \space minute...}$

The design looks a bit weird for a 7 Days xml isn't it? Why don't you define the variables in their own element with user assigned name and type and use that name in the expression to avoid confusion?

Well, the primary goal is to simplify your xml - not bloat it up. Usually it's enough to just put some CVar and metadata in it and skip the extra properties, and all those additional features are provided just in case you need it.

## ${\color{gold}Variable}$

Variable is the bridge between game data and the expression. In general, a variable contains a value source and a default value: if it fails to retrieve the value from the source, the default value is returned. You can also require an explicit validation on the source, which prevents the whole expression from being evaluated when failed - in this case, the original cvar value provided by the trigger event itself is used.

### ${\color{yellow}Basic \space Properties}$

A variable is always wrapped in a square bracket `[]`. The prefix of the variable name defines the type of the value source, but they all share following options:

* When variable name starts with `~`, it's only evaluated once for the first target, and the result is reused for the rest of the targets. This symbol is removed before parsing the variable, but remains as part of the variable name.
* When variable name ends with `|` followed by a valid float value, the value is parsed as the default value (for example [var|1.5]). **Variable Nesting** is also supported (for example [var1|[var2]]). 0 is the default value if not present. This segment is removed before parsing the variable, but remains as part of the variable name.
* `validate`: Parsed from additional xml attibute, indicating that validation for this variable is required.
* `use_self_target`: Parsed from additional xml attibute, indicating that when retrieving entity bound values, take it from the initiator instead of the target. The default value is true except for `CVar` type variables.

A variable can be either **unique** or not according to its type. Multiple presence of the same unique variable in the expression is evaluated only once.

While all the variable types support **nested variable** as default value, some types also accept them as part of the evaluation. Nested variables are guaranteed to be evaluated before parent variable even if their value is not needed for current evaluation.

### ${\color{yellow}Uniqueness}$

2 variables of the same unique type are considered different when they differ in full string contents (case insensitive and space removed). For example, [var], [~var], [var|0] and [var|1] are considered different variable.

Non-unique types generate different variables even if their share the same contents. You can define an alias to enforce the uniqueness. If a non-unique type has extra properties, you must define an alias in order to modify them. When an alias is defined, you must use the alias name as the variable name when parsing extra properties.

Multiple instances of the same unique variable across alias definition and main expression are also considered the same variable. You can imagine the variable being nested to the alias definition and the alias being nested to the expression, and child is always evaluated before parent.

> Being defined as the top level variable of an alias definition does not count as such an instance. For example, in following expression:
>
> ```
> $alias:var1|[var2];
> [var1|[var2]] + alias + var2
> ```
>
> `var2` is evaluated once for all 3 occurence before other variables, then `[var1|[var2]]` from main expression, finally `alias`.
>
> This generally means you can also use alias to enforce different variables for the same string content regardless of uniqueness, which allows specifying different behaviour via extra properties.

### ${\color{yellow}Variable \space Types}$

As mentioned above, the prefix of the variable defines the type of the value source, aka variable type. In this section, I'll go through the types provided by kflib.

#### ${\color{lightsalmon}CVar}$

The good old CVar that everyone loves.

##### Syntax: `[@name]`

* `@` is the prefix and `name` is the cvar name.

##### Is Unique: Yes

##### Extra Properties: None

##### Validation: Check if target entity exists and has the CVar.

##### Remarks:

* If validation is skipped, 0 is returned instead of the default value even if the target does not have the CVar.

#### ${\color{lightsalmon}Metadata}$

Similar to CVar but something bound to the item. It can store `float`, `int` and `string` value but the one used in expression must be a `float` value.

##### Syntax: `[#name]`

* `#` is the prefix and `name` is the metadata name.

##### Is Unique: Yes

##### Extra Properties:

* `use_holding_item`: A bool value indicating this variable should check for the holding item of the target instead of the item being assigned to the event.

##### Validation:

* If `use_holding_item` is true, check if target entity exists first, then check if the holding item is not null;
* Otherwise, check if event assigned item is not null.

#### ${\color{lightsalmon}LocalCache}$

Local cache is a user defined value associated with designated action module. It allows modder to calculate a passive value in trigger effects and cache them for later usage, in order to improve performance and provide a bridge to other systems, specifically requirements since it's not safe to calculate a passive within a requirement - the worst case is game being locked up with a infinite loop.

I hope I can talk about the details in the module system, but for now you can refer to my MedicPerk mod.

##### Syntax: `[$name(index)]`

* `$` is the prefix and `name` is the cache name.
* `index` is the action index that stores the cached value. When not set, the current action index is used, and the brackets should be removed.

##### Is Unique: Yes

##### Extra Properties:

* `use_holding_item`: A bool value indicating this variable should check for the holding item of the target instead of the item being assigned to the event.

##### Validation:

* If `use_holding_item` is true, check if target entity exists first, then check if the holding item is not null, finally check if the action exists;
* Otherwise, check if event assigned item is not null, then check if the action exists.

#### ${\color{lightsalmon}Progression}$

Get a value based on current progression level and its corresponding value list.

##### Syntax: `[%progression(num1,num2,...,numN=value1,value2,...,valueN)]`

* `%` is the prefix and `progression` is the name of the progression, for example `%perkGunslinger`.
* `numN` must be a plain float number.
* `valueN` can be either a float number or a nested variable.

##### Is Unique: Yes

##### Extra Properties: None

##### Validation: Check if target exists and has the progression.

#### ${\color{lightsalmon}ItemTier}$

Get a value based on the quality of current item and its corresponding value list.

##### Syntax: `[tier(num1,num2,...,numN=value1,value2,...,valueN)]`

* `tier(...)` is the identity.
* `numN` must be a plain float number.
* `valueN` can be either a float number or a nested variable.

##### Is Unique: Yes

##### Extra Properties:

* `use_holding_item`: A bool value indicating this variable should check for the holding item of the target instead of the item being assigned to the event.

##### Validation:

* If `use_holding_item` is true, check if target entity exists first, then check if the holding item is not null;
* Otherwise, check if event assigned item is not null.

#### ${\color{lightsalmon}PassiveEffect}$

Calculate a passive value by the specified tags.

##### Syntax: `[^passive(tag1,tag2...)]`

* `^` is the prefix and `passive` is the passive name, for example `^EntityDamage`.
* `tag1,tag2,...` are the tags to be used. If no tags is specified, you should remove the brackets, and the item tag is used instead.
* You can also use `+tag1,tag2...` for the tags to indicate that these tags should be combined with the item tags.

##### Is Unique: Yes

##### Extra Properties:

* `use_holding_item`: A bool value indicating this variable should use the holding item of the target instead of the item being assigned to the event for passive calculation.
* `use_equipment`: A bool value indicating equipments should be included for passive calculation.
* `use_progression`: A bool value indicating progressions should be included for passive calculation.
* `use_buffs`: A bool value indicating buffs should be included for passive calculation.
* `use_challenges`: A bool value indicating challenges should be included for passive calculation.
* `use_mods`: A bool value indicating item modifiers should be included for passive calculation.
* `use_durability`: A bool value indicating durability should be considered for passive calculation.
* `tags`: Override the tags defined in the syntax. You can use this property to specify tags to simplify the expression.

##### Validation: Check if target entity exists.

#### ${\color{lightsalmon}RandomInt}$

Get a random integer between specified range.

##### Syntax: `[randomInt(min,max)]`

* `randomInt(...)` is the identity.
* `min` is the inclusive minimum and `max` is the exclusive maximum, both accepts either plain float value or nested variable.

##### Is Unique: No

##### Extra Properties: None

##### Validation: None

##### Remarks:

* If `min` > `max`, default value is returned.

#### ${\color{lightsalmon}RandomFloat}$

Get a random float between specified range.

##### Syntax: `[randomFloat(min,max)]`

* `randomFloat(...)` is the identity.
* `min` is the inclusive minimum and `max` is the exclusive maximum, both accepts either plain float value or nested variable.

##### Is Unique: No

##### Extra Properties: None

##### Validation: None

##### Remarks:

* If `min` > `max`, default value is returned.

#### ${\color{lightsalmon}Stat}$

Get the specified type of an entity stat.

##### Syntax: `[stat(stat_type,value_type)]`

* `stat(...,...)` is the identity.
* `stat_type` is the entity stat type. Supported values:
  * Health
  * Stamina
  * Food
  * Water
* `value_type` is the actual value type to retrieve. Supported values:
  * Current
  * CurrentPerc
  * Max
  * MaxModifier
  * ModifiedMax
  * ModifiedMaxPerc

##### Is Unique: Yes

##### Extra Properties: None

##### Validation: Check if the target entity exists and has the specified stat.

#### ${\color{lightsalmon}Expression}$

Nest a whole expression as a variable.

##### Syntax: `[expr=expression_body]`

* `expr=` is the prefix.
* `expression_body` is the actual expression.

##### Is Unique: Yes

##### Extra Properties: None

##### Validation: None

##### Remarks:

* A nested expression variable shared variable collection with the main expression.

#### ${\color{lightsalmon}TargetIndex}$

Get the index of the current target.

##### Syntax: `[targetIndex]`

##### Is Unique: Yes

##### Extra Properties: None

##### Validation: None

#### ${\color{lightsalmon}TargetCount}$

Get the total count of the targets.

##### Syntax: `[targetCount]`

##### Is Unique: Yes

##### Extra Properties: None

##### Validation: None

#### ${\color{lightsalmon}Distance}$

Get the distance from the initiator to the target.

##### Syntax: `[distance]`

##### Is Unique: Yes

##### Extra Properties: None

##### Validation: Check if initiator exists.

#### ${\color{lightsalmon}HPDamage}$

Get the hp damage from last attack.

##### Syntax: `[hpDamage]`

##### Is Unique: Yes

##### Extra Properties: None

##### Validation: Check if damage source exists.

##### Remarks:

* Only use this variable in specific attack triggers. Due to how the data is updated, the value you get could be either damage taken or damage dealt, depending on the trigger.

#### ${\color{lightsalmon}ArmorDamage}$

Get the armor damage from last attack.

##### Syntax: `[armorDamage]`

##### Is Unique: Yes

##### Extra Properties: None

##### Validation: Check if damage source exists.

##### Remarks:

* Only use this variable in specific attack triggers. Due to how the data is updated, the value you get could be either damage taken or damage dealt, depending on the trigger.
