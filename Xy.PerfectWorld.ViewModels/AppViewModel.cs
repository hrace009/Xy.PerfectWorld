﻿using ReactiveUI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Xy.PerfectWorld.Models;

namespace Xy.PerfectWorld.ViewModels
{
    public class AppViewModel : ReactiveObject
    {
        public static AppViewModel Instance = new AppViewModel();
        public SettingViewModel SettingVM = new SettingViewModel();

        #region AttachedGame
        readonly ObservableAsPropertyHelper<GameModel> attachedGame;
        public GameModel AttachedGame
        {
            get { return attachedGame.Value; }
        }
        #endregion
        #region AutoCombatEnabled
        bool autoCombatEnabled = false;
        public bool AutoCombatEnabled
        {
            get { return autoCombatEnabled; }
            set { this.RaiseAndSetIfChanged(ref autoCombatEnabled, value); }
        }
        #endregion
        #region AutoLootEnabled
        bool autoLootEnabled = false;
        public bool AutoLootEnabled
        {
            get { return autoLootEnabled; }
            set { this.RaiseAndSetIfChanged(ref autoLootEnabled, value); }
        }
        #endregion
        #region TargetInfo
        string targetInfo;
        public string TargetInfo
        {
            get { return targetInfo; }
            set { this.RaiseAndSetIfChanged(ref targetInfo, value); }
        }
        #endregion


        private bool lootedInLastLoop;
        private Npc currentTarget;

        private AppViewModel()
        {
            attachedGame = SettingVM.WhenAny(x => x.SelectedGame, x => x.Value)
                .ToProperty(this, x => x.AttachedGame);

            InitializeAutoCombat();
            InitializeAutoLoot();
            InitializeStatusBar();
        }

        private void InitializeAutoCombat()
        {
            Observable.Interval(TimeSpan.FromMilliseconds(333))
                .Where(_ => (AttachedGame?.Status ?? GameStatus.Offline) == GameStatus.LoggedIn && AutoCombatEnabled)
                .Where(_ => !lootedInLastLoop)
                .Subscribe(_ => AutoCombatPerform());
        }
        private void AutoCombatPerform()
        {
            try
            {
                var character = new Character(AttachedGame.Game);

                // reacquire closest target, before attacking
                var npc = new NpcContainer(AttachedGame.Game).GetItems()
                    .Where(x => x.NpcType.Value == NpcType.Monster)
                    .Where(x => x.Level.Value > 100)
                    .OrderBy(x => x.RelativeDistance.Value)
                    .FirstOrDefault();
                if (npc.UniqueID != character.SelectedTargetID)
                    npc.Target();

                character.Attack();
            }
            catch (Exception e)
            {
                Debug.WriteLine($"An exception occured in {nameof(AutoLootPerform)} : {e}");
            }
        }

        private void InitializeAutoLoot()
        {
            Observable.Interval(TimeSpan.FromMilliseconds(250))
                .Where(_ => (AttachedGame?.Status ?? GameStatus.Offline) == GameStatus.LoggedIn && AutoLootEnabled)
                .Subscribe(_ => AutoLootPerform());
        }
        private void AutoLootPerform()
        {
            try
            {
                const float MaxLootRange = 10.0f;
                var canLootGold = new Character(AttachedGame.Game).Gold != 200000000;
                var ground = new GroundContainer(AttachedGame.Game);

                var item = ground.GetItems()
                    .Where(x => ShouldLoot(x, canLootGold))
                    .Where(x => x.RelativeDistance <= MaxLootRange)
                    .OrderBy(x => x.RelativeDistance)
                    .FirstOrDefault();

                if (item != null)
                {
                    item.Loot();
                }

                lootedInLastLoop = item != null;
            }
            catch (Exception e)
            {
                Debug.WriteLine($"An exception occured in {nameof(AutoLootPerform)} : {e}");
            }
        }
        private bool ShouldLoot(GroundItem item, bool canLootGold = true)
        {
            switch (item.CollectMethod.Value)
            {
                case CollectMethod.Gold: return canLootGold;
                case CollectMethod.Resource: return false;
            }

            switch (item.ItemID.Value)
            {
                case 0x5DC0: // LM$ Silver

                case 0x527A: // Martial God·Ksitigarbha Stele
                case 0x527B: // Martial God·Ksitigarbha Stone
                case 0x527C: // Martial God·Steel Stele
                case 0x527D: // Martial God·Steel Stone
                case 0x527E: // Martial God·Virtuous Stele
                case 0x527F: // Martial God·Virtuous Stone
                case 0x5280: // Martial God·Manjushri Stele
                case 0x5281: // Martial God·Manjushri Stone
                case 0x5282: // Martial God·Hollow Stele
                case 0x5283: // Martial God·Hollow Stone
                case 0x5284: // Martial God·Removal Stele
                case 0x5285: // Martial God·Removal Stone
                case 0x5286: // Martial God·Guanyin Stele
                case 0x5287: // Martial God·Guanyin Stone

                case 0xD6D9: // g17 Ember
                case 0xD6DA: // g17 Pearl

                case 0x662C: // Raw Crystal
                case 0x64DC: // Rapture Crystal
                case 0x64DD: // Uncanny Crystal
                case 0x6610: // Soul Crystal
                case 0xAD50: // Beast Blood
                case 0xAD27: // Mystic Fiber
                case 0xAD28: // Purify Crystal
                case 0xC5A0: // G18 Ore
                    return true;
            }

            return false;
        }

        private void InitializeStatusBar()
        {
            Observable.Interval(TimeSpan.FromMilliseconds(250))
                .Where(_ => (AttachedGame?.Status ?? GameStatus.Offline) == GameStatus.LoggedIn)
                .Subscribe(_ => UpdateTarget());
            Observable.Interval(TimeSpan.FromMilliseconds(125))
                .Where(_ => (AttachedGame?.Status ?? GameStatus.Offline) == GameStatus.LoggedIn)
                .Subscribe(_ => UpdateTargetInfo());
        }
        private void UpdateTarget()
        {
            var character = new Character(AttachedGame.Game);
            var npcs = new NpcContainer(AttachedGame.Game);

            currentTarget = npcs.GetItemByID(character.SelectedTargetID);
        }
        private void UpdateTargetInfo()
        {
            // save a temp instance to prevent race condition from UpdateTarget()
            var target = currentTarget;

            if (target == null || target.NpcBase.Value == 0)
            {
                TargetInfo = "Target: null";
                return;
            }

            var maxHP = target.MaxHP;
            
            TargetInfo = string.Format("Target: {0:P2} {1:N0}",
                maxHP != 0 ? (double)target.HP.Value / target.MaxHP.Value : 0,
                target.HP.Value);
        }
    }

    public static class DumpExtension
    {
        public static IEnumerable<T> Dump<T>(this IEnumerable<T> source, string header = "Dumped : ")
        {
            foreach (var item in source)
            {
                Debug.WriteLine(header + item);
            }
            return source;
        }
    }
}
