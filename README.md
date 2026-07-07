# Snippets SDK for Unity

The Snippets SDK connects a Unity project to content created on the Snippets web portal. It imports published Snippet Sets, generates ready-to-use Unity prefabs, and plays synchronized character animation, audio, and text.

A **Snippet** is one unit of dialogue animation. It can contain voice audio, viseme data for lip sync, and character animation. A **Snippet Set** is a collection of related Snippets that is imported and managed as one unit.

Useful links:

- [Snippets documentation](https://docs.snippets3d.com/latest/)
- [Snippets web portal](https://app.snippets3d.com/)
- [SDK integrations and downloads](https://app.snippets3d.com/integrations)

## Requirements

- Unity 2022.3 LTS, Unity 6.0 LTS, or Unity 6.3 LTS
- A Snippets account
- Internet access for package installation and cloud operations
- Git installed when using the recommended Git URL installation

The package manifest targets Unity 2022.3 to preserve compatibility across the supported LTS lines.

## Workflow at a glance

1. Create a Snippet Set in the Snippets web portal.
2. Publish the set so it becomes downloadable by the SDK.
3. Install the SDK and log in from Unity.
4. Import the published set and choose a template prefab.
5. Drag generated snippet prefabs into a scene.
6. Use standalone playback, or use Scene Auto Setup for sequencing, movement, and gaze.

## Installation

Choose one of the installation options below.

### Option A: AI-assisted manifest install

Use this option if you want Codex, Claude, Copilot, or another coding agent to update `Packages/manifest.json` directly.

This path adds the required packages from Git URLs, so it does not require OpenUPM:

```text
Install the Snippets SDK by updating Packages/manifest.json.

Use this exact SDK package dependency with the Git URL found at
https://app.snippets3d.com/integrations:
- "com.snippets.sdk": "<insert Git URL from the Snippets integrations page>"

Also add these dependencies if missing:
- "com.cysharp.unitask":
  "https://github.com/Cysharp/UniTask.git?path=/src/UniTask/Assets/Plugins/UniTask"
- "org.khronos.unitygltf":
  "https://github.com/KhronosGroup/UnityGLTF.git"

Keep all existing dependencies unchanged.
```

### Option B: Unity Package Manager

#### 1. Configure OpenUPM

Add the OpenUPM registry through `Edit > Project Settings > Package Manager`, or add it directly to `Packages/manifest.json`:

```json
{
  "dependencies": {
    "...": "..."
  },
  "scopedRegistries": [
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": [
        "com.cysharp.unitask",
        "org.khronos.unitygltf"
      ]
    }
  ]
}
```

#### 2. Install the package

Git URL is the recommended method because it keeps team installations consistent and supports package updates:

1. Open `Window > Package Manager`.
2. Click `+`.
3. Select `Add package from git URL...`.
4. Paste the SDK Git URL from the [integrations page](https://app.snippets3d.com/integrations).
5. Confirm the installation.

ZIP fallback:

1. Download the SDK ZIP from the integrations page.
2. Extract it outside the Unity project's `Assets` folder.
3. Open `Window > Package Manager`.
4. Click `+`, then select `Add package from disk...`.
5. Select `package.json` in the extracted SDK folder.

### After installation

#### Import TextMeshPro essentials

If the project does not already include TMP essentials, use:

`Window > TextMeshPro > Import TMP Essential Resources`

#### Import the sample scenes

The sample scenes are optional, but they are the fastest way to inspect complete standalone, flow, movement, custom-animation, and gaze setups:

1. Select `Snippets SDK` in Package Manager.
2. Open the `Samples` tab.
3. Import `Snippets Sample Scenes`.
4. Open a sample scene and enter Play Mode.

#### Dependency note

The SDK uses UnityGLTF. Projects that also use another GLTF importer, such as glTFast, should check for package and importer conflicts.

## Prepare content in the web portal

Create a Snippet Set at [app.snippets3d.com](https://app.snippets3d.com/). The portal supports creating content from a script, an AI prompt, or uploaded audio.

Preview and edit the set, then click `Publish`. A set must finish publishing before Unity can import it. Keep the publishing tab open until the operation completes.

The Unity SDK imports and plays published content; it does not create Snippet Sets locally.

## Initial setup in Unity

### Log in

Open `Snippets > Log In`, then use the same credentials as the web portal. After a successful login, the SDK opens the Snippet Sets window automatically.

### Open Snippet Set management

After login, this window opens automatically. To reopen it later, use `Snippets > Import or Update Snippet Sets`.

Status labels:

- `[Imported]`: the set is present locally.
- `[Local Deprecated]`: the set exists locally but no longer exists remotely.
- `[Processing/Unpublished]`: the remote set is not downloadable yet.
- `[Update Processing]`: an updated remote version exists but is not downloadable yet.

Available actions:

- `Import`: download a published set that is not yet local.
- `Update`: synchronize a newer published version of an imported set.
- `Remove`: delete the local copy.

## Project folders

The settings asset is:

`SnippetsSDK/Config/Resources/ProjectSnippetsSettings`

Default settings:

- Generated root: `Assets/My Snippets`
- Raw folder setting: `Assets/My Snippets/Raw`

The raw setting supplies the nested raw folder name. The effective layout is per set:

```text
Assets/My Snippets/
└── Doctor/
    ├── DoctorSnippet1.prefab
    ├── DoctorSnippet2.prefab
    └── Raw/
```

If the raw folder setting is changed, the SDK uses its final folder name as the nested raw subfolder inside each imported set.

## Import, update, and remove

### Import a Snippet Set

1. Click `Import` next to a published set.
2. Select a Snippet template prefab asset.
3. Confirm the operation and wait for completion.

The SDK downloads the raw archive, extracts its assets, and generates one prefab for each snippet from the selected template. The import window remembers the last template selected during the current Unity session and uses the package default when available.

### Update a Snippet Set

Click `Update` after newer changes to the same set have been published.

Updates use a stable sync:

- existing generated prefabs are matched by Snippet ID and updated in place
- snippets removed remotely are deleted locally
- new snippets are generated

Reusing an existing generated prefab asset preserves scene and prefab references. Review references when content has been renamed or structurally changed.

### Remove a Snippet Set

Click `Remove` and confirm. This deletes the set's generated prefabs and raw assets from the Unity project.

Removing a local set does not delete it from the Snippets cloud platform. Scene references to removed prefabs become invalid.

## Bring an imported Snippet into a scene

### Standalone playback

1. Open `Assets/My Snippets/<SetName>`.
2. Drag a generated snippet prefab into the Hierarchy or Scene view.
3. Enter Play Mode.

The provided default template has `Play On Enable` enabled, so the snippet plays automatically. Verify that:

- the avatar animates
- audio plays when the template includes an audio player
- text appears when the template includes a text player
- the Console has no relevant errors

For manual playback, disable `Play On Enable` and call `SnippetPlayer.Play()`:

```csharp
using Snippets.Sdk;
using UnityEngine;

public class SnippetPlaybackFromCode : MonoBehaviour
{
    [SerializeField] private SnippetPlayer snippetPlayer;

    private void OnEnable()
    {
        if (snippetPlayer == null)
            return;

        snippetPlayer.PlaybackStarted.AddListener(OnPlaybackStarted);
        snippetPlayer.PlaybackStopped.AddListener(OnPlaybackStopped);
    }

    private void OnDisable()
    {
        if (snippetPlayer == null)
            return;

        snippetPlayer.PlaybackStarted.RemoveListener(OnPlaybackStarted);
        snippetPlayer.PlaybackStopped.RemoveListener(OnPlaybackStopped);
    }

    public void PlayNow() => snippetPlayer?.Play();
    public void StopNow() => snippetPlayer?.Stop();

    private void OnPlaybackStarted() => Debug.Log("Snippet playback started.");
    private void OnPlaybackStopped() => Debug.Log("Snippet playback stopped.");
}
```

### Controller-driven playback

Use controller-driven playback when a scene needs multiple actors, sequencing, movement, custom animations, or gaze.

The fastest setup is:

1. Drag one generated snippet prefab into the scene for each actor.
2. Rename each scene instance to its actor or role.
3. Open `Snippets > Scene Auto Setup`.
4. Drag the **scene instances** into the Actors area. Do not use prefab assets.
5. Choose which helper components to create.
6. Click `Create Or Update Scene Setup`.

The tool can create a `Snippet Tools` root containing:

- `Actor Registry`
- `Flow Controller`
- `Gaze Flow Controller`
- `Simple Controller`

It can also add and configure `SnippetsGazeDriver` and `SnippetsWalker` on actors.

`SnippetsActorRegistry` claims animation ownership for managed players, so standalone `Play On Enable` is suppressed while a player is controller-managed. Do not manually toggle `External Animation Control`.

## Helper tools

### `SnippetsActorRegistry`

Central map for actors, snippet libraries, movement, gaze, idle/walk clips, and reusable custom animations. It manages animation transitions and soft or hard stops.

Configure one actor entry per character with:

- a scene `SnippetPlayer`
- all snippet prefabs used by that actor
- idle and walk clips as needed
- optional `SnippetsWalker`
- optional `SnippetsGazeDriver`
- optional custom animation clips

### `SnippetsSimpleController`

Runs one action for quick testing or gameplay triggers:

- Snippet
- Walk
- Custom Animation
- Snippet + Custom Animation

Runtime methods: `Play()`, `Stop()`, and `Reset()`.

### `SnippetsFlowController`

Runs an ordered sequence of:

- Snippet
- Walk
- Pause
- Custom Animation
- Snippet + Custom Animation

It supports Play On Start, looping, automatic or manual progression, keyboard triggers, and `StepStarted` / `StepFinished` events.

### `SnippetsWalker`

Moves an actor through waypoints using straight-line movement or a `NavMeshAgent`.

### `SnippetsGazeDriver`

Provides procedural head, waist, and optional eye gaze. Modes are `FollowTarget`, `LookInFront`, and `Off`. Use its Inspector auto-setup first, then verify the detected rig references.

### `SnippetsGazeFlowController`

Synchronizes gaze with flow steps. Simple mode applies one gaze setup for a whole step; Granular mode applies percentage-based cues during a timed snippet step.

Targets can be an object, another actor, the main camera, forward, or off.

## Custom animations and Mixamo

`SnippetsActorRegistry` can store reusable custom clips. Both Simple and Flow controllers can play a custom animation by itself or combine it with snippet speech and facial playback.

For Mixamo animations:

1. Import the downloaded animation into Unity.
2. Open `Snippets > Extras > MixamoToSnippets`.
3. Assign the imported clip and run the conversion.
4. Add the converted clip under the actor's Custom Animations list in `SnippetsActorRegistry`.
5. Select `Custom Animation` or `Snippet + Custom Animation` in a controller.

For `Snippet + Custom Animation`, choose how the custom animation behaves:

- `Play Once Then Idle`: plays one cycle, then returns the body to idle even if the snippet is still speaking
- `Loop Until Snippet Ends`: keeps looping, then cross-fades to idle when snippet playback stops

The playback choice is separate from Completion, which controls when the controller or flow advances. You can also apply a snippet mask:

- None
- Head Only
- Upper Body
- Face Only

## Template and playback modes

The default template is:

`Packages/com.snippets.sdk/Editor/SnippetsTemplates/SnippetTemplate.prefab`

Copy it into the project before customizing it.

Its main components are:

- root `SnippetPlayer`
- `SnippetTmpTextPlayer`
- `SnippetAudioSourceSoundPlayer`
- `SnippetAvatarAnimatorPlayer`

The template is a starting point. You can customize TMP styling, audio source and mixer settings, avatar visuals, and playback behavior while keeping the root `SnippetPlayer` orchestration.

`SnippetTmpTextPlayer` supports:

- `FullText`
- `HighlightAsSpoken`
- `BuildUpText`
- `RollingSubtitles`
- `ScreenSubtitles`
- `TwoLineSubtitles`
- `Typewriter`

Text can progress continuously or reset by sentence.

## Recommended validation path

1. Install the SDK.
2. Import TMP essentials.
3. Import the sample scenes.
4. Log in.
5. Import one published Snippet Set.
6. Drag one generated prefab into a scene and validate standalone playback.
7. Run `Snippets > Scene Auto Setup`.
8. Validate one action with `SnippetsSimpleController`.
9. Add a short flow of two or three steps.
10. Add movement and gaze only after basic playback and sequencing work.

## Troubleshooting

### Import or Update is unavailable

The set may still be processing or unpublished. Check its status label and confirm publishing completed in the web portal.

### Generated prefabs are missing

Check `ProjectSnippetsSettings`, then look under:

`Assets/My Snippets/<SetName>`

### Text is missing or malformed

Import TMP essentials and confirm the selected template contains `SnippetTmpTextPlayer`.

### Audio or animation is missing

Confirm the template contains its audio and avatar players, then inspect the Console for import or playback errors.

### Gaze is not working

Confirm the actor has `SnippetsGazeDriver`, run its Inspector auto-setup, and verify the head, waist, and optional eye references. For flow-driven gaze, assign both the Flow Controller and Actor Registry, then run `Sync Now`.

### Scene links changed after update

Stable sync preserves existing prefab assets when Snippet IDs match. Renamed, removed, or structurally changed content still requires reference review.

### GLTF package conflict

Check whether another package also owns GLTF import and resolve importer conflicts before debugging Snippets playback.

## Complete documentation

See the [current Snippets documentation](https://docs.snippets3d.com/latest/) for the web workflow and the complete Unity SDK guide.
