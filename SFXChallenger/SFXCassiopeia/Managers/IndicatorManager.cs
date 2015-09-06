#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 IndicatorManager.cs is part of SFXCassiopeia.

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
using SFXCassiopeia.Library;
using SFXCassiopeia.Library.Logger;
using SharpDX;
using SharpDX.Direct3D9;
using Color = System.Drawing.Color;
using Spell = SFXCassiopeia.Wrappers.Spell;

#endregion

namespace SFXCassiopeia.Managers
{
    public class IndicatorManager
    {
        private const int BarWidth = 104;
        private const int LineThickness = 9;
        private static readonly Vector2 BarOffset = new Vector2(10f, 29f);
        private static Menu _menu;

        private static readonly Dictionary<string, Func<Obj_AI_Hero, float>> Functions =
            new Dictionary<string, Func<Obj_AI_Hero, float>>();

        private static Line _line;

        public static void AddToMenu(Menu menu, bool subMenu)
        {
            try
            {
                _menu = subMenu ? menu.AddSubMenu(new Menu(Global.Lang.Get("F_IDM"), menu.Name + ".indicator")) : menu;

                var drawingMenu =
                    _menu.AddSubMenu(
                        new Menu(
                            Global.Lang.Get("G_Drawing"),
                            _menu.Name.Replace(ObjectManager.Player.ChampionName, string.Empty) + ".drawing"));
                drawingMenu.AddItem(
                    new MenuItem(drawingMenu.Name + ".color", Global.Lang.Get("G_Color")).SetValue(Color.Orange));
                drawingMenu.AddItem(
                    new MenuItem(drawingMenu.Name + ".opacity", Global.Lang.Get("G_Opacity")).SetValue(
                        new Slider(60, 5)));

                _menu.AddItem(
                    new MenuItem(_menu.Name + ".attacks", Global.Lang.Get("G_UseAutoAttacks")).SetValue(
                        new Slider(2, 0, 10)));

                Add(Global.Lang.Get("IDM_Items"), hero => ItemManager.CalculateComboDamage(hero, false));
                Add(Global.Lang.Get("IDM_Summoners"), hero => SummonerManager.CalculateComboDamage(hero, false));
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        public static void Finale()
        {
            try
            {
                if (_menu != null)
                {
                    _menu.AddItem(new MenuItem(_menu.Name + ".enabled", Global.Lang.Get("G_Enabled")).SetValue(true));

                    _line = new Line(Drawing.Direct3DDevice) { Width = LineThickness };

                    Drawing.OnPreReset += OnDrawingPreReset;
                    Drawing.OnPostReset += OnDrawingPostReset;
                    Drawing.OnEndScene += DrawingOnEndScene;
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private static void OnDrawingPreReset(EventArgs args)
        {
            try
            {
                if (_line != null && !_line.IsDisposed)
                {
                    _line.OnLostDevice();
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private static void OnDrawingPostReset(EventArgs args)
        {
            try
            {
                if (_line != null && !_line.IsDisposed)
                {
                    _line.OnResetDevice();
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private static void DrawingOnEndScene(EventArgs args)
        {
            try
            {
                if (Drawing.Direct3DDevice == null || Drawing.Direct3DDevice.IsDisposed)
                {
                    return;
                }

                if (_line != null && !_line.IsDisposed)
                {
                    if (_menu == null || !_menu.Item(_menu.Name + ".enabled").GetValue<bool>())
                    {
                        return;
                    }
                    var drawingName = _menu.Name.Replace(ObjectManager.Player.ChampionName, string.Empty);
                    var color = _menu.Item(drawingName + ".drawing.color").GetValue<Color>();
                    var alpha =
                        (byte) (_menu.Item(drawingName + ".drawing.opacity").GetValue<Slider>().Value * 255 / 100);
                    var sharpColor = new ColorBGRA(color.R, color.G, color.B, alpha);
                    foreach (var unit in
                        GameObjects.EnemyHeroes.Where(
                            u => u.IsHPBarRendered && u.Position.IsOnScreen() && u.IsValidTarget()))
                    {
                        var damage = CalculateDamage(unit);
                        if (damage <= 0)
                        {
                            continue;
                        }
                        var damagePercentage = ((unit.Health - damage) > 0 ? (unit.Health - damage) : 0) /
                                               unit.MaxHealth;
                        var currentHealthPercentage = unit.Health / unit.MaxHealth;
                        var startPoint =
                            new Vector2(
                                (int) (unit.HPBarPosition.X + BarOffset.X + damagePercentage * BarWidth),
                                (int) (unit.HPBarPosition.Y + BarOffset.Y) - 5);
                        var endPoint =
                            new Vector2(
                                (int) (unit.HPBarPosition.X + BarOffset.X + currentHealthPercentage * BarWidth) + 1,
                                (int) (unit.HPBarPosition.Y + BarOffset.Y) - 5);
                        _line.Begin();
                        _line.Draw(new[] { startPoint, endPoint }, sharpColor);
                        _line.End();
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        public static void Add(string name, Func<Obj_AI_Hero, float> calcDamage)
        {
            try
            {
                if (_menu == null)
                {
                    return;
                }
                _menu.AddItem(new MenuItem(_menu.Name + "." + name, name).SetValue(false));
                Functions.Add(name, calcDamage);
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        public static void Add(Spell spell, bool readyCheck = true)
        {
            try
            {
                if (_menu == null)
                {
                    return;
                }
                _menu.AddItem(
                    new MenuItem(_menu.Name + "." + spell.Slot, spell.Slot.ToString().ToUpper()).SetValue(false));
                if (readyCheck)
                {
                    Functions.Add(spell.Slot.ToString(), hero => spell.IsReady() ? spell.GetDamage(hero) : 0);
                }
                else
                {
                    Functions.Add(spell.Slot.ToString(), hero => spell.GetDamage(hero));
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private static float CalculateDamage(Obj_AI_Hero target)
        {
            var damage = 0f;
            try
            {
                var aa = _menu.Item(_menu.Name + ".attacks").GetValue<Slider>().Value;
                if (aa > 0)
                {
                    damage += (float) (ObjectManager.Player.GetAutoAttackDamage(target) * aa);
                }
                damage +=
                    Functions.Where(function => _menu.Item(_menu.Name + "." + function.Key).GetValue<bool>())
                        .Sum(function => function.Value(target));
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
            return damage;
        }
    }
}