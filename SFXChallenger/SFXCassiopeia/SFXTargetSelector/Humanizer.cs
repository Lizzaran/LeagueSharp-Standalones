#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 Humanizer.cs is part of SFXCassiopeia.

 SFXCassiopeia is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXCassiopeia is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXCassiopeia. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

#region

using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SFXCassiopeia.Library.Logger;
using SharpDX;

#endregion

namespace SFXCassiopeia.SFXTargetSelector
{
    public class Humanizer
    {
        private static Menu _mainMenu;
        private static float _lastRange;
        private static float _lastRangeChange;

        internal static void AddToMenu(Menu mainMenu)
        {
            try
            {
                _mainMenu = mainMenu;

                _mainMenu.AddItem(
                    new MenuItem(_mainMenu.Name + ".fow", "Target Acquire Delay").SetShared()
                        .SetValue(new Slider(350, 0, 1500)));
                _mainMenu.AddItem(
                    new MenuItem(_mainMenu.Name + ".range", "Range Change Delay").SetShared()
                        .SetValue(new Slider(350, 0, 1500)));
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        public static IEnumerable<Targets.Item> FilterTargets(IEnumerable<Targets.Item> targets,
            Vector3 from,
            float range)
        {
            var finalTargets = targets.ToList();
            try
            {
                var rangeDelay = _mainMenu.Item(_mainMenu.Name + ".range").GetValue<Slider>().Value;
                var fowDelay = _mainMenu.Item(_mainMenu.Name + ".fow").GetValue<Slider>().Value;
                if (rangeDelay > 0 && range > 0)
                {
                    if (_lastRange > 0 && Game.Time - _lastRangeChange <= rangeDelay / 1000f)
                    {
                        finalTargets =
                            finalTargets.Where(
                                t =>
                                    t.Hero.Distance(
                                        from.Equals(default(Vector3)) ? ObjectManager.Player.ServerPosition : from) <=
                                    _lastRange).ToList();
                    }
                    else if (Math.Abs(_lastRange - range) > 1)
                    {
                        _lastRange = range;
                        _lastRangeChange = Game.Time;
                    }
                }
                if (fowDelay > 0)
                {
                    finalTargets =
                        finalTargets.Where(item => Game.Time - item.LastVisibleChange > fowDelay / 1000f).ToList();
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return finalTargets;
        }
    }
}