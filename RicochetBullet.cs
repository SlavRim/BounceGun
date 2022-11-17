namespace BounceGun;

public class RicochetBullet : Bullet
{
    public RicochetBullet() { }

    public virtual bool RicochetChance(float chance) => Rand.Chance(chance);

    protected readonly List<LocalTargetInfo> previousTargets = new();

    public RicochetBulletDef Def => def as RicochetBulletDef;
    public virtual SkillDef AccuracySkill => SkillDefOf.Shooting;
    public float Accuracy
    {
        get
        {
            try
            {
                return Shooter.Pawn.skills.GetSkill(AccuracySkill).Level / 20;
            }
            catch { }
            return 0f;
        }
    }

    public Verb_Shoot Verb { get; protected set; }
    public Pawn User => launcher as Pawn;
    public LocalTargetInfo Shooter { get; protected set; } = LocalTargetInfo.Invalid;
    public LocalTargetInfo InitialTarget { get; protected set; } = LocalTargetInfo.Invalid;
    public LocalTargetInfo CurrentTarget { get; protected set; } = LocalTargetInfo.Invalid;
    public override int DamageAmount => Mathf.RoundToInt(base.DamageAmount * weaponDamageMultiplier);

    protected bool ricochet;
    protected int ricochetCounter, maxRicochetHits = -1;
    protected override void Impact(Thing hitThing, bool blockedByShield = false)
    {
        previousTargets.Add(hitThing);
        // prepare
        if (!InitialTarget.IsValid)
            InitialTarget = CurrentTarget = intendedTarget;
        if (!Shooter.IsValid && User is { })
            previousTargets.Add(Shooter = User);

        if (blockedByShield) goto impact;

        if(maxRicochetHits < 0)
            maxRicochetHits = Random.Range(Mathf.Max(Def.MinRicochetHits, 0), Def.BaseMaxRicochetHits + Mathf.CeilToInt((Def.SkillMaxRicochetHits - Def.BaseMaxRicochetHits) * Accuracy));

        // check and prepare ricochet
        if (weaponDamageMultiplier <= 0) goto impact;
        ricochet &= RicochetChance(Def.RicochetChance + (Accuracy * Def.AccuracyFactor));
        ricochet &= ricochetCounter++ < maxRicochetHits;
        if (!ricochet) goto impact;

        weaponDamageMultiplier *= Def.RicochetDamageModifier;

        // search next target
        var pawns = Map.listerThings.ThingsInGroup(ThingRequestGroup.Pawn);

        LocalTargetInfo nextTarget = pawns
                .OrderBy(x => x.Position.DistanceTo(Position))
                .FirstOrDefault(x => !previousTargets.Contains(x) && CanHit(x, out _));

        if (!nextTarget.IsValid) goto impact;

        // goto next target
        CurrentTarget = nextTarget;

        GoNext();
        // 

        impact:
        base.Impact(hitThing, blockedByShield);
    }
    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        if (ricochet) return; // Don't destroy while ricochet
        landed = true;
        base.Destroy(mode);
    }

    public ThingWithComps Equipment { get; private set; }

    public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
    {
        Equipment = equipment as ThingWithComps;
        base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
    }

    public bool CanHit(LocalTargetInfo target, out ShootLine shootLine)
    {
        shootLine = default;
        Verb ??= Equipment.GetComp<CompEquippable>().AllVerbs.OfType<Verb_Shoot>().FirstOrDefault();//new() { caster = launcher, verbProps = new() { verbClass = typeof(Verb_LaunchProjectile) }, verbTracker = User?.verbTracker };
        return Verb?.TryFindShootLineFromTo(Position, target, out shootLine) ?? false;
    }

    public bool GoNext()
    {
        if (Verb is null) return false;
        if (!CanHit(CurrentTarget, out var shootLine)) return false;
        var pawn = Shooter.Pawn;
        var equipment = Verb.EquipmentSource;
        var projectileHitFlags = ProjectileHitFlags.NonTargetWorld;
        if (Verb.verbProps.ForcedMissRadius > 0.5f)
        {
            var num = Verb.verbProps.ForcedMissRadius;
            Pawn caster;
            if (pawn is not null && (caster = pawn) is not null)
            {
                num *= Verb.verbProps.GetForceMissFactorFor(equipment, caster);
            }
            float forcedMiss = VerbUtility.CalculateAdjustedForcedMiss(num, CurrentTarget.Cell - Position);
            if (forcedMiss > 0.5f)
            {
                var forcedMissTarget = GetForcedMissTarget(forcedMiss);
                if (forcedMissTarget != CurrentTarget.Cell)
                {
                    if (Random.Chance(0.5f))
                    {
                        projectileHitFlags = ProjectileHitFlags.All;
                    }
                    Launch(pawn, DrawPos, forcedMissTarget, CurrentTarget, projectileHitFlags, false, equipment, null);
                    return true;
                }
            }
        }
        var shotReport = ShotReport.HitReportFor(Verb.caster, Verb, CurrentTarget);
        var randomCoverToMissInto = shotReport.GetRandomCoverToMissInto();
        var targetCoverDef = (randomCoverToMissInto is not null) ? randomCoverToMissInto.def : null;
        if (CurrentTarget.Thing is not null)
        {
            Launch(pawn, DrawPos, CurrentTarget, CurrentTarget, ProjectileHitFlags.IntendedTarget, false, equipment, targetCoverDef);
        }
        else
        {
            Launch(pawn, DrawPos, shootLine.Dest, CurrentTarget, ProjectileHitFlags.All, false, equipment, targetCoverDef);
        }

        return true;
    }
    protected IntVec3 GetForcedMissTarget(float forcedMissRadius)
    {
        int max = GenRadial.NumCellsInRadius(forcedMissRadius);
        int num = Rand.Range(0, max);
        return CurrentTarget.Cell + GenRadial.RadialPattern[num];
    }
}