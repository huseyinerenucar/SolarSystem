using System.IO;
using UnityEngine;

public static class FileHelper
{
    public static string MakePath(params string[] folderNames)
    {
        return Path.Combine(folderNames);
    }

    public static string PersistantDataPath
    {
        get
        {
            return Application.persistentDataPath;
        }
    }

    public static void SaveBytesToFile(string path, string fileName, byte[] data, bool log = false)
    {
        string fullPath = Path.Combine(path, fileName + ".bytes");

        using (BinaryWriter writer = new(File.Open(fullPath, FileMode.Create)))
            writer.Write(data);

        if (log)
            Debug.Log("Saved data to: " + fullPath);
    }

    public static void SaveTextToFile(string path, string fileName, string fileExtension, string data, bool log = false)
    {
        if (fileExtension[0] != '.')
            fileExtension = "." + fileExtension;

        string fullPath = Path.Combine(path, fileName + fileExtension);

        SaveTextToFile(fullPath, data, log);
    }

    public static void SaveTextToFile(string fullPath, string data, bool log = false)
    {
        using (var writer = new StreamWriter(File.Open(fullPath, FileMode.Create)))
            writer.Write(data);

        if (log)
            Debug.Log("Saved data to: " + fullPath);
    }
}
