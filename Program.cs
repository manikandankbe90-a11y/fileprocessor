  // Update version history text file
  var versionHistoryFile = Path.Combine(folderPath, "VersionHistory.txt");
  if (File.Exists(versionHistoryFile))
  {
     
      catch (Exception ex)
      {
          Console.WriteLine($"\n[ERR] {Path.GetFileName(txtFile)} - {ex.Message}");
      }
  }

