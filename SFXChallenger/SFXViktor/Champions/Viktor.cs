#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 Viktor.cs is part of SFXViktor.

 SFXViktor is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXViktor is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXViktor. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

#region

using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SFXViktor.Abstracts;
using SFXViktor.Args;
using SFXViktor.Enumerations;
using SFXViktor.Helpers;
using SFXViktor.Library;
using SFXViktor.Library.Extensions.NET;
using SFXViktor.Library.Logger;
using SFXViktor.Managers;
using SharpDX;
using DamageType = SFXViktor.Enumerations.DamageType;
using MinionManager = SFXViktor.Library.MinionManager;
using MinionOrderTypes = SFXViktor.Library.MinionOrderTypes;
using MinionTeam = SFXViktor.Library.MinionTeam;
using MinionTypes = SFXViktor.Library.MinionTypes;
using Orbwalking = SFXViktor.Wrappers.Orbwalking;
using Spell = SFXViktor.Wrappers.Spell;
using TargetSelector = SFXViktor.SFXTargetSelector.TargetSelector;
using Utils = SFXViktor.Helpers.Utils;

#endregion

namespace SFXViktor.Champions
{
    internal class Viktor : Champion
    {
        private const float ELength = 700f;
        private const float RMoveInterval = 500f;
        private Obj_AI_Base _lastAfterFarmTarget;
        private float _lastAutoAttack;
        private Obj_AI_Base _lastBeforeFarmTarget;
        private Obj_AI_Base _lastQKillableTarget;
        private float _lastRMoveCommand = Environment.TickCount;
        private GameObject _rObject;
        private UltimateManager _ultimate;

        protected override ItemFlags ItemFlags
        {
            get { return ItemFlags.Offensive | ItemFlags.Defensive | ItemFlags.Flee; }
        }

        protected override ItemUsageType ItemUsage
        {
            get { return ItemUsageType.Custom; }
        }

        protected override void OnLoad()
        {
            Orbwalking.BeforeAttack += OnOrbwalkingBeforeAttack;
            Orbwalking.AfterAttack += OnOrbwalkingAfterAttack;
            GapcloserManager.OnGapcloser += OnEnemyGapcloser;
            Interrupter2.OnInterruptableTarget += OnInterruptableTarget;
            GameObject.OnCreate += OnGameObjectCreate;
        }

        protected override void SetupSpells()
        {
            Q = new Spell(SpellSlot.Q, Player.BoundingRadius + 600f, DamageType.Magical);
            Q.Range += GameObjects.EnemyHeroes.Select(e => e.BoundingRadius).DefaultIfEmpty(25).Min();
            Q.SetTargetted(0.15f, 2050f);

            W = new Spell(SpellSlot.W, 700f, DamageType.Magical);
            W.SetSkillshot(1f, 300f, float.MaxValue, false, SkillshotType.SkillshotCircle);

            E = new Spell(SpellSlot.E, 525f, DamageType.Magical);
            E.SetSkillshot(0f, 90f, 800f, false, SkillshotType.SkillshotLine);

            R = new Spell(SpellSlot.R, 700f, DamageType.Magical);
            R.SetSkillshot(0.2f, 300f, float.MaxValue, false, SkillshotType.SkillshotCircle);

            _ultimate = new UltimateManager
            {
                Combo = true,
                Assisted = true,
                Auto = true,
                Flash = false,
                Required = true,
                Force = true,
                Gapcloser = false,
                GapcloserDelay = false,
                Interrupt = true,
                InterruptDelay = true,
                DamageCalculation =
                    (hero, resMulti, rangeCheck) =>
                        CalcComboDamage(
                            hero, resMulti, rangeCheck, Menu.Item(Menu.Name + ".combo.q").GetValue<bool>(),
                            Menu.Item(Menu.Name + ".combo.e").GetValue<bool>(), true)
            };
        }

