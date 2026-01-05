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

  // Update resx files with new UDSVXtoY data entry
  var resxFiles = filesToProcess.Where(f => f.EndsWith(".resx", StringComparison.OrdinalIgnoreCase)).ToArray();
  foreach (var resxFile in resxFiles)
  {
      try
      {
          var content = File.ReadAllText(resxFile, Encoding.UTF8);
          
          // Pattern for <data name="UDSV25to26" type="System.Resources.ResXFileRef...">
          var upgradeDbPattern = @"<data name=""UpgradeDbSchemaVer(\d+)to(\d+)""";
          var upgradeDbMatches = Regex.Matches(content, upgradeDbPattern);
          
          if (upgradeDbMatches.Count > 0)
          {
              // Get the last UpgradeDbSchemaVerXtoY numbers
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
              
              // Create new entry name (e.g., UpgradeDbSchemaVer27to28)
              var newEntryName = $"UpgradeDbSchemaVer{lastTo}to{lastTo + 1}";
              
              // Check if this entry already exists
              if (!content.Contains($"name=\"{newEntryName}\""))
              {
                  // Find the last UpgradeDbSchemaVer data entry and add new one after it
                  var lastDataPattern = $@"(<data name=""UDSV{lastFrom}to{lastTo}"" type=""System\.Resources\.ResXFileRef, System\.Windows\.Forms"">\s*<value>\.\.\\InstallSqlScripts\\UpgradeDbSchemaVer{lastFrom}to{lastTo}\.sql;System\.String, mscorlib,\s*Version=4\.0\.0\.0, Culture=neutral, PublicKeyToken=b77a5c561934e089;Windows-1252</value>\s*</data>)";
                  
                  var newDataEntry = $"\r\n  <data name=\"{newEntryName}\" type=\"System.Resources.ResXFileRef, System.Windows.Forms\">\r\n    <value>..\\InstallSqlScripts\\{newEntryName}.sql;System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089;Windows-1252</value>\r\n  </data>";
                  
                  content = Regex.Replace(content, lastDataPattern, "$1" + newDataEntry);
                  File.WriteAllText(resxFile, content, Encoding.UTF8);
                  Console.WriteLine($"\n[OK] {Path.GetFileName(resxFile)} - Added: {newEntryName}");
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

  // Update csproj files with new None Include entry for UpgradeDbSchemaVerXtoY.sql
  var csprojFiles = filesToProcess.Where(f => f.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)).ToArray();
  foreach (var csprojFile in csprojFiles)
  {
      try
      {
          var content = File.ReadAllText(csprojFile, Encoding.UTF8);
          
          // Pattern for <None Include="Resources\UpgradeDbSchemaVer26to27.sql" />
          var upgradeDbPattern = @"<None Include=""Resources\\UpgradeDbSchemaVer(\d+)to(\d+)\.sql""\s*/>";
          var upgradeDbMatches = Regex.Matches(content, upgradeDbPattern);
          
          if (upgradeDbMatches.Count > 0)
          {
              // Get the last UpgradeDbSchemaVerXtoY numbers
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
              
              // Create new entry (e.g., UpgradeDbSchemaVer27to28.sql)
              var newFileName = $"UpgradeDbSchemaVer{lastTo}to{lastTo + 1}.sql";
              
              // Check if this entry already exists
              if (!content.Contains(newFileName))
              {
                  var newNoneEntry = $"{lastMatch}\r\n    <None Include=\"Resources\\{newFileName}\" />";
                  content = content.Replace(lastMatch, newNoneEntry);
                  File.WriteAllText(csprojFile, content, Encoding.UTF8);
                  Console.WriteLine($"\n[OK] {Path.GetFileName(csprojFile)} - Added: <None Include=\"Resources\\{newFileName}\" />");
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

  // Update bat files with new ECHO and sqlcmd lines for UpgradeDbSchemaVerXtoY
  var batFiles = filesToProcess.Where(f => f.EndsWith(".bat", StringComparison.OrdinalIgnoreCase)).ToArray();
  foreach (var batFile in batFiles)
  {
      try
      {
          var content = File.ReadAllText(batFile, Encoding.UTF8);
          bool modified = false;
          
		  
          var echoPattern = @"ECHO Upgrading db\s+-\s+script\s+(\d+)\s+DBSV\s+(\d+)\s+HPSM Version\s+([\d.]+)";
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
              
              // Create new ECHO line
              int newScriptNum = lastScriptNum + 1;
              int newDbVersion = lastDbVersion + 1;
              var newEchoLine = $"ECHO Upgrading db - script {newScriptNum} DBSchema Version {newDbVersion} HPSM Version {newVersion}";
              
              // Check if new version already exists
              if (!content.Contains($"DBSchema Version {newDbVersion}"))
              {
                  // Add new ECHO line after the last one
                  content = content.Replace(lastEchoLine, lastEchoLine + "\r\n" + newEchoLine);
                  modified = true;
              }
          }
          
          // Pattern 2: sqlcmd -S %1 -i UpgradeDbSchemaVerXXtoYY.sql -v DBNAME=%2 -o Install10UpgradeDbSpliterZZZZ.log
          var sqlcmdPattern = @"sqlcmd\s+-S\s+%1\s+-i\s+UpgradeDbSchemaVer(\d+)to(\d+)\.sql\s+-v\s+DBNAME=%2\s+-o\s+InstallOrUpgradeDb(\d+)\.log";
          var sqlcmdMatches = Regex.Matches(content, sqlcmdPattern, RegexOptions.IgnoreCase);
          
          if (sqlcmdMatches.Count > 0)
          {
              // Get the last UpgradeDbSchemaVerXtoY numbers
              int lastFrom = 0;
              int lastTo = 0;
              int lastLogNum = 0;
              string lastSqlcmdLine = "";
              foreach (Match m in sqlcmdMatches)
              {
                  int from = int.Parse(m.Groups[1].Value);
                  int to = int.Parse(m.Groups[2].Value);
                  int logNum = int.Parse(m.Groups[3].Value);
                  if (to > lastTo)
                  {
                      lastFrom = from;
                      lastTo = to;
                      lastLogNum = logNum;
                      lastSqlcmdLine = m.Value;
                  }
              }
              
              // Create new sqlcmd line
              var newSqlcmdLine = $"sqlcmd -S %1 -i UpgradeDbSchemaVer{lastTo}to{lastTo + 1}.sql -v DBNAME=%2 -o Install10UpgradeDbSpliter{lastLogNum + 1:D4}.log";
              
              // Check if new version already exists
              if (!content.Contains($"UpgradeDbSchemaVer{lastTo}to{lastTo + 1}.sql"))
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

  // Update TXT files with new UDSVXtoY.sql entry (----.txt pattern)
  var txtFiles = filesToProcess.Where(f => f.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)).ToArray();
  foreach (var txtFile in txtFiles)
  {
      try
      {
          var content = File.ReadAllText(txtFile, Encoding.UTF8);
          
          // Pattern for UpgradeDbSchemaVerXtoY.sql entries
          var upgradeDbPattern = @"UpgradeDbSchemaVer(\d+)to(\d+)\.sql";
          var upgradeDbMatches = Regex.Matches(content, upgradeDbPattern);
          
          if (upgradeDbMatches.Count > 0)
          {
              // Get the last UpgradeDbSchemaVerXtoY numbers
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
              
              // Create new entry (e.g., UpgradeDbSchemaVer27to28.sql)
              var newEntry = $"UDSV{lastTo}to{lastTo + 1}.sql";
              
              // Check if this entry already exists
              if (!content.Contains(newEntry))
              {
                  // Add new entry after the last one
                  content = content.Replace(lastEntry, lastEntry + "\r\n" + newEntry);
                  File.WriteAllText(txtFile, content, Encoding.UTF8);
                  Console.WriteLine($"\n[OK] {Path.GetFileName(txtFile)} - Added: {newEntry}");
              }
          }
      }
      catch (Exception ex)
      {
          Console.WriteLine($"\n[ERR] {Path.GetFileName(txtFile)} - {ex.Message}");
      }
  }
