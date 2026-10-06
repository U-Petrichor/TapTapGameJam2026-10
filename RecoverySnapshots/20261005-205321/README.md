# Scene recovery inspection

The exact scene state immediately before the external-change reload has not been found.

GameScene_ResourcesUI_IconsRemoved.unity is a reconstruction based on the currently saved scene. It removes the three Icon GameObjects under HUDCanvas/ResourcesUI/Coins, Nutrition and CockroachNum, matching the user-confirmed pre-reload edit. It is not a complete historical backup. Other unreported UI changes have not been reconstructed.

The active Assets/Scenes/GameScene.unity was not overwritten during recovery. All candidate snapshots and logs are preserved here.

The morning recovery scene still contains the three Icons, so replacing the active scene with it would not restore the requested UI state.
