// Pattern: policy:PolicyVersion attribute (e.g., policy:PolicyVersion="1.0")
var policyVersionPattern = @"(policy:PolicyVersion\s*=\s*"")([^""]*?)("")";
var policyVersionRegex = new Regex(policyVersionPattern, RegexOptions.IgnoreCase);

if (policyVersionRegex.IsMatch(content))
{
    var oldVersion = policyVersionRegex.Match(content).Groups[2].Value;
    content = policyVersionRegex.Replace(content, $"$1{newVersion}$3");
    File.WriteAllText(file, content, Encoding.UTF8);
    xmlFilesModified++;
    Console.WriteLine($"  [OK] {Path.GetFileName(file)} - {oldVersion} -> {newVersion}");
}
else
{
    Console.WriteLine($"  [--] {Path.GetFileName(file)} - No policy:PolicyVersion found");
}
