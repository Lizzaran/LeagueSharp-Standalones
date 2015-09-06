#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 Vladimir.cs is part of SFXVladimir.

 SFXVladimir is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXVladimir is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXVladimir. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

#region

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SFXVladimir.Abstracts;
using SFXVladimir.Enumerations;
using SFXVladimir.Helpers;
using SFXVladimir.Library;
using SFXVladimir.Library.Logger;
using SFXVladimir.Managers;
using DamageType = SFXVladimir.Enumerations.DamageType;
using MinionManager = SFXVladimir.Library.MinionManager;
using MinionTeam = SFXVladimir.Library.MinionTeam;
using MinionTypes = SFXVladimir.Library.MinionTypes;
using Orbwalking = SFXVladimir.Wrappers.Orbwalking;
using Spell = SFXVladimir.Wrappers.Spell;
using TargetSelector = SFXVladimir.SFXTargetSelector.TargetSelector;

#endregion

namespace SFXVladimir.Champions
{
    internal class Vladimir : Champion
    {
        private MenuItem _eStacks;

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
            Core.OnPostUpdate += OnCorePostUpdate;
            AntiGapcloser.OnEnemyGapcloser += OnEnemyGapcloser;
            CustomEvents.Unit.OnDash += OnUnitDash;
            Drawing.OnDraw += OnDrawingDraw;
            Orbwalking.OnNonKillableMinion += OnOrbwalkingNonKillableMinion;
        }

        protected override void OnUnload()
        {
            Core.OnPostUpdate -= OnCorePostUpdate;
            AntiGapcloser.OnEnemyGapcloser -= OnEnemyGapcloser;
            CustomEvents.Unit.OnDash -= OnUnitDash;
            Drawing.OnDraw -= OnDrawingDraw;
            Orbwalking.OnNonKillableMinion -= OnOrbwalkingNonKillableMinion;
        }

