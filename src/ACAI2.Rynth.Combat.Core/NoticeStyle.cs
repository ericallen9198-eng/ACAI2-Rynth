namespace ACAI2.Rynth.Combat.Core;

public enum NoticeGroup { None,Buff,Debuff,Skill }
/// <summary>Notification appearance only. These groups never classify damage or alter statistics.</summary>
public static class NoticeStyle
{
    public static NoticeGroup Group(FloatEvent e)=>e.Group!=NoticeGroup.None?e.Group:e.Kind switch
    {
        "Imperil"=>NoticeGroup.Debuff,
        "KineticReady" or "KineticRelease"=>NoticeGroup.Skill,
        "Ward" or "Buff"=>NoticeGroup.Buff,
        _=>e.Kind.StartsWith("Vuln",StringComparison.Ordinal)?NoticeGroup.Debuff:NoticeGroup.None
    };
    public static float Scale(FloatEvent e,Appearance a)=>Group(e) switch
    {NoticeGroup.Buff=>a.BuffsScale,NoticeGroup.Debuff=>a.DebuffsScale,NoticeGroup.Skill=>a.SkillsScale,_=>1};
    public static float Focus(FloatEvent e,double age)=>e.Kind=="KineticReady"?
        age<.12?1.10f:age<.7?1:.85f:Group(e)==NoticeGroup.Debuff?age<.12?1.08f:age<.55?1:.9f:1;
    public static string Label(FloatEvent e,double age)=>e.Kind=="KineticReady"&&age>=.7?"READY"+(e.Count>1?" ×"+e.Count:""):FloatStream.Format(e);
}
