using UnityEditor;
internal class CommandScript : IRunCommand {
 public void Execute(ExecutionResult result) { AssetDatabase.Refresh(); }
}
