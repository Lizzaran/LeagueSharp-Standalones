#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 Base.cs is part of SFXLastPosition.

 SFXLastPosition is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXLastPosition is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXLastPosition. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

#region

using System;
using LeagueSharp.Common;
using SFXLastPosition.Library.Extensions.NET;
using SFXLastPosition.Library.Logger;

#endregion

namespace SFXLastPosition.Classes
{
    public abstract class Base
    {
        protected Base()
        {
            Global.SFX.OnUnload += OnUnload;
        }

        public abstract bool Enabled { get; }
        public abstract string Name { get; }
        public bool Initialized { get; protected set; }
        public bool Unloaded { get; protected set; }
        public Menu Menu { get; set; }
        public event EventHandler OnInitialized;
        public event EventHandler OnEnabled;
        public event EventHandler OnDisabled;

        protected virtual void OnEnable()
        {
            try
            {
                if (Unloaded)
                {
                    return;
                }
                if (!Initialized)
                {
                    OnInitialize();
                }
                if (Initialized && !Enabled)
                {
                    OnEnabled.RaiseEvent(null, null);
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        protected virtual void OnInitialize()
        {
            try
            {
                if (!Initialized && !Unloaded)
                {
                    Initialized = true;
                    OnInitialized.RaiseEvent(this, null);
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        protected virtual void OnDisable()
        {
            try
            {
                if (Initialized && Enabled && !Unloaded)
                {
                    OnDisabled.RaiseEvent(null, null);
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        protected virtual void OnUnload(object sender, UnloadEventArgs args)
        {
            try
            {
                if (Unloaded)
                {
                    return;
                }
                OnDisable();
                if (args != null && args.Final)
                {
                    Unloaded = true;
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }
    }
}