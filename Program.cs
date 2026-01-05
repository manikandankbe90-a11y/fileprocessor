using System.Text;
using System.Text.RegularExpressions;

namespace FileProcessor;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║              FILE PROCESSOR - VERSION & GUID UPDATER         ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();

        // Get folder path from user
        Console.Write("Enter folder path (or press Enter for current directory): ");
        var folderPath = Console.ReadLine()?.Trim();
        
        if (string.IsNullOrEmpty(folderPath))
        {
            folderPath = Directory.GetCurrentDirectory();
        }

        if (!Directory.Exists(folderPath))
        {
            Console.WriteLine($"Error: Folder '{folderPath}' does not exist.");
            WaitForExit();
            return;
        }

        // Get the application's base directory (where the exe is located)
        var appBasePath = AppDomain.CurrentDomain.BaseDirectory;
        
        // Check for FileList.txt in the application folder (one-time setup)
        var fileListPath = Path.Combine(appBasePath, "FileList.txt");
        string[] filesToProcess;
        string[] xmlFiles;
        string[] csFiles;
        
        try
        {
            if (File.Exists(fileListPath))
            {
                // Read file names from FileList.txt in app folder
                var fileNames = File.ReadAllLines(fileListPath)
                    .Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => line.Trim())
                    .ToArray();
                
                // Search for each file in all folders and subfolders
                var foundFiles = new List<string>();
                foreach (var fileName in fileNames)
                {
                    var matchingFiles = Directory.GetFiles(folderPath, fileName, SearchOption.AllDirectories);
                    foundFiles.AddRange(matchingFiles);
                }
                filesToProcess = foundFiles.ToArray();
                
                xmlFiles = filesToProcess.Where(f => f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)).ToArray();
                csFiles = filesToProcess.Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)).ToArray();
                
                Console.WriteLine($"\nReading file list from FileList.txt (app folder)...");
                Console.WriteLine($"Found {filesToProcess.Length} file(s) to process:");
                foreach (var file in filesToProcess)
                {
                    Console.WriteLine($"  - {file}");
                }
            }
            else
            {
                // Fallback to scanning directory
                xmlFiles = Directory.GetFiles(folderPath, "*.xml", SearchOption.AllDirectories);
                csFiles = Directory.GetFiles(folderPath, "*.cs", SearchOption.AllDirectories);
                filesToProcess = xmlFiles.Concat(csFiles).ToArray();
                
                Console.WriteLine($"\nNo FileList.txt found in app folder. Scanning target directory...");
                Console.WriteLine($"Found {xmlFiles.Length} XML file(s) and {csFiles.Length} CS file(s):");
                foreach (var file in xmlFiles)
                {
                    Console.WriteLine($"  - {Path.GetFileName(file)}");
                }
                foreach (var file in csFiles)
                {
                    Console.WriteLine($"  - {Path.GetFileName(file)}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading folder: {ex.Message}");
            WaitForExit();
            return;
        }

        if (xmlFiles.Length == 0 && csFiles.Length == 0)
        {
            Console.WriteLine("No XML or CS files found to process.");
            WaitForExit();
            return;
        }
        Console.WriteLine();

        // Get new version number
        Console.Write("Enter new version number: ");
        var newVersion = Console.ReadLine()?.Trim();
        
        if (string.IsNullOrEmpty(newVersion))
        {
            Console.WriteLine("Version cannot be empty.");
            WaitForExit();
            return;
        }

        // Generate new GUID
        var newGuid = Guid.NewGuid().ToString();
        Console.WriteLine($"\nGenerated new GUID: {newGuid}");

        // Update version in all XML files
        Console.WriteLine("\nUpdating XML files (Version)...\n");
        int xmlFilesModified = 0;

        foreach (var file in xmlFiles)
        {
            try
            {
                var content = File.ReadAllText(file, Encoding.UTF8);
                var pattern = @"<Version>(.*?)</Version>";
                var replacement = $"<Version>{newVersion}</Version>";
                var regex = new Regex(pattern, RegexOptions.IgnoreCase);
                
                if (regex.IsMatch(content))
                {
                    var oldVersion = regex.Match(content).Groups[1].Value;
                    content = regex.Replace(content, replacement);
                    File.WriteAllText(file, content, Encoding.UTF8);
                    xmlFilesModified++;
                    Console.WriteLine($"  [OK] {Path.GetFileName(file)} - {oldVersion} -> {newVersion}");
                }
                else
                {
                    Console.WriteLine($"  [--] {Path.GetFileName(file)} - No <Version> tag found");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [ERR] {Path.GetFileName(file)} - {ex.Message}");
            }
        }

        // Update ID in all CS files
        Console.WriteLine("\nUpdating CS files (ID)...\n");
        int csFilesModified = 0;

        foreach (var file in csFiles)
        {
            try
            {
                var content = File.ReadAllText(file, Encoding.UTF8);
                bool modified = false;

                // Pattern to match: const string ID = "..."
                var idPattern = @"(const\s+string\s+ID\s*=\s*"")[^""]*("")";
                var idReplacement = $"$1{newGuid}$2";
                var idRegex = new Regex(idPattern);
                
                if (idRegex.IsMatch(content))
                {
                    var oldGuid = Regex.Match(content, @"const\s+string\s+ID\s*=\s*""([^""]*)""").Groups[1].Value;
                    content = idRegex.Replace(content, idReplacement);
                    modified = true;
                    Console.WriteLine($"  [OK] {Path.GetFileName(file)} - ID updated: {oldGuid} -> {newGuid}");
                }

                // Check if file has versionnew field - if so, add new version field and update versionnew
                var versionnewPattern = @"public\s+const\s+string\s+versionnew\s*=\s*(\w+)\s*;";
                var versionnewMatch = Regex.Match(content, versionnewPattern);
                
                if (versionnewMatch.Success)
                {
                    // Create new field name from version (e.g., "1.0.1" -> "version101")
                    var newFieldName = "version" + newVersion.Replace(".", "");
                    
                    // Find the line with private const string version = "..."
                    var versionFieldPattern = @"(private\s+const\s+string\s+version\s*=\s*""[^""]*""\s*;)";
                    var versionFieldMatch = Regex.Match(content, versionFieldPattern);
                    
                    if (versionFieldMatch.Success)
                    {
                        // Add new version field after the existing version field
                        var newVersionField = $"\n        private const string {newFieldName} = \"{newVersion}\";";
                        content = Regex.Replace(content, versionFieldPattern, $"$1{newVersionField}");
                        
                        // Update versionnew to reference the new field
                        content = Regex.Replace(content, versionnewPattern, $"public const string versionnew = {newFieldName};");
                        
                        modified = true;
                        Console.WriteLine($"  [OK] {Path.GetFileName(file)} - Added {newFieldName} = \"{newVersion}\" and updated versionnew");
                    }
                }

                // Check if file has versionXtoY methods (TestApp3.cs pattern)
                // Look for pattern like: public static string version3to4()
                var versionMethodPattern = @"public\s+static\s+string\s+version(\d+)to(\d+)\s*\(\)";
                var versionMethodMatches = Regex.Matches(content, versionMethodPattern);
                
                if (versionMethodMatches.Count > 0)
                {
                    // Get the last versionXtoY method numbers
                    int lastFrom = 0;
                    int lastTo = 0;
                    foreach (Match m in versionMethodMatches)
                    {
                        int from = int.Parse(m.Groups[1].Value);
                        int to = int.Parse(m.Groups[2].Value);
                        if (to > lastTo)
                        {
                            lastFrom = from;
                            lastTo = to;
                        }
                    }
                    
                    // Create new method name (e.g., version4to5)
                    var newMethodName = $"version{lastTo}to{lastTo + 1}";
                    
                    // Check if method already exists
                    if (!content.Contains($"public static string {newMethodName}()"))
                    {
                        // Find the last closing brace of a versionXtoY method and add new method after it
                        var lastMethodPattern = $@"(public\s+static\s+string\s+version{lastFrom}to{lastTo}\s*\(\)\s*\{{\s*return\s+test\.version{lastFrom}to{lastTo}\.ToString\(\);\s*\}})";
                        var newMethod = $"\n\n        public static string {newMethodName}()\n        {{\n            return test.{newMethodName}.ToString();\n        }}";
                        
                        content = Regex.Replace(content, lastMethodPattern, $"$1{newMethod}");
                        modified = true;
                        Console.WriteLine($"  [OK] {Path.GetFileName(file)} - Added method {newMethodName}()");
                    }
                    
                    // Check if file has run() calls - add new run() line
                    var lastRunPattern = $@"(run\(version{lastFrom}to{lastTo}\(\),\s*cs,\s*dn\);)";
                    if (Regex.IsMatch(content, lastRunPattern))
                    {
                        var newRunCall = $"\n            run({newMethodName}(), cs, dn);";
                        content = Regex.Replace(content, lastRunPattern, $"$1{newRunCall}");
                        modified = true;
                        Console.WriteLine($"  [OK] {Path.GetFileName(file)} - Added run({newMethodName}(), cs, dn);");
                    }
                }

                if (modified)
                {
                    File.WriteAllText(file, content, Encoding.UTF8);
                    csFilesModified++;
                }
                else
                {
                    Console.WriteLine($"  [--] {Path.GetFileName(file)} - No updates needed");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [ERR] {Path.GetFileName(file)} - {ex.Message}");
            }
        }

        // Update version history text file
        var versionHistoryFile = Path.Combine(folderPath, "VersionHistory.txt");
        if (File.Exists(versionHistoryFile))
        {
            try
            {
                var lines = File.ReadAllLines(versionHistoryFile).ToList();
                
                // Get the last versionXtoY entry (e.g., "version3to4.txt" -> 4)
                int lastVersionTo = 4; // default
                foreach (var line in lines)
                {
                    var match = Regex.Match(line, @"version(\d+)to(\d+)");
                    if (match.Success)
                    {
                        lastVersionTo = int.Parse(match.Groups[2].Value);
                    }
                }
                
                // Add new versionXtoY entry (e.g., "version4to5.txt")
                var newToEntry = $"version{lastVersionTo}to{lastVersionTo + 1}.txt";
                
                // Find the last "version N = " entry and get the version number
                int lastVersionNum = 0;
                foreach (var line in lines)
                {
                    var match = Regex.Match(line, @"version\s+(\d+)\s*=");
                    if (match.Success)
                    {
                        lastVersionNum = int.Parse(match.Groups[1].Value);
                    }
                }
                
                // Add new version number entry with the new version (e.g., version 4 = "1.0.3")
                var newVersionEntry = $"version {lastVersionNum + 1} = \"{newVersion}\"";
                
                // Find where to insert the new entries
                // Insert versionXtoY after the last versionXtoY line
                int lastToIndex = -1;
                int lastVersionNumIndex = -1;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (Regex.IsMatch(lines[i], @"version\d+to\d+"))
                    {
                        lastToIndex = i;
                    }
                    if (Regex.IsMatch(lines[i], @"version\s+\d+\s*="))
                    {
                        lastVersionNumIndex = i;
                    }
                }
                
                // Insert new entries at appropriate positions
                if (lastVersionNumIndex >= 0)
                {
                    lines.Insert(lastVersionNumIndex + 1, newVersionEntry);
                }
                else
                {
                    lines.Add(newVersionEntry);
                }
                
                if (lastToIndex >= 0)
                {
                    lines.Insert(lastToIndex + 1, newToEntry);
                }
                else
                {
                    lines.Insert(0, newToEntry);
                }
                
                File.WriteAllLines(versionHistoryFile, lines, Encoding.UTF8);
                Console.WriteLine($"\n[OK] VersionHistory.txt - Added: {newToEntry}");
                Console.WriteLine($"[OK] VersionHistory.txt - Added: {newVersionEntry}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERR] VersionHistory.txt - {ex.Message}");
            }
        }

        // Update resx file with new data entry
        var resxFile = Path.Combine(folderPath, "Resources.resx");
        if (File.Exists(resxFile))
        {
            try
            {
                var content = File.ReadAllText(resxFile, Encoding.UTF8);
                
                // Get the new versionXtoY.txt name from VersionHistory
                var versionHistoryLines = File.Exists(versionHistoryFile) 
                    ? File.ReadAllLines(versionHistoryFile) 
                    : Array.Empty<string>();
                
                // Find the latest versionXtoY.txt entry
                string latestVersionToTxt = "version4to5.txt";
                foreach (var line in versionHistoryLines)
                {
                    var match = Regex.Match(line, @"(version\d+to\d+\.txt)");
                    if (match.Success)
                    {
                        latestVersionToTxt = match.Groups[1].Value;
                    }
                }
                
                // Get name without .txt extension
                var dataName = latestVersionToTxt.Replace(".txt", "");
                
                // Check if this entry already exists in resx
                if (!content.Contains($"name=\"{dataName}\""))
                {
                    // Add new data entry before </root>
                    // Format: <data name="version4to5"><value>version4to5.txt</value></data>
                    var newDataEntry = $"  <data name=\"{dataName}\" xml:space=\"preserve\">\n    <value>{latestVersionToTxt}</value>\n  </data>\n</root>";
                    content = content.Replace("</root>", newDataEntry);
                    File.WriteAllText(resxFile, content, Encoding.UTF8);
                    Console.WriteLine($"\n[OK] Resources.resx - Added: {dataName} = \"{latestVersionToTxt}\"");
                }
                else
                {
                    Console.WriteLine($"\n[--] Resources.resx - Entry {dataName} already exists");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERR] Resources.resx - {ex.Message}");
            }
        }

        // Update csproj file with new None Include entry
        var csprojFile = Path.Combine(folderPath, "Test.csproj");
        if (File.Exists(csprojFile))
        {
            try
            {
                var content = File.ReadAllText(csprojFile, Encoding.UTF8);
                
                // Get the new versionXtoY.txt name from VersionHistory
                var versionHistoryLines = File.Exists(versionHistoryFile) 
                    ? File.ReadAllLines(versionHistoryFile) 
                    : Array.Empty<string>();
                
                // Find the latest versionXtoY.txt entry
                string latestVersionToTxt = "version4to5.txt";
                foreach (var line in versionHistoryLines)
                {
                    var match = Regex.Match(line, @"(version\d+to\d+\.txt)");
                    if (match.Success)
                    {
                        latestVersionToTxt = match.Groups[1].Value;
                    }
                }
                
                // Check if this entry already exists in csproj
                if (!content.Contains($"Include=\"{latestVersionToTxt}\""))
                {
                    // Find the last None Include entry and add new one after it
                    var lastNonePattern = @"(<None Include=""version\d+to\d+\.txt""\s*/>)";
                    var matches = Regex.Matches(content, lastNonePattern);
                    
                    if (matches.Count > 0)
                    {
                        var lastMatch = matches[matches.Count - 1].Value;
                        var newNoneEntry = $"{lastMatch}\n    <None Include=\"{latestVersionToTxt}\" />";
                        content = content.Replace(lastMatch, newNoneEntry);
                        File.WriteAllText(csprojFile, content, Encoding.UTF8);
                        Console.WriteLine($"\n[OK] Test.csproj - Added: <None Include=\"{latestVersionToTxt}\" />");
                    }
                }
                else
                {
                    Console.WriteLine($"\n[--] Test.csproj - Entry {latestVersionToTxt} already exists");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERR] Test.csproj - {ex.Message}");
            }
        }

        // Update bat file with new echo line
        var batFile = Path.Combine(folderPath, "update.bat");
        if (File.Exists(batFile))
        {
            try
            {
                var lines = File.ReadAllLines(batFile).ToList();
                
                // Get the last versionXtoY entry numbers from VersionHistory
                int lastFrom = 3;
                int lastTo = 4;
                var versionHistoryLines = File.Exists(versionHistoryFile) 
                    ? File.ReadAllLines(versionHistoryFile) 
                    : Array.Empty<string>();
                
                foreach (var line in versionHistoryLines)
                {
                    var match = Regex.Match(line, @"version(\d+)to(\d+)");
                    if (match.Success)
                    {
                        lastFrom = int.Parse(match.Groups[1].Value);
                        lastTo = int.Parse(match.Groups[2].Value);
                    }
                }
                
                // Create new echo line
                // Format: echo sccript 4 versiontext 5 -v Install4.log version 1.3 cmd update4to5.txt
                var newEchoLine = $"echo sccript {lastTo} versiontext {lastTo + 1} -v Install{lastTo}.log version {newVersion} cmd update{lastTo}to{lastTo + 1}.txt";
                
                // Check if this line already exists
                if (!lines.Any(l => l.Contains($"update{lastTo}to{lastTo + 1}.txt")))
                {
                    lines.Add(newEchoLine);
                    File.WriteAllLines(batFile, lines, Encoding.UTF8);
                    Console.WriteLine($"\n[OK] update.bat - Added: {newEchoLine}");
                }
                else
                {
                    Console.WriteLine($"\n[--] update.bat - Entry for update{lastTo}to{lastTo + 1}.txt already exists");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERR] update.bat - {ex.Message}");
            }
        }

        // Update deploy.bat file with new echo line
        var deployBatFile = Path.Combine(folderPath, "deploy.bat");
        if (File.Exists(deployBatFile))
        {
            try
            {
                var lines = File.ReadAllLines(deployBatFile).ToList();
                
                // Get the last versionXtoY entry numbers from VersionHistory
                int lastFrom = 3;
                int lastTo = 4;
                var versionHistoryLines2 = File.Exists(versionHistoryFile) 
                    ? File.ReadAllLines(versionHistoryFile) 
                    : Array.Empty<string>();
                
                foreach (var line in versionHistoryLines2)
                {
                    var match = Regex.Match(line, @"version(\d+)to(\d+)");
                    if (match.Success)
                    {
                        lastFrom = int.Parse(match.Groups[1].Value);
                        lastTo = int.Parse(match.Groups[2].Value);
                    }
                }
                
                // Create new echo line
                // Format: echo deploy 4 versiontext 5 -v Deploy4.log version 1.3 cmd deploy4to5.txt
                var newEchoLine = $"echo deploy {lastTo} versiontext {lastTo + 1} -v Deploy{lastTo}.log version {newVersion} cmd deploy{lastTo}to{lastTo + 1}.txt";
                
                // Check if this line already exists
                if (!lines.Any(l => l.Contains($"deploy{lastTo}to{lastTo + 1}.txt")))
                {
                    lines.Add(newEchoLine);
                    File.WriteAllLines(deployBatFile, lines, Encoding.UTF8);
                    Console.WriteLine($"\n[OK] deploy.bat - Added: {newEchoLine}");
                }
                else
                {
                    Console.WriteLine($"\n[--] deploy.bat - Entry for deploy{lastTo}to{lastTo + 1}.txt already exists");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERR] deploy.bat - {ex.Message}");
            }
        }

        // Update SQL file with new GUID
        var sqlFile = Path.Combine(folderPath, "InsertData.sql");
        if (File.Exists(sqlFile))
        {
            try
            {
                var content = File.ReadAllText(sqlFile, Encoding.UTF8);
                
                // Pattern to match GUID in INSERT statements
                var guidPattern = @"VALUES\s*\(\s*'([a-fA-F0-9\-]{36})'";
                
                if (Regex.IsMatch(content, guidPattern))
                {
                    var oldGuid = Regex.Match(content, guidPattern).Groups[1].Value;
                    content = Regex.Replace(content, @"'[a-fA-F0-9\-]{36}'", $"'{newGuid}'");
                    File.WriteAllText(sqlFile, content, Encoding.UTF8);
                    Console.WriteLine($"\n[OK] InsertData.sql - GUID updated: {oldGuid} -> {newGuid}");
                }
                else
                {
                    Console.WriteLine($"\n[--] InsertData.sql - No GUID found to update");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERR] InsertData.sql - {ex.Message}");
            }
        }

        // Update VersionInsert.sql file with new GUID and version number
        var versionInsertSqlFile = Path.Combine(folderPath, "VersionInsert.sql");
        if (File.Exists(versionInsertSqlFile))
        {
            try
            {
                var content = File.ReadAllText(versionInsertSqlFile, Encoding.UTF8);
                
                // Get the last version number from VersionHistory
                int lastVersionTo = 4;
                var versionHistoryLines3 = File.Exists(versionHistoryFile) 
                    ? File.ReadAllLines(versionHistoryFile) 
                    : Array.Empty<string>();
                
                foreach (var line in versionHistoryLines3)
                {
                    var match = Regex.Match(line, @"version(\d+)to(\d+)");
                    if (match.Success)
                    {
                        lastVersionTo = int.Parse(match.Groups[2].Value);
                    }
                }
                
                // Update GUID
                content = Regex.Replace(content, @"'[a-fA-F0-9\-]{36}'", $"'{newGuid}'");
                
                // Update version number (e.g., 4 -> 5)
                var versionNumPattern = @"(VALUES\s*\('[^']+',\s*)(\d+)(,)";
                content = Regex.Replace(content, versionNumPattern, $"$1{lastVersionTo + 1}$3");
                
                File.WriteAllText(versionInsertSqlFile, content, Encoding.UTF8);
                Console.WriteLine($"\n[OK] VersionInsert.sql - GUID updated to: {newGuid}");
                Console.WriteLine($"[OK] VersionInsert.sql - VersionNum updated to: {lastVersionTo + 1}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERR] VersionInsert.sql - {ex.Message}");
            }
        }

        // Create new SQL file from SourceQueries.sql template (from app folder)
        var sourceQueriesFile = Path.Combine(appBasePath, "SourceQueries.sql");
        if (File.Exists(sourceQueriesFile))
        {
            try
            {
                // Get the last version number from VersionHistory
                int lastVersionTo = 4;
                var versionHistoryLines4 = File.Exists(versionHistoryFile) 
                    ? File.ReadAllLines(versionHistoryFile) 
                    : Array.Empty<string>();
                
                foreach (var line in versionHistoryLines4)
                {
                    var match = Regex.Match(line, @"version(\d+)to(\d+)");
                    if (match.Success)
                    {
                        lastVersionTo = int.Parse(match.Groups[2].Value);
                    }
                }
                
                // Read source content
                var content = File.ReadAllText(sourceQueriesFile, Encoding.UTF8);
                
                // Update GUID
                content = Regex.Replace(content, @"'[a-fA-F0-9\-]{36}'", $"'{newGuid}'");
                
                // Update version number
                content = Regex.Replace(content, @"VersionNum = \d+", $"VersionNum = {lastVersionTo + 1}");
                content = Regex.Replace(content, @"VersionNum,\s*\d+,", $"VersionNum, {lastVersionTo + 1},");
                
                // Update version text (e.g., version3to4 -> version4to5)
                content = Regex.Replace(content, @"version\d+to\d+", $"version{lastVersionTo}to{lastVersionTo + 1}");
                
                // Create new file with version name
                var newFileName = $"Queries_version{lastVersionTo}to{lastVersionTo + 1}.sql";
                var newFilePath = Path.Combine(folderPath, newFileName);
                File.WriteAllText(newFilePath, content, Encoding.UTF8);
                Console.WriteLine($"\n[OK] Created: {newFileName} (copied from SourceQueries.sql)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERR] SourceQueries.sql - {ex.Message}");
            }
        }

        // Update ProdVersion.txt - increment prod_version
        var prodVersionFile = Path.Combine(folderPath, "ProdVersion.txt");
        if (File.Exists(prodVersionFile))
        {
            try
            {
                var content = File.ReadAllText(prodVersionFile, Encoding.UTF8);
                
                // Pattern to match prod_version=X
                var match = Regex.Match(content, @"prod_version=(\d+)");
                if (match.Success)
                {
                    int currentVersion = int.Parse(match.Groups[1].Value);
                    int newProdVersion = currentVersion + 1;
                    content = Regex.Replace(content, @"prod_version=\d+", $"prod_version={newProdVersion}");
                    File.WriteAllText(prodVersionFile, content, Encoding.UTF8);
                    Console.WriteLine($"\n[OK] ProdVersion.txt - prod_version updated: {currentVersion} -> {newProdVersion}");
                }
                else
                {
                    Console.WriteLine($"\n[--] ProdVersion.txt - No prod_version found");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[ERR] ProdVersion.txt - {ex.Message}");
            }
        }

        Console.WriteLine($"\nDone! Updated {xmlFilesModified} XML file(s) and {csFilesModified} CS file(s).");
        Console.WriteLine($"GUID used: {newGuid}");
        WaitForExit();
    }

    static void WaitForExit()
    {
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}
