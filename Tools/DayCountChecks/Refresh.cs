using UnityEditor;
internal class CommandScript : IRunCommand {
 public void Execute(ExecutionResult result) {
  result.Log("Refresh requested"); AssetDatabase.Refresh();
 }
}
