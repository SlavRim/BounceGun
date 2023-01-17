namespace BounceGun;

public static class RicochetSoundDefinitions
{
    public static string RicochetSoundPrefix = "VSWBG_Ricochet_";
    public static SoundDef GetRicochetSound(int id) => DefDatabase<SoundDef>.GetNamed(RicochetSoundPrefix + id);
    static int? cachedCount;
    public static int RicochetSoundsCount => cachedCount ??= DefDatabase<SoundDef>.AllDefs.Count(x => x.defName.StartsWith(RicochetSoundPrefix));
    public static SoundDef GetRandomRicochetSound() => GetRicochetSound(UnityEngine.Random.Range(1, RicochetSoundsCount));
    public static void PlayRandomFor(Bullet bullet)
    {
        try
        {
            var ricochetSound = GetRandomRicochetSound();
            LocalTargetInfo localTargetInfo = bullet.Position;
            var targetInfo = localTargetInfo.ToTargetInfo(bullet.Map);
            ricochetSound.PlayOneShot(targetInfo);
        }
        catch { }
    }
}