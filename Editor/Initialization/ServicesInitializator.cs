using UnityEditor;
using UnityEngine;

namespace Snippets.Sdk
{
    /// <summary>
    /// Initialize the services on editor start or assemblies reload, so that all the SDK can use them
    /// </summary>
    [InitializeOnLoad]
    public static class ServicesInitializator
    {
        /// <summary>
        /// Static constructor called by Unity when the scripts are reloaded
        /// </summary>
        static ServicesInitializator()
        {
            // Initialize the services
            InitializeServices();
        }

        /// <summary>
        /// Initializes the services with the desired implementations
        /// </summary>
        private static void InitializeServices()
        {
            Services.SetService<IProjectSnippetsSettings>(new ProjectSnippetsSettingsService());
            Services.SetService<IBackendApiCaller>(new BackendApiCaller());
            Services.SetService<ILoginManager>(new BackendLoginManager());
            Services.SetService<ISnippetSetsProvider>(new BackendSnippetSetsProvider());

            //uncomment to use mock providers
            //Services.SetService<IBackendApiCaller>(new BackendApiCaller());
            //Services.SetService<ILoginManager>(new Mocks.MockLoginManager());
            //Services.SetService<ISnippetSetsProvider>(new Mocks.MockSnippetsSetProvider());

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