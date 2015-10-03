#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 Graves.cs is part of SFXGraves.

 SFXGraves is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXGraves is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXGraves. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

#region

using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SFXGraves.Abstracts;
using SFXGraves.Args;
using SFXGraves.Enumerations;
using SFXGraves.Helpers;
using SFXGraves.Library;
using SFXGraves.Library.Logger;
using SFXGraves.Managers;
using SharpDX;
using DamageType = SFXGraves.Enumerations.DamageType;
using Orbwalking = SFXGraves.Wrappers.Orbwalking;
using Spell = SFXGraves.Wrappers.Spell;
using TargetSelector = SFXGraves.SFXTargetSelector.TargetSelector;

#endregion

namespace SFXGraves.Champions
{
    internal class Graves : Champion
    {
        private UltimateManager _ultimate;

        protected override ItemFlags ItemFlags
        {
            get { return ItemFlags.Offensive | ItemFlags.Defensive | ItemFlags.Flee; }
        }

        protected override ItemUsageType ItemUsage
        {
            get { return ItemUsageType.AfterAttack; }
        }

        public Spell R2 { get; private set; }

        protected override void OnLoad()
        {
            GapcloserManager.OnGapcloser += OnEnemyGapcloser;
        }

        protected override void SetupSpells()
        {
            Q = new Spell(SpellSlot.Q, 850f);
            Q.SetSkillshot(0.25f, 15f * (float) Math.PI / 180, 2000f, false, SkillshotType.SkillshotCone);

            W = new Spell(SpellSlot.W, 900f, DamageType.Magical);
            W.SetSkillshot(0.35f, 250f, 1650f, false, SkillshotType.SkillshotCircle);

            E = new Spell(SpellSlot.E, 425f);

            R = new Spell(SpellSlot.R, 1100f);
            R.SetSkillshot(0.25f, 110f, 2100f, false, SkillshotType.SkillshotLine);

            R2 = new Spell(SpellSlot.R, 700f);
            R2.SetSkillshot(0f, 110f, 1500f, false, SkillshotType.SkillshotCone);

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
                Interrupt = false,
                InterruptDelay = false,
                Spells = Spells,
                DamageCalculation =
                    hero => CalcComboDamage(hero, Menu.Item(Menu.Name + ".combo.q").GetValue<bool>(), true)
            };
        }

