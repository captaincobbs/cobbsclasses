# STATS FROM OTHER MODS
----
## Butchering

Maxes are suggested, you can go over but the mod said that they "don't support it".

* butcherSpeedMul, speed of butchering animals on hook and table (-0.5 - 5)
* butcheringDurabilityLossMul, reduces tool damage when butchering for knife and cleaver (-0.8 - 0.8)
* animalCarryingEfficiencyMul, Counters how much animal mass slows you down (-0.5 - 0.5)
* fieldHarvestRuteMul, Doesn't work? Probably implement on my own?

## Immersive Woodworking

Mod maker said that they do not want to add normal skill modifiers for class mods, instead they added XSkills modifiers. Sounds like I will likely need harmony. All modifiers shown on [this](https://mods.vintagestory.at/immersivewoodworking) page

## PetAI

API is [here](https://github.com/G3rste/petai/blob/main/README.md#api-information)

## Footprints

Mod author gave me an explanation on how to modify player/animal footprint lifespan that I can copy on request. Suggestion I access the mod's assembly, access the modsystem's ConfigClient, then in the ConfigClient modify PlayerFootprintLifespam, AnimalFootprintLifespan, etc. Also suggested I could do something similar with their blood trails mod.

## Alchemy

Seems like generally patches can just do this? Otherwise, look into its base library, EffectLib

## StoneQuarry

PlugWorkModifier?