using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Snippets.Sdk
{
    /// <summary>
    /// Saves data for the Snippets imported in the project and makes it persist among sessions
    /// thanks to the use of a singleton scriptable object
    /// </summary>
    [FilePath("/Assets/_Snippets/Config/ProjectSnippetsData.asset", FilePathAttribute.Location.ProjectFolder)]
    public class ProjectSnippetsSoData : ScriptableSingleton<ProjectSnippetsSoData>, IProjectSnippetsData
    {
        /// <inheritdoc />
        [field: SerializeField]
        public string LastUsername { get; private set; }

        /// <inheritdoc />
        [field: SerializeField]
        public List<SnippetsSetMetadata> LocalSnippetSets { get; private set; } = new List<SnippetsSetMetadata>();

        /// <inheritdoc />
        public void SnippetsSetImported(SnippetsSetMetadata addedSnippetsSet, string username)
        {
            //remove the snippet set if it was already present
            LocalSnippetSets.RemoveAll(x => x.Id == addedSnippetsSet.Id);

            LocalSnippetSets.Add(addedSnippetsSet);
            LastUsername = username;
            Save(true);            
        }

        /// <inheritdoc />
        public void SnippetsSetRemoved(SnippetsSetMetadata removedSnippetsSet, string username)
        {
            LocalSnippetSets.RemoveAll(x => x.Id == removedSnippetsSet.Id);
            LastUsername = username;
            Save(true);
        }
    }
}