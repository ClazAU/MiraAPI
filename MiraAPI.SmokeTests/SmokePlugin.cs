global using static Reactor.Utilities.Logger<MiraAPI.SmokeTests.SmokePlugin>;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using MiraAPI.PluginLoading;
using Reactor;
using Reactor.Networking;
using Reactor.Networking.Attributes;

namespace MiraAPI.SmokeTests;

[BepInAutoPlugin("mira.smoketests", "MiraSmokeTests")]
[BepInProcess("Among Us.exe")]
[BepInDependency(ReactorPlugin.Id)]
[BepInDependency(MiraApiPlugin.Id)]
[ReactorModFlags(ModFlags.RequireOnAllClients)]
public partial class SmokePlugin : BasePlugin, IMiraPlugin
{
    public Harmony Harmony { get; } = new(Id);

    public string OptionsTitleText => "Mira Smoke Tests";

    public ConfigFile GetConfigFile() => Config;

    public override void Load()
    {
        Harmony.PatchAll();
    }
}
