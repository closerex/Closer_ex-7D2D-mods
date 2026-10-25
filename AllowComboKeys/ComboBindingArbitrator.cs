using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace AllowComboKeys
{
    internal sealed class ReferenceIdentityComparer<T> : IEqualityComparer<T> where T : class
    {
        public static readonly ReferenceIdentityComparer<T> Instance = new();

        private ReferenceIdentityComparer()
        {
        }

        public bool Equals(T left, T right) => ReferenceEquals(left, right);
        public int GetHashCode(T value) => RuntimeHelpers.GetHashCode(value);
    }

    internal enum ComboBindingState
    {
        Idle,
        Active,
        Suppressed
    }

    internal readonly struct ComboBindingSnapshot<TBinding> where TBinding : class
    {
        public ComboBindingSnapshot(
            TBinding binding,
            IReadOnlyList<int> includeKeys,
            bool isEligible,
            bool canActivate,
            bool holdKeyDown,
            bool allIncludesReleased)
        {
            Binding = binding;
            IncludeKeys = includeKeys;
            IsEligible = isEligible;
            CanActivate = canActivate;
            HoldKeyDown = holdKeyDown;
            AllIncludesReleased = allIncludesReleased;
        }

        public TBinding Binding { get; }
        public IReadOnlyList<int> IncludeKeys { get; }
        public bool IsEligible { get; }
        public bool CanActivate { get; }
        public bool HoldKeyDown { get; }
        public bool AllIncludesReleased { get; }
    }

    internal sealed class ComboBindingArbitrator<TBinding> where TBinding : class
    {
        private static readonly IEqualityComparer<TBinding> identityComparer =
            ReferenceIdentityComparer<TBinding>.Instance;

        private readonly Func<int, int, bool> requirementImplies;
        private readonly Dictionary<TBinding, ComboBindingState> states = new(identityComparer);
        private readonly HashSet<TBinding> liveBindings = new(identityComparer);
        private readonly List<TBinding> bindingsToRemove = new();
        private readonly List<ComboBindingSnapshot<TBinding>> activeBindings = new();
        private readonly List<TBinding> bindingsToSuppress = new();

        public ComboBindingArbitrator(Func<int, int, bool> requirementImplies)
        {
            this.requirementImplies = requirementImplies ??
                                      throw new ArgumentNullException(nameof(requirementImplies));
        }

        public ComboBindingState GetState(TBinding binding)
        {
            if (binding == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            return states.TryGetValue(binding, out var state) ? state : ComboBindingState.Idle;
        }

        public void Update(IReadOnlyList<ComboBindingSnapshot<TBinding>> bindings)
        {
            if (bindings == null)
            {
                throw new ArgumentNullException(nameof(bindings));
            }

            ValidateAndIndexLiveBindings(bindings);
            RemoveStaleBindings();
            ResetReleasedBindings(bindings);
            ActivateNewBindings(bindings);
            SuppressStrictSubsets(bindings);
        }

        public void ForgetBinding(TBinding binding)
        {
            if (binding == null)
            {
                throw new ArgumentNullException(nameof(binding));
            }

            states.Remove(binding);
        }

        public void Clear()
        {
            states.Clear();
            liveBindings.Clear();
            bindingsToRemove.Clear();
            activeBindings.Clear();
            bindingsToSuppress.Clear();
        }

        private void ValidateAndIndexLiveBindings(IReadOnlyList<ComboBindingSnapshot<TBinding>> bindings)
        {
            liveBindings.Clear();
            for (var index = 0; index < bindings.Count; index++)
            {
                var snapshot = bindings[index];
                if (snapshot.Binding == null)
                {
                    throw new InvalidOperationException("A combo binding snapshot has no binding identity.");
                }

                if (snapshot.IncludeKeys == null || snapshot.IncludeKeys.Count == 0)
                {
                    throw new InvalidOperationException("A combo binding snapshot has no include keys.");
                }

                if (!liveBindings.Add(snapshot.Binding))
                {
                    throw new InvalidOperationException("A combo binding was included more than once in one update.");
                }
            }
        }

        private void RemoveStaleBindings()
        {
            bindingsToRemove.Clear();
            foreach (var binding in states.Keys)
            {
                if (!liveBindings.Contains(binding))
                {
                    bindingsToRemove.Add(binding);
                }
            }

            for (var index = 0; index < bindingsToRemove.Count; index++)
            {
                states.Remove(bindingsToRemove[index]);
            }
        }

        private void ResetReleasedBindings(IReadOnlyList<ComboBindingSnapshot<TBinding>> bindings)
        {
            if (states.Count == 0)
            {
                return;
            }

            for (var index = 0; index < bindings.Count; index++)
            {
                var snapshot = bindings[index];
                // Suppression is a separate lock: ending a parent never unlocks its children.
                if (snapshot.AllIncludesReleased ||
                    (GetState(snapshot.Binding) == ComboBindingState.Active && !snapshot.HoldKeyDown))
                {
                    states.Remove(snapshot.Binding);
                }
            }
        }

        private void ActivateNewBindings(IReadOnlyList<ComboBindingSnapshot<TBinding>> bindings)
        {
            for (var index = 0; index < bindings.Count; index++)
            {
                var snapshot = bindings[index];
                if (snapshot is { IsEligible: true, CanActivate: true } && !states.ContainsKey(snapshot.Binding))
                {
                    states.Add(snapshot.Binding, ComboBindingState.Active);
                }
            }
        }

        private void SuppressStrictSubsets(IReadOnlyList<ComboBindingSnapshot<TBinding>> bindings)
        {
            activeBindings.Clear();
            for (var index = 0; index < bindings.Count; index++)
            {
                var snapshot = bindings[index];
                // Both participants must be eligible for NEW suppression. Disabled/listening
                // bindings retain their existing state, but do not consume other bindings.
                if (snapshot.IsEligible && GetState(snapshot.Binding) == ComboBindingState.Active)
                {
                    activeBindings.Add(snapshot);
                }
            }

            bindingsToSuppress.Clear();
            for (var childIndex = 0; childIndex < activeBindings.Count; childIndex++)
            {
                var child = activeBindings[childIndex];
                for (var parentIndex = 0; parentIndex < activeBindings.Count; parentIndex++)
                {
                    if (childIndex == parentIndex)
                    {
                        continue;
                    }

                    var parent = activeBindings[parentIndex];
                    if (IsStrictParent(parent.IncludeKeys, child.IncludeKeys))
                    {
                        bindingsToSuppress.Add(child.Binding);
                        break;
                    }
                }
            }

            for (var index = 0; index < bindingsToSuppress.Count; index++)
            {
                states[bindingsToSuppress[index]] = ComboBindingState.Suppressed;
            }
        }

        private bool IsStrictParent(IReadOnlyList<int> parent, IReadOnlyList<int> child)
        {
            return RequirementsImply(parent, child) && !RequirementsImply(child, parent);
        }

        private bool RequirementsImply(IReadOnlyList<int> stronger, IReadOnlyList<int> weaker)
        {
            for (var weakerIndex = 0; weakerIndex < weaker.Count; weakerIndex++)
            {
                var requirementFound = false;
                for (var strongerIndex = 0; strongerIndex < stronger.Count; strongerIndex++)
                {
                    if (requirementImplies(stronger[strongerIndex], weaker[weakerIndex]))
                    {
                        requirementFound = true;
                        break;
                    }
                }

                if (!requirementFound)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
