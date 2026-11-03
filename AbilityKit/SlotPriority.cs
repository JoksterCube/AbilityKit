namespace JoksterCube.AbilityKit;

// Each ability slot's place in the row beside the Guardian Power (lower further left). One value per slot, so no two
// slots share a place: a new ability claims a free number here (and in the README) before using it. Leave gaps.
// The number is what goes into the slot's object name, so never change a value a released mod already uses.
public enum SlotPriority
{
    PingMarkPerception = 100,
    TempestRitePetPouch = 400,
    PingMarkFishsight = 800,
    TempestRiteDomainBorder = 850,
    TempestRiteRallyCall = 900,
    TempestRiteRallyRefuse = 901
}
