namespace AgDatabaseMove
{
  using System.Collections.Generic;


  public interface IBackupChain
  {
    IEnumerable<BackupMetadata> RestoreOrder { get; }
  }
}