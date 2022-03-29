using System.Runtime.CompilerServices;


[assembly: InternalsVisibleTo("AgDatabaseMove.Integration")]
[assembly: InternalsVisibleTo("AgDatabaseMove.Unit")]

namespace AgDatabaseMove
{
  using System;
  using System.Collections.Concurrent;
  using System.Collections.Generic;
  using System.Data.SqlClient;
  using System.Linq;
  using System.Threading;
  using System.Threading.Tasks;
  using Exceptions;
  using Microsoft.SqlServer.Management.Smo;
  using Polly;
  using SmoFacade;
  using AvailabilityGroup = SmoFacade.AvailabilityGroup;
  using Server = SmoFacade.Server;


  public interface IAgDatabase
  {
    bool Restoring { get; }
    string Name { get; }
    bool Exists();
    void Delete();
    void LogBackup();
    List<BackupMetadata> RecentBackups();
    void JoinAg();

    void Restore(IEnumerable<BackupMetadata> backupOrder, Func<int, TimeSpan> retryDurationProvider,
      Func<string, string> fileRelocation = null);

    void AddLogin(LoginProperties login);
    IEnumerable<LoginProperties> AssociatedLogins();
    void DropLogin(LoginProperties login);
    void DropAllLogins();
    void AddRole(LoginProperties login, RoleProperties role);
    IEnumerable<RoleProperties> AssociatedRoles();
    void ContainsLogin(string loginName);
  }


  /// <summary>
  ///   A connection to the primary instance of an availability group referencing a database name.
  ///   The database does not have to exist or be a part of the availability group, and can be created or added to the AG via
  ///   this interface.
  /// </summary>
  public class AgDatabase : IDisposable, IAgDatabase
  {
    private readonly string _backupPathSqlQuery;
    internal readonly IListener _listener;

    /// <summary>
    ///   A constructor that uses a config object for more options.
    /// </summary>
    /// <param name="dbConfig">A DatabaseConfig where the DataSource is the AG listener.</param>
    public AgDatabase(DatabaseConfig dbConfig)
    {
      Name = dbConfig.DatabaseName;
      _backupPathSqlQuery = dbConfig.BackupPathSqlQuery;
      _listener = new Listener(new SqlConnectionStringBuilder(dbConfig.ConnectionString) { InitialCatalog = "master" },
                               dbConfig.CredentialName);
    }

    public decimal SizeMb => _listener.Primary.DatabaseSizeMb(Name);

    public int ServerRemainingDiskMb => _listener.Primary.RemainingDiskMb();

    /// <summary>
    ///   Determines if the database is in a restoring state.
    /// </summary>
    public bool Restoring => _listener.Primary.Database(Name)?.Restoring ?? false;

    /// <summary>
    ///   Database name
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///   Determines if the database exists.
    /// </summary>
    public bool Exists()
    {
      return _listener.Primary.Database(Name) != null;
    }

    /// <summary>
    ///   Removes the database from the AG and deletes it from all instances.
    /// </summary>
    public void Delete()
    {
      _listener.AvailabilityGroup.Remove(Name);
      _listener.ForEachAgInstance(s => s.Database(Name)?.Drop());
    }

    /// <summary>
    ///   Takes a log backup on the primary instance.
    /// </summary>
    public void LogBackup()
    {
      _listener.Primary.LogBackup(Name, _backupPathSqlQuery);
    }

    /// <summary>
    ///   Restores the database backups to each instance in the AG.
    ///   We suggest using <see cref="AgDatabaseMove" /> to assist with restores.
    /// </summary>
    /// <param name="backupOrder">An ordered list of backups to restore.</param>
    /// <param name="retryDurationProvider">Retry duration function.</param>
    /// <param name="fileRelocation">A method to generate the new file location when moving the database.</param>
    public void Restore(IEnumerable<BackupMetadata> backupOrder, Func<int, TimeSpan> retryDurationProvider,
      Func<string, string> fileRelocation = null)
    {
      _listener.ForEachAgInstance(s => s.Restore(backupOrder, Name, retryDurationProvider, fileRelocation));
    }

    /// <summary>
    ///   Builds a list of recent backups from msdb on each AG instance.
    /// </summary>
    public List<BackupMetadata> RecentBackups()
    {
      var bag = new ConcurrentBag<BackupMetadata>();
      _listener.ForEachAgInstance(s => s.Database(Name).RecentBackups().ForEach(backup => bag.Add(backup)));
      return bag.ToList();
    }

    /// <summary>
    ///   Joins the database to the AG on each instance.
    /// </summary>
    public void JoinAg()
    {
      FinalizePrimary();
      _listener.ForEachAgInstance((s, ag) => {
        if(ag.IsPrimaryInstance)
          ag.JoinPrimary(Name);
      });
      _listener.ForEachAgInstance((s, ag) => {
        if(!ag.IsPrimaryInstance)
          ag.JoinSecondary(Name);
      });
    }

