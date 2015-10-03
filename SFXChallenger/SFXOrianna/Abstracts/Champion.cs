#region License

/*
 Copyright 2014 - 2015 Nikita Bernthaler
 Champion.cs is part of SFXOrianna.

 SFXOrianna is free software: you can redistribute it and/or modify
 it under the terms of the GNU General Public License as published by
 the Free Software Foundation, either version 3 of the License, or
 (at your option) any later version.

 SFXOrianna is distributed in the hope that it will be useful,
 but WITHOUT ANY WARRANTY; without even the implied warranty of
 MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 GNU General Public License for more details.

 You should have received a copy of the GNU General Public License
 along with SFXOrianna. If not, see <http://www.gnu.org/licenses/>.
*/

#endregion License

#region

using System;
using System.Collections.Generic;
using System.Linq;
using LeagueSharp;
using LeagueSharp.Common;
using SFXOrianna.Enumerations;
using SFXOrianna.Interfaces;
using SFXOrianna.Library.Logger;
using SFXOrianna.Managers;
using SFXOrianna.Menus;
using SFXOrianna.SFXTargetSelector;
using Orbwalking = SFXOrianna.Wrappers.Orbwalking;
using Spell = SFXOrianna.Wrappers.Spell;
using TargetSelector = SFXOrianna.SFXTargetSelector.TargetSelector;

#endregion

namespace SFXOrianna.Abstracts
{
    internal abstract class Champion : IChampion
    {
        protected readonly Obj_AI_Hero Player = ObjectManager.Player;
        private List<Spell> _spells;
        private bool _useMuramana;
        protected Spell E;
        protected Spell Q;
        protected Spell R;
        protected Spell W;

        protected Champion()
        {
            Core.OnBoot += OnCoreBoot;
            Core.OnShutdown += OnCoreShutdown;
        }

        protected abstract ItemFlags ItemFlags { get; }
        protected abstract ItemUsageType ItemUsage { get; }
        public Menu SFXMenu { get; private set; }

        public List<Spell> Spells
        {
            get { return _spells ?? (_spells = new List<Spell> { Q, W, E, R }); }
        }

        public Menu Menu { get; private set; }
        public Orbwalking.Orbwalker Orbwalker { get; private set; }

