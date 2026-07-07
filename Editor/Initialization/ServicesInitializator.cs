using UnityEditor;
using UnityEngine;

namespace Snippets.Sdk
{
    [InitializeOnLoad]
    public static class ServicesInitializator
    {
        static ServicesInitializator()
        {
            InitializeServices();
        }

        private static void InitializeServices()
        {
            Services.SetService<IProjectSnippetsSettings>(new ProjectSnippetsSettingsService());
            Services.SetService<IBackendApiCaller>(new BackendApiCaller());
            Services.SetService<ILoginManager>(new BackendLoginManager());
            Services.SetService<ISnippetSetsProvider>(new BackendSnippetSetsProvider());

            Services.SetService<ISnippetsSetZipper>(new SnippetsSetZipper());  
            Services.SetService<IProjectSnippetsData>(ProjectSnippetsSoData.instance);
            Services.SetService<ISnippetsSetsManager>(
                new DefaultSnippetsSetsManager(
                    Services.GetService<IProjectSnippetsSettings>(), 
                    Services.GetService<ISnippetSetsProvider>(), 
                    Services.GetService<ISnippetsSetZipper>())
                );

            Debug.Log("[Snippets SDK] Services initialized");
        }
    }
}