        protected override void AddToMenu()
        {
            _ultimate.AddToMenu(Menu);

            var comboMenu = Menu.AddSubMenu(new Menu("Combo", Menu.Name + ".combo"));
            HitchanceManager.AddToMenu(
                comboMenu.AddSubMenu(new Menu("Hitchance", comboMenu.Name + ".hitchance")), "combo",
                new Dictionary<string, HitChance>
                {
                    { "Q", HitChance.VeryHigh },
                    { "W", HitChance.VeryHigh },
                    { "R", HitChance.High }
                });
            comboMenu.AddItem(
                new MenuItem(comboMenu.Name + ".e-mode", "E Mode").SetValue(new StringList(new[] { "Auto", "Cursor" })));
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".q", "Use Q").SetValue(true));
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".w", "Use W").SetValue(true));
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".e", "Use E").SetValue(true));

            var harassMenu = Menu.AddSubMenu(new Menu("Harass", Menu.Name + ".harass"));
            HitchanceManager.AddToMenu(
                harassMenu.AddSubMenu(new Menu("Hitchance", harassMenu.Name + ".hitchance")), "harass",
                new Dictionary<string, HitChance> { { "Q", HitChance.High } });
            ResourceManager.AddToMenu(
                harassMenu,
                new ResourceManagerArgs(
                    "harass", ResourceType.Mana, ResourceValueType.Percent, ResourceCheckType.Minimum)
                {
                    DefaultValue = 30
                });
            harassMenu.AddItem(new MenuItem(harassMenu.Name + ".q", "Use Q").SetValue(true));

            var laneclearMenu = Menu.AddSubMenu(new Menu("Lane Clear", Menu.Name + ".lane-clear"));
            ResourceManager.AddToMenu(
                laneclearMenu,
                new ResourceManagerArgs(
                    "lane-clear", ResourceType.Mana, ResourceValueType.Percent, ResourceCheckType.Minimum)
                {
                    Advanced = true,
                    MaxValue = 101,
                    LevelRanges = new SortedList<int, int> { { 1, 6 }, { 6, 12 }, { 12, 18 } },
                    DefaultValues = new List<int> { 50, 30, 30 }
                });
            laneclearMenu.AddItem(new MenuItem(laneclearMenu.Name + ".q", "Use Q").SetValue(true));
            laneclearMenu.AddItem(new MenuItem(laneclearMenu.Name + ".q-min", "Q Min.").SetValue(new Slider(3, 1, 5)));

            var fleeMenu = Menu.AddSubMenu(new Menu("Flee", Menu.Name + ".flee"));
            fleeMenu.AddItem(new MenuItem(fleeMenu.Name + ".e", "Use E").SetValue(true));

            var killstealMenu = Menu.AddSubMenu(new Menu("Killsteal", Menu.Name + ".killsteal"));
            killstealMenu.AddItem(new MenuItem(killstealMenu.Name + ".q", "Use Q").SetValue(true));

            var miscMenu = Menu.AddSubMenu(new Menu("Misc", Menu.Name + ".miscellaneous"));

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

            var eGapcloserMenu = miscMenu.AddSubMenu(new Menu("E Gapcloser", miscMenu.Name + "e-gapcloser"));
            GapcloserManager.AddToMenu(
                eGapcloserMenu,
                new HeroListManagerArgs("e-gapcloser")
                {
                    IsWhitelist = false,
                    Allies = false,
                    Enemies = true,
                    DefaultValue = false
                }, true);
            BestTargetOnlyManager.AddToMenu(eGapcloserMenu, "e-gapcloser");

            IndicatorManager.AddToMenu(DrawingManager.Menu, true);
            IndicatorManager.Add(Q);
            IndicatorManager.Add(W);
            IndicatorManager.Add(R);
            IndicatorManager.Finale();
        }

        protected override void OnPreUpdate() {}

        protected override void OnPostUpdate()
        {
            if (_ultimate.IsActive(UltimateModeType.Assisted) && R.IsReady())
            {
                if (_ultimate.ShouldMove(UltimateModeType.Assisted))
                {
                    Orbwalking.MoveTo(Game.CursorPos, Orbwalker.HoldAreaRadius);
                }

                if (!RLogic(UltimateModeType.Assisted, TargetSelector.GetTarget(R)))
                {
                    RLogicSingle(UltimateModeType.Assisted);
                }
            }

            if (_ultimate.IsActive(UltimateModeType.Auto) && R.IsReady())
            {
                if (!RLogic(UltimateModeType.Auto, TargetSelector.GetTarget(R)))
                {
                    RLogicSingle(UltimateModeType.Auto);
                }
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
                if (args.UniqueId == "e-gapcloser" && E.IsReady() &&
                    BestTargetOnlyManager.Check("e-gapcloser", E, args.Hero))
                {
                    E.Cast(args.End.Extend(Player.Position, args.End.Distance(Player.Position) + E.Range));
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        protected override void Combo()
        {
            var useQ = Menu.Item(Menu.Name + ".combo.q").GetValue<bool>() && Q.IsReady();
            var useW = Menu.Item(Menu.Name + ".combo.w").GetValue<bool>() && W.IsReady();
            var useE = Menu.Item(Menu.Name + ".combo.e").GetValue<bool>() && E.IsReady();
            var useR = _ultimate.IsActive(UltimateModeType.Combo) && R.IsReady();

            if (useR)
            {
                var target = TargetSelector.GetTarget(R.Range, R.DamageType);
                if (target != null)
                {
                    if (!RLogic(UltimateModeType.Combo, target))
                    {
                        RLogicSingle(UltimateModeType.Combo);
                    }
                }
            }
            if (useE)
            {
                var target = TargetSelector.GetTarget((E.Range + Player.AttackRange) * 0.9f, E.DamageType);
                if (target != null)
                {
                    var pos = Menu.Item(Menu.Name + ".combo.e-mode").GetValue<StringList>().SelectedIndex == 0
                        ? GetDashPosition(target)
                        : Player.Position.Extend(
                            Game.CursorPos, Math.Min(E.Range, Player.Position.Distance(Game.CursorPos)));
                    if (pos.Equals(Vector3.Zero) && !pos.UnderTurret(true))
                    {
                        E.Cast(pos);
                    }
                }
            }
            if (useQ)
            {
                Casting.SkillShot(Q, Q.GetHitChance("combo"));
            }
            if (useW)
            {
                var target = TargetSelector.GetTarget(W);
                var best = CPrediction.Circle(W, target, W.GetHitChance("combo"));
                if (best.TotalHits > 0 && !best.CastPosition.Equals(Vector3.Zero))
                {
                    W.Cast(best.CastPosition);
                }
            }
        }

        private bool RLogic(UltimateModeType mode, Obj_AI_Hero target)
        {
            try
            {
                if (_ultimate.IsActive(mode))
                {
                    var hits = GetRHits(target);
                    if (_ultimate.Check(mode, hits.Item2))
                    {
                        R.Cast(hits.Item3);
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

        private void RLogicSingle(UltimateModeType mode)
        {
            try
            {
                if (_ultimate.ShouldSingle(mode))
                {
                    foreach (var target in GameObjects.EnemyHeroes.Where(t => _ultimate.CheckSingle(mode, t)))
                    {
                        var hits = GetRHits(target);
                        if (hits.Item1 > 0)
                        {
                            R.Cast(hits.Item3);
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private float CalcComboDamage(Obj_AI_Hero target, bool q, bool r)
        {
            try
            {
                if (target == null)
                {
                    return 0;
                }

                var damage = 0f;
                var totalMana = 0f;
                var manaMulti = (GameObjects.EnemyHeroes.Count(x => x.IsValidTarget(2000)) == 1
                    ? 100
                    : _ultimate.DamagePercent) / 100f;

                if (r && R.IsReady() && R.IsInRange(target, R.Range + R2.Range))
                {
                    var rMana = R.ManaCost * manaMulti;
                    if (totalMana + rMana <= Player.Mana)
                    {
                        totalMana += rMana;
                        damage += R.GetDamage(target);
                    }
                }
                if (q && Q.IsReady() && Q.IsInRange(target))
                {
                    var qMana = Q.ManaCost * manaMulti;
                    if (totalMana + qMana <= Player.Mana)
                    {
                        damage += Q.GetDamage(target);
                    }
                }
                if (target.Distance(Player) <= Orbwalking.GetRealAutoAttackRange(target) * 0.85f)
                {
                    damage += 2 * (float) Player.GetAutoAttackDamage(target, true);
                }
                damage += ItemManager.CalculateComboDamage(target);
                damage += SummonerManager.CalculateComboDamage(target);
                return damage;
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return 0;
        }

        private Tuple<int, List<Obj_AI_Hero>, Vector3> GetRHits(Obj_AI_Hero target)
        {
            var hits = new List<Obj_AI_Hero>();
            var castPos = Vector3.Zero;
            try
            {
                var pred = R.GetPrediction(target);
                if (pred.Hitchance >= R.GetHitChance("combo"))
                {
                    castPos = pred.CastPosition;
                    hits.Add(target);
                    var pos = Player.Position.Extend(castPos, Math.Min(Player.Distance(pred.UnitPosition), R.Range));
                    var pos2 = Player.Position.Extend(pos, Player.Distance(pos) + R2.Range);

                    var input = new PredictionInput
                    {
                        Range = R2.Range,
                        Delay = Player.Position.Distance(pred.UnitPosition) / R.Speed + 0.1f,
                        From = pos,
                        RangeCheckFrom = pos,
                        Radius = R2.Width,
                        Type = SkillshotType.SkillshotLine,
                        Speed = R2.Speed
                    };

                    var rect = new Geometry.Polygon.Rectangle(pos, pos2, R2.Width);

                    foreach (var enemy in
                        GameObjects.EnemyHeroes.Where(e => e.IsValidTarget() && e.NetworkId != target.NetworkId))
                    {
                        input.Unit = enemy;
                        var pred2 = Prediction.GetPrediction(input);
                        if (!pred2.UnitPosition.Equals(Vector3.Zero))
                        {
                            if (
                                new Geometry.Polygon.Circle(enemy.Position, enemy.BoundingRadius).Points.Any(
                                    p => rect.IsInside(p)))
                            {
                                hits.Add(enemy);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return new Tuple<int, List<Obj_AI_Hero>, Vector3>(hits.Count, hits, castPos);
        }

        private Vector3 GetDashPosition(Obj_AI_Hero target)
        {
            if (!target.IsMelee && Player.CountEnemiesInRange(E.Range + 500) == 1)
            {
                return Game.CursorPos;
            }
            var aRc = new Geometry.Polygon.Circle(Player.ServerPosition.To2D(), E.Range);
            var cursorPos = Game.CursorPos;
            var targetPosition = target.ServerPosition;
            var pList = new List<Vector3>();
            var additionalDistance = (0.2 + Game.Ping / 2000f) * target.MoveSpeed;

            if (!IsDangerousPosition(cursorPos))
            {
                return cursorPos;
            }

            foreach (var p in aRc.Points)
            {
                var v3 = new Vector2(p.X, p.Y).To3D();

                if (target.IsFacing(Player))
                {
                    if (!IsDangerousPosition(v3) && v3.Distance(targetPosition) < 550)
                    {
                        pList.Add(v3);
                    }
                }
                else
                {
                    if (!IsDangerousPosition(v3) && v3.Distance(targetPosition) < 550 - additionalDistance)
                    {
                        pList.Add(v3);
                    }
                }
            }
            if (Player.UnderTurret() || Player.CountEnemiesInRange(800) == 1)
            {
                return pList.Count > 1 ? pList.OrderBy(el => el.Distance(cursorPos)).FirstOrDefault() : Vector3.Zero;
            }
            if (!IsDangerousPosition(cursorPos))
            {
                return pList.Count > 1 ? pList.OrderBy(el => el.Distance(cursorPos)).FirstOrDefault() : Vector3.Zero;
            }
            return pList.Count > 1
                ? pList.OrderByDescending(el => el.Distance(cursorPos)).FirstOrDefault()
                : Vector3.Zero;
        }

        private bool IsDangerousPosition(Vector3 pos)
        {
            return
                HeroManager.Enemies.Any(
                    e => e.IsValidTarget() && e.IsVisible || (pos.UnderTurret(true) && !Player.UnderTurret(true))) ||
                pos.IsWall();
        }

        protected override void Harass()
        {
            if (!ResourceManager.Check("harass"))
            {
                return;
            }

            if (Menu.Item(Menu.Name + ".harass.q").GetValue<bool>() && Q.IsReady())
            {
                Casting.SkillShot(Q, Q.GetHitChance("harass"));
            }
        }

        protected override void LaneClear()
        {
            if (!ResourceManager.Check("lane-clear"))
            {
                return;
            }

            var useQ = Menu.Item(Menu.Name + ".lane-clear.q").GetValue<bool>() && Q.IsReady();
            var minQ = Menu.Item(Menu.Name + ".lane-clear.q-min").GetValue<Slider>().Value;

            if (useQ)
            {
                Casting.Farm(Q, minQ, 200f);
            }
        }

        protected override void Flee()
        {
            if (Menu.Item(Menu.Name + ".flee.e").GetValue<bool>() && E.IsReady())
            {
                E.Cast(Player.Position.Extend(Game.CursorPos, E.Range));
            }
        }

        protected override void Killsteal()
        {
            if (Menu.Item(Menu.Name + ".killsteal.q").GetValue<bool>() && Q.IsReady())
            {
                var fPredEnemy =
                    GameObjects.EnemyHeroes.Where(e => e.IsValidTarget(Q.Range * 1.2f) && Q.IsKillable(e))
                        .Select(enemy => Q.GetPrediction(enemy, true))
                        .FirstOrDefault(pred => pred.Hitchance >= HitChance.High);
                if (fPredEnemy != null)
                {
                    Q.Cast(fPredEnemy.CastPosition);
                }
            }
        }
    }
}