        void IChampion.Combo()
        {
            try
            {
                Combo();
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        void IChampion.Harass()
        {
            try
            {
                Harass();
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        void IChampion.LaneClear()
        {
            try
            {
                LaneClear();
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        void IChampion.Flee()
        {
            try
            {
                Orbwalker.SetAttack(false);
                Orbwalking.MoveTo(Game.CursorPos, Orbwalker.HoldAreaRadius);
                Flee();
                Utility.DelayAction.Add(
                    750, delegate
                    {
                        if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Flee)
                        {
                            ItemManager.UseFleeItems();
                        }
                    });
                Utility.DelayAction.Add(
                    125, delegate
                    {
                        if (Orbwalker.ActiveMode != Orbwalking.OrbwalkingMode.Flee)
                        {
                            Orbwalker.SetAttack(true);
                        }
                    });
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        void IChampion.Killsteal()
        {
            try
            {
                Killsteal();
                KillstealManager.Killsteal();
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        protected virtual void OnCorePreUpdate(EventArgs args)
        {
            try
            {
                if (!_useMuramana)
                {
                    ItemManager.Muramana(null, false);
                }
                if (_useMuramana && Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo)
                {
                    ItemManager.Muramana(null, true, float.MaxValue);
                    Utility.DelayAction.Add(1000 + Game.Ping, () => _useMuramana = false);
                }
                OnPreUpdate();
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        protected virtual void OnCorePostUpdate(EventArgs args)
        {
            try
            {
                OnPostUpdate();
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        protected abstract void OnLoad();
        protected abstract void SetupSpells();
        protected abstract void AddToMenu();
        protected abstract void OnPreUpdate();
        protected abstract void OnPostUpdate();
        protected abstract void Combo();
        protected abstract void Harass();
        protected abstract void LaneClear();
        protected abstract void Flee();
        protected abstract void Killsteal();

        private void OnCoreBoot(EventArgs args)
        {
            try
            {
                OnLoad();
                SetupSpells();
                SetupMenu();

                Weights.Range = Spells.Select(e => e.Range).DefaultIfEmpty(800f).Max();

                if (ItemUsage == ItemUsageType.AfterAttack)
                {
                    Orbwalking.AfterAttack += OnOrbwalkingAfterAttack;
                }

                Core.OnPreUpdate += OnCorePreUpdate;
                Core.OnPostUpdate += OnCorePostUpdate;
                Obj_AI_Base.OnProcessSpellCast += OnObjAiBaseProcessSpellCast;
                Drawing.OnDraw += OnDrawingDraw;
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnCoreShutdown(EventArgs args)
        {
            try
            {
                Core.OnPreUpdate -= OnCorePreUpdate;
                Core.OnPostUpdate -= OnCorePostUpdate;
                Drawing.OnDraw -= OnDrawingDraw;
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnDrawingDraw(EventArgs args)
        {
            try
            {
                DrawingManager.Draw();
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnOrbwalkingAfterAttack(AttackableUnit unit, AttackableUnit target)
        {
            try
            {
                if (unit.IsMe)
                {
                    Orbwalker.ForceTarget(null);
                    if (Orbwalker.ActiveMode == Orbwalking.OrbwalkingMode.Combo)
                    {
                        var enemy = target as Obj_AI_Hero;
                        if (enemy != null)
                        {
                            ItemManager.Muramana(enemy, true);
                            ItemManager.UseComboItems(enemy);
                            SummonerManager.UseComboSummoners(enemy);
                        }
                    }
                    else
                    {
                        ItemManager.Muramana(null, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void OnObjAiBaseProcessSpellCast(Obj_AI_Base sender, GameObjectProcessSpellCastEventArgs args)
        {
            try
            {
                if (sender.IsMe && !args.SData.IsAutoAttack())
                {
                    var slot = Player.GetSpellSlot(args.SData.Name);
                    if (args.Target is Obj_AI_Hero || slot == SpellSlot.Q || slot == SpellSlot.W || slot == SpellSlot.E ||
                        slot == SpellSlot.R)
                    {
                        _useMuramana = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }

        private void SetupMenu()
        {
            try
            {
                SFXMenu = new Menu(Global.Name, "sfx", true);

                Menu = new Menu(Global.Prefix + Player.ChampionName, SFXMenu.Name + "." + Player.ChampionName, true);

                DrawingManager.AddToMenu(Menu.AddSubMenu(new Menu("Drawings", Menu.Name + ".drawing")), this);

                TargetSelector.AddToMenu(SFXMenu.AddSubMenu(new Menu("Target Selector", SFXMenu.Name + ".ts")));

                Orbwalker = new Orbwalking.Orbwalker(SFXMenu.AddSubMenu(new Menu("Orbwalker", SFXMenu.Name + ".orb")));
                KillstealManager.AddToMenu(SFXMenu.AddSubMenu(new Menu("Killsteal", SFXMenu.Name + ".killsteal")));
                ItemManager.AddToMenu(SFXMenu.AddSubMenu(new Menu("Items", SFXMenu.Name + ".items")), ItemFlags);
                SummonerManager.AddToMenu(SFXMenu.AddSubMenu(new Menu("Summoners", SFXMenu.Name + ".summoners")));

                InfoMenu.AddToMenu(SFXMenu.AddSubMenu(new Menu("Info", SFXMenu.Name + ".info")));

                DebugMenu.AddToMenu(SFXMenu, Spells);

                Menu.AddToMainMenu();
                SFXMenu.AddToMainMenu();

                try
                {
                    AddToMenu();
                }
                catch (Exception ex)
                {
                    Global.Logger.AddItem(new LogItem(ex));
                }
            }
            catch (Exception ex)
            {
                Global.Logger.AddItem(new LogItem(ex));
            }
        }
    }
}