# OwnItemValue effect module

Use OwnItemValue,KFCommonUtilityLib as a mea_module on an attachment's triggered effect. This applies to normal modifications and cosmetic modifications, not only weapon skins. Skin XML remains owned by its author.

## Contract

- ItemValue.FireEvent saves the previous global source, selects its exact attachment receiver (or null for a non-attachment event), and restores the previous source on normal return. Multiple attachments of the same type remain distinct.
- OwnItemValue module prefixes temporarily replace the original MinEventParams.ItemValue during CanExecute and Execute. Each postfix restores the value saved by its own invocation, including nested calls using the same parameters or action instance. No parameter view or attachment is cloned.
- The switch runs before other module prefixes and the restoration after other module postfixes, independently of XML order. Explicit HarmonyPriority annotations control module callback order; equal/default priorities retain the existing order.
- Without an attachment source, this module leaves normal ItemValue writeback unchanged. Unconfigured effects and effect-group requirements use the caller's parameters normally.
- Only ItemValue is temporarily replaced. All other parameter fields and references, including ItemInventoryData and ItemActionData, remain unchanged and retain normal writeback behavior. Metadata writes affect the actual attachment.
- This uses synchronous game-thread global state, not concurrent or asynchronous dispatch. There is deliberately no try/finally, catch, or finalizer: an exception that prevents a postfix can leave the global source and/or the shared parameter ItemValue changed. Cleanup after such exceptions is not guaranteed.
- The module does not create new triggers, bypass attachment exclusion rules, issue inventory notifications, or automatically enable wear/material/UI behavior.

## Integration

Add the module to each effect that should read or write the attachment's own ItemValue. Other effects in the same group retain host semantics after a normal return. It can coexist with other mea_module entries.

Only ItemValue.FireEvent has source-scoping Harmony prefix/postfix patches. Native controller/group dispatch methods and their calls remain unchanged; there are no dispatcher reverse copies or dispatcher parameter-rewriting transpilers.
