// Get the last quoted number '5'
var currentVersionMatch = Regex.Match(content, @"'(\d+)'\s*\)");

// Update '5' -> '6'
var lastValuePattern = @"'(\d+)'(\s*\))";
content = Regex.Replace(content, lastValuePattern, $"'{currentVersion + 1}'$2");
