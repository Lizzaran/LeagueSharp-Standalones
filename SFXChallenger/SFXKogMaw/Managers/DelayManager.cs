#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 delaymanager.cs is part of SFXKogMaw.

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
using LeagueSharp.Common;
using SFXKogMaw.Library.Logger;

#endregion

namespace SFXKogMaw.Managers
{
    internal static class DelayManager
    {
        private static readonly Dictionary<string, Menu> Menues = new Dictionary<string, Menu>();

        public static void AddToMenu(Menu menu, string uniqueId, string prefix, int value, int min, int max)
        {
            try
            {
                if (Menues.ContainsKey(uniqueId))
                {
                    throw new ArgumentException(
                        string.Format("DelayManager: UniqueID \"{0}\" already exist.", uniqueId));
                }

                menu.AddItem(
                    new MenuItem(menu.Name + ".delay-manager." + uniqueId, prefix + " " + Global.Lang.Get("G_Delay"))
                        .SetValue(new Slider(value, min, max)));

                Menues[uniqueId] = menu;
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        public static int Get(string uniqueId)
        {
            try
            {
                Menu menu;
                if (Menues.TryGetValue(uniqueId, out menu))
                {
                    var value = menu.Item(menu.Name + ".delay-manager." + uniqueId).GetValue<Slider>().Value;
                    return value < 10 ? value : new Random().Next((int) (value * 0.8f), (int) (value * 1.2f));
                }
                throw new KeyNotFoundException(string.Format("DelayManager: UniqueID \"{0}\" not found.", uniqueId));
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return 0;
        }

        public static bool Check(string uniqueId, int lastCast)
        {
            try
            {
                Menu menu;
                if (Menues.TryGetValue(uniqueId, out menu))
                {
                    var value = menu.Item(menu.Name + ".delay-manager." + uniqueId).GetValue<Slider>().Value;
                    return value < 10 ||
                           Environment.TickCount >=
                           lastCast + (new Random().Next((int) (value * 0.8f), (int) (value * 1.2f)));
                }
                throw new KeyNotFoundException(string.Format("DelayManager: UniqueID \"{0}\" not found.", uniqueId));
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return false;
        }
    }
}