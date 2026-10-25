using InControl;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace AllowComboKeys
{
    // Presence is independent of visibility, enablement and listener input blocking.
    internal sealed class KeyBindingSnapshotBuilder
    {
        private static readonly Func<BindingSource, PlayerAction> getBoundTo = CreateBindingGetter<PlayerAction>("BoundTo");
        private static readonly Func<BindingSource, bool> getIsValid = CreateBindingGetter<bool>("IsValid");
        private readonly Dictionary<KeyBindingSource, CachedBinding> configurations =
            new(ReferenceIdentityComparer<KeyBindingSource>.Instance);
        private readonly HashSet<KeyBindingSource> presentBindings =
            new(ReferenceIdentityComparer<KeyBindingSource>.Instance);
        private readonly List<KeyBindingSource> bindingsToRemove = new();

        // False means physical state is unavailable, NOT that every key has been released.
        // Still reconcile ownership/configuration/removal so true lifecycle changes take effect.
        public bool Build(IReadOnlyList<PlayerActionSet> actionSets, IKeyboardProvider keyboard,
            ComboBindingArbitrator<KeyBindingSource> arbitrator,
            List<ComboBindingSnapshot<KeyBindingSource>> snapshots)
        {
            snapshots.Clear();
            presentBindings.Clear();
            for (var setIndex = 0; setIndex < actionSets.Count; setIndex++)
            {
                var set = actionSets[setIndex];
                var setBlocked = set.IsListeningForBinding && set.PreventInputWhileListeningForBinding;
                for (var actionIndex = 0; actionIndex < set.Actions.Count; actionIndex++)
                {
                    var action = set.Actions[actionIndex];
                    var eligible = set.Enabled && action.Enabled && !action.IsListeningForBinding && !setBlocked;
                    var bindings = action.UnfilteredBindings;
                    for (var bindingIndex = 0; bindingIndex < bindings.Count; bindingIndex++)
                    {
                        if (!(bindings[bindingIndex] is KeyBindingSource binding) ||
                            !ReferenceEquals(getBoundTo(binding), action))
                        {
                            // RemoveBinding leaves an orphan in regularBindings until the next update.
                            continue;
                        }
                        if (!presentBindings.Add(binding))
                        {
                            throw new InvalidOperationException(
                                "A key binding source was attached to the action graph more than once.");
                        }

                        var combo = binding.Control;
                        if (!configurations.TryGetValue(binding, out var cached) || !cached.Matches(action, combo))
                        {
                            arbitrator.ForgetBinding(binding);
                            cached = new CachedBinding(action, combo);
                            configurations[binding] = cached;
                        }
                        if (keyboard == null || cached.Includes.Length == 0)
                        {
                            continue;
                        }

                        var allReleased = true;
                        var fullMatch = true;
                        var holdKeyDown = false;
                        for (var keyIndex = 0; keyIndex < cached.Includes.Length; keyIndex++)
                        {
                            var down = keyboard.GetKeyIsPressed((Key)cached.Includes[keyIndex]);
                            allReleased &= !down;
                            fullMatch &= down;
                            if (keyIndex == cached.Includes.Length - 1)
                            {
                                holdKeyDown = down;
                            }
                        }
                        // This is KeyCombo.IsPressed's include/exclude predicate, using the same
                        // provider as the physical release/hold observations. Excludes gate entry,
                        // not an already active hold-key session.
                        if (fullMatch)
                        {
                            for (var keyIndex = 0; keyIndex < cached.Excludes.Length; keyIndex++)
                            {
                                if (keyboard.GetKeyIsPressed((Key)cached.Excludes[keyIndex]))
                                {
                                    fullMatch = false;
                                    break;
                                }
                            }
                        }
                        snapshots.Add(new ComboBindingSnapshot<KeyBindingSource>(binding, cached.Includes,
                            eligible && getIsValid(binding), fullMatch, holdKeyDown, allReleased));
                    }
                }
            }

            bindingsToRemove.Clear();
            // Every present binding was reconciled above; equal counts leave no stale entries.
            if (configurations.Count != presentBindings.Count)
            {
                foreach (var binding in configurations.Keys)
                {
                    if (!presentBindings.Contains(binding))
                    {
                        bindingsToRemove.Add(binding);
                    }
                }
                for (var index = 0; index < bindingsToRemove.Count; index++)
                {
                    ForgetBinding(bindingsToRemove[index], arbitrator);
                }
            }
            return keyboard != null;
        }

        public void ForgetBinding(KeyBindingSource binding, ComboBindingArbitrator<KeyBindingSource> arbitrator)
        {
            configurations.Remove(binding);
            arbitrator.ForgetBinding(binding);
        }

        public void Clear()
        {
            configurations.Clear();
            presentBindings.Clear();
            bindingsToRemove.Clear();
        }

        private static Func<BindingSource, TResult> CreateBindingGetter<TResult>(string propertyName)
        {
            var property = typeof(BindingSource).GetProperty(propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var getter = property?.GetGetMethod(true);
            if (getter == null)
            {
                throw new MissingMemberException(typeof(BindingSource).FullName, propertyName);
            }
            return (Func<BindingSource, TResult>)getter.CreateDelegate(typeof(Func<BindingSource, TResult>));
        }

        private sealed class CachedBinding
        {
            private readonly PlayerAction owner;
            public readonly int[] Includes;
            public readonly int[] Excludes;

            public CachedBinding(PlayerAction owner, KeyCombo combo)
            {
                this.owner = owner;
                Includes = new int[combo.IncludeCount];
                Excludes = new int[combo.ExcludeCount];
                for (var index = 0; index < Includes.Length; index++)
                {
                    Includes[index] = (int)combo.GetInclude(index);
                }
                for (var index = 0; index < Excludes.Length; index++)
                {
                    Excludes[index] = (int)combo.GetExclude(index);
                }
            }

            public bool Matches(PlayerAction action, KeyCombo combo)
            {
                if (!ReferenceEquals(owner, action) || Includes.Length != combo.IncludeCount ||
                    Excludes.Length != combo.ExcludeCount)
                {
                    return false;
                }
                for (var index = 0; index < Includes.Length; index++)
                {
                    if (Includes[index] != (int)combo.GetInclude(index))
                    {
                        return false;
                    }
                }
                for (var index = 0; index < Excludes.Length; index++)
                {
                    if (Excludes[index] != (int)combo.GetExclude(index))
                    {
                        return false;
                    }
                }
                return true;
            }
        }
    }
}
