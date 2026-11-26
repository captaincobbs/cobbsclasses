using System;
using HarmonyLib;

namespace CobbsClasses.Source.Patches
{
    internal interface ITraitPatch
    {
        string Name { get; }
        void Register(Harmony harmony);
    }

    internal sealed class AttributedPatch(Type patchType) : ITraitPatch
    {
        public string Name => patchType.Name;

        public void Register(Harmony harmony)
        {
            harmony.CreateClassProcessor(patchType).Patch();
        }
    }

    internal sealed class RegisteredPatch(string name, Action<Harmony> register) : ITraitPatch
    {
        public string Name => name;

        public void Register(Harmony harmony)
        {
            register(harmony);
        }
    }
}
