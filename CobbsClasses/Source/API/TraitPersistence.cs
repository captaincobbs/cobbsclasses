using System;

using Vintagestory.API.Datastructures;
using Vintagestory.API.Server;

namespace CobbsClasses.Source.API;

public static class TraitPersistence
{
    public static float ReadFloat(ITreeAttribute? tree, string key, float fallback, float minimum, float maximum) => TraitMath.ClampFinite(tree?.GetFloat(key, fallback) ?? fallback, minimum, maximum, fallback);

    public static double ReadTimestamp(IServerPlayer player, string key, double now)
    {
        double value;
        try
        {
            value = player.GetModData(key, double.NaN);
        }
        catch (Exception)
        {
            return now;
        }

        return double.IsFinite(value) && value <= now ? value : now;
    }

    public static void WriteVersionedFloat(ITreeAttribute tree, string key, float value, float fallback, float minimum, float maximum)
    {
        tree.SetFloat(key, TraitMath.ClampFinite(value, minimum, maximum, fallback));
        tree.SetInt(key + ":version", 1);
    }

    public static float ReadVersionedFloat(ITreeAttribute tree, string key, float fallback, float minimum, float maximum)
        => tree.GetInt(key + ":version", 0) is 0 or 1 ? ReadFloat(tree, key, fallback, minimum, maximum) : fallback;
}