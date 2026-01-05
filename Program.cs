 var content = File.ReadAllText(file, Encoding.UTF8);
 bool modified = false;

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
 
 var upgradeMethodPattern = @"public\s+static\s+string\s+UDSV(\d+)to(\d+)\s*\(\)";
 var upgradeMethodMatches = Regex.Matches(content, upgradeMethodPattern);
 
 if (upgradeMethodMatches.Count > 0)
 {
     // Get the last UDSVXtoY numbers
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
     
     // Create new method name (e.g., UDSV27to28)
     var newMethodName = $"UDSV{lastTo}to{lastTo + 1}";
     
     // Check if method already exists
     if (!content.Contains($"public static string {newMethodName}()"))
     {
         // Find the last UDSV method and add new method after it
         var lastMethodPattern = $@"(public\s+static\s+string\s+UDSV{lastFrom}to{lastTo}\s*\(\)\s*\{{\s*return\s+Resources\.UDSV{lastFrom}to{lastTo}\.ToString\(\);\s*\}})";
         var newMethod = $"\r\n\r\n        public static string {newMethodName}()\r\n        {{\r\n            return Resources.{newMethodName}.ToString();\r\n        }}";
         
         content = Regex.Replace(content, lastMethodPattern, "$1" + newMethod);
         modified = true;
         Console.WriteLine($"  [OK] {Path.GetFileName(file)} - Added method {newMethodName}()");
     }
     
     // Check if file has RSS() calls - add new RSS() line
     var lastRunPattern = $@"(RSS\(UDSV{lastFrom}to{lastTo}\(\),\s*CS,\s*DBN\);)";
     if (Regex.IsMatch(content, lastRunPattern))
     {
         if (!content.Contains($"RSS({newMethodName}()"))
         {
             var newRunCall = $"\r\n            RSS({newMethodName}(), CS, DBN);";
             content = Regex.Replace(content, lastRunPattern, "$1" + newRunCall);
             modified = true;
             Console.WriteLine($"  [OK] {Path.GetFileName(file)} - Added RSS({newMethodName}(), CS, DBN);");
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

