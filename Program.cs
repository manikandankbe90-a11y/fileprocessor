  // Create new sqlcmd line - use regex to replace version numbers
                        var newSqlcmdLine = Regex.Replace(lastSqlcmdLine, 
                            @"UDS\d+to\d+", 
                            $"UDS{lastTo}to{lastTo + 1}");

                        // Also increment log file number if present (e.g., 0827 -> 0828)
                        var logNumMatch = Regex.Match(lastSqlcmdLine, @"(\d+)\.log");
                        if (logNumMatch.Success)
                        {
                            int logNum = int.Parse(logNumMatch.Groups[1].Value);
                            newSqlcmdLine = Regex.Replace(newSqlcmdLine, @"\d+\.log", $"{logNum + 1:D4}.log");
                        }
