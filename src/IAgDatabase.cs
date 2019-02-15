namespace AgDatabaseMove
{
  using System;
  using System.Collections.Generic;
  using SmoFacade;


  public interface IAgDatabase
  {
    bool Restoring { get; }
    string Name { get; }
    bool Exists();
    void Delete();
    void LogBackup();
    List<BackupMetadata> RecentBackups();
    void JoinAg();

    void Restore(IEnumerable<BackupMetadata> backupOrder,
      Func<string, string> fileRelocation = null);

    void CopyLogins(IEnumerable<LoginProperties> logins);
    IEnumerable<LoginProperties> AssociatedLogins();
  }
}