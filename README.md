# AbilityKit

Shared ability slots for JoksterCube's Valheim mods (PingMark, TempestRite and the ones that follow): copies of the
Guardian Power slot on the HUD, lined up beside it.

AbilityKit is **not a mod**. Each mod builds it from source and merges it into its own DLL, so:

- players install nothing extra and there is no dependency to keep in step;
- each mod has its own private copy (merged as `internal`), so two mods built against different AbilityKit versions
  never clash.

## What's in it

| File | Contents |
| --- | --- |
| `SlotPriority` | Every slot's claimed place in the row, one value per slot (see below). |
| `AbilitySlot` | An ability's slot: icon, name, and "Ready" or a countdown under it, exactly like the Guardian Power's. The icon fades like a power on cooldown while the ability isn't ready. The slot's touch button is turned off; each ability has its own shortcut. An ability that is a switch can show a red cross over its icon while off (`crossed: true`), the same cross as ChestKit's flap buttons. |

## Using it in a mod

1. Put this folder next to the mod's folder (`Sources/AbilityKit`, `Sources/MyMod`).
2. In the mod's `.csproj`:

   ```xml
   <ItemGroup>
     <ProjectReference Include="..\AbilityKit\AbilityKit\AbilityKit.csproj" />
   </ItemGroup>
   ```

3. In the mod's `ILRepack.targets`, merge it (with `Internalize="true"`):

   ```xml
   <InputAssemblies Include="$(OutputPath)\AbilityKit.dll" />
   ```

4. Make the slot once and update it every frame from a `Hud.UpdateGuardianPower` postfix:

   ```csharp
   static readonly AbilitySlot Slot = new("MyMod_MyAbility", SlotPriority.MyModMyAbility);  // claim a value first

   [HarmonyPatch(typeof(Hud), nameof(Hud.UpdateGuardianPower))]
   static class MyAbilityHudPatch
   {
       static void Postfix(Hud __instance) =>
           Slot.Update(__instance, show, icon, "My Ability",
               cooldown > 0f ? AbilitySlot.TimeText(cooldown) : AbilitySlot.ReadyText, ready: cooldown <= 0f);
   }
   ```

   AbilityKit has no Harmony patches of its own, so there is nothing to initialize. The slot is made the first time it
   shows and again after each logout (the HUD is rebuilt).

Game paths come from `environment.props` (override them in a git-ignored `environment.props.user`).

## The row of slots

All slots sit in one row with the Guardian Power: the power first (while the player has one), then the slots
**left to right by priority**, lower priority further left. Slots with the same priority are ordered by id, so every
player sees the same order. Only slots that are showing take up room, so there are never gaps, and a slot takes the
power's place while no power is chosen:

```
[Guardian Power]  [PingMark: Perception]  [TempestRite: Rally Call]
[Guardian Power]  [TempestRite: Rally Call]
[TempestRite: Rally Call]
```

The slots are **spaced evenly**, one step apart. Each name, the Guardian Power's included, sits in a field a little
wider than an icon and, if too long, shrinks to fit it on one line (down to about half size), so long names don't
push the slots apart.

**Hiding the names.** A mod can offer a setting that hides the names above the Guardian Power and every slot, which
moves the slots closer still. Call this every frame from the same postfix, before updating your slots:

```csharp
AbilitySlot.SetNamesHidden(__instance, "MyMod", hidden: !ShowAbilityNames.Value);
```

It leaves an empty, inactive sibling named `AbilityKit_HideNames|owner`; while any mod has one there, every
AbilityKit slot hides its name. PingMark offers this as **Show Ability Names**.

The mods each carry their own copy of AbilityKit, so they agree through the slots themselves: a slot's object is named
`AbilityKit_Slot|priority|id`, a sibling of the Guardian Power slot, and each slot places itself among the showing
slots it finds there. Keep that name format, and `AbilityKit_HideNames|owner`, if AbilityKit changes, or older and
newer mods will stop lining up.

Priorities are claimed in the `SlotPriority` enum (`SlotPriority.cs`), one value per slot, so no two slots share a
place by accident. A new ability adds its value there and a row here before it uses it. Never change a value a
released mod already uses: the number goes into the slot's object name.

| Priority | Mod | Ability (`SlotPriority`) |
| --- | --- | --- |
| 100 | PingMark | Perception (`PingMarkPerception`) |
| 400 | TempestRite | Morgen-Pouch, Morgen-Pouch Rite (`TempestRitePetPouch`) |
| 800 | PingMark | Fishsight, while a fish is hooked (`PingMarkFishsight`) |
| 850 | TempestRite | Domain Border, inside a Domain rite's area (`TempestRiteDomainBorder`) |
| 900 | TempestRite | Rally Call, Rally-Horn Rite (`TempestRiteRallyCall`) |
| 901 | TempestRite | Refuse, beside Rally Call (`TempestRiteRallyRefuse`) |

Leave gaps (100, 200, 300...) so new abilities can go in between.

## License

GNU GPL v3 or later.
