using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using RoR2.UI;

namespace PassiveSkillHud;

[BepInDependency(RiskOfOptionsIntegration.Guid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class PassiveSkillHudPlugin : BaseUnityPlugin
{
    internal static ManualLogSource Log;
    internal static ConfigEntry<bool> ShowPassives;

    /// <summary>Bumped whenever a setting changes, so the HUDs know to rebuild their icons.</summary>
    internal static int SettingsVersion;

    private void Awake()
    {
        Log = Logger;

        ShowPassives = Config.Bind("General", "Show passives", true,
            "Show the survivor's passive skills above the skill bar.");
        ShowPassives.SettingChanged += (_, _) => SettingsVersion++;

        // Fires for every HUD (including split-screen ones) once it has a body to show.
        HUD.onHudTargetChangedGlobal += hud =>
        {
            if (!hud.GetComponent<PassiveSkillDisplay>()) hud.gameObject.AddComponent<PassiveSkillDisplay>();
        };

        RiskOfOptionsIntegration.Register(Path.GetDirectoryName(Info.Location));
    }
}
