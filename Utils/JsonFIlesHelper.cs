using Newtonsoft.Json;
using System.IO;

namespace UniqueLootHelper.Utils;

/// <summary>
/// Utility class to help reading/writing json files.
/// </summary>
internal static class JsonFIlesHelper
{
    private static readonly JsonSerializerSettings _options = new()
    {
        Formatting = Formatting.Indented,
    };
    /// <summary>
    /// If file exists - load it. If not - create new file with default object and return it.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="file"></param>
    /// <returns></returns>
    public static T CreateOrLoadJsonFile<T>(FileInfo file) where T : new()
    {
        file.Refresh();
        file.Directory?.Create();
        if (file.Exists)
        {
            var content = File.ReadAllText(file.FullName);
            return JsonConvert.DeserializeObject<T>(content, _options) ?? new T();
        }
        T newObj = new();
        SaveJsonFile(file, newObj);

        return newObj;
    }
    /// <summary>
    /// Save object to json file. 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="file"></param>
    /// <param name="obj"></param>
    public static void SaveJsonFile<T>(FileInfo file, T obj)
    {
        var content = JsonConvert.SerializeObject(obj, _options);
        File.WriteAllText(file.FullName, content);
    }
}