        protected override void AddToMenu()
        {
            var comboMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_Combo"), Menu.Name + ".combo"));
            HealthManager.AddToMenu(comboMenu, "combo-e", HealthCheckType.Minimum, HealthValueType.Percent, "E", 0);
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".q", Global.Lang.Get("G_UseQ")).SetValue(true));
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".e", Global.Lang.Get("G_UseE")).SetValue(true));

            var harassMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_Harass"), Menu.Name + ".harass"));
            HealthManager.AddToMenu(harassMenu, "harass-e", HealthCheckType.Minimum, HealthValueType.Percent, "E");
            harassMenu.AddItem(new MenuItem(harassMenu.Name + ".q", Global.Lang.Get("G_UseQ")).SetValue(true));
            harassMenu.AddItem(new MenuItem(harassMenu.Name + ".e", Global.Lang.Get("G_UseE")).SetValue(true));

            var laneclearMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_LaneClear"), Menu.Name + ".lane-clear"));
            HealthManager.AddToMenu(
                laneclearMenu, "lane-clear-e", HealthCheckType.Minimum, HealthValueType.Percent, "E");
            laneclearMenu.AddItem(new MenuItem(laneclearMenu.Name + ".q", Global.Lang.Get("G_UseQ")).SetValue(true));
            laneclearMenu.AddItem(new MenuItem(laneclearMenu.Name + ".e", Global.Lang.Get("G_UseW")).SetValue(true));
            laneclearMenu.AddItem(
                new MenuItem(laneclearMenu.Name + ".e-min", "E " + Global.Lang.Get("G_Min")).SetValue(
                    new Slider(3, 1, 5)));

            var lasthitMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_LastHit"), Menu.Name + ".lasthit"));
            lasthitMenu.AddItem(new MenuItem(lasthitMenu.Name + ".q", Global.Lang.Get("G_UseQ")).SetValue(true));
            lasthitMenu.AddItem(
                new MenuItem(lasthitMenu.Name + ".q-unkillable", "Q " + Global.Lang.Get("G_Unkillable")).SetValue(true));

            UltimateManager.AddToMenu(Menu, true, false, false, false, false, false, true, true, true);

            var killstealMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_Killsteal"), Menu.Name + ".killsteal"));
            killstealMenu.AddItem(new MenuItem(killstealMenu.Name + ".q", Global.Lang.Get("G_UseQ")).SetValue(true));

            var fleeMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_Flee"), Menu.Name + ".flee"));
            fleeMenu.AddItem(new MenuItem(fleeMenu.Name + ".q", Global.Lang.Get("G_UseQ")).SetValue(true));

            var miscMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_Miscellaneous"), Menu.Name + ".miscellaneous"));
            HeroListManager.AddToMenu(
                miscMenu.AddSubMenu(new Menu("W " + Global.Lang.Get("G_Gapcloser"), miscMenu.Name + "w-gapcloser")),
                "w-gapcloser", false, false, true, false);
            HealthManager.AddToMenu(miscMenu, "auto-e", HealthCheckType.Minimum, HealthValueType.Percent, "E");
            miscMenu.AddItem(
                new MenuItem(miscMenu.Name + ".e-auto", Global.Lang.Get("Vladimir_AutoEStacking")).SetValue(false));

            IndicatorManager.AddToMenu(DrawingManager.Menu, true);
            IndicatorManager.Add(Q);
            IndicatorManager.Add(E);
            IndicatorManager.Add(R);
            IndicatorManager.Finale();

            _eStacks = DrawingManager.Add("E " + Global.Lang.Get("G_Stacks"), true);
        }

        private void OnOrbwalkingNonKillableMinion(AttackableUnit unit)
        {
            try
            {
                if (Menu.Item(Menu.Name + ".lasthit.q-unkillable").GetValue<bool>() && Q.IsReady() && Q.IsInRange(unit))
                {
                    var target = unit as Obj_AI_Base;
                    if (target != null && HealthPrediction.GetHealthPrediction(target, (int) (Q.Delay * 1000f)) > 0)
                    {
                        Q.CastOnUnit(target);
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnUnitDash(Obj_AI_Base sender, Dash.DashItem args)
        {
            try
            {
                var hero = sender as Obj_AI_Hero;
                if (!sender.IsEnemy || hero == null)
                {
                    return;
                }
                if (HeroListManager.Check("w-gapcloser", hero) && Player.Distance(args.EndPos) <= W.Width * 0.9f &&
                    W.IsReady())
                {
                    W.Cast();
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnEnemyGapcloser(ActiveGapcloser args)
        {
            try
            {
                if (!args.Sender.IsEnemy)
                {
                    return;
                }
                if (HeroListManager.Check("w-gapcloser", args.Sender) && Player.Distance(args.End) <= W.Width * 0.9f &&
                    W.IsReady())
                {
                    W.Cast();
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        protected override void SetupSpells()
        {
            Q = new Spell(SpellSlot.Q, 600f, DamageType.Magical);
            Q.Range += GameObjects.EnemyHeroes.Select(e => e.BoundingRadius).DefaultIfEmpty(50).Average();
            Q.SetTargetted(Q.Instance.SData.CastFrame / 30f, Q.Instance.SData.MissileSpeed);

            W = new Spell(SpellSlot.W, 175f, DamageType.Magical);

            E = new Spell(SpellSlot.E, 600f, DamageType.Magical);
            E.Delay = E.Instance.SData.CastFrame / 30f;
            E.Width = E.Range;

            R = new Spell(SpellSlot.R, 700f, DamageType.Magical);
            R.SetSkillshot(0.25f, 175f, float.MaxValue, false, SkillshotType.SkillshotCircle);
        }

        private void OnCorePostUpdate(EventArgs args)
        {
            try
            {
                if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.LastHit &&
                    Menu.Item(Menu.Name + ".lasthit.q").GetValue<bool>() && Q.IsReady())
                {
                    var m =
                        MinionManager.GetMinions(Q.Range, MinionTypes.All, MinionTeam.NotAlly)
                            .FirstOrDefault(e => Q.IsKillable(e));
                    if (m != null)
                    {
                        Casting.TargetSkill(m, Q);
                    }
                }

                if (UltimateManager.Assisted() && R.IsReady())
                {
                    if (Menu.Item(Menu.Name + ".ultimate.assisted.move-cursor").GetValue<bool>())
                    {
                        Orbwalking.MoveTo(Game.CursorPos, Orbwalker.HoldAreaRadius);
                    }
                    var target = TargetSelector.GetTarget(R);
                    if (target != null &&
                        !RLogic(
                            target, Menu.Item(Menu.Name + ".ultimate.assisted.min").GetValue<Slider>().Value,
                            Menu.Item(Menu.Name + ".combo.q").GetValue<bool>() && Q.IsReady(),
                            Menu.Item(Menu.Name + ".combo.e").GetValue<bool>() && E.IsReady()))
                    {
                        if (Menu.Item(Menu.Name + ".ultimate.assisted.duel").GetValue<bool>())
                        {
                            RLogicDuel(
                                Menu.Item(Menu.Name + ".combo.q").GetValue<bool>() && Q.IsReady(),
                                Menu.Item(Menu.Name + ".combo.e").GetValue<bool>() && E.IsReady());
                        }
                    }
                }

                if (UltimateManager.Auto() && R.IsReady())
                {
                    var target = TargetSelector.GetTarget(R);
                    if (target != null &&
                        !RLogic(
                            target, Menu.Item(Menu.Name + ".ultimate.auto.min").GetValue<Slider>().Value,
                            Menu.Item(Menu.Name + ".combo.q").GetValue<bool>() && Q.IsReady(),
                            Menu.Item(Menu.Name + ".combo.e").GetValue<bool>() && E.IsReady(), "auto"))
                    {
                        if (Menu.Item(Menu.Name + ".ultimate.auto.duel").GetValue<bool>())
                        {
                            RLogicDuel(
                                Menu.Item(Menu.Name + ".combo.q").GetValue<bool>() && Q.IsReady(),
                                Menu.Item(Menu.Name + ".combo.e").GetValue<bool>() && E.IsReady());
                        }
                    }
                }

                if (Menu.Item(Menu.Name + ".miscellaneous.e-auto").GetValue<bool>() && E.IsReady() &&
                    HealthManager.Check("auto-e") && !Player.IsRecalling() && !Player.InFountain())
                {
                    var buff = GetEBuff();
                    if (buff == null || (buff.EndTime - Game.Time) <= Game.Ping / 2000f + 0.5f)
                    {
                        E.Cast();
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private BuffInstance GetEBuff()
        {
            try
            {
                return
                    Player.Buffs.FirstOrDefault(
                        b => b.Name.Equals("vladimirtidesofbloodcost", StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return null;
        }

        private Tuple<int, List<Obj_AI_Hero>> GetEHits()
        {
            try
            {
                var hits =
                    GameObjects.EnemyHeroes.Where(
                        e =>
                            e.IsValidTarget() && e.Distance(Player) < E.Width * 0.8f ||
                            e.Distance(Player) < E.Width && e.IsFacing(Player)).ToList();
                return new Tuple<int, List<Obj_AI_Hero>>(hits.Count, hits);
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return new Tuple<int, List<Obj_AI_Hero>>(0, null);
        }

        protected override void Combo()
        {
            var q = Menu.Item(Menu.Name + ".combo.q").GetValue<bool>() && Q.IsReady();
            var e = Menu.Item(Menu.Name + ".combo.e").GetValue<bool>() && E.IsReady() && HealthManager.Check("combo-e");
            var r = UltimateManager.Combo() && R.IsReady();

            var rTarget = TargetSelector.GetTarget(R);
            if (r)
            {
                if (!RLogic(rTarget, Menu.Item(Menu.Name + ".ultimate.combo.min").GetValue<Slider>().Value, q, e))
                {
                    if (Menu.Item(Menu.Name + ".ultimate.combo.duel").GetValue<bool>())
                    {
                        RLogicDuel(q, e);
                    }
                }
            }
            if (q)
            {
                Casting.TargetSkill(Q);
            }
            if (e)
            {
                if (GetEHits().Item1 > 0)
                {
                    E.Cast();
                }
            }
            if (rTarget != null && CalcComboDamage(rTarget, q, e, r) > rTarget.Health)
            {
                ItemManager.UseComboItems(rTarget);
                SummonerManager.UseComboSummoners(rTarget);
            }
        }

        protected override void Harass()
        {
            var q = Menu.Item(Menu.Name + ".harass.q").GetValue<bool>() && Q.IsReady();
            var e = Menu.Item(Menu.Name + ".harass.e").GetValue<bool>() && E.IsReady() &&
                    HealthManager.Check("harass-e");

            if (q)
            {
                Casting.TargetSkill(Q);
            }
            if (e)
            {
                if (GetEHits().Item1 > 0)
                {
                    E.Cast();
                }
            }
        }

        private float CalcComboDamage(Obj_AI_Hero target, bool q, bool e, bool r)
        {
            try
            {
                if (target == null)
                {
                    return 0;
                }
                float damage = 0;
                if (q && Q.IsReady())
                {
                    damage += Q.GetDamage(target) * 2;
                }
                if (e)
                {
                    damage += E.GetDamage(target) * 2;
                }
                if (r && R.IsReady())
                {
                    damage += 1.2f;
                    damage += R.GetDamage(target);
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

        private bool RLogic(Obj_AI_Hero target, int min, bool q, bool e, string mode = "combo")
        {
            try
            {
                var pred = CPrediction.Circle(R, target, HitChance.High, false);
                if (pred.TotalHits > 0 &&
                    UltimateManager.Check(mode, min, pred.Hits, hero => CalcComboDamage(hero, q, e, true)))
                {
                    R.Cast(pred.CastPosition);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return false;
        }

        private void RLogicDuel(bool q, bool e)
        {
            try
            {
                foreach (var enemy in
                    GameObjects.EnemyHeroes.Where(t => UltimateManager.CheckDuel(t, CalcComboDamage(t, q, e, true))))
                {
                    if (RLogic(enemy, 1, q, e))
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        protected override void LaneClear()
        {
            var q = Menu.Item(Menu.Name + ".lane-clear.q").GetValue<bool>() && Q.IsReady();
            var e = Menu.Item(Menu.Name + ".lane-clear.e").GetValue<bool>() && E.IsReady() &&
                    HealthManager.Check("lane-clear-e");
            var eMin = Menu.Item(Menu.Name + ".lane-clear.e-min").GetValue<Slider>().Value;

            if (q)
            {
                Casting.Farm(Q, 1);
            }
            if (e)
            {
                Casting.FarmSelfAoe(E, eMin);
            }
        }

        protected override void Flee()
        {
            if (Menu.Item(Menu.Name + ".flee.q").GetValue<bool>() && Q.IsReady())
            {
                var target =
                    GameObjects.EnemyHeroes.Select(e => e as Obj_AI_Base)
                        .Concat(GameObjects.EnemyMinions)
                        .Where(e => e.IsValidTarget(Q.Range))
                        .OrderBy(e => e is Obj_AI_Hero)
                        .FirstOrDefault();
                if (target != null)
                {
                    Casting.TargetSkill(target, Q);
                }
            }
        }

        protected override void Killsteal()
        {
            if (Menu.Item(Menu.Name + ".killsteal.q").GetValue<bool>() && Q.IsReady())
            {
                var target = GameObjects.EnemyHeroes.FirstOrDefault(e => e.IsValidTarget(Q.Range) && Q.IsKillable(e));
                if (target != null)
                {
                    Casting.TargetSkill(target, Q);
                }
            }
        }

        private void OnDrawingDraw(EventArgs args)
        {
            try
            {
                if (E.Level > 0 && _eStacks != null && _eStacks.GetValue<bool>() && !Player.IsDead)
                {
                    var buff = GetEBuff();
                    var stacks = buff != null ? buff.Count - 1 : -1;
                    if (stacks > -1)
                    {
                        var x = Player.HPBarPosition.X + 40;
                        var y = Player.HPBarPosition.Y - 25;
                        for (var i = 0; 4 > i; i++)
                        {
                            Drawing.DrawLine(
                                x + (i * 20), y, x + (i * 20) + 10, y, 10, (i > stacks ? Color.DarkGray : Color.Orange));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }
    }
}