    public IEnumerable<LoginProperties> AssociatedLogins()
    {
      return _listener.Primary.Database(Name).Users.Where(u => u.Login != null && u.Login.Name != "sa")
        .Select(u => u.Login.Properties());
    }

    public void DropLogin(LoginProperties login)
    {
      _listener.ForEachAgInstance(server => server.DropLogin(login));
    }

    public void DropAllLogins()
    {
      _listener.ForEachAgInstance(s => s.Database(Name)?.DropAssociatedLogins());
    }

    public void AddLogin(LoginProperties login)
    {
      if (login.LoginType == LoginType.SqlLogin && login.Sid == null) {
        AddNewSqlLogin(login);
      } else
      {
        _listener.ForEachAgInstance(server => server.AddLogin(login));
      }
    }

    /// <summary>
    /// Creates a new Sql login across all members of the availability group.
    /// Assumes associated database has been created and the login being
    /// created does not previously exist. In the case of 
    /// Sql logins on availability groups, the login must first be created on
    /// a singular instance in the availability group, in this case the primary,
    /// so Sql Server will generate a new SID. This SID will be saved and used to
    /// create the same login on each ag replica.
    /// </summary>
    /// <param name="login"></param> A new login to create across all members of an availability group.
    private void AddNewSqlLogin(LoginProperties login)
    {
      var createdLogin = _listener.Primary.AddLogin(login);
      login.Sid = createdLogin.Sid;
      Parallel.ForEach(_listener.Secondaries, server => server.AddLogin(login));
    }

    public IEnumerable<RoleProperties> AssociatedRoles()
    {
      return _listener.Primary.Roles.Select(r => r.Properties());
    }

    public void AddRole(LoginProperties login, RoleProperties role)
    {
      _listener.ForEachAgInstance(server => server.AddRole(login, role));
    }

    /// <summary>
    ///   IDisposable implemented for our connection to the primary AG database server.
    /// </summary>
    public void Dispose()
    {
      _listener?.Dispose();
    }

    public void FullBackup()
    {
      _listener.Primary.FullBackup(Name, _backupPathSqlQuery);
    }

    public void FinalizePrimary()
    {
      _listener.ForEachAgInstance(FinalizePrimary);
    }

    private void FinalizePrimary(Server server, AvailabilityGroup availabilityGroup)
    {
      if(!availabilityGroup.IsPrimaryInstance)
        return;

      var database = server.Database(Name);
      if(!database.Restoring)
        return;

      database.RestoreWithRecovery();
    }

    public bool IsInitializing()
    {
      var result = 0;
      _listener.ForEachAgInstance((s, ag) => {
        if(ag.IsInitializing(Name))
          Interlocked.Increment(ref result);
      });
      return result > 0;
    }

    public void RestrictedUserMode()
    {
      _listener.Primary.Database(Name).RestrictedUserMode();
    }

    public void MultiUserMode()
    {
      _listener.Primary.Database(Name).MultiUserMode();
    }

    public void CheckDBConnections(int connectionTimeout)
    {
      _listener.ForEachAgInstance(server => server.CheckDBConnection(Name, connectionTimeout));
    }
    
    private void CheckLoginExists(Server server, AvailabilityGroup availabilityGroup, string loginName)
    {
      var matchingLogins = server.Logins.Where(l => l.Name == loginName);
      
      if (matchingLogins.Count() == 0)
        throw new MissingLoginException($"Login missing on {server.Name}, {_listener.AvailabilityGroup.Name}, {loginName}");

      if (matchingLogins.Count() > 1)
        throw new
          MultipleLoginException($"Multiple logins exist on {server.Name}, {_listener.AvailabilityGroup.Name}, {loginName}");

      var sid = matchingLogins.First().Sid;
      if (sid == null || sid.Length == 0)
        throw new MissingSidException($"Sid missing on {server.Name}, {_listener.AvailabilityGroup.Name}, {loginName}");
    }

    public void ContainsLogin(string loginName)
    {
      var exceptions = new ConcurrentQueue<Exception>();

      _listener.ForEachAgInstance((s, ag) => {
        try {
          CheckLoginExists(s, ag, loginName);
        }
        catch(MissingLoginException ex) {
          exceptions.Enqueue(ex);
        }
        catch(MultipleLoginException ex) {
          exceptions.Enqueue(ex);
        }
        catch(MissingSidException ex) {
          exceptions.Enqueue(ex);
        }
      });

      if(exceptions.Count > 0) throw new AggregateException(exceptions);
    }
  }
}