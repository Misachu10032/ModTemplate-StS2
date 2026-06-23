using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using ModTemplate.ModTemplateCode.Nodes;
using ModTemplate.ModTemplateCode.Snapshots;

namespace ModTemplate.ModTemplateCode.Patches;

internal static class SnapshotPatch
{
    // ── Add UI when a run scene loads ─────────────────────────────────────────

    [HarmonyPatch(typeof(NRun), "_Ready")]
    static class NRunReadyPatch
    {
        [HarmonyPostfix]
        static void Postfix(NRun __instance)
        {
            try
            {
                var sceneRoot = __instance.GetTree()?.Root;
                if (sceneRoot != null)
                    SnapshotUi.Initialize(sceneRoot);

                // Wire up the delegate that lets Restore() trigger the scene transition.
                var nGame = FindInTree<NGame>(__instance.GetTree().Root);
                if (nGame != null)
                {
                    // Log both candidate methods so we know their exact signatures.
                    LogMethodSig(typeof(NGame), "LaunchMainMenu");
                    LogMethodSig(typeof(NGame), "LoadRun");

                    // Wire up scene transition for Restore().
                    // Currently uses LaunchMainMenu → AutoContinuePatch path.
                    // Once we see LoadRun's signature in the log we can switch to
                    // calling it directly and skip the main menu entirely.
                    var launchMethod = AccessTools.Method(typeof(NGame), "LaunchMainMenu");
                    if (launchMethod != null)
                    {
                        var args = launchMethod.GetParameters().Select(p =>
                            p.HasDefaultValue           ? p.DefaultValue :
                            p.ParameterType.IsValueType ? Activator.CreateInstance(p.ParameterType)
                                                        : (object?)null
                        ).ToArray();
                        SnapshotManager.LaunchMainMenuAction = () => launchMethod.Invoke(nGame, args);
                    }
                    else
                        MainFile.Logger.Info("[Snapshot] NRunReady: LaunchMainMenu method not found.");
                }
                else
                    MainFile.Logger.Info("[Snapshot] NRunReady: NGame not found in tree — restore will not work.");

                // Clear the flag after the restored run has fully loaded so the
                // next run-end will delete snapshots normally.
                SnapshotManager.IsRestoring = false;
            }
            catch (Exception ex) { MainFile.Logger.Info($"[Snapshot] NRunReady error: {ex}"); }
        }

        static void LogMethodSig(Type type, string name)
        {
            var m = AccessTools.Method(type, name);
            if (m == null) { MainFile.Logger.Info($"[Snapshot] {name}: not found"); return; }
            var p = string.Join(", ", m.GetParameters().Select(x => $"{x.ParameterType.Name} {x.Name}"));
            MainFile.Logger.Info($"[Snapshot] {name}({p}) -> {m.ReturnType.Name}");
        }

        static T? FindInTree<T>(Node node) where T : Node
        {
            if (node is T t) return t;
            foreach (var child in node.GetChildren())
            {
                var found = FindInTree<T>(child);
                if (found != null) return found;
            }
            return null;
        }
    }

    // ── Drive UI every frame ──────────────────────────────────────────────────

    [HarmonyPatch(typeof(NRun), "_Process")]
    static class NRunProcessPatch
    {
        [HarmonyPostfix]
        static void Postfix(double delta)
        {
            try { SnapshotUi.Update(delta); }
            catch (Exception ex) { MainFile.Logger.Info($"[Snapshot] UI error: {ex.Message}"); }
        }
    }

    // ── Public surface used by FloorSnapshotPatch and SnapshotUi ─────────────

    public static void SaveSnapshot(int floor)
    {
        try { SnapshotManager.Save(floor); }
        catch (Exception ex) { MainFile.Logger.Info($"[Snapshot] Save error: {ex}"); }
    }

    public static void RestoreSnapshot(RunSnapshot snapshot)
    {
        try { SnapshotManager.Restore(snapshot); }
        catch (Exception ex) { MainFile.Logger.Info($"[Snapshot] Restore error: {ex}"); }
    }
}
