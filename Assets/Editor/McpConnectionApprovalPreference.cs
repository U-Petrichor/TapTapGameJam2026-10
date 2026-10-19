using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Exposes the installed Unity MCP package's otherwise hidden approval policy.
[InitializeOnLoad]
internal static class McpConnectionApprovalPreference
{
    private const string PreferenceKey = "TapTapGameJam.Mcp.AutoAcceptConnections";
    private const string MenuPath = "Tools/MCP/Auto Accept Connections";

    static McpConnectionApprovalPreference()
    {
        EditorApplication.delayCall += ApplyPreference;
    }

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        EditorPrefs.SetBool(PreferenceKey, !EditorPrefs.GetBool(PreferenceKey, true));
        ApplyPreference();
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateToggle()
    {
        Menu.SetChecked(MenuPath, EditorPrefs.GetBool(PreferenceKey, true));
        return true;
    }

    private static void ApplyPreference()
    {
        try
        {
            var manager = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Unity.AI.MCP.Editor.Settings.MCPSettingsManager"))
                .FirstOrDefault(type => type != null);
            if (manager == null) return;

            const BindingFlags staticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var settings = manager.GetProperty("Settings", staticFlags)?.GetValue(null);
            var policies = settings?.GetType().GetField("connectionPolicies")?.GetValue(settings);
            var direct = policies?.GetType().GetField("direct")?.GetValue(policies);
            var approval = direct?.GetType().GetField("requiresApproval");
            var save = manager.GetMethod("SaveSettings", staticFlags);
            if (approval == null || save == null)
            {
                Debug.LogWarning("MCP auto-accept: installed package no longer exposes the expected approval policy.");
                return;
            }

            bool requiresApproval = !EditorPrefs.GetBool(PreferenceKey, true);
            if ((bool)approval.GetValue(direct) == requiresApproval) return;
            approval.SetValue(direct, requiresApproval);
            save.Invoke(null, null);
            Debug.Log("MCP connections: " + (requiresApproval ? "manual approval enabled." : "auto-accept enabled."));
        }
        catch (Exception exception)
        {
            Debug.LogWarning("Could not apply MCP approval preference: " + exception.Message);
        }
    }
}
