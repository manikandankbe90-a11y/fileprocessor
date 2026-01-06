foreach (var file in csFiles)
{
    try
    {
        var content = File.ReadAllText(file, Encoding.UTF8);
        bool modified = false;

        // Pattern for PolicyMigrator.cs: private const string Version313 = "3.13"; and LatestVersion = Version313
        var versionConstPattern = @"private\s+const\s+string\s+Version(\d+)\s*=\s*""([^""]*)""\s*;";
        var versionConstMatches = Regex.Matches(content, versionConstPattern);
        
        if (versionConstMatches.Count > 0)
        {
            // Get the last VersionXXX number
            int lastVersionNum = 0;
            string lastVersionLine = "";
            foreach (Match m in versionConstMatches)
            {
                int versionNum = int.Parse(m.Groups[1].Value);
                if (versionNum > lastVersionNum)
                {
                    lastVersionNum = versionNum;
                    lastVersionLine = m.Value;
                }
            }
            
            // Create new version const (e.g., Version314 = "3.14")
            int newVersionNum = lastVersionNum + 1;
            var newVersionConst = $"private const string Version{newVersionNum} = \"{newVersion}\";";
            
            // Check if new version already exists
            if (!content.Contains($"Version{newVersionNum}"))
            {
                // Add new version const after the last one
                var escapedLastLine = Regex.Escape(lastVersionLine);
                content = Regex.Replace(content, escapedLastLine, lastVersionLine + "\r\n        " + newVersionConst);
                
                // Update LatestVersion to reference the new version
                var latestVersionPattern = @"(public\s+const\s+string\s+LatestVersion\s*=\s*)Version\d+(\s*;)";
                content = Regex.Replace(content, latestVersionPattern, "${1}Version" + newVersionNum + "${2}");
                
                modified = true;
                Console.WriteLine($"  [OK] {Path.GetFileName(file)} - Added Version{newVersionNum} = \"{newVersion}\" and updated LatestVersion");
            }
        }
        
        // Pattern for UDS methods: public static string UDSVer26to27()
        var upgradeMethodPattern = @"public\s+static\s+string\s+UDSVer(\d+)to(\d+)\s*\(\)";
        var upgradeMethodMatches = Regex.Matches(content, upgradeMethodPattern);
        
        if (upgradeMethodMatches.Count > 0)
        {
            // Get the last UDSVerXtoY numbers
            int lastFrom = 0;
            int lastTo = 0;
            foreach (Match m in upgradeMethodMatches)
            {
                int from = int.Parse(m.Groups[1].Value);
                int to = int.Parse(m.Groups[2].Value);
                if (to > lastTo)
                {
                    lastFrom = from;
                    lastTo = to;
                }
            }
            
            // Create new method name (e.g., UDSVer27to28)
            var newMethodName = $"UDSVer{lastTo}to{lastTo + 1}";
            
            // Check if method already exists
            if (!content.Contains($"public static string {newMethodName}()"))
            {
                // Find the last UDSVer method and add new method after it
                var lastMethodPattern = $@"(public\s+static\s+string\s+UDSVer{lastFrom}to{lastTo}\s*\(\)\s*\{{\s*return\s+Resources\.UDSVer{lastFrom}to{lastTo}\.ToString\(\);\s*\}})";
                var newMethod = $"\r\n\r\n        public static string {newMethodName}()\r\n        {{\r\n            return Resources.{newMethodName}.ToString();\r\n        }}";
                
                content = Regex.Replace(content, lastMethodPattern, "$1" + newMethod);
                modified = true;
                Console.WriteLine($"  [OK] {Path.GetFileName(file)} - Added method {newMethodName}()");
            }
            
            // Check if file has runSqlScript() calls - add new runSqlScript() line
            var lastRunPattern = $@"(runSqlScript\(UDSVer{lastFrom}to{lastTo}\(\),\s*connectionString,\s*databaseName\);)";
            if (Regex.IsMatch(content, lastRunPattern))
            {
                if (!content.Contains($"runSqlScript({newMethodName}()"))
                {
                    var newRunCall = $"\r\n            runSqlScript({newMethodName}(), connectionString, databaseName);";
                    content = Regex.Replace(content, lastRunPattern, "$1" + newRunCall);
                    modified = true;
                    Console.WriteLine($"  [OK] {Path.GetFileName(file)} - Added runSqlScript({newMethodName}(), connectionString, databaseName);");
                }
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

// Update NSI files - && lines get new version, || lines get last version
var nsiFiles = filesToProcess.Where(f => f.EndsWith(".nsi", StringComparison.OrdinalIgnoreCase)).ToArray();
foreach (var nsiFile in nsiFiles)
{
    try
    {
        var content = File.ReadAllText(nsiFile, Encoding.UTF8);
        bool modified = false;
        
        // Find the last version comment (e.g., //3.11.0)
        var versionCommentPattern = @"//(\d+\.\d+\.?\d*)";
        var versionMatches = Regex.Matches(content, versionCommentPattern);
        
        string lastVersion = "";
        foreach (Match m in versionMatches)
        {
            lastVersion = m.Groups[1].Value;
        }
        
        if (!string.IsNullOrEmpty(lastVersion))
        {
            // Pattern for lines with && - add new version comment
            var andPattern = @"(\)\s*&&\s*)(//" + Regex.Escape(lastVersion) + @")";
            if (Regex.IsMatch(content, andPattern))
            {
                content = Regex.Replace(content, andPattern, "$1//" + newVersion);
                modified = true;
            }
            
            // Pattern for lines with || - add last version comment (keep as is or update to previous)
            // || lines should have the last version, && lines should have new version
            var orPattern = @"(\)\s*\|\|\s*)(//" + Regex.Escape(lastVersion) + @")";
            // || lines keep the last version, no change needed
        }
        
        if (modified)
        {
            File.WriteAllText(nsiFile, content, Encoding.UTF8);
            Console.WriteLine($"\n[OK] {Path.GetFileName(nsiFile)} - Updated && lines with version {newVersion}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n[ERR] {Path.GetFileName(nsiFile)} - {ex.Message}");
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

// Update resx files with new UDSVerXtoY data entry
var resxFiles = filesToProcess.Where(f => f.EndsWith(".resx", StringComparison.OrdinalIgnoreCase)).ToArray();
foreach (var resxFile in resxFiles)
{
    try
    {
        var content = File.ReadAllText(resxFile, Encoding.UTF8);
        
        // Pattern for <data name="UDSVer25to26" type="System.Resources.ResXFileRef...">
        var upgradeDbPattern = @"<data name=""UDSVer(\d+)to(\d+)""";
        var upgradeDbMatches = Regex.Matches(content, upgradeDbPattern);
        
        if (upgradeDbMatches.Count > 0)
        {
            // Get the last UDSVerXtoY numbers
            int lastFrom = 0;
            int lastTo = 0;
            foreach (Match m in upgradeDbMatches)
            {
                int from = int.Parse(m.Groups[1].Value);
                int to = int.Parse(m.Groups[2].Value);
                if (to > lastTo)
                {
                    lastFrom = from;
                    lastTo = to;
                }
            }
            
            // Create new entry name (e.g., UDSVer27to28)
            var newEntryName = $"UDSVer{lastTo}to{lastTo + 1}";
            
            // Check if this entry already exists
            if (!content.Contains($"name=\"{newEntryName}\""))
            {
                // Find the last UDSVer data entry (match the closing </data> tag)
                var lastDataPattern = $@"(<data name=""UDSVer{lastFrom}to{lastTo}""[^>]*>[\s\S]*?</data>)";
                var lastDataMatch = Regex.Match(content, lastDataPattern);
                
                if (lastDataMatch.Success)
                {
                    // Get the value line format from the existing entry
                    var existingEntry = lastDataMatch.Value;
                    var newDataEntry = existingEntry
                        .Replace($"UDSVer{lastFrom}to{lastTo}", newEntryName);
                    
                    content = content.Replace(existingEntry, existingEntry + "\r\n  " + newDataEntry);
                    File.WriteAllText(resxFile, content, Encoding.UTF8);
                    Console.WriteLine($"\n[OK] {Path.GetFileName(resxFile)} - Added: {newEntryName}");
                }
            }
            else
            {
                Console.WriteLine($"\n[--] {Path.GetFileName(resxFile)} - Entry {newEntryName} already exists");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n[ERR] {Path.GetFileName(resxFile)} - {ex.Message}");
    }
}

// Update csproj files with new None Include entry for UDSVerXtoY.sql
var csprojFiles = filesToProcess.Where(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)).ToArray();
foreach (var csprojFile in csprojFiles)
{
    try
    {
        var content = File.ReadAllText(csprojFile, Encoding.UTF8);
        
        // Pattern for <None Include="Resources\UDSVer26to27.sql" />
        var upgradeDbPattern = @"<None Include=""Resources\\UDSVer(\d+)to(\d+)\.sql""\s*/>";
        var upgradeDbMatches = Regex.Matches(content, upgradeDbPattern);
        
        if (upgradeDbMatches.Count > 0)
        {
            // Get the last UDSVerXtoY numbers
            int lastFrom = 0;
            int lastTo = 0;
            string lastMatch = "";
            foreach (Match m in upgradeDbMatches)
            {
                int from = int.Parse(m.Groups[1].Value);
                int to = int.Parse(m.Groups[2].Value);
                if (to > lastTo)
                {
                    lastFrom = from;
                    lastTo = to;
                    lastMatch = m.Value;
                }
            }
            
            // Create new entry (e.g., UDSVer27to28.sql)
            var newFileName = $"UDSVer{lastTo}to{lastTo + 1}.sql";
            
            // Check if this entry already exists
            if (!content.Contains(newFileName))
            {
                // Copy the format from the last match and replace version numbers
                var newNoneEntry = lastMatch.Replace($"UDSVer{lastFrom}to{lastTo}", $"UDSVer{lastTo}to{lastTo + 1}");
                content = content.Replace(lastMatch, lastMatch + "\r\n    " + newNoneEntry);
                File.WriteAllText(csprojFile, content, Encoding.UTF8);
                Console.WriteLine($"\n[OK] {Path.GetFileName(csprojFile)} - Added: {newNoneEntry}");
            }
            else
            {
                Console.WriteLine($"\n[--] {Path.GetFileName(csprojFile)} - Entry {newFileName} already exists");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n[ERR] {Path.GetFileName(csprojFile)} - {ex.Message}");
    }
}

// Update bat files with new ECHO and sqlcmd lines for UDSVerXtoY
var batFiles = filesToProcess.Where(f => f.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)).ToArray();
foreach (var batFile in batFiles)
{
    try
    {
        var content = File.ReadAllText(batFile, Encoding.UTF8);
        bool modified = false;
        
        // Pattern 1: ECHO Upgrading db - script XX - DBS Version YY - HHHH Version Z.ZZ
        // Match entire line to preserve exact format
        var echoPattern = @"ECHO[^\r\n]*script\s+(\d+)[^\r\n]*DBS Version\s+(\d+)[^\r\n]*HHHH Version\s+([\d.]+)";
        var echoMatches = Regex.Matches(content, echoPattern, RegexOptions.IgnoreCase);
        
        if (echoMatches.Count > 0)
        {
            // Get the last script/version numbers
            int lastScriptNum = 0;
            int lastDbVersion = 0;
            string lastEchoLine = "";
            foreach (Match m in echoMatches)
            {
                int scriptNum = int.Parse(m.Groups[1].Value);
                int dbVersion = int.Parse(m.Groups[2].Value);
                if (dbVersion > lastDbVersion)
                {
                    lastScriptNum = scriptNum;
                    lastDbVersion = dbVersion;
                    lastEchoLine = m.Value;
                }
            }
            
            // Create new ECHO line by copying format and replacing numbers
            int newScriptNum = lastScriptNum + 1;
            int newDbVersion = lastDbVersion + 1;
            var newEchoLine = Regex.Replace(lastEchoLine, @"script\s+\d+", $"script {newScriptNum}");
            newEchoLine = Regex.Replace(newEchoLine, @"DBS Version\s+\d+", $"DBS Version {newDbVersion}");
            newEchoLine = Regex.Replace(newEchoLine, @"HHHH Version\s+[\d.]+", $"HHHH Version {newVersion}");
            
            // Check if new version already exists
            if (!content.Contains($"DBS Version {newDbVersion}"))
            {
                // Add new ECHO line after the last one
                content = content.Replace(lastEchoLine, lastEchoLine + "\r\n" + newEchoLine);
                modified = true;
            }
        }
        
        // Pattern 2: sqlcmd line with UDSVerXXtoYY.sql
        // Match entire line to preserve exact format
        var sqlcmdPattern = @"sqlcmd[^\r\n]*UDSVer(\d+)to(\d+)\.sql[^\r\n]*";
        var sqlcmdMatches = Regex.Matches(content, sqlcmdPattern, RegexOptions.IgnoreCase);
        
        if (sqlcmdMatches.Count > 0)
        {
            // Get the last UDSVerXtoY numbers
            int lastFrom = 0;
            int lastTo = 0;
            string lastSqlcmdLine = "";
            foreach (Match m in sqlcmdMatches)
            {
                int from = int.Parse(m.Groups[1].Value);
                int to = int.Parse(m.Groups[2].Value);
                if (to > lastTo)
                {
                    lastFrom = from;
                    lastTo = to;
                    lastSqlcmdLine = m.Value;
                }
            }
            
            // Create new sqlcmd line by copying format and replacing version numbers
            var newSqlcmdLine = lastSqlcmdLine.Replace($"UDSVer{lastFrom}to{lastTo}", $"UDSVer{lastTo}to{lastTo + 1}");
            
            // Also increment log file number if present (e.g., 0028 -> 0029)
            var logNumMatch = Regex.Match(newSqlcmdLine, @"(\d{4})\.log");
            if (logNumMatch.Success)
            {
                int logNum = int.Parse(logNumMatch.Groups[1].Value);
                newSqlcmdLine = Regex.Replace(newSqlcmdLine, @"\d{4}\.log", $"{logNum + 1:D4}.log");
            }
            
            // Check if new version already exists
            if (!content.Contains($"UDSVer{lastTo}to{lastTo + 1}.sql"))
            {
                // Add new sqlcmd line after the last one
                content = content.Replace(lastSqlcmdLine, lastSqlcmdLine + "\r\n" + newSqlcmdLine);
                modified = true;
            }
        }
        
        if (modified)
        {
            File.WriteAllText(batFile, content, Encoding.UTF8);
            Console.WriteLine($"\n[OK] {Path.GetFileName(batFile)} - Added new ECHO and/or sqlcmd lines");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n[ERR] {Path.GetFileName(batFile)} - {ex.Message}");
    }
}

// Update TXT files with new UDSVerXtoY.sql entry (Readme_InstallSqlScripts.txt pattern)
var txtFiles = filesToProcess.Where(f => f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)).ToArray();
foreach (var txtFile in txtFiles)
{
    try
    {
        var content = File.ReadAllText(txtFile, Encoding.UTF8);
        
        // Pattern 1: UDSVerXtoY.sql entries
        var upgradeDbPattern = @"UDSVer(\d+)to(\d+)\.sql";
        var upgradeDbMatches = Regex.Matches(content, upgradeDbPattern);
        bool txtModified = false;
        
        if (upgradeDbMatches.Count > 0)
        {
            // Get the last UDSVerXtoY numbers
            int lastFrom = 0;
            int lastTo = 0;
            string lastEntry = "";
            foreach (Match m in upgradeDbMatches)
            {
                int from = int.Parse(m.Groups[1].Value);
                int to = int.Parse(m.Groups[2].Value);
                if (to > lastTo)
                {
                    lastFrom = from;
                    lastTo = to;
                    lastEntry = m.Value;
                }
            }
            
            // Create new entry (e.g., UDSVer27to28.sql)
            var newEntry = $"UDSVer{lastTo}to{lastTo + 1}.sql";
            
            // Check if this entry already exists
            if (!content.Contains(newEntry))
            {
                // Add new entry after the last one
                content = content.Replace(lastEntry, lastEntry + "\r\n" + newEntry);
                txtModified = true;
                Console.WriteLine($"\n[OK] {Path.GetFileName(txtFile)} - Added: {newEntry}");
            }
        }
        
        // Pattern 2: Version XX = Y.YY entries (e.g., Version 29 = 3.15)
        var versionMapPattern = @"Version\s+(\d+)\s*=\s*([\d.]+)";
        var versionMapMatches = Regex.Matches(content, versionMapPattern);
        
        if (versionMapMatches.Count > 0)
        {
            // Get the last Version XX = Y.YY entry
            int lastVersionNum = 0;
            string lastVersionEntry = "";
            foreach (Match m in versionMapMatches)
            {
                int versionNum = int.Parse(m.Groups[1].Value);
                if (versionNum > lastVersionNum)
                {
                    lastVersionNum = versionNum;
                    lastVersionEntry = m.Value;
                }
            }
            
            // Create new version entry (e.g., Version 30 = 3.16)
            int newVersionNum = lastVersionNum + 1;
            var newVersionEntry = $"Version {newVersionNum} = {newVersion}";
            
            // Check if this entry already exists
            if (!content.Contains($"Version {newVersionNum}"))
            {
                // Add new entry after the last one
                content = content.Replace(lastVersionEntry, lastVersionEntry + "\r\n" + newVersionEntry);
                txtModified = true;
                Console.WriteLine($"[OK] {Path.GetFileName(txtFile)} - Added: {newVersionEntry}");
            }
        }
        
        if (txtModified)
        {
            File.WriteAllText(txtFile, content, Encoding.UTF8);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n[ERR] {Path.GetFileName(txtFile)} - {ex.Message}");
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
