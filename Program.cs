 var guidPattern = @"(const\s+string\s+\w+\s*=\s*"")([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})("")";
 var guidRegex = new Regex(guidPattern);

 if (guidRegex.IsMatch(content))
 {
     var oldGuid = guidRegex.Match(content).Groups[2].Value;
     content = guidRegex.Replace(content, $"$1{newGuid}$3");
     modified = true;
     Console.WriteLine($"  [OK] {Path.GetFileName(file)} - GUID updated: {oldGuid} -> {newGuid}");
 }
