# Snippets SDK

## Requirements
- Unity 2022.3 LTS or newer LTS versions, including Unity 6
- A Snippets account
- Internet access for package installation and cloud operations

## How to install
Choose one of the installation options below.

### Option A: AI-assisted manifest install (no OpenUPM required)

Use this option if you want Codex, Claude, Copilot, or another coding agent to update `Packages/manifest.json` directly.

This path adds all required packages from Git URLs, so it does not require the OpenUPM scoped registry setup.

```text
Install the Snippets SDK by updating `Packages/manifest.json`.

Use this exact SDK package dependency with the Git URL found at `https://app.snippets3d.com/integrations`:
- `"com.snippets.sdk": "<insert Git URL from the Snippets integrations page>"`

Also add these dependencies if missing:
- `"com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=/src/UniTask/Assets/Plugins/UniTask"`
- `"org.khronos.unitygltf": "https://github.com/KhronosGroup/UnityGLTF.git"`

Keep all existing dependencies unchanged.
```

### Option B: Unity Package Manager install

#### 1. Add OpenUPM to the scoped registries of your project

Open `Packages/manifest.json` and add the OpenUPM registry for the SDK dependencies:

```json
{
  "dependencies": {
    ...
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

#### 2. Install the Snippets SDK package

Recommended: install the SDK from Git URL through the Unity Package Manager.

1. Open `Window` -> `Package Manager`
2. Click the `+` button
3. Select `Add package from git URL...`
4. Paste the SDK Git URL from the Snippets integrations page: `https://app.snippets3d.com/integrations`
5. Confirm the installation

ZIP fallback:

1. Download and unzip the Snippets SDK outside your project's `Assets` folder
2. Open `Window` -> `Package Manager`
3. Click the `+` button
4. Select `Add package from disk...`
5. Pick the `package.json` file from the unzipped SDK folder

### After installation

#### 1. Import TextMeshPro essentials

If your project does not already include the TMP essentials, import them through:

`Window` -> `TextMeshPro` -> `Import TMP Essential Resources`

#### 2. Optional: import the sample scenes

The package includes sample scenes you can import from the `Samples` tab in the Package Manager:

1. Select `Snippets SDK` in the Package Manager
2. Open the `Samples` tab
3. Import `Snippets Sample Scenes`

#### 3. Dependency note

The Snippets SDK uses UnityGLTF to load GLTF files. It may conflict with other packages that also manage GLTF importing, such as glTFast.

## How to use
Once the SDK is installed, you can import Snippet Sets created on the Snippets web portal and generate ready-to-use Unity prefabs.

### 1. Create and publish your Snippet Sets on the web portal

Go to the [Snippets web portal](https://app.snippets3d.com/) and create your Snippet Sets there. The Unity SDK imports Snippet Sets created on the portal; it does not create them locally in Unity.

### 2. Create or reuse a Snippet template

Each imported Snippet is generated as a prefab variant of a template prefab. You can start from the default template at:

`Packages/com.snippets.sdk/Editor/SnippetsTemplates/SnippetTemplate.prefab`

Copy that prefab into your project if you want to customize it.

The base template contains these main parts:

- `TextPlayer` with `SnippetTmpTextPlayer` for subtitle or text display
- `AudioPlayer` with `SnippetAudioSourceSoundPlayer` for audio playback
- `AvatarPlayer` with `SnippetAvatarAnimatorPlayer` for avatar and animation playback
- `SnippetTemplate` root with `SnippetPlayer` coordinating the child players

You can replace or customize those parts as needed. For example, you can create your own text, audio, or avatar player classes by inheriting from the corresponding base classes.

The default `SnippetTmpTextPlayer` also supports multiple text and subtitle presentation styles, including full text, highlighted speech, build-up text, rolling subtitles, screen subtitles, two-line subtitles, and typewriter-style playback.

If you are unsure, start with the default template first.

### 3. Configure the project folders

The SDK settings asset is located at:

`SnippetsSDK/Config/Resources/ProjectSnippetsSettings`

By default:

- `Generated Snippets Download Folder` is `Assets/My Snippets`
- `Raw Snippets Download Folder` is `Assets/My Snippets/Raw`

Important behavior note:

- Generated prefabs are stored per Snippet Set under the generated root, for example `Assets/My Snippets/Doctor`
- Raw assets are stored inside each imported Snippet Set folder, using the raw folder name as a nested subfolder, for example `Assets/My Snippets/Doctor/Raw`
- If you change `Raw Snippets Download Folder`, the SDK uses its final folder name as the raw subfolder name inside each imported set

In practice, for a set named `Doctor`, the SDK creates a structure like:

- `Assets/My Snippets/Doctor`
- `Assets/My Snippets/Doctor/Raw`

### 4. Log in to Snippets

Open:

`Snippets` -> `Log In`

Then enter the same credentials you use on the Snippets web portal.

### 5. Open the Snippet Set management window

Open:

`Snippets` -> `Import or Update Snippet Sets`

This window shows the Snippet Sets associated with your account together with their current local or remote state.

Current status labels are:

- `[Imported]` for a set that is already imported locally
- `[Local Deprecated]` for a set that still exists locally but no longer exists remotely
- `[Processing/Unpublished]` for a remote set that is not downloadable yet
- `[Update Processing]` for an imported set whose updated remote version is still not downloadable yet

Current action buttons are:

- `Import` when the set is remote, downloadable, and not yet imported locally
- `Update` when the set is already imported and a newer downloadable version exists remotely
- `Remove` when the set is already imported locally

### 6. Import a Snippet Set

Click `Import` next to the set you want to bring into your project.

The import window lets you choose the Snippet template prefab to use. The SDK remembers the last template used during the current Unity session and falls back to the default template when available.

When you confirm the import, the SDK:

- downloads the raw Snippet Set archive
- extracts the raw files
- generates snippet prefabs from the selected template

If the operation succeeds, the generated prefabs appear in the configured generated folder for that set.

### 7. Update a Snippet Set

Click `Update` next to an imported set when a newer remote version is available.

The update flow uses the same template-selection window as import, but the SDK performs a stable sync instead of deleting and recreating the whole set:

- existing generated snippet prefabs are matched by Snippet ID and updated in place
- removed snippets are deleted
- newly added snippets are generated

This preserves scene and prefab references whenever the existing generated prefab asset is reused.

### 8. Remove a Snippet Set

Click `Remove` next to an imported set to delete its local data.

The SDK removes both:

- the generated prefab folder for that set
- the raw asset subfolder for that set

Removing a set only affects your local Unity project. It does not delete the Snippet Set from the Snippets cloud platform.

### 9. Use the generated Snippets in your project

After importing a set, open its generated folder and drag the generated snippet prefabs into your scene.

Each generated prefab contains a `SnippetPlayer` component. You can:

- call `Play()` to start playback
- call `Stop()` to stop playback
- listen to `PlaybackStarted`
- listen to `PlaybackStopped`

For more advanced runtime orchestration, the SDK also includes helper tools such as `SnippetsActorRegistry`, `SnippetsSimpleController`, `SnippetsFlowController`, `SnippetsWalker`, `SnippetsGazeDriver`, and `SnippetsGazeFlowController`.

## Enjoy the Snippets SDK
You are ready to import, update, and use Snippets in your Unity project.
