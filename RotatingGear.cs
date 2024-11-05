/*
 * Copyright (C) 2024 Game4Freak.io
 * This mod is provided under the Game4Freak EULA.
 * Full legal terms can be found at https://game4freak.io/eula/
 */

using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Rotating Gear", "VisEntities", "1.0.0")]
    [Description("Equips players with a random loadout at set intervals.")]
    public class RotatingGear : RustPlugin
    {
        #region 3rd Party Dependencies

        [PluginReference]
        private readonly Plugin GearCore;

        #endregion 3rd Party Dependencies

        #region Fields

        private static RotatingGear _plugin;
        private static Configuration _config;
        private int _currentGearIndex = 0;

        #endregion Fields

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Version")]
            public string Version { get; set; }

            [JsonProperty("Duration Between Each Gear Rotation Seconds")]
            public float DurationBetweenEachGearRotationSeconds { get; set; }

            [JsonProperty("Equip Random Gear Set")]
            public bool EquipRandomGearSet { get; set; }

            [JsonProperty("Gear Sets")]
            public List<string> GearSets { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();

            if (string.Compare(_config.Version, Version.ToString()) < 0)
                UpdateConfig();

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        private void UpdateConfig()
        {
            PrintWarning("Config changes detected! Updating...");

            Configuration defaultConfig = GetDefaultConfig();

            if (string.Compare(_config.Version, "1.0.0") < 0)
                _config = defaultConfig;

            PrintWarning("Config update complete! Updated from version " + _config.Version + " to " + Version.ToString());
            _config.Version = Version.ToString();
        }

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                Version = Version.ToString(),
                DurationBetweenEachGearRotationSeconds = 300f,
                EquipRandomGearSet = false,
                GearSets = new List<string>
                {
                    "GearSet1",
                    "GearSet2",
                    "GearSet3"
                }
            };
        }

        #endregion Configuration

        #region Oxide Hooks

        private void Init()
        {
            _plugin = this;
            PermissionUtil.RegisterPermissions();
        }

        private void Unload()
        {
            CoroutineUtil.StopAllCoroutines();
            _config = null;
            _plugin = null;
        }

        private void OnServerInitialized(bool isStartup)
        {
            if (!CheckDependencies(unloadIfNotFound: true))
                return;

            timer.Every(_config.DurationBetweenEachGearRotationSeconds, () =>
            {
                CoroutineUtil.StartCoroutine(Guid.NewGuid().ToString(), GearRotationCoroutine());
            });
        }

        #endregion Oxide Hooks

        #region Gear Rotation

        private IEnumerator GearRotationCoroutine()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player != null && !PermissionUtil.HasPermission(player, PermissionUtil.IGNORE))
                {
                    string gearSetToEquip;

                    if (_config.EquipRandomGearSet)
                    {
                        gearSetToEquip = _config.GearSets[UnityEngine.Random.Range(0, _config.GearSets.Count)];
                    }
                    else
                    {
                        gearSetToEquip = _config.GearSets[_currentGearIndex];
                    }

                    if (EquipGearSet(player, gearSetToEquip, clearInventory: true))
                        MessagePlayer(player, Lang.GearRotated, gearSetToEquip);
                }

                yield return null;
            }

            if (!_config.EquipRandomGearSet)
            {
                _currentGearIndex = (_currentGearIndex + 1) % _config.GearSets.Count;
            }
        }

        #endregion Gear Rotation

        #region Gear Set Equipping

        private bool GearSetExists(string gearSetName)
        {
            if (!PluginLoaded(_plugin.GearCore))
                return false;

            return _plugin.GearCore.Call<bool>("GearSetExists", gearSetName);
        }

        private bool EquipGearSet(BasePlayer player, string gearSetName, bool clearInventory = true)
        {
            if (!PluginLoaded(_plugin.GearCore))
                return false;

            return _plugin.GearCore.Call<bool>("EquipGearSet", player, gearSetName, clearInventory);
        }

        #endregion Gear Set Equipping

        #region Helper Functions

        private bool CheckDependencies(bool unloadIfNotFound = false)
        {
            if (!PluginLoaded(GearCore))
            {
                Puts("Gear Core is not loaded. Download it from https://game4freak.io.");

                if (unloadIfNotFound)
                    rust.RunServerCommand("oxide.unload", nameof(RotatingGear));

                return false;
            }

            return true;
        }

        private static bool PluginLoaded(Plugin plugin)
        {
            if (plugin != null && plugin.IsLoaded)
                return true;
            else
                return false;
        }

        #endregion Helper Functions

        #region Helper Classes

        public static class CoroutineUtil
        {
            private static readonly Dictionary<string, Coroutine> _activeCoroutines = new Dictionary<string, Coroutine>();

            public static void StartCoroutine(string coroutineName, IEnumerator coroutineFunction)
            {
                StopCoroutine(coroutineName);

                Coroutine coroutine = ServerMgr.Instance.StartCoroutine(coroutineFunction);
                _activeCoroutines[coroutineName] = coroutine;
            }

            public static void StopCoroutine(string coroutineName)
            {
                if (_activeCoroutines.TryGetValue(coroutineName, out Coroutine coroutine))
                {
                    if (coroutine != null)
                        ServerMgr.Instance.StopCoroutine(coroutine);

                    _activeCoroutines.Remove(coroutineName);
                }
            }

            public static void StopAllCoroutines()
            {
                foreach (string coroutineName in _activeCoroutines.Keys.ToArray())
                {
                    StopCoroutine(coroutineName);
                }
            }
        }

        #endregion Helper Classes

        #region Permissions

        private static class PermissionUtil
        {
            public const string IGNORE = "rotatinggear.ignore";
            private static readonly List<string> _permissions = new List<string>
            {
                IGNORE,
            };

            public static void RegisterPermissions()
            {
                foreach (var permission in _permissions)
                {
                    _plugin.permission.RegisterPermission(permission, _plugin);
                }
            }

            public static bool HasPermission(BasePlayer player, string permissionName)
            {
                return _plugin.permission.UserHasPermission(player.UserIDString, permissionName);
            }
        }

        #endregion Permissions

        #region Localization

        private class Lang
        {
            public const string GearRotated = "GearRotated";
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [Lang.GearRotated] = "New gear rotation! You have been switched to <color=#CACF52>{0}</color>.",

            }, this, "en");
        }

        private static string GetMessage(BasePlayer player, string messageKey, params object[] args)
        {
            string message = _plugin.lang.GetMessage(messageKey, _plugin, player.UserIDString);

            if (args.Length > 0)
                message = string.Format(message, args);

            return message;
        }

        public static void MessagePlayer(BasePlayer player, string messageKey, params object[] args)
        {
            string message = GetMessage(player, messageKey, args);
            _plugin.SendReply(player, message);
        }

        #endregion Localization
    }
}