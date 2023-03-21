using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Game.ClientState;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using System;
using Dalamud.Interface.Windowing;
using System.Text;
using Dalamud.Game.ClientState.Conditions;
using BattleMusicShuffler.Windows;
using Lumina.Data;
using System.Threading;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Collections.Generic;

namespace BattleMusicShuffler
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "Sample Plugin Edit";
        private const string CommandName = "/pmycommand";

        private DalamudPluginInterface PluginInterface { get; init; }
        private CommandManager CommandManager { get; init; }
        public Configuration Configuration { get; init; }
        public WindowSystem WindowSystem = new("SamplePlugin");

        private ChatGui chatGui { get; init; }
        private ClientState clientState { get; init; }
        private Condition clientCondition { get; init; }
        private ConfigWindow ConfigWindow { get; init; }
        private MainWindow MainWindow { get; init; }

        private List<string> bgms;
        private Random rng;

        public Plugin(
            [RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
            [RequiredVersion("1.0")] CommandManager commandManager,
            [RequiredVersion("1.0")] ChatGui chatGui,
            [RequiredVersion("1.0")] Condition clientCondition)
        {
            PluginInterface = pluginInterface;
            CommandManager = commandManager;
            this.chatGui = chatGui;
            this.clientCondition = clientCondition;
            bgms = new List<String>();

            bgms.Add(Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "massdestruction.wav"));
            bgms.Add(Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "reachout.m4a"));
            bgms.Add(Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "history.m4a"));
            bgms.Add(Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "lastsurprise.flac"));

            rng = new Random();

            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(PluginInterface);

            // you might normally want to embed resources and load them from the manifest stream
            var imagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");
            var goatImage = PluginInterface.UiBuilder.LoadImage(imagePath);

            ConfigWindow = new ConfigWindow(this);
            MainWindow = new MainWindow(this, goatImage);

            WindowSystem.AddWindow(ConfigWindow);
            WindowSystem.AddWindow(MainWindow);

            this.clientCondition.ConditionChange += OnCombatEnter;

            CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
            {
                HelpMessage = "A useful message to display in /xlhelp"
            });

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;
        }

        private void OnCombatEnter(ConditionFlag flag, bool val)
        {
            if (flag == ConditionFlag.InCombat)
            {
                if (val)
                {
                    //chatGui.Print("In combat");
                    CommandManager.ProcessCommand("/xlbgmset 1");
                    int song = this.rng.Next(bgms.Count);
                    PlayBattleBGM(song);
                }
            }
        }

        public void Dispose()
        {
            clientCondition.ConditionChange -= OnCombatEnter;
            WindowSystem.RemoveAllWindows();

            ConfigWindow.Dispose();
            MainWindow.Dispose();

            CommandManager.RemoveHandler(CommandName);

        }

        private void OnCommand(string command, string args)
        {
            // in response to the slash command, just display our main ui
            MainWindow.IsOpen = true;
            //chatGui.Print("Printing Message");
        }

        private void DrawUI()
        {
            WindowSystem.Draw();
        }

        public void DrawConfigUI()
        {
            ConfigWindow.IsOpen = true;
        }

        /* Plays the custom BGM. Music code effectively copied from Peeping Tom and AudibleCharacterStatus plugins, with lots of 
         * help from markheath.net's NAudio tutorials.*/
        private void PlayBattleBGM(int songIndex)
        {
            // TODO: Change this so that it can be configured not default 
            var soundDevice = -1;
            var audioPath = this.bgms[songIndex];
            new Thread(() =>
            {
                AudioFileReader reader;
                try
                {
                    reader = new AudioFileReader(audioPath);
                }
                catch (Exception e)
                {
                    this.chatGui.Print(e.Message);
                    return;
                }

                /*
                using var channel = new WaveChannel32(reader)
                {
                    Volume = 1.0f,
                    PadWithZeroes = false,
                };*/

                reader.Volume = 0.16f;
                var fadeInOut = new FadeInOutSampleProvider(reader);
                fadeInOut.BeginFadeIn(2000);

                using (reader)
                {
                    using var output = new WaveOutEvent
                    {
                        DeviceNumber = soundDevice,
                    };
                    try
                    {
                        output.Init(fadeInOut);
                        output.Play();
                        while (true)
                        {
                            if (this.clientCondition[ConditionFlag.InCombat] == true)
                            {
                                Thread.Sleep(500);
                            }
                            else
                            {
                                fadeInOut.BeginFadeOut(2000);
                                Thread.Sleep(2000);
                                CommandManager.ProcessCommand("/xlbgmset");
                                //chatGui.Print("Out of combat");
                                break;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        this.chatGui.Print("Exception playing music!");
                    }
                }

            }).Start();
        }
    }
}