        protected override void AddToMenu()
        {
            DrawingManager.Add("E Max", E.Range + ELength);

            var ultimateMenu = _ultimate.AddToMenu(Menu);
            ultimateMenu.AddItem(new MenuItem(ultimateMenu.Name + ".follow", "Auto Follow").SetValue(true));

            var comboMenu = Menu.AddSubMenu(new Menu("Combo", Menu.Name + ".combo"));
            HitchanceManager.AddToMenu(
                comboMenu.AddSubMenu(new Menu("Hitchance", comboMenu.Name + ".hitchance")), "combo",
                new Dictionary<string, HitChance> { { "W", HitChance.VeryHigh }, { "E", HitChance.VeryHigh } });
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".q", "Use Q").SetValue(true));
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".w", "Use W").SetValue(false));
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".e", "Use E").SetValue(true));

            var harassMenu = Menu.AddSubMenu(new Menu("Harass", Menu.Name + ".harass"));
            HitchanceManager.AddToMenu(
                harassMenu.AddSubMenu(new Menu("Hitchance", harassMenu.Name + ".hitchance")), "harass",
                new Dictionary<string, HitChance> { { "E", HitChance.High } });
            ResourceManager.AddToMenu(
                harassMenu,
                new ResourceManagerArgs(
                    "harass", ResourceType.Mana, ResourceValueType.Percent, ResourceCheckType.Minimum)
                {
                    DefaultValue = 20
                });
            harassMenu.AddItem(new MenuItem(harassMenu.Name + ".q", "Use Q").SetValue(false));
            harassMenu.AddItem(new MenuItem(harassMenu.Name + ".e", "Use E").SetValue(true));

            var laneclearMenu = Menu.AddSubMenu(new Menu("Lane Clear", Menu.Name + ".lane-clear"));
            ResourceManager.AddToMenu(
                laneclearMenu,
                new ResourceManagerArgs(
                    "lane-clear-q", ResourceType.Mana, ResourceValueType.Percent, ResourceCheckType.Minimum)
                {
                    Prefix = "Q",
                    Advanced = true,
                    MaxValue = 101,
                    LevelRanges = new SortedList<int, int> { { 1, 6 }, { 6, 12 }, { 12, 18 } },
                    DefaultValues = new List<int> { 80, 50, 50 },
                    IgnoreJungleOption = true
                });
            ResourceManager.AddToMenu(
                laneclearMenu,
                new ResourceManagerArgs(
                    "lane-clear-e", ResourceType.Mana, ResourceValueType.Percent, ResourceCheckType.Minimum)
                {
                    Prefix = "E",
                    Advanced = true,
                    MaxValue = 101,
                    LevelRanges = new SortedList<int, int> { { 1, 6 }, { 6, 12 }, { 12, 18 } },
                    DefaultValues = new List<int> { 40, 20, 20 },
                    IgnoreJungleOption = true
                });
            laneclearMenu.AddItem(new MenuItem(laneclearMenu.Name + ".q", "Use Q").SetValue(false));
            laneclearMenu.AddItem(new MenuItem(laneclearMenu.Name + ".e", "Use E").SetValue(true));
            laneclearMenu.AddItem(new MenuItem(laneclearMenu.Name + ".e-min", "E Min.").SetValue(new Slider(2, 1, 5)));

            var lasthitMenu = Menu.AddSubMenu(new Menu("Last Hit", Menu.Name + ".lasthit"));
            ResourceManager.AddToMenu(
                lasthitMenu,
                new ResourceManagerArgs(
                    "lasthit", ResourceType.Mana, ResourceValueType.Percent, ResourceCheckType.Minimum)
                {
                    Advanced = true,
                    MaxValue = 101,
                    LevelRanges = new SortedList<int, int> { { 1, 6 }, { 6, 12 }, { 12, 18 } },
                    DefaultValues = new List<int> { 50, 30, 30 }
                });
            lasthitMenu.AddItem(new MenuItem(lasthitMenu.Name + ".q-unkillable", "Q Unkillable").SetValue(true));

            var fleeMenu = Menu.AddSubMenu(new Menu("Flee", Menu.Name + ".flee"));
            fleeMenu.AddItem(new MenuItem(fleeMenu.Name + ".w", "Use W").SetValue(false));
            fleeMenu.AddItem(new MenuItem(fleeMenu.Name + ".q-upgraded", "Use Q Upgraded").SetValue(true));

            var killstealMenu = Menu.AddSubMenu(new Menu("Killsteal", Menu.Name + ".killsteal"));
            killstealMenu.AddItem(new MenuItem(killstealMenu.Name + ".e", "Use E").SetValue(false));
            killstealMenu.AddItem(new MenuItem(killstealMenu.Name + ".q-aa", "Use Q AA").SetValue(false));

            var miscMenu = Menu.AddSubMenu(new Menu("Misc", Menu.Name + ".miscellaneous"));

            var wImmobileMenu = miscMenu.AddSubMenu(new Menu("W Immobile", miscMenu.Name + "w-immobile"));
            HeroListManager.AddToMenu(
                wImmobileMenu,
                new HeroListManagerArgs("w-immobile")
                {
                    IsWhitelist = false,
                    Allies = false,
                    Enemies = true,
                    DefaultValue = false
                });
            BestTargetOnlyManager.AddToMenu(wImmobileMenu, "w-immobile");

            var wSlowedMenu = miscMenu.AddSubMenu(new Menu("W Slowed", miscMenu.Name + "w-slowed"));
            HeroListManager.AddToMenu(
                wSlowedMenu,
                new HeroListManagerArgs("w-slowed")
                {
                    IsWhitelist = false,
                    Allies = false,
                    Enemies = true,
                    DefaultValue = false,
                    Enabled = false
                });
            BestTargetOnlyManager.AddToMenu(wSlowedMenu, "w-slowed", true);

            var wGapcloserMenu = miscMenu.AddSubMenu(new Menu("W Gapcloser", miscMenu.Name + "w-gapcloser"));
            GapcloserManager.AddToMenu(
                wGapcloserMenu,
                new HeroListManagerArgs("w-gapcloser")
                {
                    IsWhitelist = false,
                    Allies = false,
                    Enemies = true,
                    DefaultValue = false,
                    Enabled = false
                }, true);
            BestTargetOnlyManager.AddToMenu(wGapcloserMenu, "w-gapcloser");

            IndicatorManager.AddToMenu(DrawingManager.Menu, true);
            IndicatorManager.Add(
                "Q", delegate(Obj_AI_Hero hero)
                {
                    var damage = 0f;
                    if (Q.IsReady())
                    {
                        damage += Q.GetDamage(hero);
                        damage += CalcPassiveDamage(hero);
                    }
                    else
                    {
                        var qInstance = Q.Instance;
                        if (HasQBuff() ||
                            qInstance.Level > 0 && qInstance.CooldownExpires - Game.Time > qInstance.Cooldown - 1f)
                        {
                            damage += CalcPassiveDamage(hero);
                        }
                    }
                    return damage;
                });
            IndicatorManager.Add(E);
            IndicatorManager.Add(
                "R Burst", delegate(Obj_AI_Hero hero)
                {
                    if (R.IsReady() && (_rObject == null || !_rObject.IsValid))
                    {
                        return R.GetDamage(hero) + R.GetDamage(hero, 1);
                    }
                    return 0f;
                });
            IndicatorManager.Finale();
        }

        protected override void OnPreUpdate() {}

        protected override void OnPostUpdate()
        {
            if (Menu.Item(Menu.Name + ".ultimate.follow").GetValue<bool>())
            {
                RFollowLogic();
            }
            if (_ultimate.IsActive(UltimateModeType.Assisted) && R.IsReady())
            {
                if (_ultimate.ShouldMove(UltimateModeType.Assisted))
                {
                    Orbwalking.MoveTo(Game.CursorPos, Orbwalker.HoldAreaRadius);
                }
                var target = TargetSelector.GetTarget(R);
                if (target != null && !RLogic(UltimateModeType.Assisted, target))
                {
                    RLogicSingle(UltimateModeType.Assisted);
                }
            }

            if (_ultimate.IsActive(UltimateModeType.Auto) && R.IsReady())
            {
                var target = TargetSelector.GetTarget(R);
                if (target != null && !RLogic(UltimateModeType.Auto, target))
                {
                    RLogicSingle(UltimateModeType.Auto);
                }
            }

            if (HeroListManager.Enabled("w-immobile") && W.IsReady())
            {
                var target =
                    GameObjects.EnemyHeroes.FirstOrDefault(
                        t =>
                            t.IsValidTarget(W.Range) && HeroListManager.Check("w-immobile", t) &&
                            BestTargetOnlyManager.Check("w-immobile", W, t) && Utils.IsImmobile(t));
                if (target != null)
                {
                    Casting.SkillShot(target, W, HitChance.High);
                }
            }

            if (HeroListManager.Enabled("w-slowed") && W.IsReady())
            {
                var target =
                    GameObjects.EnemyHeroes.FirstOrDefault(
                        t =>
                            t.IsValidTarget(W.Range) && HeroListManager.Check("w-slowed", t) &&
                            BestTargetOnlyManager.Check("w-slowed", W, t) &&
                            t.Buffs.Any(b => b.Type == BuffType.Slow && b.EndTime - Game.Time > 0.5f));
                if (target != null)
                {
                    Casting.SkillShot(target, W, HitChance.High);
                }
            }

            if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LastHit ||
                Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear)
            {
                if (Menu.Item(Menu.Name + ".lasthit.q-unkillable").GetValue<bool>() && Q.IsReady() &&
                    ResourceManager.Check("lasthit"))
                {
                    var canAttack = Game.Time >= _lastAutoAttack + Player.AttackDelay;
                    var minions =
                        MinionManager.GetMinions(Q.Range)
                            .Where(
                                m =>
                                    (!canAttack || !Orbwalking.InAutoAttackRange(m)) && m.HealthPercent <= 50 &&
                                    (_lastAfterFarmTarget == null || _lastAfterFarmTarget.NetworkId != m.NetworkId) &&
                                    (_lastBeforeFarmTarget == null || _lastBeforeFarmTarget.NetworkId != m.NetworkId))
                            .ToList();
                    if (minions.Any())
                    {
                        foreach (var minion in minions)
                        {
                            var health = HealthPrediction.GetHealthPrediction(
                                minion, (int) (Q.ArrivalTime(minion) * 1000));
                            if (health > 0 && Math.Abs(health - minion.Health) > 10 &&
                                Q.GetDamage(minion) * 0.85f > health)
                            {
                                if (Q.CastOnUnit(minion))
                                {
                                    _lastQKillableTarget = minion;
                                }
                                break;
                            }
                        }
                    }
                }
            }
        }

        private bool IsSpellUpgraded(Spell spell)
        {
            try
            {
                return
                    Player.Buffs.Select(b => b.Name.ToLower())
                        .Where(b => b.StartsWith("viktor") && b.EndsWith("aug"))
                        .Any(
                            b =>
                                spell.Slot == SpellSlot.R
                                    ? b.Contains("qwe")
                                    : b.Contains(spell.Slot.ToString().ToLower()));
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return false;
        }

        private void OnGameObjectCreate(GameObject sender, EventArgs args)
        {
            try
            {
                if (sender.Type == GameObjectType.obj_GeneralParticleEmitter &&
                    sender.Name.Equals("Viktor_ChaosStorm_green.troy", StringComparison.OrdinalIgnoreCase))
                {
                    _rObject = sender;
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnOrbwalkingBeforeAttack(Orbwalking.BeforeAttackEventArgs args)
        {
            try
            {
                var forced = Orbwalker.ForcedTarget();
                if (HasQBuff() && (forced == null || !forced.IsValidTarget()))
                {
                    if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo)
                    {
                        if ((_rObject == null || !_rObject.IsValid) && R.IsReady() &&
                            _ultimate.IsActive(UltimateModeType.Combo) &&
                            R.Instance.Name.Equals("ViktorChaosStorm", StringComparison.OrdinalIgnoreCase) &&
                            GameObjects.EnemyHeroes.Any(Orbwalking.InAutoAttackRange) &&
                            (RLogicSingle(UltimateModeType.Combo, true) ||
                             GameObjects.EnemyHeroes.Where(e => e.IsValidTarget(R.Range + R.Width))
                                 .Any(e => RLogic(UltimateModeType.Combo, e, true))))
                        {
                            args.Process = false;
                            return;
                        }
                    }
                    if (!(args.Target is Obj_AI_Hero))
                    {
                        var targets =
                            TargetSelector.GetTargets(Player.AttackRange + Player.BoundingRadius * 3f).ToList();
                        if (targets.Any())
                        {
                            var hero = targets.FirstOrDefault(Orbwalking.InAutoAttackRange);
                            if (hero != null)
                            {
                                Orbwalker.ForceTarget(hero);
                                args.Process = false;
                            }
                            else if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Mixed)
                            {
                                if (
                                    targets.Any(
                                        t =>
                                            t.Distance(Player) <
                                            (Player.BoundingRadius + t.BoundingRadius + Player.AttackRange) *
                                            (IsSpellUpgraded(Q) ? 1.4f : 1.2f)))
                                {
                                    args.Process = false;
                                }
                            }
                        }
                    }
                }
                else
                {
                    if ((args.Target is Obj_AI_Hero) &&
                        (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo ||
                         Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Mixed) &&
                        (Q.IsReady() && Player.Mana >= Q.Instance.ManaCost ||
                         E.IsReady() && Player.Mana >= E.Instance.ManaCost))
                    {
                        args.Process = false;
                    }
                }
                if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LastHit ||
                    Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear)
                {
                    var minion = args.Target as Obj_AI_Minion;
                    if (minion != null &&
                        HealthPrediction.LaneClearHealthPrediction(
                            minion, (int) (Player.AttackDelay * 1000), Game.Ping / 2) <
                        Player.GetAutoAttackDamage(minion))
                    {
                        _lastBeforeFarmTarget = minion;
                    }
                    if (_lastQKillableTarget != null && _lastQKillableTarget.NetworkId == args.Target.NetworkId)
                    {
                        args.Process = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnOrbwalkingAfterAttack(AttackableUnit unit, AttackableUnit target)
        {
            try
            {
                if (unit.IsMe)
                {
                    _lastAutoAttack = Game.Time;
                    if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LastHit ||
                        Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LaneClear)
                    {
                        var bTarget = unit as Obj_AI_Base;
                        if (bTarget != null)
                        {
                            _lastAfterFarmTarget = bTarget;
                        }
                    }
                    Orbwalker.ForceTarget(null);
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnEnemyGapcloser(object sender, GapcloserManagerArgs args)
        {
            try
            {
                if (args.UniqueId == "w-gapcloser" && W.IsReady() &&
                    BestTargetOnlyManager.Check("w-gapcloser", W, args.Hero))
                {
                    if (args.End.Distance(Player.Position) <= W.Range)
                    {
                        W.Cast(args.End);
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnInterruptableTarget(Obj_AI_Hero sender, Interrupter2.InterruptableTargetEventArgs args)
        {
            try
            {
                if (sender.IsEnemy && args.DangerLevel == Interrupter2.DangerLevel.High &&
                    _ultimate.IsActive(UltimateModeType.Interrupt, sender))
                {
                    Utility.DelayAction.Add(
                        DelayManager.Get("ultimate-interrupt-delay"), delegate
                        {
                            if (R.IsReady())
                            {
                                var pred = CPrediction.Circle(R, sender, HitChance.High, false);
                                if (pred.TotalHits > 0)
                                {
                                    R.Cast(pred.CastPosition);
                                }
                            }
                        });
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private bool HasQBuff()
        {
            return Player.HasBuff("viktorpowertransferreturn");
        }

        protected override void Combo()
        {
            var single = false;
            var q = Menu.Item(Menu.Name + ".combo.q").GetValue<bool>() && Q.IsReady();
            var w = Menu.Item(Menu.Name + ".combo.w").GetValue<bool>() && W.IsReady();
            var e = Menu.Item(Menu.Name + ".combo.e").GetValue<bool>() && E.IsReady();
            var r = _ultimate.IsActive(UltimateModeType.Combo) && R.IsReady();

            var qCasted = false;
            if (e)
            {
                var target = TargetSelector.GetTarget((E.Range + ELength + E.Width) * 1.1f, E.DamageType);
                if (target != null)
                {
                    ELogic(target, GameObjects.EnemyHeroes.ToList(), E.GetHitChance("combo"));
                }
            }
            if (q)
            {
                var target = TargetSelector.GetTarget(Q.Range, Q.DamageType);
                if (target != null)
                {
                    qCasted = Q.CastOnUnit(target);
                }
            }
            if (w)
            {
                var target = TargetSelector.GetTarget(W);
                if (target != null)
                {
                    WLogic(target, W.GetHitChance("combo"));
                }
            }
            if (r)
            {
                var target = TargetSelector.GetTarget(R);
                if (target != null && (HasQBuff() || (qCasted || !q || !Q.IsReady()) || R.IsKillable(target)) &&
                    !RLogic(UltimateModeType.Combo, target))
                {
                    RLogicSingle(UltimateModeType.Combo);
                    single = true;
                }
            }
            var rTarget = TargetSelector.GetTarget(R);
            if (rTarget != null && _ultimate.GetDamage(rTarget, UltimateModeType.Combo, single ? 1 : 5) > rTarget.Health)
            {
                ItemManager.UseComboItems(rTarget);
                SummonerManager.UseComboSummoners(rTarget);
            }
        }

        protected override void Harass()
        {
            if (!ResourceManager.Check("harass"))
            {
                return;
            }

            if (Menu.Item(Menu.Name + ".harass.e").GetValue<bool>())
            {
                var target = TargetSelector.GetTarget((E.Range + ELength + E.Width) * 1.1f, E.DamageType);
                if (target != null)
                {
                    ELogic(target, GameObjects.EnemyHeroes.ToList(), E.GetHitChance("harass"));
                }
            }
            if (Menu.Item(Menu.Name + ".harass.q").GetValue<bool>())
            {
                var target = TargetSelector.GetTarget(Q.Range, Q.DamageType);
                if (target != null)
                {
                    Q.CastOnUnit(target);
                }
            }
        }

        private float CalcComboDamage(Obj_AI_Hero target, float resMulti, bool rangeCheck, bool q, bool e, bool r)
        {
            try
            {
                if (target == null)
                {
                    return 0;
                }

                var damage = 0f;
                var totalMana = 0f;

                if (r && R.IsReady() && (!rangeCheck || R.IsInRange(target, R.Range + R.Width)))
                {
                    var rMana = R.ManaCost * resMulti;
                    if (totalMana + rMana <= Player.Mana)
                    {
                        totalMana += rMana;
                        damage += R.GetDamage(target);
                        int stacks;
                        if (!IsSpellUpgraded(R))
                        {
                            stacks = target.IsNearTurret(500f) ? 3 : 10;
                            var endTimes =
                                target.Buffs.Where(
                                    t =>
                                        t.Type == BuffType.Charm || t.Type == BuffType.Snare ||
                                        t.Type == BuffType.Knockup || t.Type == BuffType.Polymorph ||
                                        t.Type == BuffType.Fear || t.Type == BuffType.Taunt || t.Type == BuffType.Stun)
                                    .Select(t => t.EndTime)
                                    .ToList();
                            if (endTimes.Any())
                            {
                                var max = endTimes.Max();
                                if (max - Game.Time > 0.5f)
                                {
                                    stacks = 14;
                                }
                            }
                        }
                        else
                        {
                            stacks = 13;
                        }

                        damage += (R.GetDamage(target, 1) * stacks);
                    }
                }
                if (e && E.IsReady() && (!rangeCheck || E.IsInRange(target, E.Range + ELength)))
                {
                    var eMana = E.ManaCost * resMulti;
                    if (totalMana + eMana <= Player.Mana)
                    {
                        totalMana += eMana;
                        damage += E.GetDamage(target);
                    }
                }
                if (q && Q.IsReady() && (!rangeCheck || Q.IsInRange(target)))
                {
                    var qMana = Q.ManaCost * resMulti;
                    if (totalMana + qMana <= Player.Mana)
                    {
                        damage += Q.GetDamage(target);
                        if (Orbwalking.InAutoAttackRange(target))
                        {
                            damage += CalcPassiveDamage(target);
                        }
                    }
                }
                if (HasQBuff() && (!rangeCheck || Orbwalking.InAutoAttackRange(target)))
                {
                    damage += CalcPassiveDamage(target);
                }

                damage *= 1.1f;
                damage += ItemManager.CalculateComboDamage(target, rangeCheck);
                damage += SummonerManager.CalculateComboDamage(target, rangeCheck);
                return damage;
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return 0;
        }

        private float CalcPassiveDamage(Obj_AI_Base target)
        {
            try
            {
                return
                    (float)
                        Player.CalcDamage(
                            target, Damage.DamageType.Magical,
                            (new[] { 20, 25, 30, 35, 40, 45, 50, 55, 60, 70, 80, 90, 110, 130, 150, 170, 190, 210 }[
                                Player.Level - 1] + Player.TotalMagicalDamage * 0.5f + Player.TotalAttackDamage)) *
                    0.98f;
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return 0;
        }

        private void RFollowLogic()
        {
            try
            {
                if (_lastRMoveCommand + RMoveInterval > Environment.TickCount)
                {
                    return;
                }

                _lastRMoveCommand = Environment.TickCount;

                if (!R.Instance.Name.Equals("ViktorChaosStorm", StringComparison.OrdinalIgnoreCase) && _rObject != null &&
                    _rObject.IsValid)
                {
                    var pos = BestRFollowLocation(_rObject.Position);
                    if (!pos.Equals(Vector3.Zero))
                    {
                        Player.Spellbook.CastSpell(SpellSlot.R, pos);
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private bool RLogicSingle(UltimateModeType mode, bool simulated = false)
        {
            try
            {
                if (!R.Instance.Name.Equals("ViktorChaosStorm", StringComparison.OrdinalIgnoreCase) ||
                    !_ultimate.ShouldSingle(mode))
                {
                    return false;
                }

                foreach (var target in GameObjects.EnemyHeroes.Where(t => _ultimate.CheckSingle(mode, t)))
                {
                    var pred = CPrediction.Circle(R, target, HitChance.High, false);
                    if (pred.TotalHits > 0)
                    {
                        if (!simulated)
                        {
                            R.Cast(pred.CastPosition);
                            var aaTarget =
                                TargetSelector.GetTargets(Player.AttackRange + Player.BoundingRadius * 3f)
                                    .FirstOrDefault(Orbwalking.InAutoAttackRange);
                            if (aaTarget != null)
                            {
                                Orbwalker.ForceTarget(aaTarget);
                            }
                        }
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return false;
        }

        private bool RLogic(UltimateModeType mode, Obj_AI_Hero target, bool simulated = false)
        {
            try
            {
                if (!R.Instance.Name.Equals("ViktorChaosStorm", StringComparison.OrdinalIgnoreCase) ||
                    !_ultimate.IsActive(mode))
                {
                    return false;
                }
                var pred = CPrediction.Circle(R, target, HitChance.High, false);
                if (pred.TotalHits > 0 && _ultimate.Check(mode, pred.Hits))
                {
                    if (!simulated)
                    {
                        R.Cast(pred.CastPosition);
                        var aaTarget =
                            TargetSelector.GetTargets(Player.AttackRange + Player.BoundingRadius * 3f)
                                .FirstOrDefault(Orbwalking.InAutoAttackRange);
                        if (aaTarget != null)
                        {
                            Orbwalker.ForceTarget(aaTarget);
                        }
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return false;
        }

        private void WLogic(Obj_AI_Hero target, HitChance hitChance)
        {
            try
            {
                if (GameObjects.EnemyHeroes.Any(e => e.IsValidTarget() && e.Distance(Player) < W.Width * 1.1f))
                {
                    W.Cast(Player.ServerPosition);
                }
                var pred = W.GetPrediction(target, true);
                if (pred.Hitchance >= hitChance)
                {
                    W.Cast(pred.CastPosition);
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private bool ELogic(Obj_AI_Hero target, List<Obj_AI_Hero> targets, HitChance hitChance, int minHits = 1)
        {
            return ELogic(
                target, targets.Select(t => t as Obj_AI_Base).Where(t => t != null).ToList(), hitChance, minHits);
        }

        private bool ELogic(Obj_AI_Hero mainTarget, List<Obj_AI_Base> targets, HitChance hitChance, int minHits)
        {
            try
            {
                var input = new PredictionInput
                {
                    Range = ELength,
                    Delay = E.Delay,
                    Radius = E.Width,
                    Speed = E.Speed,
                    Type = E.Type
                };
                var input2 = new PredictionInput
                {
                    Range = E.Range + ELength,
                    Delay = E.Delay,
                    Radius = E.Width,
                    Speed = E.Speed,
                    Type = E.Type
                };
                var startPos = Vector3.Zero;
                var endPos = Vector3.Zero;
                var hits = 0;
                targets = targets.Where(t => t.IsValidTarget(E.Range + ELength + E.Width * 1.1f)).ToList();
                var targetCount = targets.Count;

                foreach (var target in targets)
                {
                    bool containsTarget;
                    var lTarget = target;
                    if (target.Distance(Player.Position) <= E.Range)
                    {
                        containsTarget = mainTarget == null || lTarget.NetworkId == mainTarget.NetworkId;
                        var cCastPos = target.Position;
                        foreach (var t in targets.Where(t => t.NetworkId != lTarget.NetworkId))
                        {
                            var count = 1;
                            var cTarget = t;
                            input.Unit = t;
                            input.From = cCastPos;
                            input.RangeCheckFrom = cCastPos;
                            var pred = Prediction.GetPrediction(input);
                            if (pred.Hitchance >= (hitChance - 1))
                            {
                                count++;
                                if (!containsTarget)
                                {
                                    containsTarget = t.NetworkId == mainTarget.NetworkId;
                                }
                                var rect = new Geometry.Polygon.Rectangle(
                                    cCastPos.To2D(), cCastPos.Extend(pred.CastPosition, ELength).To2D(), E.Width);
                                foreach (var c in
                                    targets.Where(
                                        c => c.NetworkId != cTarget.NetworkId && c.NetworkId != lTarget.NetworkId))
                                {
                                    input.Unit = c;
                                    var cPredPos = c.Type == GameObjectType.obj_AI_Minion
                                        ? c.Position
                                        : Prediction.GetPrediction(input).UnitPosition;
                                    if (
                                        new Geometry.Polygon.Circle(
                                            cPredPos,
                                            (c.Type == GameObjectType.obj_AI_Minion && c.IsMoving
                                                ? (c.BoundingRadius / 2f)
                                                : (c.BoundingRadius) * 0.9f)).Points.Any(p => rect.IsInside(p)))
                                    {
                                        count++;
                                        if (!containsTarget && c.NetworkId == mainTarget.NetworkId)
                                        {
                                            containsTarget = true;
                                        }
                                    }
                                }
                                if (count > hits && containsTarget)
                                {
                                    hits = count;
                                    startPos = cCastPos;
                                    endPos = cCastPos.Extend(pred.CastPosition, ELength);
                                    if (hits == targetCount)
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                        if (endPos.Equals(Vector3.Zero) && containsTarget)
                        {
                            startPos = target.IsFacing(Player) && IsSpellUpgraded(E)
                                ? Player.Position.Extend(cCastPos, Player.Distance(cCastPos) - (ELength / 10f))
                                : cCastPos;
                            endPos = Player.Position.Extend(cCastPos, ELength);
                            hits = 1;
                        }
                    }
                    else
                    {
                        input2.Unit = lTarget;
                        var castPos = Prediction.GetPrediction(input2).CastPosition;
                        var sCastPos = Player.Position.Extend(castPos, E.Range);

                        var extDist = ELength / 4f;
                        var circle =
                            new Geometry.Polygon.Circle(Player.Position, sCastPos.Distance(Player.Position), 45).Points
                                .Where(p => p.Distance(sCastPos) < extDist).OrderBy(p => p.Distance(lTarget));
                        foreach (var point in circle)
                        {
                            input2.From = point.To3D();
                            input2.RangeCheckFrom = point.To3D();
                            input2.Range = ELength;
                            var pred2 = Prediction.GetPrediction(input2);
                            if (pred2.Hitchance >= hitChance)
                            {
                                containsTarget = mainTarget == null || lTarget.NetworkId == mainTarget.NetworkId;
                                var count = 1;
                                var rect = new Geometry.Polygon.Rectangle(
                                    point, point.To3D().Extend(pred2.CastPosition, ELength).To2D(), E.Width);
                                foreach (var c in targets.Where(t => t.NetworkId != lTarget.NetworkId))
                                {
                                    input2.Unit = c;
                                    var cPredPos = c.Type == GameObjectType.obj_AI_Minion
                                        ? c.Position
                                        : Prediction.GetPrediction(input2).UnitPosition;
                                    if (
                                        new Geometry.Polygon.Circle(
                                            cPredPos,
                                            (c.Type == GameObjectType.obj_AI_Minion && c.IsMoving
                                                ? (c.BoundingRadius / 2f)
                                                : (c.BoundingRadius) * 0.9f)).Points.Any(p => rect.IsInside(p)))
                                    {
                                        count++;
                                        if (!containsTarget && c.NetworkId == mainTarget.NetworkId)
                                        {
                                            containsTarget = true;
                                        }
                                    }
                                }
                                if (count > hits && containsTarget ||
                                    count == hits && containsTarget && mainTarget != null &&
                                    point.Distance(mainTarget.Position) < startPos.Distance(mainTarget.Position))
                                {
                                    hits = count;
                                    startPos = point.To3D();
                                    endPos = startPos.Extend(pred2.CastPosition, ELength);
                                    if (hits == targetCount)
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    if (hits == targetCount)
                    {
                        break;
                    }
                }
                if (hits >= minHits && !startPos.Equals(Vector3.Zero) && !endPos.Equals(Vector3.Zero))
                {
                    if (startPos.Distance(Player.Position) > E.Range)
                    {
                        startPos = Player.Position.Extend(startPos, E.Range);
                    }
                    if (startPos.Distance(endPos) > ELength)
                    {
                        endPos = startPos.Extend(endPos, ELength);
                    }
                    E.Cast(startPos, endPos);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return false;
        }

        protected override void LaneClear()
        {
            var useE = Menu.Item(Menu.Name + ".lane-clear.e").GetValue<bool>() && E.IsReady() &&
                       ResourceManager.Check("lane-clear-e");
            var useQ = Menu.Item(Menu.Name + ".lane-clear.q").GetValue<bool>() && Q.IsReady() &&
                       ResourceManager.Check("lane-clear-q");

            if (useE)
            {
                var minions = MinionManager.GetMinions((E.Range + ELength + E.Width) * 1.2f);
                var minHits = Menu.Item(Menu.Name + ".lane-clear.e-min").GetValue<Slider>().Value;
                if (minions.Count >= minHits)
                {
                    ELogic(null, (minions.Concat(GameObjects.EnemyHeroes)).ToList(), HitChance.High, minHits);
                }
            }

            if (useQ)
            {
                var minion =
                    MinionManager.GetMinions(Q.Range)
                        .FirstOrDefault(m => m.Health < Q.GetDamage(m) || m.Health * 2 > Q.GetDamage(m));
                if (minion != null)
                {
                    Casting.TargetSkill(minion, Q);
                }
            }
        }

        protected override void JungleClear()
        {
            var useE = Menu.Item(Menu.Name + ".lane-clear.e").GetValue<bool>() && E.IsReady() &&
                       (ResourceManager.Check("lane-clear-e") || ResourceManager.IgnoreJungle("lane-clear-e"));
            var useQ = Menu.Item(Menu.Name + ".lane-clear.q").GetValue<bool>() && Q.IsReady() &&
                       (ResourceManager.Check("lane-clear-q") || ResourceManager.IgnoreJungle("lane-clear-q"));

            if (useE)
            {
                var minions = MinionManager.GetMinions(
                    (E.Range + ELength + E.Width) * 1.2f, MinionTypes.All, MinionTeam.Neutral,
                    MinionOrderTypes.MaxHealth);
                ELogic(null, (minions.Concat(GameObjects.EnemyHeroes)).ToList(), HitChance.High, 1);
            }


            if (useQ)
            {
                var minion =
                    MinionManager.GetMinions(Q.Range, MinionTypes.All, MinionTeam.Neutral, MinionOrderTypes.MaxHealth)
                        .FirstOrDefault();
                if (minion != null)
                {
                    Casting.TargetSkill(minion, Q);
                }
            }
        }

        protected override void Flee()
        {
            if (Menu.Item(Menu.Name + ".flee.w").GetValue<bool>() && W.IsReady())
            {
                var near =
                    GameObjects.EnemyHeroes.Where(e => W.CanCast(e))
                        .OrderBy(e => e.Distance(Player.Position))
                        .FirstOrDefault();
                if (near != null)
                {
                    Casting.SkillShot(near, W, W.GetHitChance("combo"));
                }
            }
            if (Menu.Item(Menu.Name + ".flee.q-upgraded").GetValue<bool>() && Q.IsReady() && IsSpellUpgraded(Q))
            {
                var near =
                    GameObjects.EnemyHeroes.Where(e => Q.CanCast(e))
                        .OrderBy(e => e.Distance(Player.Position))
                        .FirstOrDefault();
                if (near != null)
                {
                    Casting.TargetSkill(near, Q);
                }
                else
                {
                    var mobs = MinionManager.GetMinions(Q.Range, MinionTypes.All, MinionTeam.NotAlly);
                    if (mobs.Any())
                    {
                        Casting.TargetSkill(mobs.First(), Q);
                    }
                }
            }
        }

        protected override void Killsteal()
        {
            if (Menu.Item(Menu.Name + ".killsteal.q-aa").GetValue<bool>() && Q.IsReady())
            {
                var target =
                    GameObjects.EnemyHeroes.FirstOrDefault(t => t.IsValidTarget() && Orbwalking.InAutoAttackRange(t));
                if (target != null)
                {
                    var damage = CalcPassiveDamage(target) + Q.GetDamage(target);
                    if (damage - 10 > target.Health)
                    {
                        Casting.TargetSkill(target, Q);
                        Orbwalker.ForceTarget(target);
                    }
                }
            }
            if (Menu.Item(Menu.Name + ".killsteal.e").GetValue<bool>() && E.IsReady())
            {
                foreach (var target in
                    GameObjects.EnemyHeroes.Where(t => t.IsValidTarget(E.Range + ELength)))
                {
                    var damage = E.GetDamage(target);
                    if (damage * 0.95f > target.Health)
                    {
                        if (ELogic(target, GameObjects.EnemyHeroes.ToList(), HitChance.High))
                        {
                            break;
                        }
                    }
                }
            }
        }

        private Vector3 BestRFollowLocation(Vector3 position)
        {
            try
            {
                var center = Vector2.Zero;
                float radius = -1;
                var count = 0;
                var moveDistance = -1f;
                var maxRelocation = IsSpellUpgraded(R) ? R.Width * 1.2f : R.Width * 0.8f;
                var targets = GameObjects.EnemyHeroes.Where(t => t.IsValidTarget(1500f)).ToList();
                var circle = new Geometry.Polygon.Circle(position, R.Width);
                if (targets.Any())
                {
                    var minDistance = targets.Any(t => circle.IsInside(t)) ? targets.Min(t => t.BoundingRadius) * 2 : 0;
                    var possibilities =
                        ListExtensions.ProduceEnumeration(targets.Select(t => t.Position.To2D()).ToList())
                            .Where(p => p.Count > 1)
                            .ToList();
                    if (possibilities.Any())
                    {
                        foreach (var possibility in possibilities)
                        {
                            var mec = MEC.GetMec(possibility);
                            var distance = position.Distance(mec.Center.To3D());
                            if (mec.Radius < R.Width && distance < maxRelocation && distance > minDistance)
                            {
                                if (possibility.Count > count ||
                                    possibility.Count == count && (mec.Radius < radius || distance < moveDistance))
                                {
                                    moveDistance = position.Distance(mec.Center.To3D());
                                    center = mec.Center;
                                    radius = mec.Radius;
                                    count = possibility.Count;
                                }
                            }
                        }
                        if (!center.Equals(Vector2.Zero))
                        {
                            return center.To3D();
                        }
                    }
                    var dTarget = targets.OrderBy(t => t.Distance(position)).FirstOrDefault();
                    if (dTarget != null && position.Distance(dTarget.Position) > minDistance)
                    {
                        return dTarget.Position;
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return Vector3.Zero;
        }
    }
}