namespace BounceGun;

partial class Definitions
{
    public static RicochetBulletDef
        VSWBG_Bullet_RepulseRifle_ParticlePulsebeam,
        VSWBG_Bullet_RepulsePistol_ParticlePulsebeam,
        VSWBG_Bullet_RepulseSniper_ParticlePulsebeam,
        VSWBG_Bullet_RepulseRepeater_ParticlePulsebeam,
        VSWBG_Bullet_RefractorScattergun_ParticlePulsebeam,
        VSWBG_Bullet_RefractorCannon_ParticlePulsebeam;
}
public class RicochetBulletDef : ThingDef
{
    public float
        AccuracyFactor = 0.5f,
        RicochetChance = 0.5f,
        RicochetDamageModifier = 0.5f;
    public int
        MinRicochetHits = 0,
        BaseMaxRicochetHits = 1,
        SkillMaxRicochetHits = 2;
    public RicochetBulletDef()
    {
    }
}