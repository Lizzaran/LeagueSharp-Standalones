#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 TargetSelector.cs is part of SFXOrianna.

 SFXOrianna is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXOrianna is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXOrianna. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

#region

using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SFXOrianna.Enumerations;
using SFXOrianna.Library.Logger;
using SFXOrianna.Wrappers;
using SharpDX;
using DamageType = SFXOrianna.Enumerations.DamageType;
using Orbwalking = LeagueSharp.Common.Orbwalking;
using Spell = SFXOrianna.Wrappers.Spell;

#endregion

/*
 * Don't copy paste this without asking & giving credits fuckers :^) 
 */

namespace SFXOrianna.SFXTargetSelector
{
    public static class TargetSelector
    {
        private static Menu _menu;
        private static Targets.Item _lastTarget;

        static TargetSelector()
        {
            Mode = TargetSelectorModeType.Weights;
        }

        public static TargetSelectorModeType Mode { get; set; }

        public static Obj_AI_Hero LastTarget
        {
            get { return _lastTarget != null ? _lastTarget.Hero : null; }
        }

        internal static bool IsValidTarget(Obj_AI_Hero target,
            float range,
            DamageType damageType,
            bool ignoreShields = true,
            Vector3 from = default(Vector3))
        {
            try
            {
                return target.IsValidTarget() &&
                       target.Distance(
                           (from.Equals(default(Vector3)) ? ObjectManager.Player.ServerPosition : from), true) <
                       Math.Pow((range <= 0 ? Orbwalking.GetRealAutoAttackRange(target) : range), 2) &&
                       !Invulnerable.HasBuff(target, damageType, ignoreShields);
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return false;
        }

        private static IEnumerable<Targets.Item> GetOrderedChampions(List<Targets.Item> items)
        {
            try
            {
                switch (Mode)
                {
                    case TargetSelectorModeType.Weights:
                        return Weights.OrderChampions(items);

                    case TargetSelectorModeType.Priorities:
                        return Priorities.OrderChampions(items);

                    case TargetSelectorModeType.LessAttacksToKill:
                        return items.OrderBy(x => x.Hero.Health / ObjectManager.Player.TotalAttackDamage);

                    case TargetSelectorModeType.MostAbilityPower:
                        return items.OrderByDescending(x => x.Hero.TotalMagicalDamage);

                    case TargetSelectorModeType.MostAttackDamage:
                        return items.OrderByDescending(x => x.Hero.TotalAttackDamage);

                    case TargetSelectorModeType.Closest:
                        return items.OrderBy(x => x.Hero.Distance(ObjectManager.Player));

                    case TargetSelectorModeType.NearMouse:
                        return items.OrderBy(x => x.Hero.Distance(Game.CursorPos));

                    case TargetSelectorModeType.LessCastPriority:
                        return items.OrderBy(x => x.Hero.Health / ObjectManager.Player.TotalMagicalDamage);

                    case TargetSelectorModeType.LeastHealth:
                        return items.OrderBy(x => x.Hero.Health);
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return new List<Targets.Item>();
        }

        private static TargetSelectorModeType GetModeByMenuValue(string value)
        {
            var mode = TargetSelectorModeType.Weights;
            try
            {
                if (value.Equals(Global.Lang.Get("TS_Weights")))
                {
                    mode = TargetSelectorModeType.Weights;
                }
                else if (value.Equals(Global.Lang.Get("TS_Priorities")))
                {
                    mode = TargetSelectorModeType.Priorities;
                }
                else if (value.Equals(Global.Lang.Get("TS_LessAttacksToKill")))
                {
                    mode = TargetSelectorModeType.LessAttacksToKill;
                }
                else if (value.Equals(Global.Lang.Get("TS_MostAbilityPower")))
                {
                    mode = TargetSelectorModeType.MostAbilityPower;
                }
                else if (value.Equals(Global.Lang.Get("TS_MostAttackDamage")))
                {
                    mode = TargetSelectorModeType.MostAttackDamage;
                }
                else if (value.Equals(Global.Lang.Get("TS_Closest")))
                {
                    mode = TargetSelectorModeType.Closest;
                }
                else if (value.Equals(Global.Lang.Get("TS_NearMouse")))
                {
                    mode = TargetSelectorModeType.NearMouse;
                }
                else if (value.Equals(Global.Lang.Get("TS_LessCastPriority")))
                {
                    mode = TargetSelectorModeType.LessCastPriority;
                }
                else if (value.Equals(Global.Lang.Get("TS_LeastHealth")))
                {
                    mode = TargetSelectorModeType.LeastHealth;
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return mode;
        }

        public static Obj_AI_Hero GetTarget(this Spell spell,
            bool ignoreShields = true,
            Vector3 from = new Vector3(),
            IEnumerable<Obj_AI_Hero> ignoredChampions = null)
        {
            return
                GetTarget(
                    (spell.Range + spell.Width +
                     Targets.Items.Select(e => e.Hero.BoundingRadius).DefaultIfEmpty(50).Max()) * 1.1f, spell.DamageType,
                    ignoreShields, from, ignoredChampions);
        }

        public static Obj_AI_Hero GetTarget(float range,
            DamageType damageType = DamageType.True,
            bool ignoreShields = true,
            Vector3 from = default(Vector3),
            IEnumerable<Obj_AI_Hero> ignoredChampions = null)
        {
            try
            {
                var targets = GetTargets(range, damageType, ignoreShields, from, ignoredChampions);
                return targets != null ? targets.FirstOrDefault() : null;
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return null;
        }

        public static IEnumerable<Obj_AI_Hero> GetTargets(float range,
            DamageType damageType = DamageType.True,
            bool ignoreShields = true,
            Vector3 from = default(Vector3),
            IEnumerable<Obj_AI_Hero> ignoredChampions = null)
        {
            try
            {
                Weights.Range = Math.Max(range, Weights.Range);

                var selectedTarget = Selected.GetTarget(range, damageType, ignoreShields, from);
                if (selectedTarget != null)
                {
                    return new List<Obj_AI_Hero> { selectedTarget };
                }

                var targets =
                    Humanizer.FilterTargets(Targets.Items)
                        .Where(
                            h => ignoredChampions == null || ignoredChampions.All(i => i.NetworkId != h.Hero.NetworkId))
                        .Where(h => IsValidTarget(h.Hero, range, damageType, ignoreShields, from))
                        .ToList();

                if (targets.Count > 0)
                {
                    var t = GetOrderedChampions(targets).ToList();
                    if (Mode == TargetSelectorModeType.Weights && Weights.ForceFocus)
                    {
                        t = Weights.FilterTargets(t, range, damageType, ignoreShields, from).ToList();
                    }
                    if (t.Count > 0)
                    {
                        if (Selected.Target != null && Selected.Focus && t.Count > 1)
                        {
                            t = t.OrderByDescending(x => x.Hero.NetworkId.Equals(Selected.Target.NetworkId)).ToList();
                        }
                        _lastTarget = t.First();
                        _lastTarget.LastTargetSwitch = Game.Time;
                        return t.Select(h => h.Hero).ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return new List<Obj_AI_Hero>();
        }

        public static void AddToMenu(Menu menu)
        {
            try
            {
                _menu = menu;

                var drawingMenu = _menu.AddSubMenu(new Menu(Global.Lang.Get("G_Drawing"), menu.Name + ".drawing"));

                drawingMenu.AddItem(
                    new MenuItem(drawingMenu.Name + ".circle-thickness", Global.Lang.Get("G_CircleThickness")).SetValue(
                        new Slider(2, 1, 10)));

                Selected.AddToMenu(_menu, drawingMenu);
                Weights.AddToMenu(_menu, drawingMenu);
                Priorities.AddToMenu(_menu);
                Humanizer.AddToMenu(_menu);

                _menu.AddItem(
                    new MenuItem(menu.Name + ".mode", Global.Lang.Get("TS_Mode")).SetValue(
                        new StringList(
                            new[]
                            {
                                Global.Lang.Get("TS_Weights"), Global.Lang.Get("TS_Priorities"),
                                Global.Lang.Get("TS_LessAttacksToKill"), Global.Lang.Get("TS_MostAbilityPower"),
                                Global.Lang.Get("TS_MostAttackDamage"), Global.Lang.Get("TS_Closest"),
                                Global.Lang.Get("TS_NearMouse"), Global.Lang.Get("TS_LessCastPriority"),
                                Global.Lang.Get("TS_LeastHealth")
                            }))).ValueChanged +=
                    delegate(object sender, OnValueChangeEventArgs args)
                    {
                        Mode = GetModeByMenuValue(args.GetNewValue<StringList>().SelectedValue);
                    };

                Mode = GetModeByMenuValue(_menu.Item(menu.Name + ".mode").GetValue<StringList>().SelectedValue);
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }
    }
}