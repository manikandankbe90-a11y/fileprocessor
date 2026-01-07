// Update RUL files - && region adds new GUID entry, || region adds same GUID from && region
var rulFiles = filesToProcess.Where(f => f.EndsWith(".rul", StringComparison.OrdinalIgnoreCase)).ToArray();
foreach (var rulFile in rulFiles)
{
    try
    {
        var content = File.ReadAllText(rulFile, Encoding.UTF8);
        bool modified = false;

        // Pattern: szVersion = "{GUID}" followed by && and version comment //X.XX
        // && region - add new GUID entry with new version
        var andPattern = @"(szVersion\s*=\s*""\{)([a-fA-F0-9\-]{36})(\}""\s*;?\s*&&\s*//)([\d.]+)";
        var andMatches = Regex.Matches(content, andPattern, RegexOptions.IgnoreCase);

        string lastAndGuid = "";
        if (andMatches.Count > 0)
        {
            // Find the last version in && region
            string lastVersion = "0";
            Match lastMatch = null;
            foreach (Match m in andMatches)
            {
                string ver = m.Groups[4].Value;
                if (string.Compare(ver, lastVersion) > 0)
                {
                    lastVersion = ver;
                    lastMatch = m;
                    lastAndGuid = m.Groups[2].Value;
                }
            }

            if (lastMatch != null && !Regex.IsMatch(content, @"&&\s*//" + Regex.Escape(newVersion)))
            {
                // Create new szVersion line with new GUID and new version for && region
                var lastLine = lastMatch.Value;
                var newLine = $"{lastMatch.Groups[1].Value}{newGuid.ToUpper()}{lastMatch.Groups[3].Value}{newVersion}";
                
                content = content.Replace(lastLine, lastLine + "\r\n\t\t" + newLine);
                modified = true;
                Console.WriteLine($"\n[OK] {Path.GetFileName(rulFile)} - && region: Added new GUID entry //{newVersion}");
            }
        }

        // || region - add entry using the LAST GUID from && region (not new GUID)
        var orPattern = @"(szVersion\s*=\s*""\{)([a-fA-F0-9\-]{36})(\}""\s*;?\s*\|\|\s*//)([\d.]+)";
        var orMatches = Regex.Matches(content, orPattern, RegexOptions.IgnoreCase);

        if (orMatches.Count > 0 && !string.IsNullOrEmpty(lastAndGuid))
        {
            // Find the last version in || region
            string lastVersion = "0";
            Match lastMatch = null;
            foreach (Match m in orMatches)
            {
                string ver = m.Groups[4].Value;
                if (string.Compare(ver, lastVersion) > 0)
                {
                    lastVersion = ver;
                    lastMatch = m;
                }
            }

            if (lastMatch != null && !Regex.IsMatch(content, @"\|\|\s*//" + Regex.Escape(newVersion)))
            {
                // Create new szVersion line using LAST && GUID (not new GUID) for || region
                var lastLine = lastMatch.Value;
                var newLine = $"{lastMatch.Groups[1].Value}{lastAndGuid.ToUpper()}{lastMatch.Groups[3].Value}{newVersion}";
                
                content = content.Replace(lastLine, lastLine + "\r\n\t\t" + newLine);
                modified = true;
                Console.WriteLine($"\n[OK] {Path.GetFileName(rulFile)} - || region: Added GUID from && region //{newVersion}");
            }
        }

        if (modified)
        {
            File.WriteAllText(rulFile, content, Encoding.UTF8);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n[ERR] {Path.GetFileName(rulFile)} - {ex.Message}");
    }
}
