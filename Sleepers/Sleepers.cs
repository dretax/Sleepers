using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Timers;
using Fougerite;
using RustProto;
using UnityEngine;

namespace Sleepers
{
    public class Sleepers : Fougerite.Module
    {
        public int sleeperLifeInMinutes = 5;
        public int timerElapse = 60000;
        public bool Debug = false;
        public IniParser Settings;
        public Timer _timer;
        public readonly Dictionary<ulong, int> data = new Dictionary<ulong,int>();
        public readonly Dictionary<ulong, Vector3> data2 = new Dictionary<ulong, Vector3>();

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
            get { return new Version("1.1"); }
        }

        public override void Initialize()
        {
            if (!File.Exists(Path.Combine(ModuleFolder, "Settings.ini")))
            {
                File.Create(Path.Combine(ModuleFolder, "Settings.ini")).Dispose();
                Settings = new IniParser(Path.Combine(ModuleFolder, "Settings.ini"));
                Settings.AddSetting("Settings", "sleeperLifeInMinutes", sleeperLifeInMinutes.ToString());
                Settings.AddSetting("Settings", "timerElapse", timerElapse.ToString());
                Settings.AddSetting("Settings", "Debug", Debug.ToString());
                Settings.Save();
            }
            else
            {
                try
                {
                    Settings = new IniParser(Path.Combine(ModuleFolder, "Settings.ini"));
                    sleeperLifeInMinutes = int.Parse(Settings.GetSetting("Settings", "sleeperLifeInMinutes"));
                    timerElapse = int.Parse(Settings.GetSetting("Settings", "timerElapse"));
                    Debug = Settings.GetBoolSetting("Settings", "Debug");
                }
                catch
                {
                    Logger.LogError("[Sleepers] Missing config options! Remove the config and restart the server!");
                }
            }
            sleeperLifeInMinutes = sleeperLifeInMinutes * 60000;
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

        public void OnCommand(Fougerite.Player player, string cmd, string[] args)
        {
            if (player.Admin)
            {
                if (cmd == "sleepers")
                {
                    Settings = new IniParser(Path.Combine(ModuleFolder, "Settings.ini"));
                    sleeperLifeInMinutes = int.Parse(Settings.GetSetting("Settings", "sleeperLifeInMinutes"));
                    sleeperLifeInMinutes = sleeperLifeInMinutes * 60000;
                    timerElapse = int.Parse(Settings.GetSetting("Settings", "timerElapse"));
                    Debug = Settings.GetBoolSetting("Settings", "Debug");
                    player.Message("Sleepers Plugin Reloaded!");
                }
            }
        }

        public void OnPlayerConnected(Fougerite.Player player)
        {
            if (data.ContainsKey(player.UID))
            {
                data.Remove(player.UID);
            }
            if (data2.ContainsKey(player.UID))
            {
                data2.Remove(player.UID);
            }
        }

        public void OnPlayerDisconnected(Fougerite.Player player)
        {
            data[player.UID] = System.Environment.TickCount;
            data2[player.UID] = player.DisconnectLocation;
        }

        public void RunC(object sender, ElapsedEventArgs e)
        {
            _timer.Dispose();
            var systick = System.Environment.TickCount;
            List<ulong> idstoremove = new List<ulong>();
            foreach (var id in data.Keys)
            {
                var playercd = data[id];
                var location = data2[id];
                var sleepers = UnityEngine.Physics.OverlapSphere(location, 2f);
                foreach (var sleeper in sleepers)
                {
                    var name = sleeper.name;
                    if (!name.ToLower().Contains("malesleeper"))
                    {
                        continue;
                    }
                    bool remove = double.IsNaN(systick - playercd) || (systick - playercd) < 0;
                    var calc = systick - playercd;
                    if (calc >= sleeperLifeInMinutes || remove)
                    {
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

                            if (transientData.exists)
                            {
                                transientData.AdjustIncomingAvatar(ref playerAvatar);
                                NetUser.SaveAvatar(id, ref playerAvatar);
                            }
                            if (Debug)
                            {
                                Logger.Log("[Sleepers] Sleeper: " + id + " should be removed.");
                            }
                            idstoremove.Add(id);
                        }
                    }
                }
            }
            foreach (var x in idstoremove)
            {
                if (data.ContainsKey(x)) { data.Remove(x); }
                if (data2.ContainsKey(x)) { data2.Remove(x); }
            }
            _timer = new Timer(timerElapse);
            _timer.Elapsed += RunC;
            _timer.Start();
        }
    }
}
