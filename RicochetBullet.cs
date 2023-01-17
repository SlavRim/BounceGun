namespace BounceGun;

public class RicochetBullet : Bullet
{
    public RicochetBullet() { }

    public virtual bool RicochetChance(float chance) => Rand.Chance(chance);
    public virtual LocalTargetInfo FindNextTarget()
    {
        var pawns = Map.listerThings.ThingsInGroup(ThingRequestGroup.Pawn);

        LocalTargetInfo nextTarget = pawns
                .OrderBy(x => x.Position.DistanceTo(Position))
                .FirstOrDefault(x => !previousTargets.Contains(x) && CanHit(x, out _));
        return nextTarget;
    }

    protected readonly List<Thing> previousTargets = new();

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
    public virtual Pawn User => launcher as Pawn;
    public LocalTargetInfo Shooter { get; protected set; } = LocalTargetInfo.Invalid;
    public LocalTargetInfo InitialTarget { get; protected set; } = LocalTargetInfo.Invalid;
    public LocalTargetInfo PreviousTarget { get; protected set; } = LocalTargetInfo.Invalid;
    public LocalTargetInfo CurrentTarget { get; protected set; } = LocalTargetInfo.Invalid;
    public override int DamageAmount => Mathf.RoundToInt(base.DamageAmount * weaponDamageMultiplier);

    protected bool ricochet;
    protected int ricochetCounter, maxRicochetHits = -1;
    public override void Impact(Thing hitThing, bool blockedByShield = false)
    {
        previousTargets.Add(hitThing);
        // prepare
        if (!InitialTarget.IsValid)
            InitialTarget = CurrentTarget = intendedTarget;
        if (!Shooter.IsValid && User is { })
            previousTargets.Add((PreviousTarget = Shooter = User).Thing);

        if (blockedByShield) goto impact;

        if(maxRicochetHits < 0)
            maxRicochetHits = UnityEngine.Random.Range(Mathf.Max(Def.MinRicochetHits, 0), Def.BaseMaxRicochetHits + Mathf.CeilToInt((Def.SkillMaxRicochetHits - Def.BaseMaxRicochetHits) * Accuracy));

        // check and prepare ricochet
        if (weaponDamageMultiplier <= 0) goto impact;
        ricochet = RicochetChance(Def.RicochetChance + (Accuracy * Def.AccuracyFactor));
        ricochet &= ricochetCounter++ < maxRicochetHits;
        if (!ricochet) goto impact;

        weaponDamageMultiplier *= Def.RicochetDamageModifier;

        // search next target

        PreviousTarget = CurrentTarget;

        CurrentTarget = FindNextTarget();

        // goto next target

        ricochet = GoNext();
        //

        impact:
        if (ricochet)
            RicochetSoundDefinitions.PlayRandomFor(this);
        weaponDamageMultiplier = Mathf.Max(weaponDamageMultiplier, 0);
        base.Impact(hitThing, blockedByShield);
    }
    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        if (ricochet) return; // Don't destroy while ricochet
        landed = true;
        base.Destroy(mode);
    }

    public ThingWithComps Equipment { get; protected set; }

    public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
    {
        Equipment = equipment as ThingWithComps;
        base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
    }

    public virtual bool CanHit(LocalTargetInfo target, out ShootLine shootLine)
    {
        shootLine = default;
        Verb ??= Equipment.GetComp<CompEquippable>().AllVerbs.OfType<Verb_Shoot>().FirstOrDefault();
        return Verb?.TryFindShootLineFromTo(Position, target, out shootLine) ?? false;
    }

    public virtual bool GoNext(bool preventFriendlyDamage = false) => GoTo(CurrentTarget, preventFriendlyDamage);
    
    /// <summary>
    /// Copy-paste from Verb_LaunchProjectile
    /// </summary>
    public virtual bool GoTo(LocalTargetInfo target, bool preventFriendlyDamage = false)
    {
        if (Verb is not { verbProps: { }, EquipmentSource: { } }) return false;
        if (!target.IsValid || !CanHit(target, out var shootLine)) return false;
        var pawn = Shooter.Pawn;
        var equipment = Verb.EquipmentSource;
        var projectileHitFlags = ProjectileHitFlags.NonTargetWorld;
        if (Verb.verbProps.ForcedMissRadius > 0.5f)
        {
            var num = Verb.verbProps.ForcedMissRadius;
            Pawn caster;
            if (pawn is not null && (caster = pawn) is not null)
                num *= Verb.verbProps.GetForceMissFactorFor(equipment, caster);
            var forcedMiss = VerbUtility.CalculateAdjustedForcedMiss(num, target.Cell - Position);
            if (forcedMiss > 0.5f)
            {
                var forcedMissTarget = GetForcedMissTarget(forcedMiss);
                if (forcedMissTarget != target.Cell)
                {
                    if (Rand.Chance(0.5f))
                        projectileHitFlags = ProjectileHitFlags.All;
                    Launch(pawn, DrawPos, forcedMissTarget, target, projectileHitFlags, preventFriendlyDamage, equipment, null);
                    return true;
                }
            }
        }
        var shotReport = ShotReport.HitReportFor(User, Verb, target);
        shotReport.covers = CoverUtility.CalculateCoverGiverSet(target, Position, Map);
        shotReport.coversOverallBlockChance = CoverUtility.CalculateOverallBlockChance(target, Position, Map);
        var randomCoverToMissInto = shotReport.GetRandomCoverToMissInto();
        var targetCoverDef = randomCoverToMissInto?.def;

        if (Verb.verbProps.canGoWild && !Rand.Chance(shotReport.AimOnTargetChance_IgnoringPosture))
        {
            shootLine.ChangeDestToMissWild(shotReport.AimOnTargetChance_StandardTarget);
            Launch(pawn, DrawPos, shootLine.Dest, target, ProjectileHitFlags.NonTargetWorld, preventFriendlyDamage, equipment, targetCoverDef);
            return true;
        }
        if (target is { Thing.def.CanBenefitFromCover: true } && !Rand.Chance(shotReport.PassCoverChance))
        {
            Launch(pawn, DrawPos, randomCoverToMissInto, target, ProjectileHitFlags.NonTargetWorld, preventFriendlyDamage, equipment, targetCoverDef);
            return true;
        }
        projectileHitFlags = ProjectileHitFlags.IntendedTarget;

        if (target.Thing is null or { def.Fillage: FillCategory.Full })
            projectileHitFlags |= ProjectileHitFlags.NonTargetWorld;

        Launch(pawn, DrawPos, PreviousTarget, target, projectileHitFlags, preventFriendlyDamage, equipment, targetCoverDef);

        return true;
    }
    protected virtual IntVec3 GetForcedMissTarget(float forcedMissRadius)
    {
        int max = GenRadial.NumCellsInRadius(forcedMissRadius);
        int num = Rand.Range(0, max);
        return CurrentTarget.Cell + GenRadial.RadialPattern[num];
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref ricochet, nameof(ricochet));
        Scribe_Values.Look(ref ricochetCounter, nameof(ricochetCounter));
        Scribe_Values.Look(ref maxRicochetHits, nameof(maxRicochetHits));
    }
}