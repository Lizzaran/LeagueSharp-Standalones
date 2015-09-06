#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 Invulnerable.cs is part of SFXCorki.

 SFXCorki is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXCorki is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXCorki. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

#region

using System;
using System.Collections.Generic;
using LeagueSharp;
using LeagueSharp.Common;
using SFXCorki.Library.Logger;
using DamageType = SFXCorki.Enumerations.DamageType;

#endregion

namespace SFXCorki.Wrappers
{
    internal static class Invulnerable
    {
        // ReSharper disable StringLiteralTypo
        private static readonly HashSet<InvulnerableStruct> Invulnerables = new HashSet<InvulnerableStruct>
        {
            new InvulnerableStruct(
                "Alistar", "FerociousHowl", null, false,
                (target, type) =>
                    ObjectManager.Player.CountEnemiesInRange(Orbwalking.GetRealAutoAttackRange(ObjectManager.Player)) >
                    1),
            new InvulnerableStruct(
                "MasterYi", "Meditate", null, false,
                (target, type) =>
                    ObjectManager.Player.CountEnemiesInRange(Orbwalking.GetRealAutoAttackRange(ObjectManager.Player)) >
                    1),
            new InvulnerableStruct("Tryndamere", "UndyingRage", null, false, (target, type) => target.HealthPercent < 5),
            new InvulnerableStruct("Kayle", "JudicatorIntervention", null, false),
            new InvulnerableStruct(null, "BlackShield", DamageType.Magical, true),
            new InvulnerableStruct(null, "BansheesVeil", DamageType.Magical, true),
            new InvulnerableStruct("Sivir", "SivirE", null, true),
            new InvulnerableStruct("Nocturne", "ShroudofDarkness", null, true)
        };

        // ReSharper restore StringLiteralTypo
        public static bool HasBuff(Obj_AI_Hero target,
            DamageType damageType = DamageType.True,
            bool ignoreShields = true)
        {
            try
            {
                if (target.HasBuffOfType(BuffType.Invulnerability))
                {
                    return true;
                }
                foreach (var invulnerable in Invulnerables)
                {
                    if (invulnerable.Champion == null || invulnerable.Champion == target.ChampionName)
                    {
                        if (invulnerable.DamageType == null || invulnerable.DamageType == damageType)
                        {
                            if (!ignoreShields && invulnerable.IsShield && target.HasBuff(invulnerable.BuffName))
                            {
                                return true;
                            }
                            if (invulnerable.CustomCheck != null && invulnerable.CustomCheck(target, damageType))
                            {
                                return true;
                            }
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return false;
        }
    }

    internal struct InvulnerableStruct
    {
        public InvulnerableStruct(string champion,
            string buffName,
            DamageType? damageType,
            bool isShield,
            Func<Obj_AI_Base, DamageType, bool> customCheck = null) : this()
        {
            Champion = champion;
            BuffName = buffName;
            DamageType = damageType;
            IsShield = isShield;
            CustomCheck = customCheck;
        }

        public string Champion { get; set; }
        public string BuffName { get; private set; }
        public DamageType? DamageType { get; private set; }
        public bool IsShield { get; private set; }
        public Func<Obj_AI_Base, DamageType, bool> CustomCheck { get; private set; }
    }
}