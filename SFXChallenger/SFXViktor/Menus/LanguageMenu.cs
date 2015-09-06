#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 LanguageMenu.cs is part of SFXViktor.

 SFXViktor is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXViktor is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXViktor. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

#region

using System;
using System.IO;
using System.Linq;
using LeagueSharp.Common;
using SFXViktor.Library.Logger;

#endregion

namespace SFXViktor.Menus
{
    internal class LanguageMenu
    {
        public static void AddToMenu(Menu menu)
        {
            try
            {
                menu.AddItem(
                    new MenuItem(menu.Name + ".language", Global.Lang.Get("F_Language")).SetValue(
                        new StringList(
                            new[] { Global.Lang.Get("Language_Auto") }.Concat(Global.Lang.Languages.ToArray()).ToArray())))
                    .ValueChanged += delegate(object sender, OnValueChangeEventArgs args)
                    {
                        try
                        {
                            var preName = string.Format("{0}.language.", Global.Name.ToLower());
                            var autoName = Global.Lang.Get("Language_Auto");
                            var files = Directory.GetFiles(
                                AppDomain.CurrentDomain.BaseDirectory, preName + "*", SearchOption.TopDirectoryOnly);
                            var selectedLanguage = args.GetNewValue<StringList>().SelectedValue;
                            foreach (var file in files)
                            {
                                try
                                {
                                    File.Delete(file);
                                }
                                catch
                                {
                                    // ignored
                                }
                            }
                            if (!selectedLanguage.Equals(autoName, StringComparison.OrdinalIgnoreCase))
                            {
                                File.Create(
                                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, preName + selectedLanguage));
                            }
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
            try
            {
                var file =
                    Directory.GetFiles(
                        AppDomain.CurrentDomain.BaseDirectory,
                        string.Format("{0}.language.", Global.Name.ToLower()) + "*", SearchOption.TopDirectoryOnly)
                        .FirstOrDefault();
                if (!string.IsNullOrEmpty(file))
                {
                    string ext = null;
                    var splitted = file.Split('.');
                    if (splitted.Any())
                    {
                        ext = splitted.Last();
                    }
                    if (!string.IsNullOrEmpty(ext))
                    {
                        menu.Item(menu.Name + ".language")
                            .SetValue(
                                new StringList(
                                    new[] { ext }.Concat(
                                        menu.Item(menu.Name + ".language")
                                            .GetValue<StringList>()
                                            .SList.Where(val => !val.Equals(ext, StringComparison.OrdinalIgnoreCase))
                                            .ToArray()).ToArray()));
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }
    }
}