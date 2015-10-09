#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 Extensions.cs is part of SFXKalista.

 SFXKalista is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXKalista is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXKalista. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

#region

using System.Collections.Generic;
using LeagueSharp;
using SFXKalista.Wrappers;
using SharpDX;

#endregion

namespace SFXKalista.SFXTargetSelector
{
    public static class Extensions
    {
        public static Obj_AI_Hero GetTargetNoCollision(this Spell spell,
            bool ignoreShields = true,
            Vector3 from = default(Vector3),
            IEnumerable<Obj_AI_Hero> ignoredChampions = null)
        {
            return TargetSelector.GetTargetNoCollision(spell, ignoreShields, from, ignoredChampions);
        }

        public static Obj_AI_Hero GetTarget(this Spell spell,
            bool ignoreShields = true,
            Vector3 from = default(Vector3),
            IEnumerable<Obj_AI_Hero> ignoredChampions = null)
        {
            return TargetSelector.GetTarget(spell, ignoreShields, from, ignoredChampions);
        }
    }
}