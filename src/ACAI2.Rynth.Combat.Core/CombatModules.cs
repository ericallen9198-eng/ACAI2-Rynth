// Minimal portable helper retained from ACAI2 fa363107; no legacy module settings.
namespace ACAI2.Rynth.Combat.Core;

public static class CombatComponents
{
    public static bool IsDirect(CombatEventRecord record) =>
        string.IsNullOrEmpty(record.Component) || record.Component == "Direct";
}
