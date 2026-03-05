# Snippets SDK

## How to install
Follow these steps to install the Snippets SDK:

### 1. Add OpenUPM to the scoped registries of your project

Open the `Packages/manifest.json` file in the folder of your project and add the following lines 
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

### 2. Add the Snippets SDK to your project folders

Unzip the ZIP file of the Snippets SDK we provided to you. If you want, move it to a folder in your project (NOT in the `Assets` folder).

### 3. Import the Snippets SDK through the package manager

Open the Unity Package Manager (`Window` -> `Package Manager`) and click on the `+` button in the top left corner. Select `Add package from disk...` and navigate to the `package.json` file in the Snippets SDK folder you just added. 

Wait for the package manager to do all the heavy lifting. It may take a few minutes.

### 4. Import the TextMeshPro Essentials

If you don't have TextMeshPro in your project, you can import it by clicking on the `Import TMP Essentials` button in the `Window` -> `TextMeshPro` -> `Import TMP Essential Resources` menu.

### 5. Be careful of the conflicts
The Snippets SDK uses UnityGLTF to load GLTF files, and it may conflict with other packages that import GLTF files, like glTFast.  

## How to use
Once the Snippets SDK is installed, you can immediately start to use it create your own snippets. This is a quick guide to get you started:

### 1. Create your Snippet Sets on the web portal
Go to the [Snippets web portal](https://app.snippets3d.com/) and create your own Snippet Sets. The Unity SDK does not allow the creation of snippets, but only the importing of snippets created on the web portal. Refer to the Snippets documentation to learn how to create your own Snippet Sets.

### 2. Create your own Snippet template
A Snippet is basically a moving avatar saying a sentence. As a developer, you can create your own Snippet template that will define how the Snippets of the Snippets Set you created online are displayed in your Unity project. This is a very flexible system that allows you, the developer, to customize how you want your Snippets to be shown to the user.

For every Snippet that you will generate, the system will create a prefab variant of a template prefab. You can find a sample template prefab to start from in the folder `Packages/com.snippets.sdk/Editor/SnippetsTemplates/SnippetTemplate.prefab`. You have to copy this prefab and paste it into your Asset folder so that you can modify it according to your needs. 

This is the structure of the base prefab:
- The child `TextPlayer`, which contains the script `SnippetTmpTextPlayer`, is responsible for showing the text of the Snippet. This is particularly important for accessibility purposes, as it allows the Snippet to be read by screen readers. The script has a parameter, `Disable Text When Not Playing` that allows you to show the text only when the character is speaking, and hide it otherwise. Its child, `TextLabel`, has a TextMeshPro component that you can modify to change the text style as you want. Notice that if you want, you can also change the whole logic of the text management, by not using the `SnippetTmpTextPlayer` script, but using your own script that inherits from the base class `SnippetTextPlayer` and that manages the text display.
- The child `AudioPlayer`, which contains the script `SnippetAudioSourceSoundPlayer`, is responsible for playing the audio of the Snippet. The script plays the audio through the Unity Audio System, and it is associated to an AudioSource contained in the child `AudioSource`. Also in this case, you can change the audio settings of the AudioSource as you want. Notice that you can also change the whole logic of the audio management, by not using the `SnippetAudioSourceSoundPlayer` script, but using your own script that inherits from the base class `SnippetSoundPlayer` and that manages the audio playback.
- The child `AvatarPlayer`, which contains the script `SnippetAvatarAnimatorPlayer`, is responsible for showing the avatar of the Snippet and playing its animations. In the default template there is a child of this element that has the very telling name `Substitute-this-with-your-avatar`. You can choose to do two things with this child: you can do nothing, and in that case, the system will try to take the avatar from the data saved on the server; or you can substitute this gameobject with an avatar featuring a skinned mesh render and a bone structure compatible with the avatar you selected online. What happens is that in the first case, the generated Snippets will have the avatar you selected online, while in the second case you will have the avatar that you put as a child of this gameobject. Notice that if you want, you can also change the whole logic of the avatar management, by not using the `SnippetAvatarAnimatorPlayer` script, but using your own script that inherits from the base class `SnippetAvatarPlayer` and that manages the avatar animation display.
- The main root object `SnippetTemplate` has the script `SnippetPlayer` that is responsible for the coordination of all the children gameobjects. It ensures that the avatar animation is playing together with the audio file and the text writing. You should assign to the field `Snippet Text Player`, `Snippet Sound Player`, and `Snippet Avatar Player` the corresponding children of the prefab. You can also not assign some of them if you don't want that playback feature to be used: for instance, if you leave the `Snippet Sound Player` to null, the avatar will show the animation and the text, but have no sound. This allows for full flexibility of visualization. There is an additional parameter, `Play On Enable`, that allows you to choose if the Snippet should start playing as soon as it is instantiated, or if it should wait for a manual call to the `Play()` method to start playing. This is useful if you want to instantiate the Snippet prefab and then play it later, for instance when the user clicks on a button.

If you are in doubt about how to create your own Snippet template, you can start by using the default one.

### 3. Configure the folders
There is a small configuration file called `ProjectSnippetsSettings` in the `/SnippetsSDK/Config/Resources` folder. Modifying it allows you to change the default folders where the Snippets SDK operates. There are two main folders you can configure:
- **Raw Snippets Downlaod Folder**: This is the folder where the raw data of the Snippets will be downloaded. For instance, all the audio file and animation files will be saved here. Usually you don't touch the files in this folder
- **Generated Snippets Download Folder**: This is the folder where the Snippet prefabs will be generated. This is where you will look for your prefabs of the Snippets to add to your project.

In both folders, the system will create a subfolder with the name of the Snippet Set you are importing, and inside it will save all the files related to that Snippet Set.

### 4. Log in to Snippets
To use the Snippets SDK, you need to log in to your Snippets account. You can do this by opening the `Snippets` menu in Unity and clicking on `Log In`. This will open a small UI window where you can enter your credentials. This is fundamental so that you can access from Unity the Snippets that you created on the web portal.

### 5. Access to your list of Snippet Sets
Once you are logged in, you can access your list of Snippet Sets by opening the `Snippets` menu in Unity and clicking on `Import or Update Snippet Sets`. This will open a window where you can see all the Snippet Sets you created on the web portal and what is their current importing status. 

Every Snippet Set will appear in the list with its thumbnail and its name. After the name, there may be some tag in square brackets that indicates some special status of the Snippet Set:
- **[Processing]** means that the Snippet Set is being processed by the server and it is not yet ready to be used yet
- **[Imported]** means that the Snippet Set has already been successfully imported in your project and you can use it
- **[Local Deprecated]** means that the Snippet Set has already been successfully imported in your project, but it has actually been deleted on the cloud (so only the local version of the Snippet Set exists)

Every Snippet Set may have close to it also one or multiple buttons that represent the actions you can perform on it:
- **Import** means that the Snippet Set is not yet imported in your project, and you can click on this button to start the import process
- **Update** means that the Snippet Set has been imported in your project but there is an updated version on the cloud. You can click on this button to update the local version of the Snippet Set
- **Remove** means that the Snippet Set has been imported in your project, and you can click on this button to delete the local version of it

### 6. Perform operations on the Snippet Sets
Once you have your Snippet Sets list, you can perform some operations on them directly from the Snippet Sets window. The operations you can perform are:

#### Import a Snippet Set
To import a Snippet Set, you can click on the `Import` button next to the Snippet Set you want to import. The system will show you a window asking what is the template prefab you want to use for the Snippets of that Snippet Set. You can choose the prefab you created in the previous step, or you can create a new one. Once you select the prefab, you can click the `Import` button and the system will start the import process, at first downloading the raw data of the Snippets, and then generating the prefabs of the Snippets in the folder you configured in the previous step. If everything went smoothly, you will have the Snippet prefabs generated in the folder you configured. If something went wrong, you will see an error message in the console.

#### Remove a Snippet Set
To remove a Snippet Set, you can click on the `Remove` button next to the Snippet Set you want to remove. The system will ask you to confirm the removal in a dialogue window, and once you confirm it, it will delete the local version of the Snippet Set, both the raw data and the generated prefabs. If you were using the Snippet prefabs in some scene, they will become invalid. Notice that if the Snippet Set was imported from the cloud, it will still exist on the cloud, but it will not be available in your project anymore.

#### Update a Snippet Set
To update a Snippet Set, you can click on the `Update` button next to the Snippet Set you want to update. Notice that the button will appear only if there is an updated version of the Snippet Set on the cloud. The dialogue window for the Update process is mostly identical to the one of the Import process. Be careful that in the current implementation, the Update process consists in removing the local version of the Snippet Set and then importing it again from the cloud. This means that you will lose any changes you made to the Snippet prefabs, and you will also lose any reference you had to your prefabs in your scenes, so be careful when using this feature. We plan to improve the update process to make it less disruptive in a future version of our SDK.

### 7. Use the Snippets in your project
Once you have imported the Snippets Sets you want to use, you can start using the Snippets in your project. You can do this by instantiating the Snippet prefab you generated in the previous step. The Snippet prefab will have the `SnippetPlayer` component that you can use to control the playback of the Snippet. You can call the `Play()` method to start playing the Snippet, and you can also stop it by calling the `Stop()` method. The `SnippetPlayer` component also has two events: `PlaybackStarted` and `PlaybackStopped` that you can use to perform actions when the playback starts or finishes: for instance, you can start the playback of a second Snippet when the first one finishes, or you can show a UI message when the playback starts, etc...

### 8. Enjoy the Snippets SDK!
You now know how to use the Snippets SDK in your project. Have fun!
