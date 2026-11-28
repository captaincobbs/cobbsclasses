using System;
using System.Collections.Generic;
using System.Linq;

using CobbsClasses.Source.Compatibility;
using CobbsClasses.Source.Conditions;

using Vintagestory.API.Common;

namespace CobbsClasses.Source.API
{
    public enum PatchStatus { Applied, AlreadyApplied, NotRequired, MissingTarget, Failed }
    public sealed record PatchDiagnostic(string ModId, string Target, PatchStatus Status, string? Details = null);

    public sealed class CobbsClassesAPI
    {
        public Version ApiVersion { get; } = new(1, 0);
        public TraitRegistry Attributes { get; } = new();
        private readonly Dictionary<string, Func<IConditionalTrait>> conditions = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Func<IModIntegration>> integrations = new(StringComparer.Ordinal);
        private readonly List<PatchDiagnostic> diagnostics = [];
        private bool conditionsFrozen;
        private bool integrationsFrozen;
        private HashSet<string> availableClasses = new(StringComparer.Ordinal);

        public IReadOnlyList<PatchDiagnostic> CompatibilityReport => [.. diagnostics];
        public IReadOnlyList<string> AvailableClasses => [.. availableClasses.OrderBy(code => code, StringComparer.Ordinal)];
        public bool IsClassAvailable(string code) => availableClasses.Contains(code);

        public void RegisterCondition(string code, Func<IConditionalTrait> factory)
        {
            if (conditionsFrozen)
            {
                throw new InvalidOperationException("Register conditions during Start, before GameReady.");
            }

            Register(conditions, code, factory);
        }

        public void RegisterIntegration(string code, Func<IModIntegration> factory)
        {
            if (integrationsFrozen)
            {
                throw new InvalidOperationException("Register integrations during Start, before asset finalization.");
            }

            Register(integrations, code, factory);
        }

        private static void Register<T>(Dictionary<string, Func<T>> registry, string code, Func<T> factory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(code);
            ArgumentNullException.ThrowIfNull(factory);
            if (!registry.TryAdd(code, factory))
            {
                throw new InvalidOperationException($"Extension {code} already registered.");
            }
        }

        internal IEnumerable<KeyValuePair<string, Func<IConditionalTrait>>> Conditions()
        {
            conditionsFrozen = true;
            return [.. conditions];
        }

        internal IEnumerable<KeyValuePair<string, Func<IModIntegration>>> Integrations()
        {
            integrationsFrozen = true;
            return [.. integrations];
        }

        internal void Record(PatchDiagnostic diagnostic) => diagnostics.Add(diagnostic);
        internal void SetClasses(IEnumerable<string> codes) => availableClasses = new HashSet<string>(codes, StringComparer.Ordinal);
        internal static CobbsClassesAPI? For(ICoreAPI api) => api.ModLoader.GetModSystem<CobbsClassesModSystem>()?.ClassesAPI;
    }
}