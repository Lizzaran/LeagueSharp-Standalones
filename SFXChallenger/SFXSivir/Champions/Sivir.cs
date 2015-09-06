#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 Sivir.cs is part of SFXSivir.

 SFXSivir is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXSivir is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXSivir. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

#region

using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SFXSivir.Abstracts;
using SFXSivir.Enumerations;
using SFXSivir.Events;
using SFXSivir.Helpers;
using SFXSivir.Library;
using SFXSivir.Library.Logger;
using SFXSivir.Managers;
using MinionManager = SFXSivir.Library.MinionManager;
using MinionOrderTypes = SFXSivir.Library.MinionOrderTypes;
using MinionTeam = SFXSivir.Library.MinionTeam;
using MinionTypes = SFXSivir.Library.MinionTypes;
using Orbwalking = SFXSivir.Wrappers.Orbwalking;
using Spell = SFXSivir.Wrappers.Spell;
using Utils = SFXSivir.Helpers.Utils;

#endregion

namespace SFXSivir.Champions
{
    internal class Sivir : Champion
    {
        protected override ItemFlags ItemFlags
        {
            get { return ItemFlags.Offensive | ItemFlags.Defensive | ItemFlags.Flee; }
        }

        protected override ItemUsageType ItemUsage
        {
            get { return ItemUsageType.AfterAttack; }
        }

        protected override void OnLoad()
        {
            Core.OnPostUpdate += OnCorePostUpdate;
            TargetSpellManager.OnEnemyTargetCast += OnEnemyTargetCast;
            Orbwalking.AfterAttack += OnOrbwalkingAfterAttack;
        }

        protected override void OnUnload()
        {
            Core.OnPostUpdate -= OnCorePostUpdate;
            TargetSpellManager.OnEnemyTargetCast -= OnEnemyTargetCast;
            Orbwalking.AfterAttack -= OnOrbwalkingAfterAttack;
        }

        protected override void AddToMenu()
        {
            var comboMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_Combo"), Menu.Name + ".combo"));
            HitchanceManager.AddToMenu(
                comboMenu.AddSubMenu(new Menu(Global.Lang.Get("F_MH"), comboMenu.Name + ".hitchance")), "combo",
                new Dictionary<string, HitChance> { { "Q", HitChance.VeryHigh } });
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".q", Global.Lang.Get("G_UseQ")).SetValue(true));
            comboMenu.AddItem(new MenuItem(comboMenu.Name + ".w", Global.Lang.Get("G_UseW")).SetValue(true));

            var harassMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_Harass"), Menu.Name + ".harass"));
            HitchanceManager.AddToMenu(
                harassMenu.AddSubMenu(new Menu(Global.Lang.Get("F_MH"), harassMenu.Name + ".hitchance")), "harass",
                new Dictionary<string, HitChance> { { "Q", HitChance.VeryHigh } });
            ManaManager.AddToMenu(harassMenu, "harass", ManaCheckType.Minimum, ManaValueType.Percent);
            harassMenu.AddItem(new MenuItem(harassMenu.Name + ".q", Global.Lang.Get("G_UseQ")).SetValue(true));
            harassMenu.AddItem(new MenuItem(harassMenu.Name + ".w", Global.Lang.Get("G_UseW")).SetValue(true));

            var laneclearMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_LaneClear"), Menu.Name + ".lane-clear"));
            ManaManager.AddToMenu(laneclearMenu, "lane-clear-q", ManaCheckType.Minimum, ManaValueType.Percent, "Q");
            ManaManager.AddToMenu(laneclearMenu, "lane-clear-w", ManaCheckType.Minimum, ManaValueType.Percent, "W");
            laneclearMenu.AddItem(
                new MenuItem(laneclearMenu.Name + ".q-min", "Q " + Global.Lang.Get("G_Min")).SetValue(
                    new Slider(3, 1, 5)));
            laneclearMenu.AddItem(
                new MenuItem(laneclearMenu.Name + ".w-min", "W " + Global.Lang.Get("G_Min")).SetValue(
                    new Slider(3, 1, 5)));
            laneclearMenu.AddItem(new MenuItem(laneclearMenu.Name + ".q", Global.Lang.Get("G_UseQ")).SetValue(true));
            laneclearMenu.AddItem(new MenuItem(laneclearMenu.Name + ".w", Global.Lang.Get("G_UseW")).SetValue(true));

            var fleeMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_Flee"), Menu.Name + ".flee"));
            fleeMenu.AddItem(new MenuItem(fleeMenu.Name + ".r", Global.Lang.Get("G_UseR")).SetValue(false));

