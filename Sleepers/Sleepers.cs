using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Timers;
using Fougerite;
using Fougerite.Permissions;
using RustProto;
using UnityEngine;

namespace Sleepers
{
    public class Sleepers : Fougerite.Module
    {
        private static Sleepers _instance;
        public int sleeperLifeInMinutes = 5;
        public int timerElapse = 120000;
        public bool Debug = false;
        public IniParser Settings;
        public Timer _timer;
        public readonly Dictionary<ulong, double> Data = new Dictionary<ulong, double>();

        public override string Name
        {
            get { return "Sleepers"; }
        }

        public override string Author
        {
            get { return "DreTaX"; }
        }

        public override string Description
        {
            get { return "Sleepers"; }
        }

        public override Version Version
        {
            get { return new Version("1.2"); }
        }

        public override void Initialize()
        {
            _instance = this;
            Fougerite.Hooks.OnPlayerDisconnected += OnPlayerDisconnected;
            Fougerite.Hooks.OnPlayerConnected += OnPlayerConnected;
            Fougerite.Hooks.OnCommand += OnCommand;
            _timer = new Timer(timerElapse);
            _timer.Elapsed += RunC;
            _timer.Start();
        }

        public override void DeInitialize()
        {
            Fougerite.Hooks.OnPlayerDisconnected -= OnPlayerDisconnected;
            Fougerite.Hooks.OnPlayerConnected -= OnPlayerConnected;
            Fougerite.Hooks.OnCommand -= OnCommand;
            _timer.Dispose();
        }

        public void ReloadConfig()
        {
            if (!File.Exists(Path.Combine(ModuleFolder, "Settings.ini")))
            {
                File.Create(Path.Combine(ModuleFolder, "Settings.ini")).Dispose();
                Settings = new IniParser(Path.Combine(ModuleFolder, "Settings.ini"));
                Settings.AddSetting("Settings", "sleeperLifeInMinutes", sleeperLifeInMinutes.ToString());
                Settings.AddSetting("Settings", "timerElapse", timerElapse.ToString());
                Settings.AddSetting("Settings", "Debug", Debug.ToString());
                Settings.Save();
                sleeperLifeInMinutes = int.Parse(Settings.GetSetting("Settings", "sleeperLifeInMinutes"));
                sleeperLifeInMinutes = sleeperLifeInMinutes * 60;
                timerElapse = int.Parse(Settings.GetSetting("Settings", "timerElapse"));
                Debug = Settings.GetBoolSetting("Settings", "Debug");
            }
            else
            {
                try
                {
                    Settings = new IniParser(Path.Combine(ModuleFolder, "Settings.ini"));
                    sleeperLifeInMinutes = int.Parse(Settings.GetSetting("Settings", "sleeperLifeInMinutes"));
                    sleeperLifeInMinutes = sleeperLifeInMinutes * 60;
                    timerElapse = int.Parse(Settings.GetSetting("Settings", "timerElapse"));
                    Debug = Settings.GetBoolSetting("Settings", "Debug");
                }
                catch (Exception ex)
                {
                    Logger.LogError("[Sleepers] Missing config options! Remove the config and restart the server! " + ex);
                }
            }
        }

        public static Sleepers GetInstance()
        {
            return _instance;
        }

        public void OnCommand(Fougerite.Player player, string cmd, string[] args)
        {
            if (cmd == "sleepers")
            {
                if (player.Admin || PermissionSystem.GetPermissionSystem()
                    .PlayerHasPermission(player, "sleepers.reload"))
                {
                    ReloadConfig();
                    player.Message("Sleepers Plugin Reloaded!");
                }
            }
        }

        public void OnPlayerConnected(Fougerite.Player player)
        {
            if (Data.ContainsKey(player.UID))
            {
                Data.Remove(player.UID);
            }
        }

        public void OnPlayerDisconnected(Fougerite.Player player)
        {
            Data[player.UID] = TimeSpan.FromTicks(DateTime.Now.Ticks).TotalSeconds;
        }

        public void RunC(object sender, ElapsedEventArgs e)
        {
            _timer.Dispose();
            List<ulong> idstoremove = new List<ulong>();
            foreach (var id in Data.Keys)
            {
                double playercd = Data[id];

                double calc = TimeSpan.FromTicks(DateTime.Now.Ticks).TotalSeconds - playercd;

                if (calc >= sleeperLifeInMinutes)
                {
                    RustProto.Avatar playerAvatar = NetUser.LoadAvatar(id);

                    //Check if the player has a SLUMBER away event & a timestamp that's older than the oldest permitted, calculated above
                    if (playerAvatar != null && playerAvatar.HasAwayEvent &&
                        playerAvatar.AwayEvent.Type == AwayEvent.Types.AwayEventType.SLUMBER &&
                        playerAvatar.AwayEvent.HasTimestamp)
                    {
                        idstoremove.Add(id);
                    }
                }
            }

            Loom.QueueOnMainThread(() =>
            {
                foreach (ulong id in idstoremove)
                {
                    if (Data.ContainsKey(id))
                    {
                        Data.Remove(id);
                    }

                    RustProto.Avatar playerAvatar = NetUser.LoadAvatar(id);
                    //Check if the player has a SLUMBER away event & a timestamp that's older than the oldest permitted, calculated above
                    if (playerAvatar != null && playerAvatar.HasAwayEvent &&
                        playerAvatar.AwayEvent.Type == AwayEvent.Types.AwayEventType.SLUMBER &&
                        playerAvatar.AwayEvent.HasTimestamp)
                    {

                        //There's an internal SleepingAvatar.Close method that takes a ulong for the playerID
                        SleepingAvatar.TransientData transientData = SleepingAvatar.Close(id);
                        //MethodInfo info = typeof (SleepingAvatar).GetMethod("Close", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                        //SleepingAvatar.TransientData transientData = (SleepingAvatar.TransientData) info.Invoke(null, new object[] {id});

                        // Loom.QueueOnMainThread might be needed here according to a post from 2016. Damn wish i had the knowledge of that time.
                        if (transientData.exists)
                        {
                            transientData.AdjustIncomingAvatar(ref playerAvatar);
                            NetUser.SaveAvatar(id, ref playerAvatar);
                        }

                        if (Debug)
                        {
                            Logger.Log("[Sleepers] Sleeper: " + id + " should be removed.");
                        }
                    }
                }
            });

            _timer = new Timer(timerElapse);
            _timer.Elapsed += RunC;
            _timer.Start();
        }
    }
}
