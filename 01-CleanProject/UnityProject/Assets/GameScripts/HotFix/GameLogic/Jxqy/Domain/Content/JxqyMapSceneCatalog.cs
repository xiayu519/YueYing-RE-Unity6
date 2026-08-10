using System;
using System.Collections.Generic;

namespace Jxqy.Domain.Content
{
    [Serializable]
    public sealed class JxqyMapSceneCatalog
    {
        public const string Address =
            "jxqy/scenes/map-scene-catalog.json";

        public string GeneratorVersion = string.Empty;
        public List<JxqyMapSceneEntry> Maps = new();
    }

    [Serializable]
    public sealed class JxqyMapSceneEntry
    {
        public string SceneKey = string.Empty;
        public string MapStableId = string.Empty;
        public string MapRelativePath = string.Empty;
        public string SceneAddress = string.Empty;
        public string SceneAssetPath = string.Empty;
        public int ColumnCount;
        public int RowCount;
    }
}
