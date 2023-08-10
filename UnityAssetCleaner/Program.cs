using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.GZip;

string[] arguments = Environment.GetCommandLineArgs().Skip(1).ToArray();
bool keepScripts = false;
string filename = "";
foreach (string arg in arguments)
{
    if (arg.ToLower() == "--keepscripts")
    {
        keepScripts = true;
    }
    else
    {
        filename = arg;
    }
}
if (filename.Length == 0)
{
    Console.WriteLine("Usage: UnityAssetCleaner [filename] (--keepScripts)");
    Environment.Exit(0);
}

if (!File.Exists(filename))
{
    Console.WriteLine($"'{filename}' doesn't exit");
    Environment.Exit(0);
}

if (!filename.EndsWith(".unitypackage"))
{
    Console.WriteLine($"'{filename}' is not a unitypackage");
    Environment.Exit(0);
}

var tempFolder = Path.Combine(Path.GetTempPath(), "tmp_" + Path.GetFileNameWithoutExtension(filename));

if (Directory.Exists(tempFolder))
    Directory.Delete(tempFolder, true);
Directory.CreateDirectory(tempFolder);

Console.WriteLine("Extracting unitypackage...");
ExtractTGZ(filename, tempFolder);
Console.WriteLine("Processing extracted unitypackage...");
List<string> deletedFiles = ProcessExtracted(tempFolder);
if (deletedFiles.Count == 0)
{
    Console.WriteLine("unitypackage is already clean.");
    Directory.Delete(tempFolder, true);
    Environment.Exit(0);
}
    foreach (string file in deletedFiles)
{
    Console.WriteLine($"Removed: {file}");
}
Console.WriteLine($"Removed {deletedFiles.Count} Files");
Console.WriteLine("Creating unitypackage...");
CreateUnityPackage(tempFolder, $"{Path.GetFileNameWithoutExtension(filename)}-clean.unitypackage");
Directory.Delete(tempFolder, true);

void ExtractTGZ(string gzArchiveName, string destFolder)
{
    Stream inStream = File.OpenRead(gzArchiveName);
    Stream gzipStream = new GZipInputStream(inStream);

    TarArchive tarArchive = TarArchive.CreateInputTarArchive(gzipStream, System.Text.Encoding.ASCII);
    tarArchive.ExtractContents(destFolder);
    tarArchive.Close();

    gzipStream.Close();
    inStream.Close();
}

void CreateUnityPackage(string folder, string filename)
{
    Stream outStream = File.Create(filename);
    Stream gzoStream = new GZipOutputStream(outStream);
    TarArchive tarArchive = TarArchive.CreateOutputTarArchive(gzoStream);

    tarArchive.RootPath = folder.Replace("\\\\", "/");
    if (tarArchive.RootPath.EndsWith("/"))
        tarArchive.RootPath = tarArchive.RootPath.Remove(tarArchive.RootPath.Length - 1);

    AddDirectoryFilesToTar(tarArchive, folder, true, true);

    tarArchive.Close();
}

List<string> ProcessExtracted(string tempFolder)
{
    List<string> deletedFiles = new List<string>();
    foreach (string d in Directory.EnumerateDirectories(tempFolder))
    {
        if (File.Exists(Path.Combine(d, "pathname"))) {
            string filepath = File.ReadAllText(Path.Combine(d, "pathname"));
            string extension = Path.GetExtension(filepath);
            if (extension == ".dll" || extension == ".exe" || (extension == ".cs" && !keepScripts))
            {
                deletedFiles.Add(filepath);
                Directory.Delete(d, true);
            }
        }
    }
    return deletedFiles;
}

void AddDirectoryFilesToTar(TarArchive tarArchive, string sourceDirectory, bool recurse, bool isRoot)
{
    TarEntry tarEntry;

    if (!isRoot)
    {
        tarEntry = TarEntry.CreateEntryFromFile(sourceDirectory);
        tarArchive.WriteEntry(tarEntry, false);
    }

    string[] filenames = Directory.GetFiles(sourceDirectory);
    foreach (string filename in filenames)
    {
        tarEntry = TarEntry.CreateEntryFromFile(filename);
        tarArchive.WriteEntry(tarEntry, true);
    }

    if (recurse)
    {
        string[] directories = Directory.GetDirectories(sourceDirectory);
        foreach (string directory in directories)
            AddDirectoryFilesToTar(tarArchive, directory, recurse, false);
    }
}