#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 SummonerManager.cs is part of SFXKogMaw.

 SFXKogMaw is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXKogMaw is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXKogMaw. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

#region

using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SFXKogMaw.Enumerations;
using SFXKogMaw.Library.Logger;
using SharpDX;

#endregion

namespace SFXKogMaw.Managers
{
    internal class SummonerSpell
    {
        private SpellSlot? _slot;
        public string Name { get; set; }
        public float Range { get; set; }
        public CastType CastType { get; set; }

        public SpellSlot GetSlot(Obj_AI_Hero hero)
        {
            return (SpellSlot) (_slot ?? (_slot = hero.GetSpellSlot(Name)));
        }
    }

    internal static class SummonerManager
    {
        private static Menu _menu;
        public static SummonerSpell BlueSmite;
        public static SummonerSpell RedSmite;
        public static SummonerSpell Ghost;
        public static SummonerSpell Clarity;
        public static SummonerSpell Heal;
        public static SummonerSpell Barrier;
        public static SummonerSpell Exhaust;
        public static SummonerSpell Cleanse;
        public static SummonerSpell Flash;
        public static SummonerSpell Ignite;
        public static SummonerSpell Smite;
        public static List<SummonerSpell> SummonerSpells;
        public static float MaxRange;

        static SummonerManager()
        {
            try
            {
                // ReSharper disable once StringLiteralTypo
                BlueSmite = new SummonerSpell
                {
                    Name = "s5_summonersmiteplayerganker",
                    CastType = CastType.Target,
                    Range = 750f
                };
                RedSmite = new SummonerSpell
                {
                    Name = "s5_summonersmiteSingle",
                    CastType = CastType.Target,
                    Range = 750f
                };
                Ghost = new SummonerSpell { Name = "SummonerHaste", CastType = CastType.Self, Range = float.MaxValue };
                Clarity = new SummonerSpell { Name = "SummonerMana", CastType = CastType.Self, Range = 600f };
                Heal = new SummonerSpell { Name = "SummonerHeal", CastType = CastType.Self, Range = 850f };
                Barrier = new SummonerSpell
                {
                    Name = "SummonerBarrier",
                    CastType = CastType.Self,
                    Range = float.MaxValue
                };
                Exhaust = new SummonerSpell { Name = "SummonerExhaust", CastType = CastType.Target, Range = 650f };
                Cleanse = new SummonerSpell { Name = "SummonerBoost", CastType = CastType.Self, Range = float.MaxValue };
                Flash = new SummonerSpell { Name = "SummonerFlash", CastType = CastType.Position, Range = 425f };
                Ignite = new SummonerSpell { Name = "SummonerDot", CastType = CastType.Target, Range = 600f };
                Smite = new SummonerSpell { Name = "SummonerSmite", CastType = CastType.Target, Range = 750f };

                SummonerSpells = new List<SummonerSpell>
                {
                    Ghost,
                    Clarity,
                    Heal,
                    Barrier,
                    Exhaust,
                    Cleanse,
                    Flash,
                    Ignite,
                    Smite,
                    BlueSmite,
                    RedSmite
                };

                MaxRange = SummonerSpells.Max(s => s.Range);
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        public static bool IsReady(this SummonerSpell spell, Obj_AI_Hero sender = null)
        {
            return (spell.GetSlot(sender ?? ObjectManager.Player) != SpellSlot.Unknown &&
                    spell.GetSlot(sender ?? ObjectManager.Player).IsReady());
        }

        public static List<SummonerSpell> AvailableSummoners()
        {
            return SummonerSpells.Where(ss => ss.Exists()).ToList();
        }

        public static bool Exists(this SummonerSpell spell, Obj_AI_Hero sender = null)
        {
            return spell.GetSlot(sender ?? ObjectManager.Player) != SpellSlot.Unknown;
        }

        public static SpellDataInst GetSpell(this SummonerSpell spell, Obj_AI_Hero sender = null)
        {
            var slot = spell.GetSlot((sender ?? ObjectManager.Player));
            return slot == SpellSlot.Unknown ? null : (sender ?? ObjectManager.Player).Spellbook.GetSpell(slot);
        }

        public static void Cast(this SummonerSpell spell, Obj_AI_Hero sender = null)
        {
            (sender ?? ObjectManager.Player).Spellbook.CastSpell(spell.GetSlot(sender ?? ObjectManager.Player));
        }

        // ReSharper disable once MethodOverloadWithOptionalParameter
        public static void Cast(this SummonerSpell spell, Obj_AI_Hero target, Obj_AI_Hero sender = null)
        {
            (sender ?? ObjectManager.Player).Spellbook.CastSpell(spell.GetSlot(sender ?? ObjectManager.Player), target);
        }

        public static void Cast(this SummonerSpell spell, Vector3 position, Obj_AI_Hero sender = null)
        {
            (sender ?? ObjectManager.Player).Spellbook.CastSpell(
                spell.GetSlot(sender ?? ObjectManager.Player), position);
        }

        public static float CalculateBlueSmiteDamage()
        {
            return 20 + ObjectManager.Player.Level * 8;
        }

        public static float CalculateRedSmiteDamage()
        {
            return 54 + ObjectManager.Player.Level * 6;
        }

        public static float CalculateComboDamage(Obj_AI_Hero target, bool rangeCheck = true)
        {
            if (_menu == null || target == null || !_menu.Item(_menu.Name + ".enabled").GetValue<bool>())
            {
                return 0f;
            }
            try
            {
                var ignite = _menu.Item(_menu.Name + ".ignite").GetValue<bool>() && Ignite.Exists() && Ignite.IsReady();
                var smite = _menu.Item(_menu.Name + ".smite").GetValue<bool>() &&
                            (BlueSmite.Exists() && BlueSmite.IsReady() || RedSmite.Exists() && RedSmite.IsReady());

                if (!ignite && !smite)
                {
                    return 0f;
                }
                var distance = target.Position.Distance(ObjectManager.Player.Position, true);

                var damage = 0f;
                if (ignite && (!rangeCheck || distance <= Math.Pow(Ignite.Range, 2)))
                {
                    damage += (float) ObjectManager.Player.GetSummonerSpellDamage(target, Damage.SummonerSpell.Ignite);
                }
                if (smite)
                {
                    if (!rangeCheck || distance <= Math.Pow(BlueSmite.Range, 2))
                    {
                        damage += CalculateBlueSmiteDamage();
                    }
                    if (!rangeCheck || distance <= Math.Pow(RedSmite.Range, 2))
                    {
                        damage += CalculateRedSmiteDamage();
                    }
                }
                return damage;
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return 0f;
        }

        public static bool HasSmite(Obj_AI_Hero hero)
        {
            return Smite.Exists(hero) || BlueSmite.Exists(hero) || RedSmite.Exists(hero);
        }

        public static bool IsSmiteReady(Obj_AI_Hero hero)
        {
            return Smite.IsReady(hero) || BlueSmite.IsReady(hero) || RedSmite.IsReady(hero);
        }

        public static SpellDataInst GetSmiteSpell(Obj_AI_Hero hero)
        {
            var smite = Smite.GetSpell(hero);
            if (smite != null)
            {
                return smite;
            }
            var blueSmite = BlueSmite.GetSpell(hero);
            if (blueSmite != null)
            {
                return blueSmite;
            }
            return RedSmite.GetSpell(hero);
        }

        public static void UseComboSummoners(Obj_AI_Hero target)
        {
            if (_menu == null || target == null || !_menu.Item(_menu.Name + ".enabled").GetValue<bool>())
            {
                return;
            }
            try
            {
                var ignite = _menu.Item(_menu.Name + ".ignite").GetValue<bool>() && Ignite.Exists() && Ignite.IsReady();
                var smite = _menu.Item(_menu.Name + ".smite").GetValue<bool>() &&
                            (BlueSmite.Exists() && BlueSmite.IsReady() || RedSmite.Exists() && RedSmite.IsReady());

                if (!ignite && !smite)
                {
                    return;
                }
                var distance = target.Position.Distance(ObjectManager.Player.Position, true);
                if (ignite && distance <= Math.Pow(Ignite.Range, 2))
                {
                    Ignite.Cast(target);
                }
                if (smite)
                {
                    if (distance <= Math.Pow(BlueSmite.Range, 2))
                    {
                        BlueSmite.Cast(target);
                    }
                    else if (distance <= Math.Pow(RedSmite.Range, 2))
                    {
                        RedSmite.Cast(target);
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        public static void AddToMenu(Menu menu)
        {
            try
            {
                _menu = menu;
                menu.AddItem(new MenuItem(_menu.Name + ".ignite", "Use Ignite").SetValue(true));
                menu.AddItem(new MenuItem(_menu.Name + ".smite", "Use Smite").SetValue(true));
                menu.AddItem(new MenuItem(menu.Name + ".enabled", "Enabled").SetValue(false));
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }
    }
}