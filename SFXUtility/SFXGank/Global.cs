#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 Global.cs is part of SFXGank.

 SFXGank is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXGank is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXGank. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SFXGank.Interfaces;
using SFXGank.Library.Logger;

#endregion

namespace SFXGank
{
    public class Global
    {
        public static ILogger Logger;
        public static string DefaultFont = "Calibri";
        public static string Name = "SFXGank";
        public static string UpdatePath = "Lizzaran/LeagueSharp-Standalones/master/SFXUtility/SFXGank";
        public static string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
        public static string LogDir = Path.Combine(BaseDir, Name + " - Logs");
        public static string CacheDir = Path.Combine(BaseDir, Name + " - Cache");
        public static SFXGank SFX = null;
        public static List<IChild> Features = new List<IChild>();

        static Global()
        {
            Logger = new FileLogger(LogDir) { LogLevel = LogLevel.High };

            try
            {
                Directory.GetFiles(LogDir)
                    .Select(f => new FileInfo(f))
                    .Where(f => f.CreationTime < DateTime.Now.AddDays(-7))
                    .ToList()
                    .ForEach(f => f.Delete());
            }
            catch (Exception ex)
            {
                Logger.AddItem(new LogItem(ex));
            }
        }
    }
}