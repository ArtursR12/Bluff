// Assets/Editor/iOSPostBuild.cs
// Runs automatically after Unity exports the Xcode project.
// Adds Photon Fusion's required Info.plist keys for iOS 14+ local networking.

#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using System.IO;

public static class iOSPostBuild
{
    [PostProcessBuild(999)]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS) return;

        string plistPath = Path.Combine(buildPath, "Info.plist");
        PlistDocument plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        PlistElementDict root = plist.root;

        // ── Photon Fusion: local network access (iOS 14+) ─────
        // Required or the app will fail to discover peers on the same WiFi.
        root.SetString(
            "NSLocalNetworkUsageDescription",
            "Bluff uses local networking to connect players on the same Wi-Fi network.");

        // Bonjour service type used by Photon Fusion for peer discovery
        PlistElementArray bonjourServices;
        if (root["NSBonjourServices"] == null)
        {
            bonjourServices = root.CreateArray("NSBonjourServices");
        }
        else
        {
            bonjourServices = root["NSBonjourServices"].AsArray();
            if (bonjourServices == null)
            {
                root.values.Remove("NSBonjourServices");
                bonjourServices = root.CreateArray("NSBonjourServices");
            }
        }
        // Only add if not already present
        bool hasEntry = false;
        foreach (var el in bonjourServices.values)
            if (el.AsString() == "_fusion-peer._tcp") { hasEntry = true; break; }
        if (!hasEntry)
            bonjourServices.AddString("_fusion-peer._tcp");

        plist.WriteToFile(plistPath);
        UnityEngine.Debug.Log("[iOSPostBuild] Info.plist updated with Photon local network keys.");
    }
}
#endif
