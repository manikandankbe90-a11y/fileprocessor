var versionVarName = newVersion.Replace(".", "");
var newVersionConst = $"private const string Version{versionVarName} = \"{newVersion}\";";
// Creates: Version316 = "3.16"  ← Valid variable name!
