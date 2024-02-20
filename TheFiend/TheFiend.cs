using UnityEngine;
using HarmonyLib;
using BepInEx;
using LethalLib.Modules;
using System.IO;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Reflection;
using BepInEx.Configuration;
using System.Linq;
using UnityEngine.Rendering;

namespace TheFiend
{
    [BepInPlugin("com.RuthlessCompany", "The Fiend", "0.0.0")]
    public class TheFiend : BaseUnityPlugin
    {
        public static TheFiend instance;
        public static string RoleCompanyFolder = "Assets/TheFiend/";
        public static AssetBundle bundle;
        // Configs
        public static ConfigEntry<int> SpawnChance;
        public static ConfigEntry<Levels.LevelTypes> Moon;
        public static ConfigEntry<int> FlickerRngChance;
        public static ConfigEntry<bool> WillRageAfterApparatus;
        public static ConfigEntry<float> Volume;
        void Awake()
        {
            var customFile = new ConfigFile(Path.Combine(Paths.ConfigPath, "Fiend.cfg"), true);
            SpawnChance = customFile.Bind("Fiend", "Spawn Weight", 30, new ConfigDescription("The Chance to spawn the fiend inside of the building"));
            Moon = customFile.Bind("Fiend", "Moon", Levels.LevelTypes.All, new ConfigDescription("What is the only moon it can spawn on. Only one VALUE at a time."));
            FlickerRngChance = customFile.Bind("Fiend", "Flicker Chance", 1000, new ConfigDescription("This is a Random chance out of 1/1000 happening to a random player"));
            WillRageAfterApparatus = customFile.Bind("Fiend", "Rage After Apparatus", true, new ConfigDescription("Trigger his rage mode if you remove the Apparatus."));
            Volume = customFile.Bind("Fiend", "Volume", 1f, new ConfigDescription("Sounds as scream and idle sound, not step sounds"));
            var types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (var type in types)
            {
                var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                foreach (var method in methods)
                {
                    var attributes = method.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                    if (attributes.Length > 0)
                    {
                        method.Invoke(null, null);
                    }
                }
            }
            instance = this;

            bundle = AssetBundle.LoadFromFile(Path.Combine(Path.GetDirectoryName(Info.Location), "thefiend"));
            EnemyType Enemii = TheFiend.bundle.LoadAsset<EnemyType>(RoleCompanyFolder + "TheFiend.asset");
            Enemies.RegisterEnemy(Enemii, SpawnChance.Value,Moon.Value, bundle.LoadAsset<TerminalNode>(RoleCompanyFolder + "TheFiendNode.asset"), bundle.LoadAsset<TerminalKeyword>(RoleCompanyFolder + "TheFiendKey.asset"));
            NetworkPrefabs.RegisterNetworkPrefab(Enemii.enemyPrefab);
            Utilities.FixMixerGroups(Enemii.enemyPrefab);
        }
        public void AddScrap(string Name, int Rare, Levels.LevelTypes level)
        {
            Item NewItem = TheFiend.bundle.LoadAsset<Item>(RoleCompanyFolder + Name + ".asset");
            NetworkPrefabs.RegisterNetworkPrefab(NewItem.spawnPrefab);

            Utilities.FixMixerGroups(NewItem.spawnPrefab);
            Items.RegisterScrap(NewItem, Rare, level);
        }
    }
}