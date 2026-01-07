var guidPattern = @"(const\s+string\s+\w+\s*=\s*"")([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})("")";
var guidRegex = new Regex(guidPattern);

var guidMatches = guidRegex.Matches(content);
foreach (Match gm in guidMatches)
{
    var oldGuid = gm.Groups[2].Value;
    var oldFullMatch = gm.Value;
    var newFullMatch = gm.Groups[1].Value + newGuid + gm.Groups[3].Value;
    content = content.Replace(oldFullMatch, newFullMatch);
    modified = true;
    Console.WriteLine($"  [OK] {Path.GetFileName(file)} - GUID updated: {oldGuid} -> {newGuid}");
}
