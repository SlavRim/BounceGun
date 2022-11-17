global using Mod = BounceGun.Mod;

namespace BounceGun;

public class Mod : Verse.Mod
{
    public static Mod Instance;
    public Mod(ModContentPack content) : base(content)
    {
    }
}