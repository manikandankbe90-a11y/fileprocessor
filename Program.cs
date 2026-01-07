
                // Get the current version number from the file itself (e.g., VALUES ('GUID', 29, 1, '29'))
                var currentVersionMatch = Regex.Match(content, @"VALUES\s*\('[^']+',\s*(\d+)");
                int currentVersion = 4; // default
                if (currentVersionMatch.Success)
                {
                    currentVersion = int.Parse(currentVersionMatch.Groups[1].Value);
                }

                // Update GUID
                content = Regex.Replace(content, @"'[a-fA-F0-9\-]{36}'", $"'{newGuid}'");

                // Update version number (e.g., 29 -> 30)
                var versionNumPattern = @"(VALUES\s*\('[^']+',\s*)(\d+)(,)";
                content = Regex.Replace(content, versionNumPattern, $"$1{currentVersion + 1}$3");

                // Also update the last value if it's the version string (e.g., '29' -> '30')
                var lastValuePattern = @"(,\s*)'(\d+)'(\s*\))";
                content = Regex.Replace(content, lastValuePattern, $"$1'{currentVersion + 1}'$3");

                File.WriteAllText(versionInsertSqlFile, content, Encoding.UTF8);
