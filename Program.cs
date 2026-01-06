// Create new version const (e.g., Version316 = "3.16")
// Remove dots from version for variable name (3.16 -> 316)
var versionVarName = newVersion.Replace(".", "");
var newVersionConst = $"private const string Version{versionVarName} = \"{newVersion}\";";

// Check if new version already exists
if (!content.Contains($"Version{versionVarName}"))
{
    // Add new version const after the last one
    var escapedLastLine = Regex.Escape(lastVersionLine);
    content = Regex.Replace(content, escapedLastLine, lastVersionLine + "\r\n        " + newVersionConst);

    // Update LatestVersion to reference the new version
    var latestVersionPattern = @"(public\s+const\s+string\s+LatestVersion\s*=\s*)Version[\d.]+(\s*;)";
    content = Regex.Replace(content, latestVersionPattern, "${1}Version" + versionVarName + "${2}");

    modified = true;
    Console.WriteLine($"  [OK] {Path.GetFileName(file)} - Added Version{versionVarName} = \"{newVersion}\" and updated LatestVersion");
}
