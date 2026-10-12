using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using RiskOfOptions;
using RiskOfOptions.Options;
using UnityEngine;

namespace PassiveSkillHud;

internal static class RiskOfOptionsIntegration
{
    public const string Guid = "com.rune580.riskofoptions";

    public static void Register(string pluginDir)
    {
        if (!BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey(Guid)) return;
        try
        {
            AddOptions(pluginDir);
        }
        catch (Exception ex)
        {
            PassiveSkillHudPlugin.Log.LogError($"Error during RiskOfOptions integration: {ex}");
        }
    }

    // Kept in its own method so RiskOfOptions is only loaded when it's installed.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AddOptions(string pluginDir)
    {
        // The build writes the csproj's Description into the assembly, so it's only set in one place.
        var description = typeof(RiskOfOptionsIntegration).Assembly.GetCustomAttribute<AssemblyDescriptionAttribute>();
        if (description != null) ModSettingsManager.SetModDescription(description.Description);
        var icon = LoadIcon(Path.Combine(pluginDir, "icon.png"));
        if (icon) ModSettingsManager.SetModIcon(icon);
        ModSettingsManager.AddOption(new CheckBoxOption(PassiveSkillHudPlugin.ShowPassives));
    }

    /// <summary>Loads the Thunderstore package icon, which the mod manager installs next to the plugin.</summary>
    private static Sprite LoadIcon(string path)
    {
        if (!File.Exists(path)) return null;
        var texture = new Texture2D(2, 2);
        if (!texture.LoadImage(File.ReadAllBytes(path))) return null;
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
    }
}