            var shieldMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("Sivir_Shield"), Menu.Name + ".shield"));
            TargetSpellManager.AddToMenu(
                shieldMenu.AddSubMenu(new Menu(Global.Lang.Get("G_Whitelist"), shieldMenu.Name + ".whitelist")), false,
                true);
            ManaManager.AddToMenu(shieldMenu, "shield", ManaCheckType.Minimum, ManaValueType.Percent, null, 0);
            shieldMenu.AddItem(new MenuItem(shieldMenu.Name + ".enabled", Global.Lang.Get("G_Enabled")).SetValue(true));

            var miscMenu = Menu.AddSubMenu(new Menu(Global.Lang.Get("G_Miscellaneous"), Menu.Name + ".miscellaneous"));
            HeroListManager.AddToMenu(
                miscMenu.AddSubMenu(new Menu("Q " + Global.Lang.Get("G_Immobile"), miscMenu.Name + "q-immobile")),
                "q-immobile", false, false, true, false);

            IndicatorManager.AddToMenu(DrawingManager.Menu, true);
            IndicatorManager.Add(Q);
            IndicatorManager.Add(W);
            IndicatorManager.Finale();
        }

        protected override void SetupSpells()
        {
            Q = new Spell(SpellSlot.Q, 850f);
            Q.SetSkillshot(0.25f, 90f, 1350f, false, SkillshotType.SkillshotLine);

            W = new Spell(SpellSlot.W, 800f);
            E = new Spell(SpellSlot.E);
            R = new Spell(SpellSlot.R, 1100f);
        }

        private void OnEnemyTargetCast(object sender, TargetCastArgs args)
        {
            try
            {
                if (Menu.Item(Menu.Name + ".shield.enabled").GetValue<bool>() && args.Target.IsMe &&
                    ManaManager.Check("shield"))
                {
                    if (args.Type == SpellDataTargetType.SelfAoe)
                    {
                        E.Cast();
                    }
                    if (args.Type == SpellDataTargetType.Unit && args.Target != null && args.Target.IsMe)
                    {
                        var delay = (int) (Utils.SpellArrivalTime(args.Sender, Player, args.Delay, args.Speed, true)) *
                                    1000;
                        var ping = Game.Ping / 2000;
                        if (delay - 200 - ping > 0)
                        {
                            Utility.DelayAction.Add(
                                delay - 100 - ping, delegate
                                {
                                    if (E.IsReady())
                                    {
                                        E.Cast();
                                    }
                                });
                        }
                        else if (E.IsReady())
                        {
                            E.Cast();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnCorePostUpdate(EventArgs args)
        {
            try
            {
                if (Q.IsReady())
                {
                    var target =
                        GameObjects.EnemyHeroes.OrderBy(e => e.Distance(Player))
                            .Where(e => Q.IsInRange(e))
                            .FirstOrDefault(t => HeroListManager.Check("q-immobile", t) && Utils.IsImmobile(t));
                    if (target != null)
                    {
                        Q.Cast(target.Position);
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
                if (unit.IsMe && W.IsReady())
                {
                    var useW = false;
                    var wMin = 0;
                    var laneclear = false;
                    switch (Orbwalker.ActiveMode)
                    {
                        case Orbwalking.OrbwalkingMode.Combo:
                            useW = Menu.Item(Menu.Name + ".combo.w").GetValue<bool>();
                            break;
                        case Orbwalking.OrbwalkingMode.Mixed:
                            useW = Menu.Item(Menu.Name + ".harass.w").GetValue<bool>();
                            break;
                        case Orbwalking.OrbwalkingMode.LaneClear:
                            useW = Menu.Item(Menu.Name + ".lane-clear.w").GetValue<bool>();
                            wMin = Menu.Item(Menu.Name + ".lane-clear.w-min").GetValue<Slider>().Value;
                            laneclear = true;
                            break;
                    }
                    if (useW && (!laneclear || ManaManager.Check("lane-clear-w")))
                    {
                        var range = W.Range + Player.BoundingRadius * 2f;
                        var targets = laneclear
                            ? MinionManager.GetMinions(
                                range + 450, MinionTypes.All, MinionTeam.NotAlly, MinionOrderTypes.MaxHealth)
                            : GameObjects.EnemyHeroes.Where(e => e.IsValidTarget(range + 450))
                                .Cast<Obj_AI_Base>()
                                .ToList();
                        if (targets.Count >= wMin && targets.Any(Orbwalking.InAutoAttackRange) &&
                            (wMin == 0 ||
                             targets.Any(
                                 t =>
                                     Orbwalking.InAutoAttackRange(t) &&
                                     targets.Any(t2 => t2.NetworkId != t.NetworkId && t2.Distance(t) <= 450))))
                        {
                            W.Cast();
                            Orbwalking.ResetAutoAttackTimer();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        protected override void Combo()
        {
            if (Menu.Item(Menu.Name + ".combo.q").GetValue<bool>() && Q.IsReady() &&
                (!Menu.Item(Menu.Name + ".combo.w").GetValue<bool>() ||
                 (W.Level == 0 || !W.IsReady() || !GameObjects.EnemyHeroes.Any(Orbwalking.InAutoAttackRange))))
            {
                Casting.SkillShot(Q, Q.GetHitChance("combo"));
            }
        }

        protected override void Harass()
        {
            if (!ManaManager.Check("harass"))
            {
                return;
            }

            if (Menu.Item(Menu.Name + ".harass.q").GetValue<bool>() && Q.IsReady() &&
                (!Menu.Item(Menu.Name + ".harass.w").GetValue<bool>() ||
                 (W.Level == 0 || !W.IsReady() || !GameObjects.EnemyHeroes.Any(Orbwalking.InAutoAttackRange))))
            {
                Casting.SkillShot(Q, Q.GetHitChance("combo"));
            }
        }

        protected override void LaneClear()
        {
            if (!ManaManager.Check("lane-clear-q"))
            {
                return;
            }

            var useQ = Menu.Item(Menu.Name + ".lane-clear.q").GetValue<bool>() && Q.IsReady();
            var minQ = Menu.Item(Menu.Name + ".lane-clear.q-min").GetValue<Slider>().Value;

            if (useQ)
            {
                Casting.Farm(Q, minQ);
            }
        }

        protected override void Flee()
        {
            if (Menu.Item(Menu.Name + ".flee.r").GetValue<bool>() && R.IsReady())
            {
                R.Cast();
            }
        }

        protected override void Killsteal() {}
    }
}