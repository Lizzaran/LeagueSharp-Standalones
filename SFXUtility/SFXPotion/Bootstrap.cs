#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 Bootstrap.cs is part of SFXPotion.

 SFXPotion is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXPotion is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXPotion. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

#region

using System;
using System.Collections.Generic;
using System.Reflection;
using LeagueSharp.Common;
using SFXPotion.Interfaces;
using SFXPotion.Library;
using SFXPotion.Library.Logger;

using SFXPotion.Features.Activators;

#endregion

namespace SFXPotion
{
    public class Bootstrap
    {
        public static void Init()
        {
            try
            {
                AppDomain.CurrentDomain.UnhandledException +=
                    delegate(object sender, UnhandledExceptionEventArgs eventArgs)
                    {
                        try
                        {
                            var ex = sender as Exception;
                            if (ex != null)
                            {
                                Global.Logger.AddItem(new LogItem(ex));
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex);
                        }
                    };

                #region GameObjects

                GameObjects.Initialize();

                #endregion GameObjects

                Global.SFX = new SFXPotion();

                var app = new App();

                CustomEvents.Game.OnGameLoad += delegate
                {
                    Global.Features.AddRange(
                        new List<IChild>
                        {
                            new Potion(app)
                        });
                    foreach (var feature in Global.Features)
                    {
                        try
                        {
                            feature.HandleEvents();
                        }
                        catch (Exception ex)
                        {
                            Global.Logger.AddItem(new LogItem(ex));
                        }
                    }
                    try
                    {
                        Update.Check(
                            Global.Name, Assembly.GetExecutingAssembly().GetName().Version, Global.UpdatePath, 10000);
                    }
                    catch (Exception ex)
                    {
                        Global.Logger.AddItem(new LogItem(ex));
                    }
                };
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }
    }
}