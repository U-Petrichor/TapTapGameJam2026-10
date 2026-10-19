using UnityEditor;
internal class CommandScript : IRunCommand { public void Execute(ExecutionResult result) { ArcLiquidProgressSetup.Install(); } }
